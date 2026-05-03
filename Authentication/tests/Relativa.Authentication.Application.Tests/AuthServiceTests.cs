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
    private readonly Mock<IUserProvisioningService> _userProvisioning = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IValidator<LoginRequestDto>> _loginValidator = new();
    private readonly Mock<IValidator<UpdateMyProfileRequest>> _updateProfileValidator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepo.Object,
            _userProvisioning.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _loginValidator.Object,
            _updateProfileValidator.Object);
    }

    private void SetupValidLogin() =>
        _loginValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<LoginRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenWithExpiry()
    {
        var request = new LoginRequestDto("Kovalenko@Relativa.io", "Str0ngP@ss");
        var normalizedEmail = "kovalenko@relativa.io";
        var user = new User { Id = 7, Email = normalizedEmail, Password = "bcrypt-hash" };
        var expiresAt = DateTime.UtcNow.AddHours(1);

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync(normalizedEmail, It.IsAny<CancellationToken>()))
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
        var request = new LoginRequestDto("Nobody@Relativa.io", "Str0ngP@ss");

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync("nobody@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var request = new LoginRequestDto("kovalenko@relativa.io", "wr0ng-p@ss");
        var user = new User { Id = 7, Email = "kovalenko@relativa.io", Password = "bcrypt-hash" };

        SetupValidLogin();
        _userRepo
            .Setup(r => r.GetByEmailAsync("kovalenko@relativa.io", It.IsAny<CancellationToken>()))
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
    public async Task RegisterAsync_DelegatesToUserProvisioning()
    {
        var request = new RegisterRequestDto("Taras", "Melnyk", "Melnyk@Relativa.io", "Secur3P@ss");
        var expected = new RegisterResponseDto(1, "melnyk@relativa.io", "Taras", "Melnyk");

        _userProvisioning
            .Setup(p => p.CreateUserAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.RegisterAsync(request);

        result.Should().Be(expected);
        _userProvisioning.Verify(
            p => p.CreateUserAsync(request, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
