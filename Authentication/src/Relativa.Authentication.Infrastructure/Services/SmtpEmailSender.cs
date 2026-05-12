using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody,
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _opts.Host,
            _opts.Port,
            _opts.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrEmpty(_opts.Username))
        {
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
