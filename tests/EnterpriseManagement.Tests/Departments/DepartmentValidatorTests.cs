using EnterpriseManagement.Application.Features.Departments.Dtos;
using EnterpriseManagement.Application.Features.Departments.Validators;

namespace EnterpriseManagement.Tests.Departments;

public class DepartmentQueryParametersValidatorTests
{
    private readonly DepartmentQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("name")]
    [InlineData("createdAt")]
    [InlineData("employeeCount")]
    [InlineData("NAME")]
    [InlineData(null)]
    [InlineData("")]
    public void Accepts_whitelisted_sort_fields(string? sortBy)
    {
        Assert.True(_validator.Validate(new DepartmentQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("id; DROP TABLE departments;--")]
    [InlineData("nonsense")]
    public void Rejects_anything_else_rather_than_silently_ignoring_it(string sortBy)
    {
        // A misspelled sortBy that silently falls back looks like a server bug
        // to the client: they get data in an order they did not ask for and no
        // explanation. A 400 says exactly what is wrong.
        var result = _validator.Validate(new DepartmentQueryParameters { SortBy = sortBy });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("sortBy must be one of"));
    }

    [Theory]
    [InlineData("asc", true)]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    [InlineData(null, true)]
    [InlineData("sideways", false)]
    public void Validates_sort_order(string? order, bool expectedValid)
    {
        var result = _validator.Validate(new DepartmentQueryParameters { SortOrder = order });

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Rejects_an_over_long_search_term()
    {
        var result = _validator.Validate(new DepartmentQueryParameters
        {
            Search = new string('a', 101)
        });

        Assert.False(result.IsValid);
    }
}

public class CreateDepartmentRequestValidatorTests
{
    private readonly CreateDepartmentRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_request()
    {
        var result = _validator.Validate(new CreateDepartmentRequest
        {
            Name = "Engineering",
            Description = "Builds things"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Requires_both_fields()
    {
        var result = _validator.Validate(new CreateDepartmentRequest());

        Assert.Equal(2, result.Errors.Select(e => e.PropertyName).Distinct().Count());
    }

    [Fact]
    public void Enforces_the_same_length_as_the_database_column()
    {
        // Validating at the column limit turns a would-be truncation error
        // (500) into a clear 400.
        var result = _validator.Validate(new CreateDepartmentRequest
        {
            Name = new string('a', 101),
            Description = "ok"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("100"));
    }
}
