using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Required: a task cannot exist outside a project.</summary>
    public int ProjectId { get; set; }

    /// <summary>Optional, so a task can be created in the backlog before anyone owns it.</summary>
    public int? AssignedEmployeeId { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal? EstimatedHours { get; set; }
}
