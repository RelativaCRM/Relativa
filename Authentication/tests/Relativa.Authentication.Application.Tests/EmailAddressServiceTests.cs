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

public sealed class EmailAddressServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IEmailLocalizer> _localizer = new();
    private readonly Mock<IEmailRateLimiter> _rateLimiter = new();
    private readonly EmailVerificationOptions _options = new();
    private readonly EmailAddressService _sut;

    public EmailAddressServiceTests()
    {
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] _) => key);
        _rateLimiter.Setup(r => r.TryConsume(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Returns(true);
        _sut = new EmailAddressService(
            _userRepo.Object, _emailSender.Object, _localizer.Object,
            _rateLimiter.Object, Create(_options),
            Mock.Of<ILogger<EmailAddressService>>());
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void HasUser(User user) =>
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    [Fact]
    public async Task ListAsync_ReturnsPrimaryFirstThenExtras()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserEmail { Address = "extra@relativa.io", IsVerified = true, Source = "manual" }]);

        var result = await _sut.ListAsync(1);

        result.Should().HaveCount(2);
        result[0].Address.Should().Be("primary@relativa.io");
        result[0].IsPrimary.Should().BeTrue();
        result[1].Address.Should().Be("extra@relativa.io");
        result[1].IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_InvalidEmail_ThrowsEmailInvalid()
    {
        var act = () => _sut.AddAsync(1, "not-an-email");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_invalid");
    }

    [Fact]
    public async Task AddAsync_AddressTakenAnywhere_ThrowsTaken()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.EmailExistsAnywhereAsync("extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.AddAsync(1, "extra@relativa.io");

        await act.Should().ThrowAsync<EmailAddressTakenException>();
        _userRepo.Verify(r => r.AddEmailAsync(It.IsAny<UserEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_RateLimited_ThrowsRateLimit()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.EmailExistsAnywhereAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("add-email:")), It.IsAny<int>(), It.IsAny<TimeSpan>())).Returns(false);

        var act = () => _sut.AddAsync(1, "extra@relativa.io");

        await act.Should().ThrowAsync<RateLimitExceededException>();
    }

    [Fact]
    public async Task AddAsync_Valid_PersistsUnverifiedEmailAndSendsCode()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io", FirstName = "Ivan" });
        _userRepo.Setup(r => r.EmailExistsAnywhereAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _sut.AddAsync(1, "Extra@Relativa.io");

        _userRepo.Verify(r => r.AddEmailAsync(It.Is<UserEmail>(e =>
            e.Address == "extra@relativa.io" && !e.IsVerified && e.VerificationToken != null && e.Source == "manual"),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.SendAsync("extra@relativa.io", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_CorrectCode_MarksVerified()
    {
        var email = new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false, VerificationToken = Hash("ABC123"), VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(email);

        await _sut.VerifyAsync(1, "extra@relativa.io", "abc123");

        email.IsVerified.Should().BeTrue();
        email.VerificationToken.Should().BeNull();
        _userRepo.Verify(r => r.UpdateEmailAsync(email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_UnknownAddress_ThrowsNotFound()
    {
        _userRepo.Setup(r => r.GetEmailAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserEmail?)null);

        var act = () => _sut.VerifyAsync(1, "ghost@relativa.io", "abc123");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_address_not_found");
    }

    [Fact]
    public async Task VerifyAsync_AlreadyVerified_IsNoOp()
    {
        var email = new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = true };
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(email);

        await _sut.VerifyAsync(1, "extra@relativa.io", "whatever");

        _userRepo.Verify(r => r.UpdateEmailAsync(It.IsAny<UserEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_WrongCode_ThrowsInvalidVerificationCode()
    {
        var email = new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false, VerificationToken = Hash("ABC123"), VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(email);

        var act = () => _sut.VerifyAsync(1, "extra@relativa.io", "WRONG9");

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
    }

    [Fact]
    public async Task SetPrimaryAsync_OAuthManagedAccount_Throws()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io", Password = null });

        var act = () => _sut.SetPrimaryAsync(1, "extra@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("primary_email_oauth_managed");
    }

    [Fact]
    public async Task SetPrimaryAsync_UnverifiedTarget_Throws()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io", Password = "hash" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false });

        var act = () => _sut.SetPrimaryAsync(1, "extra@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_not_verified_for_primary");
    }

    [Fact]
    public async Task SetPrimaryAsync_VerifiedTarget_SwapsPrimaryAndDemotesOld()
    {
        var user = new User { Id = 1, Email = "old@relativa.io", Password = "hash" };
        HasUser(user);
        var target = new UserEmail { UserId = 1, Address = "new@relativa.io", IsVerified = true };
        _userRepo.Setup(r => r.GetEmailAsync(1, "new@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await _sut.SetPrimaryAsync(1, "new@relativa.io");

        user.Email.Should().Be("new@relativa.io");
        _userRepo.Verify(r => r.RemoveEmailAsync(target, It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(r => r.AddEmailAsync(It.Is<UserEmail>(e => e.Address == "old@relativa.io" && e.IsVerified), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPrimaryAsync_AlreadyPrimary_IsNoOp()
    {
        HasUser(new User { Id = 1, Email = "same@relativa.io", Password = "hash" });

        await _sut.SetPrimaryAsync(1, "same@relativa.io");

        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_PrimaryEmail_Throws()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });

        var act = () => _sut.RemoveAsync(1, "primary@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("primary_email_cannot_remove");
    }

    [Fact]
    public async Task RemoveAsync_ExistingSecondary_Removes()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        var email = new UserEmail { UserId = 1, Address = "extra@relativa.io" };
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(email);

        await _sut.RemoveAsync(1, "extra@relativa.io");

        _userRepo.Verify(r => r.RemoveEmailAsync(email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_UnknownSecondary_ThrowsNotFound()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "ghost@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync((UserEmail?)null);

        var act = () => _sut.RemoveAsync(1, "ghost@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_address_not_found");
    }

    [Fact]
    public async Task SetPrimaryAsync_UnknownTarget_ThrowsNotFound()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io", Password = "hash" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "ghost@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync((UserEmail?)null);

        var act = () => _sut.SetPrimaryAsync(1, "ghost@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_address_not_found");
    }

    [Fact]
    public async Task VerifyAsync_NullToken_ThrowsInvalidVerificationCode()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false, VerificationToken = null });

        var act = () => _sut.VerifyAsync(1, "extra@relativa.io", "ABCDEF");

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
    }

    [Fact]
    public async Task VerifyAsync_ExpiredToken_ThrowsInvalidVerificationCode()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEmail
            {
                UserId = 1,
                Address = "extra@relativa.io",
                IsVerified = false,
                VerificationToken = Hash("ABCDEF"),
                VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });

        var act = () => _sut.VerifyAsync(1, "extra@relativa.io", "ABCDEF");

        await act.Should().ThrowAsync<InvalidVerificationCodeException>();
    }

    [Fact]
    public async Task ResendAsync_UnknownAddress_ThrowsNotFound()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "ghost@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync((UserEmail?)null);

        var act = () => _sut.ResendAsync(1, "ghost@relativa.io");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("email_address_not_found");
    }

    [Fact]
    public async Task ResendAsync_AlreadyVerified_IsNoOp()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = true });

        await _sut.ResendAsync(1, "extra@relativa.io");

        _userRepo.Verify(r => r.UpdateEmailAsync(It.IsAny<UserEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_RateLimited_SilentlySkips()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false });
        _rateLimiter.Setup(r => r.TryConsume(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>())).Returns(false);

        await _sut.ResendAsync(1, "extra@relativa.io");

        _userRepo.Verify(r => r.UpdateEmailAsync(It.IsAny<UserEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_Unverified_RotatesTokenAndSendsCode()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io", Settings = new UserSettings { Locale = "uk" } });
        var email = new UserEmail { UserId = 1, Address = "extra@relativa.io", IsVerified = false, VerificationToken = "OLD" };
        _userRepo.Setup(r => r.GetEmailAsync(1, "extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(email);

        await _sut.ResendAsync(1, "extra@relativa.io");

        email.VerificationToken.Should().NotBe("OLD");
        email.VerificationTokenExpiresAt.Should().NotBeNull();
        _userRepo.Verify(r => r.UpdateEmailAsync(email, It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(s => s.SendAsync("extra@relativa.io", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_EmailSenderThrows_SwallowsAndStillPersists()
    {
        HasUser(new User { Id = 1, Email = "primary@relativa.io" });
        _userRepo.Setup(r => r.EmailExistsAnywhereAsync("extra@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _emailSender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var act = () => _sut.AddAsync(1, "extra@relativa.io");

        await act.Should().NotThrowAsync();
        _userRepo.Verify(r => r.AddEmailAsync(It.IsAny<UserEmail>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
