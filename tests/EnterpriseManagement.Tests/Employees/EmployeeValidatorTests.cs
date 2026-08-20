using EnterpriseManagement.Application.Features.Employees.Dtos;
using EnterpriseManagement.Application.Features.Employees.Validators;

namespace EnterpriseManagement.Tests.Employees;

public class CreateEmployeeRequestValidatorTests
{
    private readonly CreateEmployeeRequestValidator _validator = new();

    private static CreateEmployeeRequest Valid() => new()
    {
        EmployeeCode = "EMP-0042",
        FirstName = "Grace",
        LastName = "Hopper",
        Email = "grace.hopper@example.com",
        JobTitle = "Engineer",
        HireDate = new DateOnly(2026, 3, 1),
        DepartmentId = 1
    };

    [Fact]
    public void Accepts_a_valid_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("EMP-0042")]
    [InlineData("GEN000123")]
    [InlineData("a1")]
    public void Accepts_alphanumeric_and_hyphenated_codes(string code)
    {
        var request = Valid();
        request.EmployeeCode = code;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("EMP 0042")]      // space
    [InlineData("EMP/0042")]      // slash would break URLs and filenames
    [InlineData("EMP;DROP")]      
    [InlineData("")]
    public void Rejects_codes_outside_the_allowed_character_set(string code)
    {
        var request = Valid();
        request.EmployeeCode = code;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_an_absurdly_future_hire_date()
    {
        // Catches a year typed as 2206 instead of 2026.
        var request = Valid();
        request.HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5));

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("year in the future"));
    }

    [Fact]
    public void Allows_a_near_future_hire_date_for_a_signed_but_unstarted_employee()
    {
        var request = Valid();
        request.HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2));

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_an_implausibly_old_hire_date()
    {
        var request = Valid();
        request.HireDate = new DateOnly(1900, 1, 1);

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Requires_a_positive_department_id()
    {
        var request = Valid();
        request.DepartmentId = 0;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Allows_an_absent_user_link()
    {
        // Not every employee has a login.
        var request = Valid();
        request.UserId = null;

        Assert.True(_validator.Validate(request).IsValid);
    }
}

public class EmployeeQueryParametersValidatorTests
{
    private readonly EmployeeQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("employeeCode")]
    [InlineData("lastName")]
    [InlineData("hireDate")]
    [InlineData("HIREDATE")]
    [InlineData(null)]
    public void Accepts_whitelisted_sort_fields(string? sortBy)
    {
        Assert.True(_validator.Validate(new EmployeeQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("salary")]
    [InlineData("passwordHash")]
    [InlineData("id; DROP TABLE employees;--")]
    public void Rejects_everything_else(string sortBy)
    {
        Assert.False(_validator.Validate(new EmployeeQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Fact]
    public void Rejects_a_non_positive_department_filter()
    {
        Assert.False(_validator.Validate(new EmployeeQueryParameters { DepartmentId = 0 }).IsValid);
    }

    [Fact]
    public void Bounds_the_search_term()
    {
        // The search runs a LIKE across four columns; an unbounded pattern is a
        // cheap way to make every request expensive.
        Assert.False(_validator.Validate(new EmployeeQueryParameters
        {
            Search = new string('a', 101)
        }).IsValid);
    }
}
