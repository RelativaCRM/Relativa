using FluentValidation;
using Relativa.Core.Application.DTOs.OrgInvitation;

namespace Relativa.Core.Application.Validators;

public sealed class InviteToOrgRequestValidator : AbstractValidator<InviteToOrgRequest>
{
    public InviteToOrgRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("A valid email address is required.");
    }
}
