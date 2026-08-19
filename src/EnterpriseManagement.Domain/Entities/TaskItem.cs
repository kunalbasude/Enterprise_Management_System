using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Domain.Entities;

/// <summary>
/// A single piece of work inside a project.
/// </summary>
/// <remarks>
/// Named <c>TaskItem</c> because <c>Task</c> is taken by
/// <see cref="System.Threading.Tasks.Task"/>, which implicit usings import into
/// every file. An entity called <c>Task</c> makes every <c>async Task&lt;T&gt;</c>
/// signature ambiguous.
/// </remarks>
public class TaskItem : BaseEntity, IAuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Every task belongs to exactly one project. Required.</summary>
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Optional: a task can exist in the backlog before anyone owns it. Also lets
    /// a task be unassigned without deleting it.
    /// </summary>
    public int? AssignedEmployeeId { get; set; }
    public Employee? AssignedEmployee { get; set; }

    /// <summary>Due date. A calendar date, so <see cref="DateOnly"/>; "overdue" means strictly before today.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Set when the task reaches <see cref="TaskItemStatus.Done"/>, cleared if it is reopened.</summary>
    public DateTime? CompletedAt { get; set; }

    public decimal? EstimatedHours { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
