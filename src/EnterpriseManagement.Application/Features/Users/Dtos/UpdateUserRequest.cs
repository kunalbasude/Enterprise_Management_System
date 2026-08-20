namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>
/// Updates profile fields and active state.
/// </summary>
/// <remarks>
/// Roles and password are handled by separate endpoints. Splitting them keeps
/// the audit trail meaningful — "changed roles" is a different event from
/// "corrected a typo in a name" — and means a routine profile edit cannot
/// accidentally carry a privilege change.
/// </remarks>
public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
