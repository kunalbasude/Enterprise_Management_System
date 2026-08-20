# Employee Search Optimisation

Measured on this project's development machine (Ubuntu 24.04, PostgreSQL 16.15
in Docker, 100,001 employee rows). Numbers are from actual `EXPLAIN ANALYZE`
runs, reproducible with the commands below. They are not estimates.

## The problem

Employee search matches a term against four columns:

```sql
WHERE first_name ILIKE '%john%'
   OR last_name  ILIKE '%john%'
   OR email      ILIKE '%john%'
   OR employee_code ILIKE '%john%'
```

The leading `%` is what hurts. A B-tree index orders values by their opening
characters, so it can answer `LIKE 'john%'` but never `LIKE '%john%'` — there is
no way to seek to a substring that may start anywhere.

### Two queries, two different costs

Pagination issues **two** statements: a `COUNT(*)` over the filtered set, and the
page itself. They behave very differently.

The **page query** was already acceptable, because `ORDER BY last_name,
first_name` matches `ix_employees_last_name_first_name`. PostgreSQL walks that
index and stops as soon as `LIMIT 20` is satisfied:

```
Limit  (actual time=11.829..11.832 rows=20 loops=1)
  ->  Index Scan using ix_employees_last_name_first_name  (actual time=3.846..11.685 rows=251)
        Filter: first_name ILIKE '%john%' OR ...
        Rows Removed by Filter: 6500
Execution Time: 11.874 ms
```

The **COUNT query** has no `LIMIT` to stop at, so it must examine every row:

```
Seq Scan on employees  (actual time=0.011..114.968 rows=9750 loops=1)
  Rows Removed by Filter: 90251
Execution Time: 115.413 ms
```

That is the query worth fixing.

## The fix: pg_trgm GIN indexes

`pg_trgm` decomposes each value into overlapping three-character sequences
(`john` → `  j`, ` jo`, `joh`, `ohn`). A GIN index over those trigrams *can*
serve a substring match, because the search term is decomposed the same way.

Applied in migration `20260820185000_AddEmployeeTrigramSearchIndexes`:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX ix_employees_first_name_trgm    ON employees USING gin (first_name gin_trgm_ops);
CREATE INDEX ix_employees_last_name_trgm     ON employees USING gin (last_name gin_trgm_ops);
CREATE INDEX ix_employees_email_trgm         ON employees USING gin (email gin_trgm_ops);
CREATE INDEX ix_employees_employee_code_trgm ON employees USING gin (employee_code gin_trgm_ops);
```

Four separate indexes rather than one composite: the predicate is a set of ORs,
so the planner consults each column independently and combines the results with
a `BitmapOr`. A multi-column GIN index would not serve that.

## Result

`SELECT count(*)` with the `%john%` predicate, three consecutive runs each, warm
cache. The "without" case is produced with `SET enable_bitmapscan = off`, which
forces the pre-index plan on identical data.

| | run 1 | run 2 | run 3 | average |
|---|---|---|---|---|
| Without trigram index | 115.4 ms | 110.3 ms | 109.4 ms | **111.7 ms** |
| With trigram index | 7.5 ms | 7.3 ms | 8.1 ms | **7.6 ms** |

**About 14.7x faster on this dataset.** Both return the same 9,750 rows, so the
speedup is not the result of doing less work.

The plan changes from a sequential scan to:

```
Aggregate  (actual time=7.880..7.883 rows=1 loops=1)
  ->  Bitmap Heap Scan on employees  (actual time=1.327..7.520 rows=9750)
        ->  BitmapOr  (actual time=1.166..1.168)
              ->  Bitmap Index Scan on ix_employees_first_name_trgm     (rows=5000)
              ->  Bitmap Index Scan on ix_employees_last_name_trgm      (rows=5000)
              ->  Bitmap Index Scan on ix_employees_email_trgm          (rows=0)
              ->  Bitmap Index Scan on ix_employees_employee_code_trgm  (rows=0)
```

## What it costs

Index sizes on this dataset:

| Index | Size |
|---|---|
| ix_employees_email_trgm | 3392 kB |
| ix_employees_employee_code_trgm | 2184 kB |
| ix_employees_last_name_trgm | 1328 kB |
| ix_employees_first_name_trgm | 1240 kB |
| **trigram total** | **~8.1 MB** |

Table 13 MB, all indexes 21 MB — the indexes now exceed the table. Every insert
and update maintains many GIN entries instead of one B-tree entry, so writes get
slower. This is a good trade for a directory read far more often than written; it
would be a poor trade for a high-volume append-only log.

### Limitation: terms shorter than three characters

A two-character term has no complete trigram, so the index cannot be used and
PostgreSQL falls back to a scan:

```
EXPLAIN ANALYZE SELECT count(*) FROM employees WHERE first_name ILIKE '%jo%';

Index Only Scan using ix_employees_last_name_first_name  (actual time=0.543..27.543 rows=10000)
  Filter: first_name ILIKE '%jo%'
  Rows Removed by Filter: 90001
Execution Time: 27.859 ms
```

Options if this mattered: require a minimum search length in the UI, or lower
`pg_trgm.similarity_threshold` and use `%` similarity matching instead of
`ILIKE`. Neither is implemented here, because a two-character search over a
staff directory returns too many rows to be useful anyway.

## End-to-end API timings

Through the running API over the same 100,001 rows, including JSON
serialisation and HTTP:

| Request | Matches | Time |
|---|---|---|
| `/api/employees?pageSize=20` | 100,001 | 138 ms |
| `/api/employees?search=john` | 9,750 | 41 ms |
| `/api/employees?search=GEN-000123` | 1 | 15 ms |
| `/api/employees?departmentId=9` | 5,000 | 15 ms |
| `/api/employees?departmentId=9&search=john` | 250 | 47 ms |
| `/api/employees?page=500&pageSize=20` | 100,001 | 18 ms |

The unfiltered first page is the slowest because its `COUNT(*)` has no predicate
to narrow it — counting 100,001 rows is inherently the expensive case. Real
systems often replace an exact total with an estimate from `pg_class.reltuples`,
or drop the total entirely in favour of cursor pagination. Neither is done here;
an exact count is correct and honest at this scale.

## Reproducing

```bash
# 1. Seed 100k rows
docker exec -i ems-postgres psql -U ems_user -d enterprise_management < scripts/seed-performance-data.sql

# 2. Force the pre-index plan and time it
docker exec -i ems-postgres psql -U ems_user -d enterprise_management <<'SQL'
\timing on
SET enable_bitmapscan = off;
SELECT count(*) FROM employees
WHERE first_name ILIKE '%john%' OR last_name ILIKE '%john%'
   OR email ILIKE '%john%' OR employee_code ILIKE '%john%';
RESET enable_bitmapscan;
SQL

# 3. Same query using the trigram indexes
docker exec -i ems-postgres psql -U ems_user -d enterprise_management <<'SQL'
\timing on
SELECT count(*) FROM employees
WHERE first_name ILIKE '%john%' OR last_name ILIKE '%john%'
   OR email ILIKE '%john%' OR employee_code ILIKE '%john%';
SQL

# 4. Inspect any plan
EXPLAIN (ANALYZE, BUFFERS) <query>;
```

To see the SQL EF Core generates, set
`Microsoft.EntityFrameworkCore.Database.Command` to `Information` in
`appsettings.Development.json` — already configured in this project.
