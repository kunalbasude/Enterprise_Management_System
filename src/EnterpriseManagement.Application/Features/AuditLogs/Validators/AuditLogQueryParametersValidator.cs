using EnterpriseManagement.Application.Features.AuditLogs.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.AuditLogs.Validators;

public class AuditLogQueryParametersValidator : AbstractValidator<AuditLogQueryParameters>
{
    public AuditLogQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            AuditLogQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", AuditLogQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.Action).IsInEnum().When(x => x.Action.HasValue);

        RuleFor(x => x.UserId).GreaterThan(0).When(x => x.UserId.HasValue);
        RuleFor(x => x.EntityId).GreaterThan(0).When(x => x.EntityId.HasValue);
        RuleFor(x => x.EntityType).MaximumLength(100);
        RuleFor(x => x.Search).MaximumLength(100);

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'to' must not be earlier than 'from'.");
    }
}
