using EnterpriseManagement.Application.Features.Auth.Dtos;

namespace EnterpriseManagement.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Returns the profile of the currently authenticated user.</summary>
    Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}
