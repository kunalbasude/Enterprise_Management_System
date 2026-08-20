using EnterpriseManagement.Application.Features.Users.Dtos;
using EnterpriseManagement.Domain.Common;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Users.Validators;

/// <summary>
/// Shared password rules, so the registration, admin-create, admin-reset and
/// self-change paths cannot drift apart.
/// </summary>
/// <remarks>
/// A weaker rule on any one of these endpoints would be the weakest link: an
/// attacker only needs the least protected route to set a guessable password.
/// </remarks>
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            // BCrypt truncates beyond 72 bytes, so anything longer gives a false
            // sense of strength.
            .MaximumLength(72).WithMessage("Password must not exceed 72 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password).Password();

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("At least one role must be assigned.");

        RuleForEach(x => x.Roles)
            .Must(role => RoleNames.All.Contains(role?.Trim().ToUpperInvariant()))
            .WithMessage($"Role must be one of: {string.Join(", ", RoleNames.All)}.");
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200);
    }
}

public class UpdateUserRolesRequestValidator : AbstractValidator<UpdateUserRolesRequest>
{
    public UpdateUserRolesRequestValidator()
    {
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("At least one role must be assigned.");

        RuleForEach(x => x.Roles)
            .Must(role => RoleNames.All.Contains(role?.Trim().ToUpperInvariant()))
            .WithMessage($"Role must be one of: {string.Join(", ", RoleNames.All)}.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).Password();
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        // Presence only, exactly as at login: applying complexity rules to the
        // CURRENT password would reveal whether it meets the policy, and would
        // lock out anyone whose password predates a policy change.
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword).Password();

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must differ from the current one.");
    }
}

public class UserQueryParametersValidator : AbstractValidator<UserQueryParameters>
{
    public UserQueryParametersValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            UserQueryParameters.SortableFields
                                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", UserQueryParameters.SortableFields)}.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.IsNullOrWhiteSpace(order) ||
                           order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                           order.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be 'asc' or 'desc'.");

        RuleFor(x => x.Role)
            .Must(role => string.IsNullOrWhiteSpace(role) ||
                          RoleNames.All.Contains(role.Trim().ToUpperInvariant()))
            .WithMessage($"role must be one of: {string.Join(", ", RoleNames.All)}.");

        RuleFor(x => x.Search).MaximumLength(100);
    }
}
