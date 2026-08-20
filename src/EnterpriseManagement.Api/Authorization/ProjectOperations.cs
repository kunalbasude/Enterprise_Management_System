using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EnterpriseManagement.Api.Authorization;

/// <summary>
/// The operations that can be performed on a project, as authorisation
/// requirements.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OperationAuthorizationRequirement"/> is the built-in requirement
/// that carries a name, which is exactly what is needed here: the same handler
/// can answer "may this caller manage the project?" and "may they change who
/// manages it?" by inspecting the operation, rather than needing a separate
/// requirement type per verb.
/// </para>
/// <para>
/// These are singletons because a requirement carries no per-request state.
/// </para>
/// </remarks>
public static class ProjectOperations
{
    /// <summary>Edit the project, and assign or remove team members.</summary>
    public static readonly OperationAuthorizationRequirement Manage =
        new() { Name = nameof(Manage) };

    /// <summary>
    /// Reassign the project to a different manager, or delete it. ADMIN only.
    /// </summary>
    /// <remarks>
    /// Separated from <see cref="Manage"/> because a manager who could reassign
    /// their own project could hand it to an accomplice, or seize another
    /// manager's project by naming themselves. Ownership changes are an
    /// administrative act.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement ChangeOwnership =
        new() { Name = nameof(ChangeOwnership) };
}
