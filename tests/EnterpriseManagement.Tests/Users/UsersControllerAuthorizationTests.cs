using System.Reflection;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EnterpriseManagement.Tests.Users;

/// <summary>
/// Locks in the authorisation layout of <see cref="UsersController"/>.
/// </summary>
/// <remarks>
/// <para>
/// These exist because of a real bug found during this phase. Multiple
/// <c>[Authorize]</c> attributes are combined with AND, not overridden. A
/// controller-level <c>AdminOnly</c> plus an action-level
/// <c>AuthenticatedUser</c> therefore meant "admin AND authenticated", which
/// silently locked ordinary users out of changing their own password — a 403
/// with no body and no obvious cause.
/// </para>
/// <para>
/// The rule these tests encode: the controller-level policy must be the
/// weakest one, and requirements only ever tighten as you move inward.
/// </para>
/// </remarks>
public class UsersControllerAuthorizationTests
{
    private static readonly Type Controller = typeof(UsersController);

    private static IEnumerable<MethodInfo> Actions =>
        Controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static string? PolicyOf(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault()?.Policy;

    [Fact]
    public void Controller_default_is_the_weakest_policy()
    {
        // If this ever becomes AdminOnly again, the self-service action breaks.
        Assert.Equal(AuthorizationPolicies.AuthenticatedUser, PolicyOf(Controller));
    }

    [Fact]
    public void Actions_are_discovered()
    {
        // A reflection test that matches nothing passes forever.
        Assert.True(Actions.Count() >= 8, $"Expected at least 8 actions, found {Actions.Count()}.");
    }

    [Theory]
    [InlineData(nameof(UsersController.GetAll))]
    [InlineData(nameof(UsersController.GetUserById))]
    [InlineData(nameof(UsersController.Create))]
    [InlineData(nameof(UsersController.Update))]
    [InlineData(nameof(UsersController.UpdateRoles))]
    [InlineData(nameof(UsersController.ResetPassword))]
    [InlineData(nameof(UsersController.Delete))]
    public void Administrative_actions_require_the_admin_policy(string actionName)
    {
        var action = Controller.GetMethod(actionName)!;

        Assert.Equal(AuthorizationPolicies.AdminOnly, PolicyOf(action));
    }

    [Fact]
    public void Self_service_password_change_does_not_require_admin()
    {
        var action = Controller.GetMethod(nameof(UsersController.ChangeOwnPassword))!;

        // Inherits the controller's AuthenticatedUser policy. An AdminOnly
        // attribute here would be the original bug.
        Assert.NotEqual(AuthorizationPolicies.AdminOnly, PolicyOf(action));
    }

    [Fact]
    public void Self_service_password_change_takes_no_user_id_parameter()
    {
        // The account changed must come from the token. An id parameter would
        // let any authenticated user reset any other user's password — broken
        // object level authorisation.
        var action = Controller.GetMethod(nameof(UsersController.ChangeOwnPassword))!;

        Assert.DoesNotContain(action.GetParameters(),
            p => p.Name is not null && p.Name.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_action_is_anonymous()
    {
        var anonymous = Actions
            .Where(a => a.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(a => a.Name)
            .ToList();

        Assert.True(anonymous.Count == 0,
            "User administration must never be anonymous: " + string.Join(", ", anonymous));
    }
}
