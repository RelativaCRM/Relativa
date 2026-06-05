using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Application.Emails;

public static class PasswordResetEmail
{
    public static (string Subject, string Html, string Text) Build(
        IEmailLocalizer localizer,
        string? locale,
        string firstName,
        string resetLink)
    {
        var subject = localizer.Get(locale, "passwordReset.subject");
        var title = localizer.Get(locale, "passwordReset.title");
        var body = localizer.Get(locale, "passwordReset.body", firstName);
        var button = localizer.Get(locale, "passwordReset.button");
        var linkHelp = localizer.Get(locale, "passwordReset.linkHelp");
        var footer = localizer.Get(locale, "passwordReset.footer");

        var html = EmailLayout.Render(title, body, button, resetLink, linkHelp, footer);
        var text = $"{body}\n\n{resetLink}\n\n{footer}";

        return (subject, html, text);
    }
}
