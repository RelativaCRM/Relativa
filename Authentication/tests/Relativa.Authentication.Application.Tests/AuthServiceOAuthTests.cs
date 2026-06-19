using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class AuthServiceOAuthTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserProvisioningService> _userProvisioning = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IExternalIdentityVerifier> _externalIdentityVerifier = new();
    private readonly Mock<ITwoFactorService> _twoFactorService = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IEmailLocalizer> _emailLocalizer = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<IValidator<LoginRequestDto>> _loginValidator = new();
    private readonly Mock<IValidator<UpdateMyProfileRequest>> _updateProfileValidator = new();
    private readonly Mock<IValidator<ForgotPasswordRequest>> _forgotPasswordValidator = new();
    private readonly Mock<IValidator<ResetPasswordRequest>> _resetPasswordValidator = new();
    private readonly AuthService _sut;
    private readonly DateTime _expiresAt = DateTime.UtcNow.AddHours(1);

    public AuthServiceOAuthTests()
    {
        _sut = new AuthService(
            _userRepo.Object, _userProvisioning.Object, _tokenService.Object, _passwordHasher.Object,
            _externalIdentityVerifier.Object, _twoFactorService.Object, _emailSender.Object,
            _emailLocalizer.Object, _configuration.Object, _loginValidator.Object,
            _updateProfileValidator.Object, _forgotPasswordValidator.Object, _resetPasswordValidator.Object);

        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns(("jwt", _expiresAt));
        _loginValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<LoginRequestDto>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
        _updateProfileValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateMyProfileRequest>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
    }

    private static ExternalIdentity Identity(string email = "user@relativa.io") =>
        new("google", "subject-123", email, "Ivan", "Petryk");

    private void VerifierReturns(ExternalIdentity identity) =>
        _externalIdentityVerifier.Setup(v => v.VerifyAsync("google", "tok", It.IsAny<CancellationToken>())).ReturnsAsync(identity);

    [Fact]
    public async Task LoginAsync_EmailNotVerified_ThrowsEmailNotVerified()
    {
        var request = new LoginRequestDto("user@relativa.io", "pass");
        var user = new User { Id = 1, Email = request.Email, Password = "hash", EmailVerified = false };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(request.Password, user.Password)).Returns(true);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<EmailNotVerifiedException>();
    }

    [Fact]
    public async Task LoginAsync_TwoFactorEnabledNoCode_ThrowsTwoFactorRequired()
    {
        var request = new LoginRequestDto("user@relativa.io", "pass");
        var user = new User { Id = 1, Email = request.Email, Password = "hash", EmailVerified = true, TwoFactorEnabled = true };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(request.Password, user.Password)).Returns(true);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<TwoFactorRequiredException>();
    }

    [Fact]
    public async Task LoginAsync_TwoFactorEnabledWrongCode_ThrowsInvalidTwoFactorCode()
    {
        var request = new LoginRequestDto("user@relativa.io", "pass", "000000");
        var user = new User { Id = 1, Email = request.Email, Password = "hash", EmailVerified = true, TwoFactorEnabled = true };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(request.Password, user.Password)).Returns(true);
        _twoFactorService.Setup(t => t.VerifyAsync(user, "000000", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<InvalidTwoFactorCodeException>();
    }

    [Fact]
    public async Task LoginAsync_TwoFactorEnabledValidCode_ReturnsToken()
    {
        var request = new LoginRequestDto("user@relativa.io", "pass", "123456");
        var user = new User { Id = 1, Email = request.Email, Password = "hash", EmailVerified = true, TwoFactorEnabled = true };
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(request.Password, user.Password)).Returns(true);
        _twoFactorService.Setup(t => t.VerifyAsync(user, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.LoginAsync(request);

        result.AccessToken.Should().Be("jwt");
    }

    [Fact]
    public async Task OAuthLoginAsync_ExistingExternalLogin_ReturnsToken()
    {
        VerifierReturns(Identity());
        var user = new User { Id = 1, Email = "user@relativa.io" };
        _userRepo.Setup(r => r.GetByExternalLoginAsync("google", "subject-123", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.OAuthLoginAsync("google", new OAuthLoginRequestDto("tok"));

        result.AccessToken.Should().Be("jwt");
        _userProvisioning.Verify(p => p.CreateExternalUserAsync(It.IsAny<ExternalIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OAuthLoginAsync_NoExternalLoginButEmailMatches_LinksProvider()
    {
        VerifierReturns(Identity());
        _userRepo.Setup(r => r.GetByExternalLoginAsync("google", "subject-123", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var user = new User { Id = 1, Email = "user@relativa.io" };
        _userRepo.Setup(r => r.GetByEmailAsync("user@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await _sut.OAuthLoginAsync("google", new OAuthLoginRequestDto("tok"));

        _userRepo.Verify(r => r.AddExternalLoginAsync(It.Is<UserExternalLogin>(l => l.UserId == 1 && l.Provider == "google" && l.Subject == "subject-123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OAuthLoginAsync_UnknownUser_ProvisionsExternalUser()
    {
        VerifierReturns(Identity());
        _userRepo.Setup(r => r.GetByExternalLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userProvisioning.Setup(p => p.CreateExternalUserAsync(It.IsAny<ExternalIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 9, Email = "user@relativa.io" });

        var result = await _sut.OAuthLoginAsync("google", new OAuthLoginRequestDto("tok"));

        result.AccessToken.Should().Be("jwt");
        _userProvisioning.Verify(p => p.CreateExternalUserAsync(It.IsAny<ExternalIdentity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkProviderAsync_AlreadyLinkedToDifferentUser_Throws()
    {
        VerifierReturns(Identity());
        _userRepo.Setup(r => r.GetByExternalLoginAsync("google", "subject-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "other@relativa.io" });

        var act = () => _sut.LinkProviderAsync(1, "google", new OAuthLoginRequestDto("tok"));

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("provider_already_linked");
    }

    [Fact]
    public async Task LinkProviderAsync_NewLink_AddsExternalLogin()
    {
        VerifierReturns(Identity("user@relativa.io"));
        _userRepo.Setup(r => r.GetByExternalLoginAsync("google", "subject-123", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = 1, Email = "user@relativa.io" });

        await _sut.LinkProviderAsync(1, "google", new OAuthLoginRequestDto("tok"));

        _userRepo.Verify(r => r.AddExternalLoginAsync(It.Is<UserExternalLogin>(l => l.UserId == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkProviderAsync_UserNotFound_Throws()
    {
        VerifierReturns(Identity());
        _userRepo.Setup(r => r.GetByExternalLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.LinkProviderAsync(1, "google", new OAuthLoginRequestDto("tok"));

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task UpdateMyProfileAsync_ValidRequest_PersistsPhoneAndDelegates()
    {
        var user = new User { Id = 1, Email = "user@relativa.io" };
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var dto = new UserProfileDto(1, user.Email, "Ivan", "Petryk", false, "+380501234567", new DateOnly(1990, 1, 1), [], true);
        _userProvisioning.Setup(p => p.UpdateUserProfileAsync(1, "Ivan", "Petryk", 1, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await _sut.UpdateMyProfileAsync(1, new UpdateMyProfileRequest("Ivan", "Petryk", " +380501234567 ", new DateOnly(1990, 1, 1)));

        user.Phone.Should().Be("+380501234567");
        user.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        result.Should().BeSameAs(dto);
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.UpdateMyProfileAsync(1, new UpdateMyProfileRequest("Ivan", "Petryk"));

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("user_not_found");
    }

    [Fact]
    public async Task DeleteMyAccountAsync_DelegatesToArchive()
    {
        await _sut.DeleteMyAccountAsync(5);

        _userProvisioning.Verify(p => p.ArchiveUserAsync(5, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailExistsAsync_BlankEmail_ReturnsFalseWithoutHittingRepo()
    {
        var result = await _sut.EmailExistsAsync("  ");

        result.Should().BeFalse();
        _userRepo.Verify(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailExistsAsync_KnownEmail_ReturnsTrue()
    {
        _userRepo.Setup(r => r.ExistsAsync("user@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.EmailExistsAsync("User@Relativa.io");

        result.Should().BeTrue();
    }
}
