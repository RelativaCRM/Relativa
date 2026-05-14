using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Contracts;
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

    [Fact]
    public async Task CreateUserAsync_WhenRepositoryReportsEmailFree_AllowsCreate()
    {
        var request = new RegisterRequestDto("Olena", "Koval", "Olena@Relativa.io", "Secur3P@ss");

        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _userRepo.Setup(r => r.ExistsAsync("olena@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(request.Password)).Returns("bcrypt-result");

        User? captured = null;
        _userRepo
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);

        var result = await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        result.Email.Should().Be("olena@relativa.io");
        captured.Should().NotBeNull();
        captured!.IsArchived.Should().BeFalse();
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithAuditWriter_NullActor_EnqueuesWithNewUserIdAsActor()
    {
        var request = new RegisterRequestDto("Ivan", "Franko", "ivan@relativa.io", "Secur3P@ss");
        var auditWriter = new Mock<IOutboxWriter>();

        var sut = new UserProvisioningService(
            _userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditWriter.Object);

        _registerValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<RegisterRequestDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
        auditWriter.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        auditWriter.Verify(w => w.EnqueueAuditAsync(
            It.Is<AuditEventContract>(c => c.Action == "user_registered"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithAuditWriter_ActorProvided_EnqueuesProvisionedAction()
    {
        var request = new RegisterRequestDto("Lesya", "Ukrainka", "lesya@relativa.io", "Secur3P@ss");
        var auditWriter = new Mock<IOutboxWriter>();

        var sut = new UserProvisioningService(
            _userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditWriter.Object);

        _registerValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<RegisterRequestDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
        auditWriter.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.CreateUserAsync(request, auditActorUserId: 99, CancellationToken.None);

        auditWriter.Verify(w => w.EnqueueAuditAsync(
            It.Is<AuditEventContract>(c => c.Action == "user_provisioned" && c.ActorUserId == 99),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class UserProvisioningServiceUpdateTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IValidator<RegisterRequestDto>> _registerValidator = new();
    private readonly UserProvisioningService _sut;

    public UserProvisioningServiceUpdateTests()
    {
        _sut = new UserProvisioningService(
            _userRepo.Object,
            _passwordHasher.Object,
            _registerValidator.Object,
            auditOutboxWriter: null);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_UserFound_UpdatesNamesAndReturnsProfile()
    {
        var user = new User { Id = 5, Email = "user@relativa.io", FirstName = "Old", LastName = "Name", Password = "h" };
        _userRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.UpdateUserProfileAsync(5, "New", "Name2", auditActorUserId: 5, CancellationToken.None);

        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("Name2");
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.FirstName == "New" && u.LastName == "Name2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.UpdateUserProfileAsync(99, "A", "B", auditActorUserId: 1, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("User not found.");
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithAuditWriter_EnqueuesAuditEvent()
    {
        var user = new User { Id = 3, Email = "u@relativa.io", FirstName = "X", LastName = "Y", Password = "h" };
        var auditWriter = new Mock<IOutboxWriter>();
        var sut = new UserProvisioningService(
            _userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditWriter.Object);

        _userRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        auditWriter.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.UpdateUserProfileAsync(3, "New", "Name", auditActorUserId: 7, CancellationToken.None);

        auditWriter.Verify(w => w.EnqueueAuditAsync(
            It.Is<AuditEventContract>(c => c.Action == "user_profile_updated" && c.ActorUserId == 7 && c.TargetId == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveUserAsync_UserFound_SetsIsArchivedTrue()
    {
        var user = new User { Id = 4, Email = "a@relativa.io", FirstName = "A", LastName = "B", Password = "h", IsArchived = false };
        _userRepo.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _sut.ArchiveUserAsync(4, auditActorUserId: 1, CancellationToken.None);

        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.IsArchived),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.ArchiveUserAsync(99, auditActorUserId: 1, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("User not found.");
    }

    [Fact]
    public async Task ArchiveUserAsync_WithAuditWriter_EnqueuesAuditEvent()
    {
        var user = new User { Id = 6, Email = "b@relativa.io", FirstName = "B", LastName = "C", Password = "h", IsArchived = false };
        var auditWriter = new Mock<IOutboxWriter>();
        var sut = new UserProvisioningService(
            _userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditWriter.Object);

        _userRepo.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        auditWriter.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await sut.ArchiveUserAsync(6, auditActorUserId: 1, CancellationToken.None);

        auditWriter.Verify(w => w.EnqueueAuditAsync(
            It.Is<AuditEventContract>(c => c.Action == "user_archived" && c.TargetId == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
