using System.Security.Claims;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Features.Projects.Dtos;
using EnterpriseManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseManagement.Tests.Projects;

/// <summary>
/// Covers the rule a role attribute cannot express: a MANAGER may act on the
/// projects they manage and no others.
/// </summary>
public class ProjectAuthorizationHandlerTests
{
    private const int AlicesEmployeeId = 100;
    private const int BobsEmployeeId = 200;

    private static readonly ProjectAccessInfo AlicesProject = new(ProjectId: 1, ManagerEmployeeId: AlicesEmployeeId);

    private sealed class StubCurrentUser : ICurrentUser
    {
        public int? UserId { get; init; }
        public string? Email { get; init; }
        public int? EmployeeId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public bool IsAuthenticated => UserId is not null;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private static ClaimsPrincipal PrincipalWith(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "Test"));

    private static async Task<bool> IsAllowedAsync(
        OperationAuthorizationRequirement operation,
        string[] roles,
        int? employeeId,
        ProjectAccessInfo? resource = null)
    {
        var handler = new ProjectAuthorizationHandler(
            new StubCurrentUser { UserId = 1, EmployeeId = employeeId, Roles = roles },
            NullLogger<ProjectAuthorizationHandler>.Instance);

        var context = new AuthorizationHandlerContext(
            [operation],
            PrincipalWith(roles),
            resource ?? AlicesProject);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task Admin_may_manage_any_project()
    {
        Assert.True(await IsAllowedAsync(
            ProjectOperations.Manage, [RoleNames.Admin], employeeId: 999));
    }

    [Fact]
    public async Task Admin_may_change_ownership()
    {
        Assert.True(await IsAllowedAsync(
            ProjectOperations.ChangeOwnership, [RoleNames.Admin], employeeId: null));
    }

    [Fact]
    public async Task Manager_may_manage_their_own_project()
    {
        Assert.True(await IsAllowedAsync(
            ProjectOperations.Manage, [RoleNames.Manager], employeeId: AlicesEmployeeId));
    }

    [Fact]
    public async Task Manager_may_not_manage_another_managers_project()
    {
        // The whole point of resource-based authorisation: identical role,
        // different resource, different answer.
        Assert.False(await IsAllowedAsync(
            ProjectOperations.Manage, [RoleNames.Manager], employeeId: BobsEmployeeId));
    }

    [Fact]
    public async Task Manager_may_not_change_ownership_even_of_their_own_project()
    {
        // Otherwise a manager could hand a project to an accomplice, or seize
        // another manager's project by naming themselves.
        Assert.False(await IsAllowedAsync(
            ProjectOperations.ChangeOwnership, [RoleNames.Manager], employeeId: AlicesEmployeeId));
    }

    [Fact]
    public async Task Manager_without_a_linked_employee_record_is_denied()
    {
        // Fails closed. A null employee id must never compare equal to anything.
        Assert.False(await IsAllowedAsync(
            ProjectOperations.Manage, [RoleNames.Manager], employeeId: null));
    }

    [Fact]
    public async Task Employee_role_is_denied_even_on_a_project_they_belong_to()
    {
        // Membership grants visibility, not the right to modify.
        Assert.False(await IsAllowedAsync(
            ProjectOperations.Manage, [RoleNames.Employee], employeeId: AlicesEmployeeId));
    }

    [Fact]
    public async Task A_user_with_no_roles_is_denied()
    {
        Assert.False(await IsAllowedAsync(
            ProjectOperations.Manage, [], employeeId: AlicesEmployeeId));
    }

    [Fact]
    public async Task A_user_holding_both_admin_and_manager_is_allowed_via_admin()
    {
        // Multi-role users are why Role is a table rather than a single column.
        Assert.True(await IsAllowedAsync(
            ProjectOperations.ChangeOwnership,
            [RoleNames.Manager, RoleNames.Admin],
            employeeId: BobsEmployeeId));
    }
}
