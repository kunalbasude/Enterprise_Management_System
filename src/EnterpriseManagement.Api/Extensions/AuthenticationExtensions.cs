using System.Text;
using EnterpriseManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseManagement.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures JWT bearer authentication: how an incoming token is validated
    /// on every request.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "The 'Jwt' configuration section is missing. Set Jwt__Key, Jwt__Issuer and " +
                "Jwt__Audience via user-secrets or environment variables.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // No cookies are involved, so there is no CSRF vector and no reason
            // to persist the token server-side.
            options.SaveToken = false;

            // Detailed failure reasons are returned in the WWW-Authenticate
            // header in Development only; in production they would tell an
            // attacker precisely why a forged token was rejected.
            options.IncludeErrorDetails = configuration.GetValue<bool>("DetailedAuthErrors");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                // Proves the token was signed with our key and not altered.
                // Without this check a caller could edit the payload to grant
                // themselves ADMIN and the API would believe it.
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),

                // Rejects a validly signed token minted by a different system or
                // intended for a different service.
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,

                ValidateLifetime = true,

                // Default clock skew is FIVE MINUTES, which silently keeps an
                // expired token working for that long. Zero means expiry means
                // expiry. Acceptable here because issuer and validator are the
                // same host; across machines a small tolerance is reasonable.
                ClockSkew = TimeSpan.Zero,

                // Pin the algorithm. Without this a token could arrive declaring
                // a different alg in its header, which is the family of attacks
                // that includes alg:none and HMAC/RSA confusion.
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // Distinguishing an expired token from an invalid one lets a
                    // client refresh instead of forcing a full re-login.
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("X-Token-Expired", "true");
                    }

                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
