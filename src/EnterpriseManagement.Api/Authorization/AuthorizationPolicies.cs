using EnterpriseManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseManagement.Api.Authorization;

/// <summary>
/// Named authorisation policies, defined once and referenced by name.
/// </summary>
/// <remarks>
/// <para>
/// Preferred over scattering <c>[Authorize(Roles = "ADMIN,MANAGER")]</c> through
/// controllers. When a rule changes — "managers may now create departments" —
/// it changes here, in one place, instead of across every action that happens
/// to carry that role string. Missing one of those is a silent privilege bug
/// that no compiler catches.
/// </para>
/// <para>
/// The policy names are also <c>const</c>, so a typo in
/// <c>[Authorize(Policy = ...)]</c> fails the build rather than silently
/// creating an unmatched policy at runtime.
/// </para>
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>Administrative operations: user management, department changes, audit logs.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Operations a manager performs across their own projects, and admins perform anywhere.</summary>
    public const string ManagerOrAdmin = nameof(ManagerOrAdmin);

    /// <summary>Any authenticated account, whatever its role.</summary>
    public const string AuthenticatedUser = nameof(AuthenticatedUser);

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));

            options.AddPolicy(ManagerOrAdmin, policy =>
                policy.RequireRole(RoleNames.Admin, RoleNames.Manager));

            options.AddPolicy(AuthenticatedUser, policy =>
                policy.RequireAuthenticatedUser());

            // Every endpoint requires authentication unless it opts out with
            // [AllowAnonymous]. Secure by default: forgetting [Authorize] on a
            // new controller leaves it protected rather than public, which is
            // the failure mode you want.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
