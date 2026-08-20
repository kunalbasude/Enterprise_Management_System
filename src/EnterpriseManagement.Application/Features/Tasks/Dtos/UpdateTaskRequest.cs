using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

/// <summary>
/// Updates a task's details.
/// </summary>
/// <remarks>
/// Status is deliberately absent. It has its own endpoint because a status
/// change is a workflow event governed by transition rules, and mixing it into
/// a general edit makes "who moved this to Done, and when" unanswerable.
/// ProjectId is absent too: moving a task between projects would silently
/// change who is authorised to touch it.
/// </remarks>
public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; }

    public int? AssignedEmployeeId { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal? EstimatedHours { get; set; }
}
