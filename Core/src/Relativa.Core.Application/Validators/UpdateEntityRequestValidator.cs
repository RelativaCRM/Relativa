using FluentValidation;
using Relativa.Core.Application.DTOs.Entity;

namespace Relativa.Core.Application.Validators;

public sealed class UpdateEntityRequestValidator : AbstractValidator<UpdateEntityRequest>
{
    public UpdateEntityRequestValidator()
    {
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
