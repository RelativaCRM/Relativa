using FluentValidation;
using Relativa.Core.Application.DTOs.Invitation;

namespace Relativa.Core.Application.Validators;

public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required.");
    }
}
