using FluentValidation;
using Relativa.Core.Application.DTOs.WsJoinRequest;

namespace Relativa.Core.Application.Validators;

public sealed class CreateWsJoinRequestRequestValidator : AbstractValidator<CreateWsJoinRequestRequest>
{
    public CreateWsJoinRequestRequestValidator()
    {
        RuleFor(x => x.Message)
            .MaximumLength(500).WithMessage("Message must be 500 characters or fewer.")
            .When(x => x.Message is not null);
    }
}
