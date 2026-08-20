namespace EnterpriseManagement.Application.Features.Users.Dtos;

/// <summary>Administrative password reset. Does not require the old password.</summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
