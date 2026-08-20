using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Projects.Dtos;
using EnterpriseManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EnterpriseManagement.Api.Authorization;

/// <summary>
/// Decides whether the caller may act on a specific project.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule a role attribute cannot express. <c>[Authorize(Roles =
/// "MANAGER")]</c> answers "is this caller a manager?" — but the actual
/// requirement is "is this caller the manager <i>of this project</i>?", which
/// depends on the resource and so can only be evaluated once it is loaded.
/// </para>
/// <para>
/// The comparison uses the <c>employee_id</c> claim carried in the JWT, so no
/// extra database round-trip is needed to answer it. The trade-off is the usual
/// one for claims: if an account's employee link changes, the old token holds a
/// stale id until it expires.
/// </para>
/// <para>
/// A handler that does not call <c>Succeed</c> simply leaves the requirement
/// unmet — it never calls <c>Fail</c> unless the denial should be final.
/// Multiple handlers can contribute to one requirement, and any one of them
/// succeeding is enough, so failing here would veto other legitimate handlers.
/// </para>
/// </remarks>
public class ProjectAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, ProjectAccessInfo>
{
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ProjectAuthorizationHandler> _logger;

    public ProjectAuthorizationHandler(
        ICurrentUser currentUser,
        ILogger<ProjectAuthorizationHandler> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ProjectAccessInfo resource)
    {
        // Administrators may do anything to any project.
        if (context.User.IsInRole(RoleNames.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Ownership changes and deletion are administrative, full stop. A
        // manager permitted to reassign their own project could hand it to an
        // accomplice or seize someone else's by naming themselves.
        if (requirement.Name == ProjectOperations.ChangeOwnership.Name)
        {
            return Task.CompletedTask;
        }

        if (!context.User.IsInRole(RoleNames.Manager))
        {
            return Task.CompletedTask;
        }

        var employeeId = _currentUser.EmployeeId;

        if (employeeId is null)
        {
            // A MANAGER account with no linked employee record cannot own a
            // project, so there is nothing to compare against.
            _logger.LogWarning(
                "User {UserId} holds the MANAGER role but has no linked employee record",
                _currentUser.UserId);

            return Task.CompletedTask;
        }

        if (employeeId.Value == resource.ManagerEmployeeId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
