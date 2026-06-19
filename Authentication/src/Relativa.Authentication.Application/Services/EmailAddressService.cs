using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Emails;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Validators;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Services;

public sealed class EmailAddressService(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IEmailLocalizer emailLocalizer,
    IEmailRateLimiter rateLimiter,
    IOptions<EmailVerificationOptions> options,
    ILogger<EmailAddressService> logger) : IEmailAddressService
{
    private readonly EmailVerificationOptions _opts = options.Value;

    public async Task<IReadOnlyList<UserEmailDto>> ListAsync(int userId, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        var result = new List<UserEmailDto> { new(user.Email, true, true, "primary") };
        var extras = await userRepository.GetEmailsAsync(userId, ct);
        result.AddRange(extras.Select(e => new UserEmailDto(e.Address, false, e.IsVerified, e.Source)));
        return result;
    }

    public async Task AddAsync(int userId, string address, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(address);
        if (!Regex.IsMatch(normalized, EmailValidation.Pattern))
        {
            throw new AuthException("email_invalid", 400, EmailValidation.Message);
        }

        var user = await Require(userId, ct);
        if (await userRepository.EmailExistsAnywhereAsync(normalized, ct))
        {
            throw new EmailAddressTakenException();
        }

        if (!rateLimiter.TryConsume($"add-email:{userId}", _opts.MaxPerEmailPerHour, TimeSpan.FromHours(1)))
        {
            throw new RateLimitExceededException("Too many email requests. Please try again later.");
        }

        var code = GenerateCode();
        var email = new UserEmail
        {
            UserId = userId,
            Address = normalized,
            IsVerified = false,
            Source = "manual",
            VerificationToken = Hash(code),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(_opts.CodeLifetimeMinutes),
            CreatedAt = DateTime.UtcNow,
        };
        await userRepository.AddEmailAsync(email, ct);
        await SendCode(user, normalized, code, ct);
    }

    public async Task VerifyAsync(int userId, string address, string code, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(address);
        var email = await userRepository.GetEmailAsync(userId, normalized, ct)
            ?? throw new AuthException("email_address_not_found", 404, "Email address not found.");
        if (email.IsVerified)
        {
            return;
        }

        if (email.VerificationToken is null
            || email.VerificationTokenExpiresAt <= DateTime.UtcNow
            || email.VerificationToken != Hash(code.Trim().ToUpperInvariant()))
        {
            throw new InvalidVerificationCodeException();
        }

        email.IsVerified = true;
        email.VerificationToken = null;
        email.VerificationTokenExpiresAt = null;
        await userRepository.UpdateEmailAsync(email, ct);
    }

    public async Task ResendAsync(int userId, string address, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(address);
        var user = await Require(userId, ct);
        var email = await userRepository.GetEmailAsync(userId, normalized, ct)
            ?? throw new AuthException("email_address_not_found", 404, "Email address not found.");
        if (email.IsVerified)
        {
            return;
        }

        if (!rateLimiter.TryConsume($"add-email:{userId}", _opts.MaxPerEmailPerHour, TimeSpan.FromHours(1)))
        {
            return;
        }

        var code = GenerateCode();
        email.VerificationToken = Hash(code);
        email.VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(_opts.CodeLifetimeMinutes);
        await userRepository.UpdateEmailAsync(email, ct);
        await SendCode(user, normalized, code, ct);
    }

    public async Task SetPrimaryAsync(int userId, string address, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(address);
        var user = await Require(userId, ct);
        if (string.IsNullOrEmpty(user.Password))
        {
            throw new AuthException("primary_email_oauth_managed", 409, "The primary email is managed by your sign-in provider and cannot be changed.");
        }

        if (user.Email == normalized)
        {
            return;
        }

        var email = await userRepository.GetEmailAsync(userId, normalized, ct)
            ?? throw new AuthException("email_address_not_found", 404, "Email address not found.");
        if (!email.IsVerified)
        {
            throw new AuthException("email_not_verified_for_primary", 409, "This email must be verified before it can become primary.");
        }

        var previousPrimary = user.Email;
        await userRepository.RemoveEmailAsync(email, ct);
        user.Email = normalized;
        await userRepository.UpdateAsync(user, ct);
        await userRepository.AddEmailAsync(
            new UserEmail
            {
                UserId = userId,
                Address = previousPrimary,
                IsVerified = true,
                Source = "manual",
                CreatedAt = DateTime.UtcNow,
            },
            ct);
    }

    public async Task RemoveAsync(int userId, string address, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(address);
        var user = await Require(userId, ct);
        if (user.Email == normalized)
        {
            throw new AuthException("primary_email_cannot_remove", 409, "The primary email cannot be removed.");
        }

        var email = await userRepository.GetEmailAsync(userId, normalized, ct)
            ?? throw new AuthException("email_address_not_found", 404, "Email address not found.");
        await userRepository.RemoveEmailAsync(email, ct);
    }

    private async Task SendCode(User user, string address, string code, CancellationToken ct)
    {
        var locale = user.Settings?.Locale;
        var subject = emailLocalizer.Get(locale, "addEmail.subject");
        var title = emailLocalizer.Get(locale, "addEmail.title");
        var body = emailLocalizer.Get(locale, "addEmail.body", user.FirstName);
        var footer = emailLocalizer.Get(locale, "addEmail.footer");
        var html = EmailLayout.RenderCode(title, body, code, footer);
        var text = $"{body}\n\n{code}\n\n{footer}";

        try
        {
            await emailSender.SendAsync(address, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email-verification code to {Address}", address);
        }
    }

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

    private async Task<User> Require(int userId, CancellationToken ct) =>
        await userRepository.GetByIdAsync(userId, ct)
        ?? throw new AuthException("user_not_found", 404, "User not found.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
