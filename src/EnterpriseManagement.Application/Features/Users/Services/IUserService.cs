using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Users.Dtos;

namespace EnterpriseManagement.Application.Features.Users.Services;

public interface IUserService
{
    Task<PagedResult<UserListDto>> GetAsync(
        UserQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<UserListDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<UserListDto> CreateAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserListDto> UpdateAsync(
        int id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserListDto> UpdateRolesAsync(
        int id, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        int id, ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <remarks>
    /// The user id is taken from the validated token, never from the request
    /// body or route: accepting it as input would let any user change any other
    /// user's password.
    /// </remarks>
    Task ChangeOwnPasswordAsync(
        int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
