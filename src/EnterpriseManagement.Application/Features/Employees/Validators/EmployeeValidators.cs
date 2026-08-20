using EnterpriseManagement.Application.Features.Employees.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Employees.Validators;

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("Employee code is required.")
            .MaximumLength(20)
            // Constrained so the business key stays machine-readable and safe to
            // embed in exports and filenames.
            .Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Employee code may contain only letters, digits and hyphens.");

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(256)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(100);

        RuleFor(x => x.HireDate)
            .NotEmpty().WithMessage("Hire date is required.")
            // Rejects a date typed as 2206 instead of 2026. A future hire date
            // is legitimate for a signed-but-not-started employee, so a modest
            // window is allowed rather than banning the future outright.
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))
            .WithMessage("Hire date must not be more than a year in the future.")
            .Must(date => date >= new DateOnly(1950, 1, 1))
            .WithMessage("Hire date must be after 1950.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).WithMessage("A valid department is required.");

        RuleFor(x => x.UserId)
            .GreaterThan(0).When(x => x.UserId.HasValue)
            .WithMessage("User id must be a positive number when supplied.");
    }
}

public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(256)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(100);

        RuleFor(x => x.HireDate)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))
            .WithMessage("Hire date must not be more than a year in the future.")
            .Must(date => date >= new DateOnly(1950, 1, 1))
            .WithMessage("Hire date must be after 1950.");

        RuleFor(x => x.DepartmentId).GreaterThan(0);

        RuleFor(x => x.UserId)
            .GreaterThan(0).When(x => x.UserId.HasValue);
    }
}

public class EmployeeQueryParametersValidator : AbstractValidator<EmployeeQueryParameters>
{
    public EmployeeQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            EmployeeQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", EmployeeQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).When(x => x.DepartmentId.HasValue)
            .WithMessage("departmentId must be a positive number.");

        RuleFor(x => x.Search)
            // Bounded because the search runs a LIKE across four columns; an
            // unbounded pattern is an easy way to make every request expensive.
            .MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");
    }
}
