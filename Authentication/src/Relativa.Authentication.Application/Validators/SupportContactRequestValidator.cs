using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed class SupportContactRequestValidator : AbstractValidator<SupportContactRequest>
{
    public SupportContactRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .Matches(EmailValidation.Pattern).WithMessage(EmailValidation.Message);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(5000);

        RuleFor(x => x.Name)
            .MaximumLength(100);
    }
}
