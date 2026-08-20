namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>Self-service password change.</summary>
/// <remarks>
/// Requires the current password, unlike the admin reset. This is what stops an
/// unattended logged-in session, or a stolen token, from being converted into
/// permanent account takeover.
/// </remarks>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
