using EnterpriseManagement.Application.Features.Departments.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Departments.Validators;

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            // Matches the column length in EmployeeConfiguration. Validating at
            // the same limit means an over-long value returns 400 with a clear
            // message instead of a database truncation error surfacing as 500.
            .MaximumLength(100).WithMessage("Department name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(100).WithMessage("Department name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

/// <summary>
/// Validates list query parameters.
/// </summary>
/// <remarks>
/// Page and page size are already clamped by <c>QueryParameters</c> rather than
/// rejected, so they are not re-validated here. Sort order is validated because
/// silently ignoring a misspelled <c>sortBy</c> hides a client bug: the caller
/// sees data in an unexpected order and has no idea why.
/// </remarks>
public class DepartmentQueryParametersValidator : AbstractValidator<DepartmentQueryParameters>
{
    public DepartmentQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            DepartmentQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", DepartmentQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");
    }
}
