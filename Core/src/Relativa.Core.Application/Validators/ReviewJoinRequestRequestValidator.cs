using FluentValidation;
using Relativa.Core.Application.DTOs.JoinRequest;

namespace Relativa.Core.Application.Validators;

public sealed class ReviewJoinRequestRequestValidator : AbstractValidator<ReviewJoinRequestRequest>
{
    public ReviewJoinRequestRequestValidator()
    {
        RuleFor(x => x.Decision).NotEmpty().Must(d => d == "Approved" || d == "Rejected").WithMessage("Decision must be 'Approved' or 'Rejected'.");
    }
}
