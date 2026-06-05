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

public sealed class AuthServiceTests
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

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepo.Object,
            _userProvisioning.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _externalIdentityVerifier.Object,
            _twoFactorService.Object,
            _emailSender.Object,
            _emailLocalizer.Object,
            _configuration.Object,
            _loginValidator.Object,
            _updateProfileValidator.Object,
            _forgotPasswordValidator.Object,
            _resetPasswordValidator.Object);

        _emailLocalizer
            .Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] _) => key);
    }

    private void SetupValidLogin() =>
        _loginValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<LoginRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidForgotPassword() =>
        _forgotPasswordValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ForgotPasswordRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidResetPassword() =>
        _resetPasswordValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ResetPasswordRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenWithExpiry()
    {
        var request = new LoginRequestDto("kovalenko@relativa.io", "Str0ngP@ss");
        var user = new User { Id = 7, Email = request.Email, Password = "bcrypt-hash", EmailVerified = true };
        var expiresAt = DateTime.UtcNow.AddHours(1);

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.Verify(request.Password, user.Password))
            .Returns(true);
        _tokenService
            .Setup(t => t.GenerateAccessToken(user))
            .Returns(("signed-jwt", expiresAt));

        var result = await _sut.LoginAsync(request);

        result.AccessToken.Should().Be("signed-jwt");
        result.ExpiresAt.Should().Be(expiresAt);
        _tokenService.Verify(t => t.GenerateAccessToken(user), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new LoginRequestDto("", "");

        _loginValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<LoginRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Email", "Email is required.")
            }));

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        var request = new LoginRequestDto("nobody@relativa.io", "Str0ngP@ss");

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var request = new LoginRequestDto("kovalenko@relativa.io", "wr0ng-p@ss");
        var user = new User { Id = 7, Email = request.Email, Password = "bcrypt-hash" };

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.Verify(request.Password, user.Password))
            .Returns(false);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("Invalid email or password.");
        _tokenService.Verify(
            t => t.GenerateAccessToken(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserWithHashedPassword()
    {
        var request = new RegisterRequestDto("Taras", "Melnyk", "melnyk@relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));
        var ct = CancellationToken.None;

        _userProvisioning
            .Setup(p => p.CreateUserAsync(request, null, ct))
            .ReturnsAsync(new RegisterResponseDto(1, request.Email, request.FirstName, request.LastName));

        var result = await _sut.RegisterAsync(request, ct);

        result.Email.Should().Be(request.Email);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        _userProvisioning.Verify(p => p.CreateUserAsync(request, null, ct), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new RegisterRequestDto("", "", "not-an-email", "123", "+380501234567", new DateOnly(1990, 1, 1));
        var ct = CancellationToken.None;

        _userProvisioning
            .Setup(p => p.CreateUserAsync(request, null, ct))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("FirstName", "First name is required."),
                new ValidationFailure("Email", "A valid email address is required.")
            }));

        var act = () => _sut.RegisterAsync(request, ct);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequestDto("Oksana", "Petrenko", "petrenko@relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));
        var ct = CancellationToken.None;

        _userProvisioning
            .Setup(p => p.CreateUserAsync(request, null, ct))
            .ThrowsAsync(new InvalidOperationException("A user with this email already exists."));

        var act = () => _sut.RegisterAsync(request, ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A user with this email already exists.");
    }

    [Fact]
    public async Task GetProfileAsync_ValidUser_ReturnsUserProfile()
    {
        var user = new User { Id = 5, Email = "shevchenko@relativa.io", FirstName = "Taras", LastName = "Shevchenko" };
        _userRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.GetProfileAsync(5);

        result.Id.Should().Be(5);
        result.Email.Should().Be("shevchenko@relativa.io");
        result.FirstName.Should().Be("Taras");
        result.LastName.Should().Be("Shevchenko");
    }

    [Fact]
    public async Task GetProfileAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetProfileAsync(99);

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_SilentlyReturnsWithoutSendingEmail()
    {
        SetupValidForgotPassword();
        _userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await _sut.ForgotPasswordAsync("unknown@example.com");

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_KnownEmail_SetsResetTokenAndSendsEmail()
    {
        var user = new User { Id = 1, Email = "user@example.com", FirstName = "Taras", Password = "hash" };
        SetupValidForgotPassword();
        _userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepo
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _configuration
            .SetupGet(c => c["App:FrontendBaseUrl"])
            .Returns("http://localhost:3000");
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ForgotPasswordAsync("user@example.com");

        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.PasswordResetToken != null && u.PasswordResetTokenExpiresAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.SendAsync(
            user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_InvalidEmail_ThrowsValidationException()
    {
        _forgotPasswordValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ForgotPasswordRequest>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Email", "A valid email address is required.")
            }));

        var act = () => _sut.ForgotPasswordAsync("not-an-email");

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_MissingFrontendBaseUrl_ThrowsConfigurationException()
    {
        var user = new User { Id = 1, Email = "user@example.com", FirstName = "Taras", Password = "hash" };
        SetupValidForgotPassword();
        _userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepo
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _configuration
            .SetupGet(c => c["App:FrontendBaseUrl"])
            .Returns((string?)null);

        var act = () => _sut.ForgotPasswordAsync("user@example.com");

        await act.Should().ThrowAsync<ConfigurationException>();
    }

    [Fact]
    public async Task ValidateResetTokenAsync_ValidToken_DoesNotThrow()
    {
        var user = new User { Id = 1, Email = "user@example.com", Password = "hash" };
        _userRepo
            .Setup(r => r.GetByResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var act = () => _sut.ValidateResetTokenAsync("valid-token");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateResetTokenAsync_InvalidToken_ThrowsArgumentException()
    {
        _userRepo
            .Setup(r => r.GetByResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.ValidateResetTokenAsync("expired-or-invalid-token");

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("Invalid or expired reset token.");
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_UpdatesPasswordAndClearsToken()
    {
        var user = new User { Id = 1, Email = "user@example.com", Password = "old-hash", PasswordResetToken = "some-hash" };
        SetupValidResetPassword();
        _userRepo
            .Setup(r => r.GetByResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("new-hash");
        _userRepo
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ResetPasswordAsync("valid-token", "NewPass123!");

        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u =>
                u.Password == "new-hash" &&
                u.PasswordResetToken == null &&
                u.PasswordResetTokenExpiresAt == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ThrowsArgumentException()
    {
        SetupValidResetPassword();
        _userRepo
            .Setup(r => r.GetByResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.ResetPasswordAsync("bad-token", "NewPass123!");

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("Invalid or expired reset token.");
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidRequest_ThrowsValidationException()
    {
        _resetPasswordValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ResetPasswordRequest>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("NewPassword", "Password must be at least 8 characters.")
            }));

        var act = () => _sut.ResetPasswordAsync("token", "short");

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Verify(r => r.GetByResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
