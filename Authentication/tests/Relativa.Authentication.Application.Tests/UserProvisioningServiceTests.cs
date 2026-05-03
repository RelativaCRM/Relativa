using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class UserProvisioningServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IValidator<RegisterRequestDto>> _registerValidator = new();
    private readonly UserProvisioningService _sut;

    public UserProvisioningServiceTests()
    {
        _sut = new UserProvisioningService(
            _userRepo.Object,
            _passwordHasher.Object,
            _registerValidator.Object,
            auditOutboxWriter: null);
    }

    [Fact]
    public async Task CreateUserAsync_PersistsNormalizedEmail()
    {
        var request = new RegisterRequestDto("Taras", "Melnyk", "Melnyk@Relativa.io", "Secur3P@ss");

        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _userRepo.Setup(r => r.ExistsAsync("melnyk@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(request.Password)).Returns("bcrypt-result");

        User? captured = null;
        _userRepo
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);

        var result = await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        result.Email.Should().Be("melnyk@relativa.io");
        captured!.Email.Should().Be("melnyk@relativa.io");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmailNormalized_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequestDto("A", "B", "DUPLICATE@Relativa.io", "Secur3P@ss");

        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _userRepo.Setup(r => r.ExistsAsync("duplicate@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.CreateUserAsync(request, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A user with this email already exists.");
    }
}
