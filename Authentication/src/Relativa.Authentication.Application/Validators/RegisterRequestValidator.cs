using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.").WithErrorCode("first_name_required")
            .MaximumLength(100).WithErrorCode("first_name_too_long");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.").WithErrorCode("last_name_required")
            .MaximumLength(100).WithErrorCode("last_name_too_long");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.").WithErrorCode("email_required")
            .Matches(EmailValidation.Pattern).WithMessage(EmailValidation.Message).WithErrorCode("email_invalid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.").WithErrorCode("password_required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.").WithErrorCode("password_too_short");
    }
}
