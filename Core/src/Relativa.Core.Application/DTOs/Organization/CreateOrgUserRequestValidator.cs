using FluentValidation;

namespace Relativa.Core.Application.DTOs.Organization;

public sealed class CreateOrgUserRequestValidator : AbstractValidator<CreateOrgUserRequest>
{
    public CreateOrgUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.OrgRoleId)
            .GreaterThan(0)
            .When(x => x.OrgRoleId.HasValue)
            .WithMessage("OrgRoleId must be greater than zero.");
    }
}
