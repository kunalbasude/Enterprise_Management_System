using EnterpriseManagement.Application.Features.Tasks.Dtos;
using EnterpriseManagement.Application.Features.Tasks.Validators;
using EnterpriseManagement.Domain.Enums;

namespace EnterpriseManagement.Tests.Tasks;

public class CreateTaskRequestValidatorTests
{
    private readonly CreateTaskRequestValidator _validator = new();

    private static CreateTaskRequest Valid() => new()
    {
        Title = "Build the report exporter",
        Description = "CSV export for the finance team",
        Priority = TaskPriority.High,
        ProjectId = 1,
        DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        EstimatedHours = 16.5m
    };

    [Fact]
    public void Accepts_a_valid_request()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Requires_a_project()
    {
        // A task cannot exist outside a project: the project is what determines
        // who is authorised to touch it.
        var request = Valid();
        request.ProjectId = 0;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Allows_an_unassigned_task()
    {
        // Backlog items exist before anyone owns them.
        var request = Valid();
        request.AssignedEmployeeId = null;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Allows_a_task_with_no_due_date()
    {
        var request = Valid();
        request.DueDate = null;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(0.1)]        // below the minimum
    [InlineData(10000.00)]   // beyond decimal(6,2)
    public void Rejects_estimated_hours_outside_the_column_range(decimal hours)
    {
        // Validating at the same bound as the decimal(6,2) column turns a
        // database overflow (500) into a clear 400.
        var request = Valid();
        request.EstimatedHours = hours;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_a_due_date_far_in_the_past()
    {
        var request = Valid();
        request.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3));

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Allows_a_recently_backdated_due_date()
    {
        // Tasks do get logged after the fact.
        var request = Valid();
        request.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_an_undefined_priority()
    {
        var request = Valid();
        request.Priority = (TaskPriority)99;

        Assert.False(_validator.Validate(request).IsValid);
    }
}

public class UpdateTaskStatusRequestValidatorTests
{
    private readonly UpdateTaskStatusRequestValidator _validator = new();

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Cancelled)]
    public void Accepts_any_defined_status(TaskItemStatus status)
    {
        // The validator only checks the value is a real enum member. Whether the
        // MOVE is legal depends on the task's stored status, which a validator
        // cannot see - that rule lives in the domain and is enforced by the
        // service.
        Assert.True(_validator.Validate(new UpdateTaskStatusRequest { Status = status }).IsValid);
    }

    [Fact]
    public void Rejects_an_undefined_status()
    {
        Assert.False(_validator.Validate(new UpdateTaskStatusRequest
        {
            Status = (TaskItemStatus)99
        }).IsValid);
    }
}

public class TaskQueryParametersValidatorTests
{
    private readonly TaskQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("dueDate")]
    [InlineData("priority")]
    [InlineData("STATUS")]
    [InlineData(null)]
    public void Accepts_whitelisted_sort_fields(string? sortBy)
    {
        Assert.True(_validator.Validate(new TaskQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Theory]
    [InlineData("completedAt")]
    [InlineData("assignedEmployeeId")]
    [InlineData("id; DROP TABLE tasks;--")]
    public void Rejects_everything_else(string sortBy)
    {
        Assert.False(_validator.Validate(new TaskQueryParameters { SortBy = sortBy }).IsValid);
    }

    [Fact]
    public void Rejects_undefined_enum_filters()
    {
        Assert.False(_validator.Validate(new TaskQueryParameters { Status = (TaskItemStatus)99 }).IsValid);
        Assert.False(_validator.Validate(new TaskQueryParameters { Priority = (TaskPriority)99 }).IsValid);
    }
}
