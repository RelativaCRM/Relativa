using System.Security.Cryptography;
using FluentValidation;
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
    IEmailSender emailSender,
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

        if (!passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);

        return new LoginResponseDto(token, expiresAt);
    }

    public Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
        => userProvisioning.CreateUserAsync(request, auditActorUserId: null, ct);

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName);
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(int userId, UpdateMyProfileRequest request, CancellationToken ct = default)
    {
        await updateProfileValidator.ValidateAndThrowAsync(request, ct);
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
        var resetLink    = $"{frontendBaseUrl}/reset-password?token={plainToken}";
        var (html, text) = PasswordResetEmail.Build(user.FirstName, resetLink, expiry);

        await emailSender.SendAsync(
            to:       user.Email,
            subject:  PasswordResetEmail.Subject,
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
