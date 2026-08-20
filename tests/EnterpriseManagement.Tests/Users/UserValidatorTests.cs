using EnterpriseManagement.Application.Features.Users.Dtos;
using EnterpriseManagement.Application.Features.Users.Validators;

namespace EnterpriseManagement.Tests.Users;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() => new()
    {
        Email = "new.user@example.com",
        Password = "Passw0rdOk",
        FullName = "New User",
        Roles = ["EMPLOYEE"]
    };

    [Fact]
    public void Accepts_a_valid_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    [InlineData("admin")]     // normalised before comparison
    public void Accepts_seeded_role_names(string role)
    {
        var request = Valid();
        request.Roles = [role];

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("SUPERUSER")]
    [InlineData("ROOT")]
    [InlineData("")]
    public void Rejects_unknown_roles(string role)
    {
        // Silently dropping an unrecognised role would create an account with
        // fewer privileges than intended and no error to explain it.
        var request = Valid();
        request.Roles = [role];

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Requires_at_least_one_role()
    {
        var request = Valid();
        request.Roles = [];

        Assert.False(_validator.Validate(request).IsValid);
    }
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_change()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "OldPassw0rd",
            NewPassword = "NewPassw0rd"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_reusing_the_same_password()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "SamePassw0rd",
            NewPassword = "SamePassw0rd"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Applies_complexity_to_the_new_password_only()
    {
        // The current password is checked for presence alone. Enforcing
        // complexity on it would reveal that the stored password meets the
        // policy, and would lock out anyone whose password predates it.
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "old",
            NewPassword = "NewPassw0rd"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Requires_the_current_password()
    {
        // Its absence is what would let a stolen token become permanent
        // account takeover.
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "",
            NewPassword = "NewPassw0rd"
        });

        Assert.False(result.IsValid);
    }
}

public class UserQueryParametersValidatorTests
{
    private readonly UserQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("email")]
    [InlineData("fullName")]
    [InlineData("createdAt")]
    [InlineData("lastLoginAt")]
    [InlineData(null)]
    public void Accepts_whitelisted_sort_fields(string? sortBy)
    {
        Assert.True(_validator.Validate(new UserQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("password_hash")]
    [InlineData("email; DROP TABLE users;--")]
    public void Rejects_everything_else(string sortBy)
    {
        Assert.False(_validator.Validate(new UserQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("employee", true)]
    [InlineData(null, true)]
    [InlineData("SUPERUSER", false)]
    public void Validates_the_role_filter(string? role, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(new UserQueryParameters { Role = role }).IsValid);
    }
}
