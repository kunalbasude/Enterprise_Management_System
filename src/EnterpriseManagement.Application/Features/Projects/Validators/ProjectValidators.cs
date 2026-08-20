using EnterpriseManagement.Application.Features.Projects.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Project code is required.")
            .MaximumLength(30)
            .Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Project code may contain only letters, digits and hyphens.");

        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);

        // IsInEnum matters: without it, ?status=99 binds successfully to an enum
        // whose underlying type is int, and an invalid value reaches the database.
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status must be a valid project status.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date must not be before the start date.");

        RuleFor(x => x.ManagerEmployeeId)
            .GreaterThan(0).WithMessage("A valid manager is required.");
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status must be a valid project status.");

        RuleFor(x => x.StartDate).NotEmpty();

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date must not be before the start date.");

        RuleFor(x => x.ManagerEmployeeId).GreaterThan(0);
    }
}

public class AssignEmployeeRequestValidator : AbstractValidator<AssignEmployeeRequest>
{
    public AssignEmployeeRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("A valid employee is required.");

        RuleFor(x => x.RoleOnProject)
            .NotEmpty().WithMessage("Role on project is required.")
            .MaximumLength(100);
    }
}

public class ProjectQueryParametersValidator : AbstractValidator<ProjectQueryParameters>
{
    public ProjectQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            ProjectQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", ProjectQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.Status)
            .IsInEnum().When(x => x.Status.HasValue)
            .WithMessage("status must be a valid project status.");

        RuleFor(x => x.ManagerEmployeeId)
            .GreaterThan(0).When(x => x.ManagerEmployeeId.HasValue);

        RuleFor(x => x.Search).MaximumLength(100);
    }
}
