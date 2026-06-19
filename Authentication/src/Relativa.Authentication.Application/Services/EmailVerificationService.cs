using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.Emails;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Services;

public sealed class EmailVerificationService(
    IUserRepository userRepository,
    IEmailSender emailSender,
    ISmsSender smsSender,
    IEmailLocalizer emailLocalizer,
    IEmailRateLimiter rateLimiter,
    IOptions<EmailVerificationOptions> options,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private readonly EmailVerificationOptions _opts = options.Value;

    public async Task ResendAsync(string email, VerificationChannel channel, string? clientIp, CancellationToken ct = default)
    {
        if (!AllowIp(clientIp))
        {
            return;
        }

        var normalized = EmailNormalizer.Normalize(email);
        var user = await userRepository.GetByEmailAsync(normalized, ct);
        if (user is null || user.EmailVerified)
        {
            return;
        }

        var resolved = channel == VerificationChannel.Sms && string.IsNullOrWhiteSpace(user.Phone)
            ? VerificationChannel.Email
            : channel;
        await SendAsync(user, resolved, ct);
    }

    public async Task<bool> SmsAvailableAsync(string email, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(email);
        var user = await userRepository.GetByEmailAsync(normalized, ct);
        return user is not null && !user.EmailVerified && !string.IsNullOrWhiteSpace(user.Phone);
    }

    public async Task VerifyAsync(string email, string code, string? clientIp, CancellationToken ct = default)
    {
        if (!AllowIp(clientIp))
        {
            throw new RateLimitExceededException("Too many verification attempts. Please try again later.");
        }

        var normalized = EmailNormalizer.Normalize(email);
        if (!rateLimiter.TryConsume($"verify-attempt:{normalized}", _opts.MaxVerifyAttemptsPerHour, TimeSpan.FromHours(1)))
        {
            throw new RateLimitExceededException("Too many verification attempts. Please try again later.");
        }

        var user = await userRepository.GetByEmailAsync(normalized, ct);
        if (user is null
            || user.EmailVerificationToken is null
            || user.EmailVerificationTokenExpiresAt <= DateTime.UtcNow
            || user.EmailVerificationToken != Hash(NormalizeCode(code)))
        {
            throw new InvalidVerificationCodeException();
        }

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await userRepository.UpdateAsync(user, ct);
    }

    private async Task SendAsync(User user, VerificationChannel channel, CancellationToken ct)
    {
        if (!rateLimiter.TryConsume($"verify-cooldown:{channel}:{user.Id}", 1, TimeSpan.FromSeconds(_opts.ResendCooldownSeconds)))
        {
            return;
        }

        var rateKey = channel == VerificationChannel.Sms
            ? $"verify-sms:{user.Phone}"
            : $"verify-email:{user.Email}";
        if (!rateLimiter.TryConsume(rateKey, _opts.MaxPerEmailPerHour, TimeSpan.FromHours(1)))
        {
            return;
        }

        var code = GenerateCode();
        user.EmailVerificationToken = Hash(code);
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(_opts.CodeLifetimeMinutes);
        await userRepository.UpdateAsync(user, ct);

        var locale = user.Settings?.Locale;

        if (channel == VerificationChannel.Sms)
        {
            var sms = emailLocalizer.Get(locale, "smsVerification.body", code);
            try
            {
                await smsSender.SendAsync(user.Phone!, sms, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send verification SMS to user {UserId}", user.Id);
            }

            return;
        }

        var (subject, html, text) = EmailVerificationEmail.Build(emailLocalizer, locale, user.FirstName, code);
        try
        {
            await emailSender.SendAsync(user.Email, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
        }
    }

    private bool AllowIp(string? clientIp) =>
        string.IsNullOrWhiteSpace(clientIp)
        || rateLimiter.TryConsume($"verify-ip:{clientIp}", _opts.MaxPerIpPerHour, TimeSpan.FromHours(1));

    private string GenerateCode()
    {
        var alphabet = _opts.CodeAlphabet;
        var chars = new char[_opts.CodeLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }

    private static string NormalizeCode(string code) =>
        code.Trim().ToUpperInvariant();

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
