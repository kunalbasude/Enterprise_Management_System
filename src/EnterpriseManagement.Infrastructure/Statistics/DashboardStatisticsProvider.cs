using System.Data.Common;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Dashboard.Dtos;
using EnterpriseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace EnterpriseManagement.Infrastructure.Statistics;

/// <summary>
/// Computes every dashboard figure in one SQL statement.
/// </summary>
/// <remarks>
/// <para>
/// The alternative — one <c>CountAsync</c> per statistic — is more readable line
/// by line and costs a round-trip each. Locally that is invisible; across an
/// availability zone at 5-15ms it turns the first screen every user loads into
/// a hundred milliseconds of pure waiting.
/// </para>
/// <para>
/// <b>Why raw SQL rather than LINQ.</b> Nine conditional counts across four
/// tables is what SQL is for. PostgreSQL's <c>FILTER</c> clause expresses
/// "count only the rows matching this predicate" directly, and every count in
/// the statement shares one scan of each table. The cost is that this is
/// PostgreSQL-specific, which is why it sits behind an interface in
/// Infrastructure.
/// </para>
/// <para>
/// <b>Injection.</b> Every value is a bound parameter. The scope is expressed as
/// boolean logic over parameters rather than by concatenating a WHERE clause,
/// so no caller input is ever part of the statement text.
/// </para>
/// </remarks>
public class DashboardStatisticsProvider : IDashboardStatisticsProvider
{
    private readonly AppDbContext _context;

