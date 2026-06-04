using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Validators;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Application.Services;

public sealed class SupportService(
    IEmailSender emailSender,
    IEmailLocalizer emailLocalizer,
    IEmailRateLimiter rateLimiter,
    IValidator<SupportContactRequest> validator,
    IOptions<SupportOptions> options) : ISupportService
{
    private readonly SupportOptions _opts = options.Value;

    public async Task SendContactAsync(SupportContactRequest request, string? clientIp = null, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        if (string.IsNullOrWhiteSpace(_opts.DevEmail) || !Regex.IsMatch(_opts.DevEmail, EmailValidation.Pattern))
        {
            throw new ConfigurationException("Support:DevEmail is not configured with a valid email address.");
        }

        if (!string.IsNullOrWhiteSpace(clientIp)
            && !rateLimiter.TryConsume($"support-ip:{clientIp}", _opts.MaxPerIpPerHour, TimeSpan.FromHours(1)))
        {
            throw new RateLimitExceededException("Too many support requests. Please try again later.");
        }

        var sender = string.IsNullOrWhiteSpace(request.Name)
            ? request.Email
            : $"{request.Name} <{request.Email}>";

        var subject = emailLocalizer.Get(request.Locale, "support.subject", request.Subject);
        var fromLine = emailLocalizer.Get(request.Locale, "support.from", sender);

        var html = $"""
<p style="font-family:'Inter',sans-serif;font-size:14px;color:#0f172a;"><strong>{WebUtility.HtmlEncode(fromLine)}</strong></p>
<p style="font-family:'Inter',sans-serif;font-size:14px;color:#0f172a;white-space:pre-wrap;">{WebUtility.HtmlEncode(request.Message)}</p>
""";
        var text = $"{fromLine}\n\n{request.Message}";

        await emailSender.SendAsync(_opts.DevEmail, subject, html, text, ct);
    }
}
