using FluentValidation;
using Relativa.Core.Application.DTOs.Workspace;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateWorkspaceRequestValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(200);
    }
}
