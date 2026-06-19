using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Application.Emails;

public static class EmailVerificationEmail
{
    public static (string Subject, string Html, string Text) Build(
        IEmailLocalizer localizer,
        string? locale,
        string firstName,
        string code)
    {
        var subject = localizer.Get(locale, "emailVerification.subject");
        var title = localizer.Get(locale, "emailVerification.title");
        var body = localizer.Get(locale, "emailVerification.body", firstName);
        var footer = localizer.Get(locale, "emailVerification.footer");

        var html = EmailLayout.RenderCode(title, body, code, footer);
        var text = $"{body}\n\n{code}\n\n{footer}";

        return (subject, html, text);
    }
}
