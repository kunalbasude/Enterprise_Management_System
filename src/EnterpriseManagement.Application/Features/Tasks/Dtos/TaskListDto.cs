using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

public class TaskListDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TaskItemStatus Status { get; set; }

    /// <summary>Enum name, so clients branch on "InProgress" rather than the stored int.</summary>
    public string StatusName => Status.ToString();

    public TaskPriority Priority { get; set; }

    public string PriorityName => Priority.ToString();

    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>Null while the task sits unclaimed in the backlog.</summary>
    public int? AssignedEmployeeId { get; set; }

    public string? AssignedEmployeeName { get; set; }

    public DateOnly? DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Computed in the projection rather than stored.
    /// </summary>
    /// <remarks>
    /// A persisted flag would be wrong the moment the clock passed midnight,
    /// unless something kept rewriting it. Deriving it means it is never stale.
    /// A task that is already Done or Cancelled is never overdue, however late
    /// it was finished.
    /// </remarks>
    public bool IsOverdue { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
