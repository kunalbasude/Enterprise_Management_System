using EnterpriseManagement.Application.Features.Auth.Dtos;
using EnterpriseManagement.Application.Features.Auth.Validators;

namespace EnterpriseManagement.Tests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Valid() => new()
    {
        Email = "ada@example.com",
        Password = "Analytical1",
        FullName = "Ada Lovelace"
    };

    [Fact]
    public void Accepts_a_well_formed_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]   // no @ at all
    [InlineData("@example.com")]   // no local part
    [InlineData("ada@")]           // no domain
    public void Rejects_a_malformed_email(string email)
    {
        var request = Valid();
        request.Email = email;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("Short1", "at least 8")]          // too short
    [InlineData("alllowercase1", "uppercase")]    // no uppercase
    [InlineData("ALLUPPERCASE1", "lowercase")]    // no lowercase
    [InlineData("NoDigitsHere", "digit")]         // no digit
    public void Rejects_a_weak_password(string password, string expectedFragment)
    {
        var request = Valid();
        request.Password = password;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(expectedFragment));
    }

    [Theory]
    [InlineData("user@localhost")]     // no dot: legal, and common on intranets
    [InlineData("user@tld")]
    [InlineData("a+tag@example.co.uk")]
    public void Accepts_addresses_that_a_strict_regex_would_wrongly_reject(string email)
    {
        // FluentValidation's EmailAddress() uses the lenient ASP.NET check
        // rather than a strict pattern, and that is correct. Hand-rolled email
        // regexes are famous for rejecting valid addresses (plus-tags,
        // dotless hosts, new TLDs). The only real proof an address works is
        // sending mail to it.
        var request = Valid();
        request.Email = email;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_a_password_beyond_the_bcrypt_72_byte_limit()
    {
        // BCrypt silently truncates past 72 bytes. Without this rule two
        // different long passwords would hash identically, so the extra
        // characters would give a false sense of strength.
        var request = Valid();
        request.Password = new string('A', 40) + new string('a', 40) + "1";

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("72"));
    }

    [Fact]
    public void Reports_every_failing_field_at_once()
    {
        // One round-trip should tell a user everything that is wrong, not the
        // first problem only.
        var result = _validator.Validate(new RegisterRequest
        {
            Email = "bad",
            Password = "weak",
            FullName = ""
        });

        var failedProperties = result.Errors.Select(e => e.PropertyName).Distinct().ToList();

        Assert.Equal(3, failedProperties.Count);
    }
}

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Requires_both_fields()
    {
        var result = _validator.Validate(new LoginRequest());

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Select(e => e.PropertyName).Distinct().Count());
    }

    [Fact]
    public void Does_not_apply_password_complexity_rules()
    {
        // Deliberate. Rejecting a weak password at login would reveal that the
        // stored password meets the complexity policy, and would lock out
        // accounts whose passwords predate a policy change.
        var result = _validator.Validate(new LoginRequest
        {
            Email = "ada@example.com",
            Password = "weak"
        });

        Assert.True(result.IsValid);
    }
}
