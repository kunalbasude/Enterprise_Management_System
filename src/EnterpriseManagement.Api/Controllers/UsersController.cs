using System.Net.Mime;
using EnterpriseManagement.Api.Authorization;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Users.Dtos;
using EnterpriseManagement.Application.Features.Users.Services;
using EnterpriseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>
/// User account administration.
/// </summary>
/// <remarks>
/// <para>
/// Every action here is ADMIN-only except the self-service password change,
/// because this controller can grant roles — it is the privilege escalation
/// surface of the whole system.
/// </para>
/// <para>
/// The controller-level attribute is the WEAKER policy on purpose. Multiple
/// [Authorize] attributes are combined with AND, not overridden, so a
/// controller-level AdminOnly would make an action-level AuthenticatedUser
/// mean "admin AND authenticated" — silently locking non-admins out of their
/// own password change. Requirements only ever tighten as you move inward.
/// </para>
/// </remarks>
[ApiController]
[Route("api/users")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    /// <summary>Lists users with paging, search, role and status filters.</summary>
    /// <remarks>
    /// Example: <c>GET /api/users?page=1&amp;pageSize=20&amp;role=ADMIN&amp;isActive=true&amp;search=ada&amp;sortBy=email</c>
    /// <para>Sortable fields: email, fullName, createdAt, lastLoginAt.</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(PagedResult<UserListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetAll(
        [FromQuery] UserQueryParameters parameters,
        CancellationToken cancellationToken) =>
        Ok(await _userService.GetAsync(parameters, cancellationToken));

    /// <summary>Gets a single user.</summary>
    [HttpGet("{id:int}", Name = nameof(GetUserById))]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListDto>> GetUserById(int id, CancellationToken cancellationToken) =>
        Ok(await _userService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates an account with explicit roles. ADMIN only.</summary>
    /// <response code="201">Created.</response>
    /// <response code="409">Email already registered.</response>
    /// <response code="422">Unknown role name.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserListDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute(nameof(GetUserById), new { id = user.Id }, user);
    }

    /// <summary>Updates profile fields and active state. Roles are not changed here.</summary>
    /// <response code="422">Would deactivate the last active administrator.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserListDto>> Update(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _userService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Replaces a user's roles with exactly the supplied set.</summary>
    /// <remarks>
    /// Separate from the profile update so that a privilege change is always a
    /// deliberate, separately auditable action.
    /// </remarks>
    /// <response code="422">Unknown role, empty set, or would remove the last administrator.</response>
    [HttpPut("{id:int}/roles")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserListDto>> UpdateRoles(
        int id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _userService.UpdateRolesAsync(id, request, cancellationToken));

    /// <summary>Administratively resets a password without knowing the old one.</summary>
    [HttpPost("{id:int}/reset-password")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Changes the caller's own password. Any authenticated user.</summary>
    /// <remarks>
    /// The account changed is taken from the token, never from the route. An id
    /// parameter here would let any user reset any other user's password.
    /// Requires the current password, so a stolen token alone cannot take over
    /// the account permanently.
    /// </remarks>
    /// <response code="401">Current password is incorrect.</response>
    /// <response code="422">New password matches the current one.</response>
    [HttpPost("me/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeOwnPassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedException("Token does not contain a valid user id.");

        await _userService.ChangeOwnPasswordAsync(userId, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Deletes an account.</summary>
    /// <remarks>
    /// Any linked employee record survives, because <c>employees.user_id</c> is
    /// SET NULL — the HR record and its task history must outlive the login.
    /// </remarks>
    /// <response code="422">Deleting yourself, or the last active administrator.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
