namespace EnterpriseManagement.Domain.Common;

/// <summary>
/// The seeded role names, as constants.
/// </summary>
/// <remarks>
/// These strings are a contract in three places at once: the Role table, the
/// JWT role claim, and every <c>[Authorize(Roles = ...)]</c> attribute. Because
/// attribute arguments must be compile-time constants, they cannot be an enum —
/// so they are <c>const string</c>, which at least makes a typo a compile error
/// instead of a silent authorisation bypass.
/// </remarks>
public static class RoleNames
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Employee = "EMPLOYEE";

    /// <summary>All seeded roles, for seeding and validation.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Admin, Manager, Employee };
}
