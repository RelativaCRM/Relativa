using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateOrganizationSettingsRequestValidator : AbstractValidator<UpdateOrganizationSettingsRequest>
{
    private static readonly string[] AllowedJoinPolicies = ["open", "invite_only"];

    public UpdateOrganizationSettingsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(100).WithMessage("Organization name must be 100 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.JoinPolicy)
            .NotEmpty().WithMessage("Join policy is required.")
            .Must(v => AllowedJoinPolicies.Contains(v)).WithMessage("Join policy must be 'open' or 'invite_only'.");

        RuleFor(x => x.DefaultOrgRoleId)
            .GreaterThan(0).WithMessage("Default org role ID must be a positive integer.")
            .When(x => x.DefaultOrgRoleId.HasValue);
    }
}
