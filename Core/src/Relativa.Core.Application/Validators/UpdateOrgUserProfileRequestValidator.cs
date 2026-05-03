using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateOrgUserProfileRequestValidator : AbstractValidator<UpdateOrgUserProfileRequest>
{
    public UpdateOrgUserProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);
    }
}
