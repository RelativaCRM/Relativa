using FluentValidation;
using Relativa.Core.Application.DTOs.OrgRole;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateOrgRoleRequestValidator : AbstractValidator<UpdateOrgRoleRequest>
{
    public UpdateOrgRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => x.Name is not null);

        RuleFor(x => x.Priority)
            .Must(p => !p.HasValue || p.Value >= 1)
            .WithMessage("Priority must be at least 1 when set.");
    }
}
