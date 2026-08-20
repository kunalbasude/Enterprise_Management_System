# Performance Notes

Measured on this project's development machine (Ubuntu 24.04, PostgreSQL 16.15
in Docker, 100,001 employee rows). Numbers are from actual `EXPLAIN ANALYZE`
runs and timed requests, reproducible with the commands below. They are not
estimates.

Two pieces of work are recorded here:

1. [Employee search](#employee-search-optimisation) — a missing index
2. [Dashboard aggregation](#dashboard-aggregation) — a bad query plan, and a
   round-trip assumption that turned out to be mostly wrong

---

# Employee Search Optimisation

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

---

# Dashboard Aggregation

`GET /api/dashboard/summary` returns eleven figures across four tables. Two
separate questions came up, and only one of them mattered.

## Question 1: does one statement beat eleven?

The textbook answer is that eleven `CountAsync` calls cost eleven round-trips,
so combining them into one statement is a large win. Measured on this machine,
against 100,001 employees, warm cache, two runs:

| | run 1 | run 2 | average |
|---|---|---|---|
| Eleven separate statements | 21.25 ms | 20.01 ms | **20.6 ms** |
| One combined statement | 17.15 ms | 16.74 ms | **17.0 ms** |

**About 18% faster — far less than the design implies.** The database does
roughly the same work either way; combining them saves parsing and planning
overhead, not scanning.

The round-trip argument is still real, but it is about *latency*, not CPU, and
on a loopback connection there is almost none to save. The honest version of the
claim is arithmetic rather than a measurement: at a 5 ms network round-trip
between application and database — an ordinary figure across availability zones
— eleven statements add roughly 55 ms of pure waiting to the first screen every
user loads, and one statement adds roughly 5 ms. That is worth having, but it is
not what made this endpoint slow.

## Question 2: why was a manager's dashboard slower than an administrator's?

This was the real problem, and it was found only by timing every role rather
than assuming the largest scope was the slowest.

| Caller | First implementation |
|---|---|
| ADMIN (sees everything) | 107 ms |
| MANAGER alice | **583 ms** |
| MANAGER bob | 308 ms |
| EMPLOYEE worker | 324 ms |

A manager sees two employees and one project. Taking five times longer than the
administrator makes no sense.

### Cause

The first implementation expressed scope as a parameter inside one statement:

```sql
WHERE @is_admin
   OR e.id = @employee_id
   OR EXISTS (SELECT 1 FROM project_employees pe WHERE pe.employee_id = e.id AND ...)
```

`EXPLAIN ANALYZE` on the scoped path:

```
Aggregate  (actual time=123.123..123.126 rows=1 loops=1)
  ->  Seq Scan on employees e  (actual time=123.097..123.103 rows=2 loops=1)
        Rows Removed by Filter: 100001
Execution Time: 142.835 ms
```

**A sequential scan of 100,001 rows to return 2.** PostgreSQL plans the statement
before it knows the parameter values, so an `OR` against a parameter cannot use
an index — the planner has to assume `@is_admin` might be true and prepare to
return everything.

### Fix

Two things, both structural rather than clever:

1. **Separate statements per scope**, chosen in C# by `isAdmin`. Each gets a plan
   suited to its case. Still exactly one round-trip either way.
2. **Drive from the small table.** Project membership is tiny, so the scoped
   statement collects employee ids from `project_employees` and joins into
   `employees` by primary key, instead of scanning `employees` and testing each
   row.

```sql
visible_employee_ids AS (
    SELECT pe.employee_id AS id
    FROM project_employees pe
    WHERE pe.unassigned_at IS NULL
      AND pe.project_id IN (SELECT id FROM visible_projects)
    UNION
    SELECT @employee_id
),
visible_employees AS (
    SELECT e.id, e.is_active, e.department_id
    FROM employees e
    JOIN visible_employee_ids v ON v.id = e.id
)
```

### Result

End-to-end through the API, best of three runs each, including JSON
serialisation and HTTP:

| Caller | Before | After | Change |
|---|---|---|---|
| ADMIN | 107 ms | **43.4 ms** | 2.5x |
| MANAGER alice | 583 ms | **2.8 ms** | **~200x** |
| MANAGER bob | 308 ms | **2.5 ms** | ~120x |
| EMPLOYEE worker | 324 ms | **2.7 ms** | ~120x |

Every figure is identical before and after, so the speedup is not the result of
computing less.

The administrator case also improved, from 107 ms to 43 ms, because its
statement now carries no predicates at all — just aggregates over whole tables.

## What is still slow, and why that is honest

43 ms for the administrator is not fast, and it is inherent: `count(*)` over
100,001 rows has to visit them. Options, none of them implemented here:

- **Cache the summary** for 30-60 seconds. A dashboard figure that is a minute
  stale is almost never a problem, and this removes the cost entirely.
- **Use `pg_class.reltuples`** for an approximate total. Fast, and wrong by a few
  rows between vacuums.
- **Maintain counters** in a summary table, updated by trigger or by the
  application. Fastest to read, and now you own a cache-invalidation problem.

Each is a real trade-off rather than a free win, which is why the exact count
stands at this scale.

## Reproducing

```bash
# Compare eleven statements against one
docker exec -i ems-postgres psql -U ems_user -d enterprise_management < /tmp/dash-bench.sql

# Inspect the scoped plan
docker exec -i ems-postgres psql -U ems_user -d enterprise_management -c "
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*) FROM employees e
WHERE false OR e.id = 100005
   OR EXISTS (SELECT 1 FROM project_employees pe WHERE pe.employee_id = e.id AND pe.unassigned_at IS NULL);"

# Time the endpoint per role
curl -s -w '%{time_total}s\n' -H "Authorization: Bearer $TOKEN" \
     http://localhost:5080/api/dashboard/summary
```

## The lesson worth keeping

The optimisation everyone expects to matter — batching eleven queries into one —
was worth 18%. The one nobody was looking for — a parameterised `OR` destroying
the query plan — was worth two orders of magnitude, and was only visible because
every role was timed instead of just the largest one.

Measure the slow case, not the case you assume is slow.
