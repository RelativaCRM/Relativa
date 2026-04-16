using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Organization name is required.").MaximumLength(200);
    }
}
