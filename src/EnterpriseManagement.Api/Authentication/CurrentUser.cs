using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Infrastructure.Identity;

namespace EnterpriseManagement.Api.Authentication;

/// <summary>
/// Reads the caller's identity from the validated JWT on the current request.
/// </summary>
/// <remarks>
/// Implemented in the Api layer because it is the only layer that may know about
/// <c>HttpContext</c>. Application consumes the <see cref="ICurrentUser"/>
/// interface and stays testable without a web host.
/// </remarks>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Read from the "sub" claim.
    /// </summary>
    /// <remarks>
    /// Both the raw "sub" and <see cref="ClaimTypes.NameIdentifier"/> are checked
    /// because ASP.NET Core's default inbound claim mapping rewrites several JWT
    /// claim names to longer XML-namespace URIs. Handling both means this keeps
    /// working whether or not that mapping is disabled.
    /// </remarks>
    public int? UserId =>
        ParseInt(FindClaim(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier));

    public string? Email => FindClaim(JwtRegisteredClaimNames.Email, ClaimTypes.Email);

    public int? EmployeeId => ParseInt(FindClaim(JwtTokenGenerator.EmployeeIdClaimType));

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    private string? FindClaim(params string[] claimTypes) =>
        claimTypes.Select(type => Principal?.FindFirst(type)?.Value)
                  .FirstOrDefault(value => !string.IsNullOrEmpty(value));

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
