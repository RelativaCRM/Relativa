using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Moq;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class TwoFactorServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ITotpProvider> _totp = new();
    private readonly TwoFactorOptions _options = new();
    private readonly TwoFactorService _sut;

    public TwoFactorServiceTests()
    {
        _sut = new TwoFactorService(_userRepo.Object, _totp.Object, Create(_options));
    }

    private void HasUser(User user) =>
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [Fact]
    public async Task GetStatusAsync_ReflectsEnabledFlag()
    {
        HasUser(new User { Id = 1, Email = "a@b.io", TwoFactorEnabled = true });

        var result = await _sut.GetStatusAsync(1);

        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatusAsync_UnknownUser_ThrowsUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetStatusAsync(99);

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task StartSetupAsync_GeneratesSecretAndPersistsIt()
    {
        var user = new User { Id = 1, Email = "user@relativa.io" };
        HasUser(user);
        _totp.Setup(t => t.GenerateSecret()).Returns("SECRET123");
        _totp.Setup(t => t.BuildOtpAuthUri("SECRET123", user.Email, _options.Issuer)).Returns("otpauth://uri");

        var result = await _sut.StartSetupAsync(1);

        result.Secret.Should().Be("SECRET123");
        result.OtpauthUri.Should().Be("otpauth://uri");
        user.TwoFactorSecret.Should().Be("SECRET123");
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartSetupAsync_AlreadyEnabled_Throws()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true });

        var act = () => _sut.StartSetupAsync(1);

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("two_factor_already_enabled");
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableAsync_ValidCode_EnablesAndIssuesMasterAndBackupCodes()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "123456")).Returns(true);

        var result = await _sut.EnableAsync(1, "123456");

        user.TwoFactorEnabled.Should().BeTrue();
        user.TwoFactorMasterCodeHash.Should().NotBeNullOrEmpty();
        result.MasterCode.Should().HaveLength(_options.MasterCodeLength);
        result.BackupCodes.Should().HaveCount(_options.BackupCodeCount);
        _userRepo.Verify(r => r.ReplaceBackupCodesAsync(1, It.Is<IReadOnlyList<UserBackupCode>>(c => c.Count == _options.BackupCodeCount), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_NoSecret_ThrowsInvalidCode()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = null });

        var act = () => _sut.EnableAsync(1, "123456");

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
    }

    [Fact]
    public async Task EnableAsync_WrongCode_ThrowsInvalidCode()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "000000")).Returns(false);

        var act = () => _sut.EnableAsync(1, "000000");

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task EnableAsync_AlreadyEnabled_Throws()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true });

        var act = () => _sut.EnableAsync(1, "123456");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("two_factor_already_enabled");
    }

    [Fact]
    public async Task DisableAsync_ValidCode_ClearsSecretsAndBackupCodes()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true, TwoFactorSecret = "SECRET", TwoFactorMasterCodeHash = "hash" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "123456")).Returns(true);

        await _sut.DisableAsync(1, "123456");

        user.TwoFactorEnabled.Should().BeFalse();
        user.TwoFactorSecret.Should().BeNull();
        user.TwoFactorMasterCodeHash.Should().BeNull();
        _userRepo.Verify(r => r.ReplaceBackupCodesAsync(1, It.Is<IReadOnlyList<UserBackupCode>>(c => c.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_NotEnabled_IsNoOp()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = false };
        HasUser(user);

        await _sut.DisableAsync(1, "123456");

        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableAsync_WrongCode_ThrowsAndKeepsEnabled()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true, TwoFactorSecret = "SECRET" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "000000")).Returns(false);
        _userRepo.Setup(r => r.GetActiveBackupCodesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var act = () => _sut.DisableAsync(1, "000000");

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
        user.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RegenerateBackupCodesAsync_NotEnabled_Throws()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = false });

        var act = () => _sut.RegenerateBackupCodesAsync(1, "123456");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("two_factor_not_enabled");
    }

    [Fact]
    public async Task RegenerateBackupCodesAsync_ValidCode_ReturnsFreshCodes()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true, TwoFactorSecret = "SECRET" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "123456")).Returns(true);

        var result = await _sut.RegenerateBackupCodesAsync(1, "123456");

        result.Codes.Should().HaveCount(_options.BackupCodeCount);
        _userRepo.Verify(r => r.ReplaceBackupCodesAsync(1, It.IsAny<IReadOnlyList<UserBackupCode>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegenerateMasterCodeAsync_ValidCode_IssuesNewMasterCode()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = true, TwoFactorSecret = "SECRET", TwoFactorMasterCodeHash = "old" };
        HasUser(user);
        _totp.Setup(t => t.VerifyCode("SECRET", "123456")).Returns(true);

        var result = await _sut.RegenerateMasterCodeAsync(1, "123456");

        result.MasterCode.Should().HaveLength(_options.MasterCodeLength);
        user.TwoFactorMasterCodeHash.Should().NotBe("old");
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegenerateMasterCodeAsync_NotEnabled_Throws()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", TwoFactorEnabled = false });

        var act = () => _sut.RegenerateMasterCodeAsync(1, "123456");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("two_factor_not_enabled");
    }

    [Fact]
    public async Task VerifyAsync_ValidTotpCode_ReturnsTrue()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET" };
        _totp.Setup(t => t.VerifyCode("SECRET", "123456")).Returns(true);

        var result = await _sut.VerifyAsync(user, "123456");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ValidMasterCode_ReturnsTrue()
    {
        var masterCode = "MASTERAB";
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET", TwoFactorMasterCodeHash = Hash(masterCode) };
        _totp.Setup(t => t.VerifyCode("SECRET", masterCode)).Returns(false);

        var result = await _sut.VerifyAsync(user, masterCode);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ValidBackupCode_ConsumesItAndReturnsTrue()
    {
        var backupRaw = "ABCD1234";
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET" };
        _totp.Setup(t => t.VerifyCode("SECRET", It.IsAny<string>())).Returns(false);
        var stored = new UserBackupCode { Id = 5, UserId = 1, CodeHash = Hash(backupRaw) };
        _userRepo.Setup(r => r.GetActiveBackupCodesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([stored]);

        var result = await _sut.VerifyAsync(user, "abcd-1234");

        result.Should().BeTrue();
        _userRepo.Verify(r => r.ConsumeBackupCodeAsync(stored, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_UnknownCode_ReturnsFalse()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", TwoFactorSecret = "SECRET" };
        _totp.Setup(t => t.VerifyCode("SECRET", It.IsAny<string>())).Returns(false);
        _userRepo.Setup(r => r.GetActiveBackupCodesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _sut.VerifyAsync(user, "nope-nope");

        result.Should().BeFalse();
        _userRepo.Verify(r => r.ConsumeBackupCodeAsync(It.IsAny<UserBackupCode>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
