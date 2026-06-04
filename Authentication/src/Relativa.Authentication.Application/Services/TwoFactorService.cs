using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Services;

public sealed class TwoFactorService(
    IUserRepository userRepository,
    ITotpProvider totp,
    IOptions<TwoFactorOptions> options) : ITwoFactorService
{
    private readonly TwoFactorOptions _opts = options.Value;

    public async Task<TwoFactorStatusDto> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        return new TwoFactorStatusDto(user.TwoFactorEnabled);
    }

    public async Task<TwoFactorSetupDto> StartSetupAsync(int userId, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        if (user.TwoFactorEnabled)
        {
            throw new AuthException("two_factor_already_enabled", 409, "Two-factor authentication is already enabled.");
        }

        var secret = totp.GenerateSecret();
        user.TwoFactorSecret = secret;
        await userRepository.UpdateAsync(user, ct);

        return new TwoFactorSetupDto(secret, totp.BuildOtpAuthUri(secret, user.Email, _opts.Issuer));
    }

    public async Task<TwoFactorEnableResultDto> EnableAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        if (user.TwoFactorEnabled)
        {
            throw new AuthException("two_factor_already_enabled", 409, "Two-factor authentication is already enabled.");
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret) || !totp.VerifyCode(user.TwoFactorSecret, code))
        {
            throw new InvalidTwoFactorCodeException();
        }

        var masterCode = GenerateCode(_opts.MasterCodeLength);
        user.TwoFactorEnabled = true;
        user.TwoFactorMasterCodeHash = Hash(masterCode);
        await userRepository.UpdateAsync(user, ct);

        return new TwoFactorEnableResultDto(await GenerateAndStoreBackupCodes(userId, ct), masterCode);
    }

    public async Task DisableAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        if (!user.TwoFactorEnabled)
        {
            return;
        }

        if (!await VerifyAsync(user, code, ct))
        {
            throw new InvalidTwoFactorCodeException();
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorMasterCodeHash = null;
        await userRepository.UpdateAsync(user, ct);
        await userRepository.ReplaceBackupCodesAsync(userId, [], ct);
    }

    public async Task<TwoFactorBackupCodesDto> RegenerateBackupCodesAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        if (!user.TwoFactorEnabled)
        {
            throw new AuthException("two_factor_not_enabled", 409, "Two-factor authentication is not enabled.");
        }

        if (!await VerifyAsync(user, code, ct))
        {
            throw new InvalidTwoFactorCodeException();
        }

        return new TwoFactorBackupCodesDto(await GenerateAndStoreBackupCodes(userId, ct));
    }

    public async Task<TwoFactorMasterCodeDto> RegenerateMasterCodeAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await Require(userId, ct);
        if (!user.TwoFactorEnabled)
        {
            throw new AuthException("two_factor_not_enabled", 409, "Two-factor authentication is not enabled.");
        }

        if (!await VerifyAsync(user, code, ct))
        {
            throw new InvalidTwoFactorCodeException();
        }

        var masterCode = GenerateCode(_opts.MasterCodeLength);
        user.TwoFactorMasterCodeHash = Hash(masterCode);
        await userRepository.UpdateAsync(user, ct);

        return new TwoFactorMasterCodeDto(masterCode);
    }

    public async Task<bool> VerifyAsync(User user, string code, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(user.TwoFactorSecret) && totp.VerifyCode(user.TwoFactorSecret, code))
        {
            return true;
        }

        var hash = Hash(NormalizeBackupCode(code));

        if (!string.IsNullOrEmpty(user.TwoFactorMasterCodeHash) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(user.TwoFactorMasterCodeHash), Encoding.UTF8.GetBytes(hash)))
        {
            return true;
        }

        var active = await userRepository.GetActiveBackupCodesAsync(user.Id, ct);
        var match = active.FirstOrDefault(c => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(c.CodeHash), Encoding.UTF8.GetBytes(hash)));
        if (match is null)
        {
            return false;
        }

        await userRepository.ConsumeBackupCodeAsync(match, ct);
        return true;
    }

    private async Task<IReadOnlyList<string>> GenerateAndStoreBackupCodes(int userId, CancellationToken ct)
    {
        var plain = new List<string>(_opts.BackupCodeCount);
        var entities = new List<UserBackupCode>(_opts.BackupCodeCount);
        for (var i = 0; i < _opts.BackupCodeCount; i++)
        {
            var raw = GenerateCode(_opts.BackupCodeLength);
            plain.Add(Format(raw));
            entities.Add(new UserBackupCode
            {
                UserId = userId,
                CodeHash = Hash(raw),
                CreatedAt = DateTime.UtcNow,
            });
        }

        await userRepository.ReplaceBackupCodesAsync(userId, entities, ct);
        return plain;
    }

    private string GenerateCode(int length)
    {
        var alphabet = _opts.BackupCodeAlphabet;
        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }

    private static string Format(string raw) =>
        raw.Length >= 6 ? $"{raw[..(raw.Length / 2)]}-{raw[(raw.Length / 2)..]}" : raw;

    private static string NormalizeBackupCode(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private async Task<User> Require(int userId, CancellationToken ct) =>
        await userRepository.GetByIdAsync(userId, ct)
        ?? throw new AuthException("user_not_found", 404, "User not found.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
