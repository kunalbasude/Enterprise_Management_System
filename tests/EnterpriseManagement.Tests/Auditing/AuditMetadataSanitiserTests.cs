using EnterpriseManagement.Infrastructure.Auditing;

namespace EnterpriseManagement.Tests.Auditing;

/// <summary>
/// Covers the guard that keeps credentials out of the audit trail.
/// </summary>
/// <remarks>
/// The audit log is deliberately long-lived, widely readable by administrators,
/// and frequently exported — the worst possible place for a secret to land.
/// "Do not pass passwords to the audit service" holds until someone logs a whole
/// request object, which is why this is enforced structurally.
/// </remarks>
public class AuditMetadataSanitiserTests
{
    [Fact]
    public void Null_metadata_produces_null()
    {
        Assert.Null(AuditMetadataSanitiser.Sanitise(null));
    }

    [Fact]
    public void Ordinary_fields_survive_intact()
    {
        var json = AuditMetadataSanitiser.Sanitise(new { Email = "ada@example.com", Roles = new[] { "ADMIN" } });

        Assert.Contains("ada@example.com", json);
        Assert.Contains("ADMIN", json);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("NewPassword")]
    [InlineData("current_password")]
    [InlineData("PasswordHash")]
    [InlineData("passwd")]
    [InlineData("Token")]
    [InlineData("accessToken")]
    [InlineData("RefreshToken")]
    [InlineData("Authorization")]
    [InlineData("ApiKey")]
    [InlineData("api_key")]
    [InlineData("ClientSecret")]
    [InlineData("PrivateKey")]
    [InlineData("ConnectionString")]
    [InlineData("Salt")]
    public void Any_credential_shaped_property_is_redacted(string propertyName)
    {
        // Matching on the NAME, not the value: a hash, a token and a password
        // are indistinguishable as strings, but their field names are not.
        var metadata = new Dictionary<string, object> { [propertyName] = "super-secret-value" };

        var json = AuditMetadataSanitiser.Sanitise(metadata)!;

        Assert.DoesNotContain("super-secret-value", json);
        Assert.Contains(AuditMetadataSanitiser.RedactedMarker, json);
    }

    [Fact]
    public void Redaction_reaches_nested_objects()
    {
        // The realistic leak: somebody logs a whole request object rather than
        // named fields.
        var metadata = new
        {
            Action = "login",
            Request = new
            {
                Email = "ada@example.com",
                Password = "hunter2"
            }
        };

        var json = AuditMetadataSanitiser.Sanitise(metadata)!;

        Assert.DoesNotContain("hunter2", json);
        Assert.Contains("ada@example.com", json);
    }

    [Fact]
    public void Redaction_reaches_inside_arrays()
    {
        var metadata = new
        {
            Attempts = new[]
            {
                new { User = "ada", Password = "first-secret" },
                new { User = "bob", Password = "second-secret" }
            }
        };

        var json = AuditMetadataSanitiser.Sanitise(metadata)!;

        Assert.DoesNotContain("first-secret", json);
        Assert.DoesNotContain("second-secret", json);
        Assert.Contains("ada", json);
    }

    [Fact]
    public void A_real_bcrypt_hash_is_redacted_when_named_as_such()
    {
        var metadata = new { Email = "ada@example.com", PasswordHash = "$2a$11$N9qo8uLOickgx2ZMRZoMy" };

        var json = AuditMetadataSanitiser.Sanitise(metadata)!;

        Assert.DoesNotContain("$2a$", json);
    }

    [Fact]
    public void A_jwt_is_redacted_when_named_as_a_token()
    {
        var metadata = new { AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.sig" };

        var json = AuditMetadataSanitiser.Sanitise(metadata)!;

        Assert.DoesNotContain("eyJ", json);
    }

    [Fact]
    public void Unserialisable_metadata_records_a_marker_rather_than_throwing()
    {
        // An audit write must never break the operation it is describing.
        var json = AuditMetadataSanitiser.Sanitise(new SelfReferencing());

        Assert.NotNull(json);
    }

    private sealed class SelfReferencing
    {
        public SelfReferencing() => Self = this;

        public SelfReferencing Self { get; }
    }
}
