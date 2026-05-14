using FluentValidation;
using Relativa.Core.Application.DTOs.Member;

namespace Relativa.Core.Application.Validators;

public sealed class AddWorkspaceMemberRequestValidator : AbstractValidator<AddWorkspaceMemberRequest>
{
    public AddWorkspaceMemberRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("User ID must be a positive integer.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Role ID must be a positive integer.");
    }
}
