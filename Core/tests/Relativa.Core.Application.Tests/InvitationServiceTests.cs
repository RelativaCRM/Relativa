using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class InvitationServiceTests
{
    private readonly Mock<IWorkspaceInvitationRepository> _invitationRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IOrgInvitationRepository> _orgInvitationRepo = new();
    private readonly Mock<IValidator<InviteMemberRequest>> _inviteValidator = new();
    private readonly Mock<IValidator<AcceptInvitationRequest>> _acceptValidator = new();
    private readonly InvitationService _sut;

    public InvitationServiceTests()
    {
        _sut = new InvitationService(
            _invitationRepo.Object,
            _memberRepo.Object,
            _roleRepo.Object,
            _orgInvitationRepo.Object,
            _inviteValidator.Object,
            _acceptValidator.Object
        );
    }

    private void SetupValidInvite() =>
        _inviteValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<InviteMemberRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidAccept() =>
        _acceptValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<AcceptInvitationRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private UserRoleWorkspace MemberWithPermission(int userId, int workspaceId, string permission) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole
            {
                Name = "sales_manager",
                RolePermissions =
                [
                    new WorkspaceRolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    [Fact]
    public async Task InviteAsync_ValidRequest_CreatesInvitationWithPendingStatus()
    {
        var request = new InviteMemberRequest("hrytsenko@relativa.io", 2);
        var caller = MemberWithPermission(1, 5, "invite_to_workspace");
        var role = new WorkspaceRole { Id = 2, Name = "analyst", WorkspaceId = null };

        SetupValidInvite();
        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        WorkspaceInvitation? captured = null;
        _invitationRepo
            .Setup(r => r.AddAsync(It.IsAny<WorkspaceInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceInvitation, CancellationToken>((i, _) => captured = i);

        var result = await _sut.InviteAsync(5, 1, request);

        result.Email.Should().Be(request.Email);
        result.Status.Should().Be("Pending");
        captured!.Token.Should().NotBeNullOrEmpty();
        captured.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<WorkspaceInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var request = new InviteMemberRequest("hrytsenko@relativa.io", 2);
        var caller = new UserRoleWorkspace
        {
            UserId = 3,
            WorkspaceId = 5,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };

        SetupValidInvite();
        _memberRepo
            .Setup(r => r.GetAsync(3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var act = () => _sut.InviteAsync(5, 3, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _invitationRepo.Verify(
            r => r.AddAsync(It.IsAny<WorkspaceInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InviteAsync_RoleFromAnotherWorkspace_ThrowsArgumentException()
    {
        var request = new InviteMemberRequest("bondarenko@relativa.io", 7);
        var caller = MemberWithPermission(1, 5, "invite_to_workspace");
        var foreignRole = new WorkspaceRole { Id = 7, Name = "custom", WorkspaceId = 99 };

        SetupValidInvite();
        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignRole);

        var act = () => _sut.InviteAsync(5, 1, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not belong to this workspace.");
    }

    [Fact]
    public async Task AcceptAsync_ValidToken_CreatesMemberAndSetsAccepted()
    {
        var request = new AcceptInvitationRequest("valid-token-abc");
        var invitation = new WorkspaceInvitation
        {
            Id = 1,
            WorkspaceId = 3,
            Email = "kravets@relativa.io",
            WsRoleId = 2,
            Token = "valid-token-abc",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(5)
        };

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("valid-token-abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _memberRepo
            .Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        await _sut.AcceptAsync(10, "kravets@relativa.io", request);

        invitation.Status.Should().Be("Accepted");
        _memberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_TokenNotFound_ThrowsKeyNotFoundException()
    {
        var request = new AcceptInvitationRequest("ghost-token");

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("ghost-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceInvitation?)null);

        var act = () => _sut.AcceptAsync(1, "someone@relativa.io", request);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found or has expired.");
    }

    [Fact]
    public async Task AcceptAsync_EmailMismatch_ThrowsUnauthorizedAccessException()
    {
        var request = new AcceptInvitationRequest("token-xyz");
        var invitation = new WorkspaceInvitation
        {
            Email = "shevchenko@relativa.io",
            Token = "token-xyz",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(3)
        };

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("token-xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(5, "intruder@relativa.io", request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("This invitation was sent to a different email address.");
    }

    [Fact]
    public async Task AcceptAsync_StatusNotPending_ThrowsInvalidOperationException()
    {
        var request = new AcceptInvitationRequest("token-used");
        var invitation = new WorkspaceInvitation
        {
            Email = "lysenko@relativa.io",
            Token = "token-used",
            Status = "Accepted",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("token-used", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(6, "lysenko@relativa.io", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invitation is no longer pending (status: Accepted).");
    }

    [Fact]
    public async Task AcceptAsync_ExpiredInvitation_SetsExpiredStatusAndThrows()
    {
        var request = new AcceptInvitationRequest("token-old");
        var invitation = new WorkspaceInvitation
        {
            Email = "moroz@relativa.io",
            Token = "token-old",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("token-old", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(7, "moroz@relativa.io", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invitation has expired.");
        invitation.Status.Should().Be("Expired");
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_AlreadyMember_ThrowsInvalidOperationException()
    {
        var request = new AcceptInvitationRequest("token-dup");
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 4,
            Email = "kovalchuk@relativa.io",
            Token = "token-dup",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(2)
        };
        var existing = new UserRoleWorkspace { UserId = 8, WorkspaceId = 4 };

        SetupValidAccept();
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("token-dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _memberRepo
            .Setup(r => r.GetAsync(8, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.AcceptAsync(8, "kovalchuk@relativa.io", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are already a member of this workspace.");
        _memberRepo.Verify(
            r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelAsync_InvitationFromAnotherWorkspace_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "invite_to_workspace");
        var invitation = new WorkspaceInvitation { Id = 10, WorkspaceId = 99, Status = "Pending" };

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _invitationRepo
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var act = () => _sut.CancelAsync(5, 10, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found.");
    }
}
