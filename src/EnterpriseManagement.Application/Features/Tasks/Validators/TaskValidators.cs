using EnterpriseManagement.Application.Features.Tasks.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Tasks.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Priority must be a valid task priority.");

        RuleFor(x => x.ProjectId)
            .GreaterThan(0).WithMessage("A valid project is required.");

        RuleFor(x => x.AssignedEmployeeId)
            .GreaterThan(0).When(x => x.AssignedEmployeeId.HasValue);

        RuleFor(x => x.EstimatedHours)
            // Matches the decimal(6,2) column: validating at the same bound
            // turns a database overflow (500) into a clear 400.
            .InclusiveBetween(0.25m, 9999.99m)
            .When(x => x.EstimatedHours.HasValue)
            .WithMessage("Estimated hours must be between 0.25 and 9999.99.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)))
            .When(x => x.DueDate.HasValue)
            // A due date far in the past is almost always a typo. A modest
            // backdate is allowed, because tasks do get logged after the fact.
            .WithMessage("Due date must not be more than a year in the past.");
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);

        RuleFor(x => x.Priority).IsInEnum();

        RuleFor(x => x.AssignedEmployeeId)
            .GreaterThan(0).When(x => x.AssignedEmployeeId.HasValue);

        RuleFor(x => x.EstimatedHours)
            .InclusiveBetween(0.25m, 9999.99m)
            .When(x => x.EstimatedHours.HasValue);
    }
}

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        // Only checks that the value is a defined enum member. Whether the move
        // is legal depends on the task's current status, which is a domain rule
        // enforced in the service — a validator has no access to stored state.
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status must be a valid task status.");
    }
}

public class TaskQueryParametersValidator : AbstractValidator<TaskQueryParameters>
{
    public TaskQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            TaskQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", TaskQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.ProjectId).GreaterThan(0).When(x => x.ProjectId.HasValue);
        RuleFor(x => x.AssignedEmployeeId).GreaterThan(0).When(x => x.AssignedEmployeeId.HasValue);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
