using FluentValidation;
using Relativa.Core.Application.DTOs.Workspace;

namespace Relativa.Core.Application.Validators;

public sealed class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(200);
    }
}
