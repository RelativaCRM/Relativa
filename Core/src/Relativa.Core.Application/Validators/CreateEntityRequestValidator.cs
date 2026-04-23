using FluentValidation;
using Relativa.Core.Application.DTOs.Entity;

namespace Relativa.Core.Application.Validators;

public sealed class CreateEntityRequestValidator : AbstractValidator<CreateEntityRequest>
{
    public CreateEntityRequestValidator()
    {
        RuleFor(x => x.EntityTypeId)
            .GreaterThan(0).WithMessage("EntityTypeId must be a positive integer.");

        RuleFor(x => x.Properties)
            .NotNull().WithMessage("Properties list is required.");

        RuleForEach(x => x.Properties)
            .ChildRules(pv =>
            {
                pv.RuleFor(p => p.PropertyId)
                    .GreaterThan(0).WithMessage("Each PropertyId must be a positive integer.");
            });
    }
}
