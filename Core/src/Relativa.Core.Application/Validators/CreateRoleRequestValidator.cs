using FluentValidation;
using Relativa.Core.Application.DTOs.Role;

namespace Relativa.Core.Application.Validators;

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100);

        RuleFor(x => x.PermissionIds)
            .NotEmpty().WithMessage("At least one permission is required.");
    }
}
