using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds pg_trgm GIN indexes to make employee search fast.
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem.</b> Employee search is <c>ILIKE '%term%'</c> across four
/// columns. The leading wildcard makes a B-tree index useless: a B-tree orders
/// values by their first characters, so it cannot find rows by a substring in
/// the middle. PostgreSQL therefore sequential-scans the table for the COUNT
/// that pagination issues.
/// </para>
/// <para>
/// <b>The fix.</b> The pg_trgm extension splits each value into overlapping
/// three-character sequences ("john" becomes "  j", " jo", "joh", "ohn"). A GIN
/// index over those trigrams can answer a substring match, because the search
/// term is decomposed the same way and the index finds rows containing those
/// trigrams.
/// </para>
/// <para>
/// <b>The costs, stated honestly.</b> A GIN trigram index is substantially
/// larger than a B-tree on the same column and makes writes slower, because
/// every insert or update maintains many index entries rather than one. It also
/// cannot help a search term shorter than three characters — there is no
/// trigram to look up, so PostgreSQL falls back to a scan. This trade is worth
/// it for a table read far more often than written, which an employee directory
/// is. It would be a poor trade for a high-volume append-only log.
/// </para>
/// <para>
/// Raw SQL rather than the fluent API because EF Core has no first-class
/// concept of an extension or an operator class.
/// </para>
/// </remarks>
public partial class AddEmployeeTrigramSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ships with PostgreSQL as a contrib module; no external install needed.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        // One index per searched column. A single multi-column GIN index would
        // not help, because the predicate is a set of ORs: the planner needs to
        // consult each column independently and combine the results with a
        // BitmapOr.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_employees_first_name_trgm
                ON employees USING gin (first_name gin_trgm_ops);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_employees_last_name_trgm
                ON employees USING gin (last_name gin_trgm_ops);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_employees_email_trgm
                ON employees USING gin (email gin_trgm_ops);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_employees_employee_code_trgm
                ON employees USING gin (employee_code gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_first_name_trgm;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_last_name_trgm;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_email_trgm;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_employee_code_trgm;");

        // The extension is deliberately NOT dropped. Something else in the
        // database may depend on it, and dropping a shared extension during a
        // rollback is a wider blast radius than this migration owns.
    }
}
