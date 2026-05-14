using FluentValidation;
using Relativa.Core.Application.DTOs.Member;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateMemberRoleRequestValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("Role ID must be a positive integer.");
    }
}
