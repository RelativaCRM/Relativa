using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
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
        var request = new RegisterRequestDto("Taras", "Melnyk", "Melnyk@Relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));

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
        var request = new RegisterRequestDto("A", "B", "DUPLICATE@Relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));

        _registerValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<RegisterRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _userRepo.Setup(r => r.ExistsAsync("duplicate@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.CreateUserAsync(request, null, CancellationToken.None);

        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("A user with this email already exists.");
    }

    [Fact]
    public async Task CreateUserAsync_WhenRepositoryReportsEmailFree_AllowsCreate()
    {
        var request = new RegisterRequestDto("Olena", "Koval", "Olena@Relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));

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
        var request = new RegisterRequestDto("Ivan", "Franko", "ivan@relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));
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
        var request = new RegisterRequestDto("Lesya", "Ukrainka", "lesya@relativa.io", "Secur3P@ss", "+380501234567", new DateOnly(1990, 1, 1));
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

public sealed class UserProvisioningServiceCreateBranchTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IValidator<RegisterRequestDto>> _registerValidator = new();
    private readonly UserProvisioningService _sut;

    public UserProvisioningServiceCreateBranchTests()
    {
        _registerValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<RegisterRequestDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
        _sut = new UserProvisioningService(
            _userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditOutboxWriter: null);
    }

    [Fact]
    public async Task CreateUserAsync_BlankPhone_StoresNullPhone()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var request = new RegisterRequestDto("Iryna", "Bond", "iryna@relativa.io", "Secur3P@ss", Phone: "   ");

        await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        captured!.Phone.Should().BeNull("a whitespace-only phone must normalize to null rather than be persisted");
    }

    [Fact]
    public async Task CreateUserAsync_PhoneProvided_StoresTrimmedPhone()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var request = new RegisterRequestDto("Iryna", "Bond", "iryna@relativa.io", "Secur3P@ss", Phone: "  +380501112233  ");

        await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        captured!.Phone.Should().Be("+380501112233");
    }

    [Fact]
    public async Task CreateUserAsync_ExplicitLocale_PersistsThatLocale()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var request = new RegisterRequestDto("Iryna", "Bond", "iryna@relativa.io", "Secur3P@ss", Locale: "uk");

        await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        captured!.Settings.Locale.Should().Be("uk");
    }

    [Fact]
    public async Task CreateUserAsync_BlankLocale_DefaultsToEnglish()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var request = new RegisterRequestDto("Iryna", "Bond", "iryna@relativa.io", "Secur3P@ss", Locale: "  ");

        await _sut.CreateUserAsync(request, auditActorUserId: null, CancellationToken.None);

        captured!.Settings.Locale.Should().Be("en");
    }

    [Fact]
    public async Task CreateUserAsync_ActorProvided_MarksEmailVerified()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var request = new RegisterRequestDto("Iryna", "Bond", "iryna@relativa.io", "Secur3P@ss");

        await _sut.CreateUserAsync(request, auditActorUserId: 42, CancellationToken.None);

        captured!.EmailVerified.Should().BeTrue("an admin-provisioned user is pre-verified, unlike self-registration");
    }
}

public sealed class UserProvisioningServiceExternalTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IValidator<RegisterRequestDto>> _registerValidator = new();

    private UserProvisioningService Build(IOutboxWriter? auditWriter = null) =>
        new(_userRepo.Object, _passwordHasher.Object, _registerValidator.Object, auditWriter);

    [Fact]
    public async Task CreateExternalUserAsync_FullIdentity_UsesProviderNamesAndNoPassword()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var identity = new ExternalIdentity("google", "sub-1", "Person@Relativa.IO", "  Ada  ", "  Lovelace  ");

        var result = await Build().CreateExternalUserAsync(identity, CancellationToken.None);

        result.Email.Should().Be("person@relativa.io");
        captured!.FirstName.Should().Be("Ada");
        captured.LastName.Should().Be("Lovelace");
        captured.Password.Should().BeNull("external identities authenticate via the provider, never a local password");
        captured.EmailVerified.Should().BeTrue();
        captured.ExternalLogins.Should().ContainSingle(l => l.Provider == "google" && l.Subject == "sub-1");
    }

    [Fact]
    public async Task CreateExternalUserAsync_BlankNames_FallsBackToEmailLocalPartAndEmptyLastName()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var identity = new ExternalIdentity("microsoft", "sub-2", "grace.hopper@relativa.io", null, "   ");

        await Build().CreateExternalUserAsync(identity, CancellationToken.None);

        captured!.FirstName.Should().Be("grace.hopper", "with no given name the email local-part is the best available label");
        captured.LastName.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateExternalUserAsync_BlankNamesAndEmptyLocalPart_FallsBackToUser()
    {
        User? captured = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u);
        var identity = new ExternalIdentity("google", "sub-3", "@relativa.io", "", null);

        await Build().CreateExternalUserAsync(identity, CancellationToken.None);

        captured!.FirstName.Should().Be("User", "a degenerate email with no local-part still needs a non-empty display name");
    }

    [Fact]
    public async Task CreateExternalUserAsync_WithAuditWriter_EnqueuesRegisteredEventForNewUser()
    {
        var auditWriter = new Mock<IOutboxWriter>();
        auditWriter.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var identity = new ExternalIdentity("google", "sub-4", "ada@relativa.io", "Ada", "Lovelace");

        await Build(auditWriter.Object).CreateExternalUserAsync(identity, CancellationToken.None);

        auditWriter.Verify(w => w.EnqueueAuditAsync(
            It.Is<AuditEventContract>(c => c.Action == "user_registered" && c.SourceService == "authentication"),
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

        await act.Should().ThrowAsync<AuthException>().WithMessage("User not found.");
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

        await act.Should().ThrowAsync<AuthException>().WithMessage("User not found.");
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
