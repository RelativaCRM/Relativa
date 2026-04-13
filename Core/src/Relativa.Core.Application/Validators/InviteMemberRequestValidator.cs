using FluentValidation;
using Relativa.Core.Application.DTOs.Invitation;

namespace Relativa.Core.Application.Validators;

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("Role ID must be a positive integer.");
    }
}
