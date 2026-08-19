using System.Text;
using Microsoft.Extensions.Options;

namespace EnterpriseManagement.Infrastructure.Identity;

/// <summary>
/// Fails application startup when JWT configuration is missing or unsafe.
/// </summary>
/// <remarks>
/// Deliberately fail-fast. A missing signing key that surfaces on the first
/// login attempt is a production incident; the same problem at startup is a
/// deployment that never goes live. Refusing to boot is the cheaper failure.
/// </remarks>
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    /// <summary>
    /// HMAC-SHA256 keys shorter than the 256-bit hash output add no security and
    /// are rejected outright by the token handler at signing time.
    /// </summary>
    private const int MinimumKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            failures.Add(
                "Jwt:Key is not configured. Set it via user-secrets for local development " +
                "or the Jwt__Key environment variable. Never commit a signing key.");
        }
        else if (Encoding.UTF8.GetByteCount(options.Key) < MinimumKeyBytes)
        {
            failures.Add($"Jwt:Key must be at least {MinimumKeyBytes} bytes for HMAC-SHA256.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is not configured.");
        }

        if (options.ExpiryMinutes is < 1 or > 1440)
        {
            failures.Add("Jwt:ExpiryMinutes must be between 1 and 1440.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
