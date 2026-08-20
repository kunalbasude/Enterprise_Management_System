using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EnterpriseManagement.Api.Authorization;

/// <summary>
/// Operations on a task. Split because a task has two legitimate owners with
/// genuinely different rights.
/// </summary>
public static class TaskOperations
{
    /// <summary>
    /// Edit, reassign or delete the task. ADMIN, or the manager of its project.
    /// </summary>
    /// <remarks>
    /// Not granted to the assignee. Someone being given work does not give them
    /// the right to change its scope, move its deadline, or hand it to a
    /// colleague — those are the project manager's decisions.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement Manage =
        new() { Name = nameof(Manage) };

    /// <summary>
    /// Move the task through its workflow. ADMIN, the project's manager, or the
    /// assignee.
    /// </summary>
    /// <remarks>
    /// This is the reason the split exists. An EMPLOYEE must be able to say "I
    /// have started this" and "I have finished this" about their own work
    /// without being able to edit the task itself. That rule cannot be written
    /// as a role check, because it depends on whether this particular task is
    /// assigned to this particular caller.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement UpdateStatus =
        new() { Name = nameof(UpdateStatus) };
}
