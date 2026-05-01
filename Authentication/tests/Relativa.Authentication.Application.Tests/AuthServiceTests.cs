using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IAuditOutboxWriter> _auditOutboxWriter = new();
    private readonly Mock<IValidator<LoginRequestDto>> _loginValidator = new();
    private readonly Mock<IValidator<RegisterRequestDto>> _registerValidator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepo.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _loginValidator.Object,
            _registerValidator.Object,
            _auditOutboxWriter.Object
        );
    }

    private void SetupValidLogin() =>
        _loginValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<LoginRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidRegister() =>
        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenWithExpiry()
    {
        var request = new LoginRequestDto("kovalenko@relativa.io", "Str0ngP@ss");
        var user = new User { Id = 7, Email = request.Email, Password = "bcrypt-hash" };
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

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
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

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
        _tokenService.Verify(
            t => t.GenerateAccessToken(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserWithHashedPassword()
    {
        var request = new RegisterRequestDto("Taras", "Melnyk", "melnyk@relativa.io", "Secur3P@ss");

        SetupValidRegister();
        _userRepo
            .Setup(r => r.ExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasher
            .Setup(h => h.Hash(request.Password))
            .Returns("bcrypt-result");

        User? captured = null;
        _userRepo
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);

        var result = await _sut.RegisterAsync(request);

        result.Email.Should().Be(request.Email);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        captured!.Password.Should().Be("bcrypt-result");
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(x => x.EnqueueAsync(It.IsAny<Relativa.Persistence.Contracts.AuditEventContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new RegisterRequestDto("", "", "not-an-email", "123");

        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("FirstName", "First name is required."),
                new ValidationFailure("Email", "A valid email address is required.")
            }));

        var act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Verify(
            r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequestDto("Oksana", "Petrenko", "petrenko@relativa.io", "Secur3P@ss");

        SetupValidRegister();
        _userRepo
            .Setup(r => r.ExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A user with this email already exists.");
        _userRepo.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