    public DashboardStatisticsProvider(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The aggregate SELECT, identical for both scopes. Only the three CTEs that
    /// define "visible" differ, so the arithmetic cannot drift between them.
    /// </summary>
    private const string AggregateSelect = """
        SELECT
            (SELECT count(*) FROM visible_employees)                                   AS total_employees,
            (SELECT count(*) FROM visible_employees WHERE is_active)                   AS active_employees,
            (SELECT count(DISTINCT department_id) FROM visible_employees)              AS total_departments,
            (SELECT count(*) FROM visible_projects)                                    AS total_projects,
            -- 1 = Active, 3 = Completed. Numeric because the column stores the
            -- enum as int; the names live in the C# enum.
            (SELECT count(*) FROM visible_projects WHERE status = 1)                   AS active_projects,
            (SELECT count(*) FROM visible_projects WHERE status = 3)                   AS completed_projects,
            (SELECT count(*) FROM visible_tasks)                                       AS total_tasks,
            -- 3 = Done, 4 = Cancelled.
            (SELECT count(*) FROM visible_tasks WHERE status = 3)                      AS completed_tasks,
            (SELECT count(*) FROM visible_tasks WHERE status NOT IN (3, 4))            AS pending_tasks,
            (SELECT count(*) FROM visible_tasks
              WHERE due_date IS NOT NULL
                AND due_date < @today
                AND status NOT IN (3, 4))                                              AS overdue_tasks,
            (SELECT count(*) FROM visible_tasks
              WHERE assigned_employee_id = @employee_id
                AND status NOT IN (3, 4))                                              AS my_open_tasks
        """;

    /// <summary>
    /// Administrator scope: everything, with no predicates at all.
    /// </summary>
    private const string AdminCtes = """
        WITH visible_projects AS (
            SELECT p.id, p.status FROM projects p
        ),
        visible_tasks AS (
            SELECT t.status, t.due_date, t.assigned_employee_id FROM tasks t
        ),
        visible_employees AS (
            SELECT e.id, e.is_active, e.department_id FROM employees e
        )
        """;

    /// <summary>
    /// Manager and employee scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written as a separate statement rather than folding the scope into a
    /// <c>WHERE @is_admin OR ...</c> predicate, because that pattern measurably
    /// destroys the plan. PostgreSQL builds the plan before it knows the
    /// parameter value, so an OR against a parameter cannot use an index: the
    /// scoped dashboard sequential-scanned all 100,001 employees to find 2,
    /// which made a manager's dashboard SLOWER than the administrator's despite
    /// covering far less data.
    /// </para>
    /// <para>
    /// The fix is to drive from the small table. Project membership is tiny, so
    /// collecting the relevant employee ids there and joining into
    /// <c>employees</c> by primary key turns a full scan into a handful of index
    /// lookups.
    /// </para>
    /// </remarks>
    private const string ScopedCtes = """
        WITH visible_projects AS (
            SELECT p.id, p.status
            FROM projects p
            WHERE p.manager_employee_id = @employee_id
               OR EXISTS (
                    SELECT 1 FROM project_employees pe
                    WHERE pe.project_id = p.id
                      AND pe.employee_id = @employee_id
                      AND pe.unassigned_at IS NULL)
        ),
        visible_tasks AS (
            SELECT t.status, t.due_date, t.assigned_employee_id
            FROM tasks t
            WHERE t.project_id IN (SELECT id FROM visible_projects)
               -- A task assigned to the caller is visible even if they were
               -- never formally added to its project. Hiding someone's own work
               -- from them would be absurd.
               OR t.assigned_employee_id = @employee_id
        ),
        visible_employee_ids AS (
            SELECT pe.employee_id AS id
            FROM project_employees pe
            WHERE pe.unassigned_at IS NULL
              AND pe.project_id IN (SELECT id FROM visible_projects)
            UNION
            -- The caller always counts as visible to themselves, even before
            -- being added to any project.
            SELECT @employee_id
        ),
        visible_employees AS (
            SELECT e.id, e.is_active, e.department_id
            FROM employees e
            JOIN visible_employee_ids v ON v.id = e.id
        )
        """;

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        bool isAdmin,
        int? employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();

        // The connection may already be open and owned by an ambient
        // transaction; opening an open connection throws.
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();

            // Two statements, selected here rather than by a parameter inside
            // one. Both are still a single round-trip; each gets a plan suited
            // to its case. See ScopedCtes for why the combined form was slow.
            command.CommandText = (isAdmin ? AdminCtes : ScopedCtes) + "\n" + AggregateSelect;

            // -1 can never match a real employee id, so a caller with no
            // employee record sees zeroes rather than everything. Failing closed
            // matters more here than anywhere: this is the first screen loaded.
            AddParameter(command, "employee_id", NpgsqlDbType.Integer, employeeId ?? -1);
            AddParameter(command, "today", NpgsqlDbType.Date, today);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                // Cannot happen: the statement always returns exactly one row.
                // Guarded anyway rather than dereferencing an empty reader.
                return new DashboardSummaryDto { Scope = DetermineScope(isAdmin, employeeId) };
            }

            return new DashboardSummaryDto
            {
                Scope = DetermineScope(isAdmin, employeeId),
                TotalEmployees = GetInt(reader, "total_employees"),
                ActiveEmployees = GetInt(reader, "active_employees"),
                TotalDepartments = GetInt(reader, "total_departments"),
                TotalProjects = GetInt(reader, "total_projects"),
                ActiveProjects = GetInt(reader, "active_projects"),
                CompletedProjects = GetInt(reader, "completed_projects"),
                TotalTasks = GetInt(reader, "total_tasks"),
                CompletedTasks = GetInt(reader, "completed_tasks"),
                PendingTasks = GetInt(reader, "pending_tasks"),
                OverdueTasks = GetInt(reader, "overdue_tasks"),
                MyOpenTasks = employeeId is null ? null : GetInt(reader, "my_open_tasks")
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, NpgsqlDbType type, object value)
    {
        var parameter = new NpgsqlParameter(name, type) { Value = value };
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// PostgreSQL's count() returns bigint, which is a long. Every figure here
    /// fits comfortably in an int, so it is narrowed once, in one place.
    /// </summary>
    private static int GetInt(DbDataReader reader, string columnName) =>
        (int)reader.GetInt64(reader.GetOrdinal(columnName));

    private static string DetermineScope(bool isAdmin, int? employeeId) =>
        isAdmin ? "Organisation"
        : employeeId is null ? "None"
        : "Assigned";
}
