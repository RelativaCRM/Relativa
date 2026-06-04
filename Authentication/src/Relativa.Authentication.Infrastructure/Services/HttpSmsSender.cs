using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class HttpSmsSender(IOptions<SmsOptions> options, ILogger<HttpSmsSender> logger) : ISmsSender
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly SmsOptions _opts = options.Value;

    public async Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.Endpoint))
        {
            logger.LogError("Sms:Endpoint is not configured; cannot send SMS to {Phone}.", toPhone);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _opts.Endpoint)
        {
            Content = JsonContent.Create(new { to = toPhone, from = _opts.From, message }),
        };
        if (!string.IsNullOrWhiteSpace(_opts.AuthToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AuthToken);
        }

        try
        {
            using var response = await Http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Phone}.", toPhone);
        }
    }
}
