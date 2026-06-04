using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.").WithErrorCode("token_required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.").WithErrorCode("password_required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.").WithErrorCode("password_too_short");
    }
}
