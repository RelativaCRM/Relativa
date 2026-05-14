using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Validators;

public sealed class ChangeOrgMemberRoleRequestValidator : AbstractValidator<ChangeOrgMemberRoleRequest>
{
    public ChangeOrgMemberRoleRequestValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Role ID must be a positive integer.");
    }
}
