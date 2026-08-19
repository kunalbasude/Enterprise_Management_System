using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseManagement.Domain.Common;
using EnterpriseManagement.Domain.Entities;
using EnterpriseManagement.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseManagement.Tests.Auth;

public class JwtTokenGeneratorTests
{
    private const string TestKey = "test-signing-key-that-is-at-least-32-bytes-long!!";

    private static readonly JwtSettings Settings = new()
    {
        Key = TestKey,
        Issuer = "EnterpriseManagement.Api",
        Audience = "EnterpriseManagement.Client",
        ExpiryMinutes = 60
    };

    private static readonly DateTimeOffset FixedNow = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static JwtTokenGenerator CreateGenerator() =>
        new(Options.Create(Settings), new FakeTimeProvider(FixedNow));

    private static User TestUser() => new()
    {
        Id = 42,
        Email = "ada@example.com",
        FullName = "Ada Lovelace"
    };

    private static JwtSecurityToken Read(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Token_carries_the_expected_identity_claims()
    {
        var (token, _) = CreateGenerator().GenerateToken(TestUser(), [RoleNames.Employee], employeeId: null);

        var jwt = Read(token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("ada@example.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("EnterpriseManagement.Api", jwt.Issuer);
        Assert.Contains("EnterpriseManagement.Client", jwt.Audiences);
    }

    [Fact]
    public void Token_never_contains_the_password_hash()
    {
        // The payload is base64, not encrypted: anyone holding the token can
        // read every claim. This asserts the rule that follows from that.
        var user = TestUser();
        user.PasswordHash = "$2a$11$SomeVerySecretHashValue";

        var (token, _) = CreateGenerator().GenerateToken(user, [RoleNames.Employee], null);

        Assert.DoesNotContain("$2a$", token);
        Assert.DoesNotContain("SomeVerySecretHashValue", token);
        Assert.DoesNotContain(user.PasswordHash, Read(token).Claims.Select(c => c.Value));
    }

    [Fact]
    public void Every_role_becomes_a_separate_role_claim()
    {
        var (token, _) = CreateGenerator()
            .GenerateToken(TestUser(), [RoleNames.Admin, RoleNames.Manager], null);

        var roles = Read(token).Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Assert.Equal(2, roles.Count);
        Assert.Contains(RoleNames.Admin, roles);
        Assert.Contains(RoleNames.Manager, roles);
    }

    [Fact]
    public void Employee_id_claim_is_present_only_when_the_user_has_one()
    {
        var withEmployee = Read(CreateGenerator().GenerateToken(TestUser(), [], employeeId: 7).Token);
        var withoutEmployee = Read(CreateGenerator().GenerateToken(TestUser(), [], employeeId: null).Token);

        Assert.Equal("7", withEmployee.Claims
            .First(c => c.Type == JwtTokenGenerator.EmployeeIdClaimType).Value);

        Assert.DoesNotContain(withoutEmployee.Claims,
            c => c.Type == JwtTokenGenerator.EmployeeIdClaimType);
    }

    [Fact]
    public void Expiry_honours_the_configured_lifetime()
    {
        var (_, expiresAt) = CreateGenerator().GenerateToken(TestUser(), [], null);

        Assert.Equal(FixedNow.UtcDateTime.AddMinutes(60), expiresAt);
    }

    [Fact]
    public void Each_token_has_a_unique_jti()
    {
        // The claim a revocation deny-list would key on, if revocation were added.
        var generator = CreateGenerator();

        var first = Read(generator.GenerateToken(TestUser(), [], null).Token);
        var second = Read(generator.GenerateToken(TestUser(), [], null).Token);

        Assert.NotEqual(
            first.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void Token_is_signed_with_HS256_and_validates_against_the_key()
    {
        var (token, _) = CreateGenerator().GenerateToken(TestUser(), [RoleNames.Employee], null);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = true,
            ValidIssuer = Settings.Issuer,
            ValidateAudience = true,
            ValidAudience = Settings.Audience,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);

        Assert.Equal(SecurityAlgorithms.HmacSha256, ((JwtSecurityToken)validated).Header.Alg);
        Assert.True(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public void A_token_signed_with_a_different_key_is_rejected()
    {
        // The property the whole scheme rests on: without the signing key you
        // cannot mint a token this API will accept.
        var attackerSettings = new JwtSettings
        {
            Key = "a-completely-different-key-also-32-bytes-long!!",
            Issuer = Settings.Issuer,
            Audience = Settings.Audience,
            ExpiryMinutes = 60
        };

        var forged = new JwtTokenGenerator(Options.Create(attackerSettings), new FakeTimeProvider(FixedNow))
            .GenerateToken(TestUser(), [RoleNames.Admin], null).Token;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(forged, parameters, out _));
    }
}

/// <summary>
/// Fixed clock, so expiry assertions are exact instead of approximate.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
