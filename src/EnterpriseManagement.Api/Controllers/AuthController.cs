using System.Net.Mime;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.Auth.Dtos;
using EnterpriseManagement.Application.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseManagement.Api.Controllers;

/// <summary>Registration, login and the current user's profile.</summary>
[ApiController]
[Route("api/auth")]
[Produces(MediaTypeNames.Application.Json)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>Registers a new account and returns an access token.</summary>
    /// <remarks>
    /// Self-registration always grants the least-privileged EMPLOYEE role.
    /// Elevation is an administrative action and can never be requested by the
    /// caller.
    /// </remarks>
    /// <response code="201">Account created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="409">Email address already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);

        // 201 with a Location header pointing at the created resource, per REST
        // convention. CreatedAtAction resolves the route by name so the URL is
        // never hand-built.
        return CreatedAtAction(nameof(Me), null, response);
    }

    /// <summary>Exchanges credentials for an access token.</summary>
    /// <response code="200">Authenticated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _authService.LoginAsync(request, ipAddress, cancellationToken);

        return Ok(response);
    }

    /// <summary>Returns the authenticated caller's profile.</summary>
    /// <remarks>
    /// The user id comes from the validated token, never from a route or query
    /// parameter. Accepting an id here would let any authenticated caller read
    /// any other user's profile — a textbook broken-object-level-authorisation
    /// flaw.
    /// </remarks>
    /// <response code="200">The current user.</response>
    /// <response code="401">No or invalid token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        // [Authorize] guarantees authentication, but the claim could still be
        // malformed, so this is checked rather than assumed.
        var userId = _currentUser.UserId
            ?? throw new Domain.Exceptions.UnauthorizedException("Token does not contain a valid user id.");

        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);

        return Ok(user);
    }
}
