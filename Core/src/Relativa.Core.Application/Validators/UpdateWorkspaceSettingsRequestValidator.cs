using FluentValidation;
using Relativa.Core.Application.DTOs.Workspace;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateWorkspaceSettingsRequestValidator : AbstractValidator<UpdateWorkspaceSettingsRequest>
{
    public UpdateWorkspaceSettingsRequestValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.HighRiskThreshold)
            .InclusiveBetween(0m, 1m).WithMessage("High risk threshold must be between 0 and 1.");

        RuleFor(x => x.MediumRiskThreshold)
            .InclusiveBetween(0m, 1m).WithMessage("Medium risk threshold must be between 0 and 1.")
            .LessThan(x => x.HighRiskThreshold).WithMessage("Medium risk threshold must be less than high risk threshold.");
    }
}
