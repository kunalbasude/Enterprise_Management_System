namespace EnterpriseManagement.Application.Features.Auth.Dtos;

/// <summary>
/// A user as the API exposes it.
/// </summary>
/// <remarks>
/// Note what is absent: <c>PasswordHash</c>. Returning the entity directly would
/// serialise it to every client. Even a hash is not for publication — it lets an
/// attacker crack offline at their leisure.
/// </remarks>
public class UserDto
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>Role names, e.g. ["ADMIN"]. Drives role-based rendering in the client.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Present when this login is linked to an employee record.</summary>
    public int? EmployeeId { get; set; }
}
