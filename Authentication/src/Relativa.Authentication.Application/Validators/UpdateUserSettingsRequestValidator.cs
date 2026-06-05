using System.Text.RegularExpressions;
using FluentValidation;
using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Validators;

public sealed partial class UpdateUserSettingsRequestValidator : AbstractValidator<UpdateUserSettingsRequest>
{
    public UpdateUserSettingsRequestValidator()
    {
        RuleFor(x => x.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("settings.locale_required")
            .Must(value => LocaleTag().IsMatch(value)).WithErrorCode("settings.locale_invalid");
    }

    [GeneratedRegex("^[a-z]{2,3}(-[A-Z]{2})?$")]
    private static partial Regex LocaleTag();
}
