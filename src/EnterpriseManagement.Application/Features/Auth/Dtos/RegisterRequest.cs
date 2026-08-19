namespace EnterpriseManagement.Application.Features.Auth.Dtos;

/// <summary>Self-service registration payload.</summary>
/// <remarks>
/// A DTO, not the <c>User</c> entity. Binding straight to an entity is the
/// classic over-posting vulnerability: a caller could send
/// <c>{"isActive":true,"passwordHash":"..."}</c> and set fields the endpoint
/// never intended to expose. A DTO can only carry what it declares.
/// </remarks>
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Plaintext, over TLS. Hashed immediately and never stored or logged.</summary>
    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
}
