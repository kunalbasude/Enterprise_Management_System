using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Tasks.Dtos;
using EnterpriseManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EnterpriseManagement.Api.Authorization;

/// <summary>
/// Decides whether the caller may act on a specific task.
/// </summary>
/// <remarks>
/// <para>
/// Three ways to qualify, each granting different operations:
/// </para>
/// <list type="bullet">
/// <item>ADMIN — everything.</item>
/// <item>The manager of the task's project — everything, because they own the work.</item>
/// <item>The assignee — status changes only, because being handed work does not
/// confer the right to redefine it.</item>
/// </list>
/// <para>
/// Note that the assignee check does not test for the EMPLOYEE role. A manager
/// assigned a task on someone else's project qualifies through the same path,
/// which is correct: the relationship to the resource is what matters, not the
/// role label.
/// </para>
/// </remarks>
public class TaskAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, TaskAccessInfo>
{
    private readonly ICurrentUser _currentUser;

    public TaskAuthorizationHandler(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        TaskAccessInfo resource)
    {
        if (context.User.IsInRole(RoleNames.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var employeeId = _currentUser.EmployeeId;

        if (employeeId is null)
        {
            // No employee record means no relationship to any task. Fails closed.
            return Task.CompletedTask;
        }

        // The project's manager owns everything about its tasks.
        if (employeeId.Value == resource.ProjectManagerEmployeeId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // The assignee may only move the task through its workflow.
        if (requirement.Name == TaskOperations.UpdateStatus.Name &&
            resource.AssignedEmployeeId == employeeId.Value)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
