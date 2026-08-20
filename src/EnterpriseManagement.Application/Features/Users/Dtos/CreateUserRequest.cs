namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>Admin-created account.</summary>
/// <remarks>
/// Unlike self-registration, this endpoint accepts roles — which is precisely
/// why it is ADMIN-only. Allowing a caller to choose their own roles anywhere
/// else would be a privilege escalation vulnerability.
/// </remarks>
public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Role names to grant. Must be a subset of the seeded roles.</summary>
    public List<string> Roles { get; set; } = [];
}
