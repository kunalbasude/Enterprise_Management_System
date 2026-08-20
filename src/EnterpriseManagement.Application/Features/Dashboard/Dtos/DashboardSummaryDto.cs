namespace EnterpriseManagement.Application.Features.Dashboard.Dtos;

/// <summary>
/// Headline figures for the landing page, scoped to what the caller may see.
/// </summary>
public class DashboardSummaryDto
{
    /// <summary>
    /// Whose numbers these are: Organisation, Manager or Employee.
    /// </summary>
    /// <remarks>
    /// Returned explicitly so the client can label the figures honestly. A
    /// manager seeing "12 projects" needs to know that means twelve of theirs,
    /// not twelve company-wide.
    /// </remarks>
    public string Scope { get; set; } = string.Empty;

    public int TotalEmployees { get; set; }

    public int ActiveEmployees { get; set; }

    public int TotalDepartments { get; set; }

    public int TotalProjects { get; set; }

    public int ActiveProjects { get; set; }

    public int CompletedProjects { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    /// <summary>Tasks that are neither Done nor Cancelled — the real backlog.</summary>
    public int PendingTasks { get; set; }

    /// <summary>Past their due date and still unfinished. Finished work is never overdue.</summary>
    public int OverdueTasks { get; set; }

    /// <summary>Unfinished tasks assigned to the caller. Null for accounts with no employee record.</summary>
    public int? MyOpenTasks { get; set; }

    /// <summary>Completion rate as a percentage, rounded. Zero when there are no tasks.</summary>
    public decimal CompletionRate =>
        TotalTasks == 0 ? 0 : Math.Round(CompletedTasks * 100m / TotalTasks, 1);
}
