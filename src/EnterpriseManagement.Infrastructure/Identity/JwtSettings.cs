namespace EnterpriseManagement.Infrastructure.Identity;

/// <summary>
/// JWT configuration, bound from the "Jwt" configuration section.
/// </summary>
/// <remarks>
/// Bound from configuration rather than hardcoded so the signing key comes from
/// user-secrets locally and an environment variable in Docker. A key committed
/// to source control lets anyone with repository access mint a token for any
/// user, including an admin.
/// </remarks>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 signing key. Must be at least 32 bytes: the algorithm's
    /// security is bounded by key length, and a short key is brute-forceable
    /// offline from a single captured token.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Who issued the token. Validated so a token from another system is rejected.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Who the token is for. Validated so a token minted for another service is rejected.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime in minutes. Short because this design has no revocation: a token
    /// stays valid until it expires even if the user is disabled or their roles
    /// change, so the expiry window is the blast radius.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;
}
