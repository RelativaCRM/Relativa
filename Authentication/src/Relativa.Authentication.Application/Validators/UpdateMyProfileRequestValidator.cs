using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed class UpdateMyProfileRequestValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("A valid phone number is required.")
            .WithErrorCode("phone_invalid")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.DateOfBirth)
            .Must(d => d!.Value.Year > 1900 && d.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("A valid date of birth is required.")
            .WithErrorCode("birthdate_invalid")
            .When(x => x.DateOfBirth.HasValue);
    }
}
