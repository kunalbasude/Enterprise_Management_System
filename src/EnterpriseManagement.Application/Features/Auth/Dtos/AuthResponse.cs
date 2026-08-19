namespace EnterpriseManagement.Application.Features.Auth.Dtos;

/// <summary>What a successful login or registration returns.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Always "Bearer" — the scheme the client must use in the Authorization header.</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Absolute UTC expiry, so a client can refresh proactively instead of
    /// waiting for a 401. Informational only: the server enforces expiry from
    /// the signed token, never from this field.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public UserDto User { get; set; } = new();
}
