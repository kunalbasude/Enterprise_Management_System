using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

/// <summary>Moves a task to a new status, subject to the transition rules.</summary>
public class UpdateTaskStatusRequest
{
    public TaskItemStatus Status { get; set; }
}
