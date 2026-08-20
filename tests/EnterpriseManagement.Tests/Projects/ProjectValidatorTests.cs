using EnterpriseManagement.Application.Features.Projects.Dtos;
using EnterpriseManagement.Application.Features.Projects.Validators;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Tests.Projects;

public class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _validator = new();

    private static CreateProjectRequest Valid() => new()
    {
        Name = "Apollo",
        Code = "PRJ-2026-01",
        Description = "Rebuild the reporting pipeline",
        Status = ProjectStatus.Planned,
        StartDate = new DateOnly(2026, 2, 1),
        EndDate = new DateOnly(2026, 8, 1),
        ManagerEmployeeId = 5
    };

    [Fact]
    public void Accepts_a_valid_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Rejects_an_end_date_before_the_start_date()
    {
        var request = Valid();
        request.StartDate = new DateOnly(2026, 8, 1);
        request.EndDate = new DateOnly(2026, 2, 1);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("before the start date"));
    }

    [Fact]
    public void Allows_an_open_ended_project()
    {
        var request = Valid();
        request.EndDate = null;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Allows_a_single_day_project()
    {
        var request = Valid();
        request.EndDate = request.StartDate;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_an_out_of_range_status()
    {
        // An enum backed by int accepts any integer at the CLR level, so without
        // IsInEnum a value like 99 would reach the database as a valid-looking
        // status that no code branch handles.
        var request = Valid();
        request.Status = (ProjectStatus)99;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("valid project status"));
    }

    [Theory]
    [InlineData("PRJ-2026-01")]
    [InlineData("APOLLO")]
    [InlineData("p1")]
    public void Accepts_alphanumeric_and_hyphenated_codes(string code)
    {
        var request = Valid();
        request.Code = code;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("PRJ 2026")]
    [InlineData("PRJ/2026")]
    [InlineData("")]
    public void Rejects_codes_outside_the_allowed_character_set(string code)
    {
        var request = Valid();
        request.Code = code;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Requires_a_manager()
    {
        var request = Valid();
        request.ManagerEmployeeId = 0;

        Assert.False(_validator.Validate(request).IsValid);
    }
}

public class ProjectQueryParametersValidatorTests
{
    private readonly ProjectQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("name")]
    [InlineData("startDate")]
    [InlineData("STATUS")]
    [InlineData(null)]
    public void Accepts_whitelisted_sort_fields(string? sortBy)
    {
        Assert.True(_validator.Validate(new ProjectQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("managerEmployeeId")]
    [InlineData("description")]
    [InlineData("id; DROP TABLE projects;--")]
    public void Rejects_everything_else(string sortBy)
    {
        Assert.False(_validator.Validate(new ProjectQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Fact]
    public void Rejects_an_out_of_range_status_filter()
    {
        Assert.False(_validator.Validate(new ProjectQueryParameters
        {
            Status = (ProjectStatus)99
        }).IsValid);
    }

    [Fact]
    public void Allows_an_absent_status_filter()
    {
        Assert.True(_validator.Validate(new ProjectQueryParameters { Status = null }).IsValid);
    }
}

public class AssignEmployeeRequestValidatorTests
{
    private readonly AssignEmployeeRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_assignment()
    {
        Assert.True(_validator.Validate(new AssignEmployeeRequest
        {
            EmployeeId = 5,
            RoleOnProject = "Backend Developer"
        }).IsValid);
    }

    [Fact]
    public void Requires_an_employee_and_a_role()
    {
        var result = _validator.Validate(new AssignEmployeeRequest());

        Assert.Equal(2, result.Errors.Select(e => e.PropertyName).Distinct().Count());
    }
}
