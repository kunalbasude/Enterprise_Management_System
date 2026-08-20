namespace EnterpriseManagement.Application.Features.Tasks.Dtos;

/// <summary>
/// The minimum an authorisation decision about a task needs.
/// </summary>
/// <remarks>
/// Carries the project's manager as well as the task's assignee, because a task
/// has two legitimate owners: whoever manages the project it belongs to, and
/// whoever the task is assigned to. They get different permissions.
/// </remarks>
/// <param name="TaskId">The task being acted on.</param>
/// <param name="ProjectId">Its project.</param>
/// <param name="ProjectManagerEmployeeId">The employee accountable for that project.</param>
/// <param name="AssignedEmployeeId">The assignee, if any.</param>
public record TaskAccessInfo(
    int TaskId,
    int ProjectId,
    int ProjectManagerEmployeeId,
    int? AssignedEmployeeId);
