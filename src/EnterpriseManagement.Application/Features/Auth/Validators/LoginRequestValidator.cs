using EnterpriseManagement.Application.Features.Auth.Dtos;
using FluentValidation;

namespace EnterpriseManagement.Application.Features.Auth.Validators;

/// <summary>
/// Validates login input.
/// </summary>
/// <remarks>
/// Deliberately weaker than registration: only presence is checked. Applying the
/// complexity rules here would reject a wrong password before ever reaching the
/// hash comparison, telling an attacker that the stored password does meet those
/// rules — and it would lock out users whose passwords predate a policy change.
/// </remarks>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(72);
    }
}
