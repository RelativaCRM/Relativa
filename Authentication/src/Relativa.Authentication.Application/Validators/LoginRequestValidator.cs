using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.").WithErrorCode("email_required")
            .EmailAddress().WithMessage("A valid email address is required.").WithErrorCode("email_invalid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.").WithErrorCode("password_required");
    }
}
