using FluentValidation;
using Relativa.Core.Application.DTOs.WsJoinRequest;

namespace Relativa.Core.Application.Validators;

public sealed class ReviewWsJoinRequestRequestValidator : AbstractValidator<ReviewWsJoinRequestRequest>
{
    public ReviewWsJoinRequestRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => d == "Approved" || d == "Rejected")
            .WithMessage("Decision must be 'Approved' or 'Rejected'.");
    }
}
