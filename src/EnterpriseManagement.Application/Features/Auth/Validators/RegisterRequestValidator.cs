using EnterpriseManagement.Application.Features.Auth.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Auth.Validators;

/// <summary>
/// Validates registration input.
/// </summary>
/// <remarks>
/// FluentValidation rather than DataAnnotations: rules live in a testable class
/// instead of attributes on the DTO, conditional and cross-field rules are
/// expressible, and the same validator can be unit tested without a web host.
/// </remarks>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            // Length is the single biggest factor in resistance to brute force.
            // Eight is a floor, not a recommendation.
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            // BCrypt truncates input beyond 72 bytes. Silently ignoring the rest
            // would mean two different long passwords match the same hash, so
            // the limit is enforced and explained rather than hidden.
            .MaximumLength(72).WithMessage("Password must not exceed 72 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");
    }
}
