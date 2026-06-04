using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Emails;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IUserProvisioningService userProvisioning,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IExternalIdentityVerifier externalIdentityVerifier,
    ITwoFactorService twoFactorService,
    IEmailSender emailSender,
    IEmailLocalizer emailLocalizer,
    IConfiguration configuration,
    IValidator<LoginRequestDto> loginValidator,
    IValidator<UpdateMyProfileRequest> updateProfileValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator) : IAuthService
{
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = EmailNormalizer.Normalize(request.Email);
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (string.IsNullOrEmpty(user.Password) || !passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.EmailVerified)
            throw new EmailNotVerifiedException(user.Email);

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                throw new TwoFactorRequiredException();

            if (!await twoFactorService.VerifyAsync(user, request.TwoFactorCode, ct))
                throw new InvalidTwoFactorCodeException();
        }

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);

        return new LoginResponseDto(token, expiresAt);
    }

    public async Task<LoginResponseDto> OAuthLoginAsync(string provider, OAuthLoginRequestDto request, CancellationToken ct = default)
    {
        var identity = await externalIdentityVerifier.VerifyAsync(provider, request.Token, ct);

        var user = await userRepository.GetByExternalLoginAsync(identity.Provider, identity.Subject, ct);
        if (user is null)
        {
            var email = EmailNormalizer.Normalize(identity.Email);
            user = await userRepository.GetByEmailAsync(email, ct);
            if (user is not null)
            {
                await userRepository.AddExternalLoginAsync(
                    new Persistence.Entities.UserExternalLogin
                    {
                        UserId = user.Id,
                        Provider = identity.Provider,
                        Subject = identity.Subject,
                        CreatedAt = DateTime.UtcNow
                    },
                    ct);
            }
            else
            {
                user = await userProvisioning.CreateExternalUserAsync(identity, ct);
            }
        }

        await AssociateProviderEmail(user, identity, ct);

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);

        return new LoginResponseDto(token, expiresAt);
    }

    public async Task LinkProviderAsync(int userId, string provider, OAuthLoginRequestDto request, CancellationToken ct = default)
    {
        var identity = await externalIdentityVerifier.VerifyAsync(provider, request.Token, ct);

        var existing = await userRepository.GetByExternalLoginAsync(identity.Provider, identity.Subject, ct);
        if (existing is not null && existing.Id != userId)
        {
            throw new InvalidOperationException("This provider account is already linked to a different user.");
        }

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (existing is null)
        {
            await userRepository.AddExternalLoginAsync(
                new Persistence.Entities.UserExternalLogin
                {
                    UserId = userId,
                    Provider = identity.Provider,
                    Subject = identity.Subject,
                    CreatedAt = DateTime.UtcNow,
                },
                ct);
        }

        await AssociateProviderEmail(user, identity, ct);
    }

    private async Task AssociateProviderEmail(Persistence.Entities.User user, ExternalIdentity identity, CancellationToken ct)
    {
        var providerEmail = EmailNormalizer.Normalize(identity.Email);
        if (string.IsNullOrWhiteSpace(providerEmail) || providerEmail == user.Email)
        {
            return;
        }

        try
        {
            var existing = await userRepository.GetEmailAsync(user.Id, providerEmail, ct);
            if (existing is null && !await userRepository.EmailExistsAnywhereAsync(providerEmail, ct))
            {
                await userRepository.AddEmailAsync(
                    new Persistence.Entities.UserEmail
                    {
                        UserId = user.Id,
                        Address = providerEmail,
                        IsVerified = true,
                        Source = identity.Provider,
                        CreatedAt = DateTime.UtcNow,
                    },
                    ct);
            }
        }
        catch
        {
            return;
        }
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = EmailNormalizer.Normalize(email);
        return await userRepository.ExistsAsync(normalized, ct);
    }

    public Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
    {
        ValidateSelfRegistration(request);
        return userProvisioning.CreateUserAsync(request, auditActorUserId: null, ct);
    }

    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{6,14}$", RegexOptions.Compiled);

    private static void ValidateSelfRegistration(RegisterRequestDto request)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            failures.Add(new ValidationFailure(nameof(request.Phone), "Phone number is required.") { ErrorCode = "phone_required" });
        }
        else if (!PhoneRegex.IsMatch(request.Phone))
        {
            failures.Add(new ValidationFailure(nameof(request.Phone), "A valid phone number is required.") { ErrorCode = "phone_invalid" });
        }

        if (request.DateOfBirth is null
            || request.DateOfBirth <= new DateOnly(1900, 1, 1)
            || request.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            failures.Add(new ValidationFailure(nameof(request.DateOfBirth), "A valid date of birth is required.") { ErrorCode = "birthdate_invalid" });
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserProfileDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.TwoFactorEnabled,
            user.Phone, user.DateOfBirth,
            user.ExternalLogins.Select(l => l.Provider).Distinct().ToList(),
            !string.IsNullOrEmpty(user.Password));
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(int userId, UpdateMyProfileRequest request, CancellationToken ct = default)
    {
        await updateProfileValidator.ValidateAndThrowAsync(request, ct);

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.DateOfBirth = request.DateOfBirth;
        await userRepository.UpdateAsync(user, ct);

        return await userProvisioning.UpdateUserProfileAsync(userId, request.FirstName, request.LastName, userId, ct);
    }

    public Task DeleteMyAccountAsync(int userId, CancellationToken ct = default)
        => userProvisioning.ArchiveUserAsync(userId, userId, ct);

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        await forgotPasswordValidator.ValidateAndThrowAsync(new ForgotPasswordRequest(email), ct);

        var normalized = EmailNormalizer.Normalize(email);
        var user       = await userRepository.GetByEmailAsync(normalized, ct);

        if (user is null)
        {
            return;
        }

        var expiry     = TimeSpan.FromHours(1);
        var plainToken = RandomNumberGenerator.GetHexString(64);
        var tokenHash  = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainToken)));

        user.PasswordResetToken          = tokenHash;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(expiry);
        await userRepository.UpdateAsync(user, ct);

        var frontendBaseUrl = configuration["App:FrontendBaseUrl"]
            ?? throw new ConfigurationException("App:FrontendBaseUrl is not configured.");
        var resetLink           = $"{frontendBaseUrl}/reset-password?token={plainToken}";
        var locale              = user.Settings?.Locale;
        var (subject, html, text) = PasswordResetEmail.Build(emailLocalizer, locale, user.FirstName, resetLink);

        await emailSender.SendAsync(
            to:       user.Email,
            subject:  subject,
            htmlBody: html,
            textBody: text,
            ct:       ct);
    }

    public async Task ValidateResetTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var user      = await userRepository.GetByResetTokenAsync(tokenHash, ct);

        if (user is null)
        {
            throw new ArgumentException("Invalid or expired reset token.");
        }
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        await resetPasswordValidator.ValidateAndThrowAsync(new ResetPasswordRequest(token, newPassword), ct);

        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var user      = await userRepository.GetByResetTokenAsync(tokenHash, ct)
            ?? throw new ArgumentException("Invalid or expired reset token.");

        user.Password                    = passwordHasher.Hash(newPassword);
        user.PasswordResetToken          = null;
        user.PasswordResetTokenExpiresAt = null;
        await userRepository.UpdateAsync(user, ct);
    }
}
