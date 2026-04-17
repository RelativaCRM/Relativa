using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Validators;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Organization name is required.").MaximumLength(200);
    }
}
