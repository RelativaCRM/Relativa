using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class SmtpSmsSender(IEmailSender emailSender, IOptions<SmsOptions> options) : ISmsSender
{
    private readonly SmsOptions _opts = options.Value;

    public Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        var subject = $"SMS → {toPhone}";
        var html = $"<p style=\"font-family:'Inter',sans-serif;font-size:14px;\">{System.Net.WebUtility.HtmlEncode(message)}</p>";
        return emailSender.SendAsync(_opts.SinkEmail, subject, html, message, ct);
    }
}
