using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Moq;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class EmailVerificationServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<ISmsSender> _smsSender = new();
    private readonly Mock<IEmailLocalizer> _localizer = new();
    private readonly Mock<IEmailRateLimiter> _rateLimiter = new();
    private readonly EmailVerificationOptions _options = new();
    private readonly EmailVerificationService _sut;

    public EmailVerificationServiceTests()
    {
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] _) => key);
        _rateLimiter.Setup(r => r.TryConsume(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(true);
        _sut = new EmailVerificationService(
            _userRepo.Object, _emailSender.Object, _smsSender.Object,
            _localizer.Object, _rateLimiter.Object, Create(_options),
            Mock.Of<ILogger<EmailVerificationService>>());
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void HasUser(User? user) =>
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

    [Fact]
    public async Task ResendAsync_KnownUnverifiedUser_SendsEmailAndStoresToken()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", FirstName = "Ivan", EmailVerified = false };
        HasUser(user);

        await _sut.ResendAsync(user.Email, VerificationChannel.Email, null);

        user.EmailVerificationToken.Should().NotBeNullOrEmpty();
        user.EmailVerificationTokenExpiresAt.Should().NotBeNull();
        _emailSender.Verify(e => e.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendAsync_AlreadyVerified_DoesNothing()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", EmailVerified = true });

        await _sut.ResendAsync("user@relativa.io", VerificationChannel.Email, null);

        _emailSender.VerifyNoOtherCalls();
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_UnknownUser_DoesNothing()
    {
        HasUser(null);

        await _sut.ResendAsync("ghost@relativa.io", VerificationChannel.Email, null);

        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResendAsync_SmsRequestedButNoPhone_FallsBackToEmail()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", FirstName = "Ivan", Phone = null, EmailVerified = false };
        HasUser(user);

        await _sut.ResendAsync(user.Email, VerificationChannel.Sms, null);

        _emailSender.Verify(e => e.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _smsSender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_SmsWithPhone_SendsSms()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", FirstName = "Ivan", Phone = "+380501234567", EmailVerified = false };
        HasUser(user);

        await _sut.ResendAsync(user.Email, VerificationChannel.Sms, null);

        _smsSender.Verify(s => s.SendAsync(user.Phone!, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_SmsSenderThrows_SwallowsException()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", FirstName = "Ivan", Phone = "+380501234567", EmailVerified = false };
        HasUser(user);
        _smsSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gateway down"));

        var act = () => _sut.ResendAsync(user.Email, VerificationChannel.Sms, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResendAsync_BlockedByIpRateLimit_DoesNothing()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", EmailVerified = false };
        HasUser(user);
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("verify-ip:")), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(false);

        await _sut.ResendAsync(user.Email, VerificationChannel.Email, "203.0.113.5");

        _userRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_CooldownActive_DoesNotSend()
    {
        var user = new User { Id = 1, Email = "user@relativa.io", EmailVerified = false };
        HasUser(user);
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("verify-cooldown:")), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(false);

        await _sut.ResendAsync(user.Email, VerificationChannel.Email, null);

        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SmsAvailableAsync_UnverifiedUserWithPhone_ReturnsTrue()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", Phone = "+380501234567", EmailVerified = false });

        (await _sut.SmsAvailableAsync("user@relativa.io")).Should().BeTrue();
    }

    [Fact]
    public async Task SmsAvailableAsync_NoPhone_ReturnsFalse()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", Phone = null, EmailVerified = false });

        (await _sut.SmsAvailableAsync("user@relativa.io")).Should().BeFalse();
    }

    [Fact]
    public async Task SmsAvailableAsync_AlreadyVerified_ReturnsFalse()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", Phone = "+380501234567", EmailVerified = true });

        (await _sut.SmsAvailableAsync("user@relativa.io")).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_CorrectCode_MarksVerifiedAndClearsToken()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@relativa.io",
            EmailVerified = false,
            EmailVerificationToken = Hash("ABC123"),
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };
        HasUser(user);

        await _sut.VerifyAsync(user.Email, "abc123", null);

        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.EmailVerificationTokenExpiresAt.Should().BeNull();
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_WrongCode_ThrowsInvalidVerificationCode()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@relativa.io",
            EmailVerificationToken = Hash("ABC123"),
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };
        HasUser(user);

        var act = () => _sut.VerifyAsync(user.Email, "WRONG1", null);

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
        user.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_ExpiredCode_ThrowsInvalidVerificationCode()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@relativa.io",
            EmailVerificationToken = Hash("ABC123"),
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        HasUser(user);

        var act = () => _sut.VerifyAsync(user.Email, "abc123", null);

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
    }

    [Fact]
    public async Task VerifyAsync_NoPendingToken_ThrowsInvalidVerificationCode()
    {
        HasUser(new User { Id = 1, Email = "user@relativa.io", EmailVerificationToken = null });

        var act = () => _sut.VerifyAsync("user@relativa.io", "abc123", null);

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
    }

    [Fact]
    public async Task VerifyAsync_AttemptRateLimitExceeded_ThrowsRateLimit()
    {
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("verify-attempt:")), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(false);

        var act = () => _sut.VerifyAsync("user@relativa.io", "abc123", null);

        await act.Should().ThrowAsync<RateLimitExceededException>();
        _userRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_BlockedByIp_ThrowsRateLimit()
    {
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("verify-ip:")), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(false);

        var act = () => _sut.VerifyAsync("user@relativa.io", "abc123", "203.0.113.5");

        await act.Should().ThrowAsync<RateLimitExceededException>();
    }
}
