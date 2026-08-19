using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseManagement.Infrastructure.Identity;

/// <summary>
/// Builds and signs JWT access tokens.
/// </summary>
/// <remarks>
/// <b>A JWT is signed, not encrypted.</b> The payload is base64url — anyone
/// holding the token can read every claim. The signature only proves the token
/// has not been altered. Nothing secret may go in a claim.
/// </remarks>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    /// <summary>Custom claim carrying the linked employee id, used by resource authorisation.</summary>
    public const string EmployeeIdClaimType = "employee_id";

    private readonly JwtSettings _settings;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(
        User user,
        IEnumerable<string> roles,
        int? employeeId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            // "sub": the subject. The canonical identifier for the principal.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

            // "jti": a unique token id. Not used for revocation here, but it is
            // what a deny-list would key on if revocation were added later.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),

            // "iat": issued-at, so a token's age is inspectable.
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // One claim per role. ClaimTypes.Role is the type ASP.NET Core's
        // [Authorize(Roles = "...")] and User.IsInRole read by default, so using
        // it means role checks work with no extra mapping.
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (employeeId.HasValue)
        {
            // Embedded so "is this project mine?" is answered from the token
            // rather than an extra database round-trip on every authorised
            // request. The trade-off: if an employee link changes, the old token
            // carries a stale id until it expires.
            claims.Add(new Claim(EmployeeIdClaimType, employeeId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
