using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrgInvitationServiceTests
{
    private readonly Mock<IOrgInvitationRepository> _invitationRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IValidator<InviteToOrgRequest>> _inviteValidator = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly OrgInvitationService _sut;

    public OrgInvitationServiceTests()
    {
        _sut = new OrgInvitationService(
            _invitationRepo.Object,
            _orgMemberRepo.Object,
            _orgRoleRepo.Object,
            _userRepo.Object,
            _inviteValidator.Object,
            _auditOutboxWriter.Object);

        _inviteValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteToOrgRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private UserRoleOrganization OrgMemberWithPermission(int userId, int orgId, string permission) =>
        new()
        {
            UserId = userId,
            OrganizationId = orgId,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions =
                [
                    new OrganizationRolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    // ── InviteAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 3, OrganizationId = 5,
                Role = new OrganizationRole { Name = "viewer", RolePermissions = [] }
            });

        var act = () => _sut.InviteAsync(5, 3, new InviteToOrgRequest("test@relativa.io"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invite_to_org*");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InviteAsync_ValidRequest_CreatesInvitationWithPendingStatusAndEnqueuesAuditEvent()
    {
        var caller = OrgMemberWithPermission(1, 5, "invite_to_org");
        var memberRole = new OrganizationRole { Id = 2, Name = "org_member" };
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync(memberRole);

        OrganizationInvitation? captured = null;
        _invitationRepo
            .Setup(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<OrganizationInvitation, CancellationToken>((i, _) => captured = i);

        var result = await _sut.InviteAsync(5, 1, new InviteToOrgRequest("new@relativa.io"));

        result.Email.Should().Be("new@relativa.io");
        result.Status.Should().Be("Pending");
        captured!.Token.Should().NotBeNullOrEmpty();
        captured.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_invitation_created" &&
                    e.TargetId == 5 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── CancelAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 5,
                Role = new OrganizationRole { Name = "viewer", RolePermissions = [] }
            });

        var act = () => _sut.CancelAsync(5, 10, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CancelAsync_InvitationNotFound_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 5, "invite_to_org");
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _invitationRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationInvitation?)null);

        var act = () => _sut.CancelAsync(5, 99, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task CancelAsync_InvitationFromAnotherOrg_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 5, "invite_to_org");
        var invitation = new OrganizationInvitation { Id = 10, OrganizationId = 99, Status = "Pending" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.CancelAsync(5, 10, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task CancelAsync_ValidRequest_SetsStatusToCancelledAndEnqueuesAuditEvent()
    {
        var caller = OrgMemberWithPermission(1, 5, "invite_to_org");
        var invitation = new OrganizationInvitation { Id = 10, OrganizationId = 5, Email = "x@r.io", Status = "Pending" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        await _sut.CancelAsync(5, 10, 1);

        invitation.Status.Should().Be("Cancelled");
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.Action == "organization_invitation_cancelled" &&
                    e.AuditScope == AuditRouting.ScopeOrganization),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AcceptAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptAsync_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _invitationRepo
            .Setup(r => r.GetByTokenAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationInvitation?)null);

        var act = () => _sut.AcceptAsync(1, "a@b.io", "ghost");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found or has expired.");
    }

    [Fact]
    public async Task AcceptAsync_EmailMismatch_ThrowsUnauthorizedAccessException()
    {
        var invitation = new OrganizationInvitation { Email = "owner@r.io", Token = "tok", Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(3) };
        _invitationRepo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(5, "intruder@r.io", "tok");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*different email address*");
    }

    [Fact]
    public async Task AcceptAsync_StatusNotPending_ThrowsInvalidOperationException()
    {
        var invitation = new OrganizationInvitation { Email = "u@r.io", Token = "tok", Status = "Accepted", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _invitationRepo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(5, "u@r.io", "tok");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer pending*Accepted*");
    }

    [Fact]
    public async Task AcceptAsync_ExpiredInvitation_SetsExpiredStatusAndThrows()
    {
        var invitation = new OrganizationInvitation
        {
            OrganizationId = 3, Email = "u@r.io", Token = "tok",
            Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _invitationRepo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(5, "u@r.io", "tok");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invitation has expired.");
        invitation.Status.Should().Be("Expired");
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_UserAlreadyMemberOfOrg_ThrowsInvalidOperationException()
    {
        var invitation = new OrganizationInvitation
        {
            OrganizationId = 3, Email = "u@r.io", Token = "tok",
            Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(3)
        };
        _invitationRepo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 5, OrganizationId = 3 });

        var act = () => _sut.AcceptAsync(5, "u@r.io", "tok");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are already a member of this organization.");
        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_ValidToken_CreatesMembershipAndEnqueuesTwoAuditEvents()
    {
        var invitation = new OrganizationInvitation
        {
            OrganizationId = 3, Email = "u@r.io", Token = "tok",
            Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(3)
        };
        var memberRole = new OrganizationRole { Id = 2, Name = "org_member" };

        _invitationRepo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        _orgMemberRepo.Setup(r => r.GetAsync(5, 3, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync(memberRole);

        await _sut.AcceptAsync(5, "u@r.io", "tok");

        invitation.Status.Should().Be("Accepted");
        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Once);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e => e.Action == "organization_member_added_via_invitation"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e => e.Action == "organization_invitation_accepted"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResendAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 5,
                Role = new OrganizationRole { Name = "viewer", RolePermissions = [] }
            });

        var act = () => _sut.ResendAsync(5, 10, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invite_to_org*");
        _invitationRepo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_InvitationNotFound_ThrowsKeyNotFoundException()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationInvitation?)null);

        var act = () => _sut.ResendAsync(5, 99, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task ResendAsync_InvitationFromAnotherOrg_ThrowsKeyNotFoundException()
    {
        var invitation = new OrganizationInvitation { Id = 10, OrganizationId = 99, Status = "Pending" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.ResendAsync(5, 10, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task ResendAsync_InvitationNotPending_ThrowsInvalidOperationException()
    {
        var invitation = new OrganizationInvitation { Id = 10, OrganizationId = 5, Status = "Accepted" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.ResendAsync(5, 10, 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Accepted*");
        _invitationRepo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendAsync_ValidRequest_RotatesTokenResetsExpiryAndEnqueuesAuditEvent()
    {
        const string originalToken = "original_token_abc";
        var invitation = new OrganizationInvitation
        {
            Id = 10, OrganizationId = 5, Email = "dev@relativa.io",
            Status = "Pending", Token = originalToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var result = await _sut.ResendAsync(5, 10, 1);

        invitation.Token.Should().NotBe(originalToken);
        invitation.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(6));
        result.Token.Should().Be(invitation.Token);
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_invitation_resent" &&
                    e.TargetId == 5 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_InvitationNotPending_ThrowsInvalidOperationException()
    {
        var invitation = new OrganizationInvitation { Id = 10, OrganizationId = 5, Status = "Accepted" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);

        var act = () => _sut.CancelAsync(5, 10, 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Accepted*");
        _invitationRepo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InviteAsync_CustomRoleNotFound_ThrowsArgumentException()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _orgRoleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("a@b.io", OrgRoleId: 99));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not exist.");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InviteAsync_CustomRoleFromAnotherOrg_ThrowsArgumentException()
    {
        var foreignRole = new OrganizationRole { Id = 7, Name = "org_member", OrganizationId = 99 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _orgRoleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("a@b.io", OrgRoleId: 7));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not belong to this organization*");
    }

    [Fact]
    public async Task InviteAsync_CustomNonDefaultRoleWithoutAssignOrgRolesPermission_ThrowsUnauthorizedAccessException()
    {
        var customRole = new OrganizationRole { Id = 7, Name = "org_admin", OrganizationId = 5 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _orgRoleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(customRole);

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("a@b.io", OrgRoleId: 7));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*assign_org_roles*");
    }

    [Fact]
    public async Task InviteAsync_CustomNonDefaultRoleWithAssignOrgRolesPermission_CreatesInvitationWithCorrectRole()
    {
        var caller = new UserRoleOrganization
        {
            UserId = 1, OrganizationId = 5,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions =
                [
                    new OrganizationRolePermission { Permission = new Permission { Name = "invite_to_org" } },
                    new OrganizationRolePermission { Permission = new Permission { Name = "assign_org_roles" } }
                ]
            }
        };
        var customRole = new OrganizationRole { Id = 7, Name = "org_admin", OrganizationId = 5 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(customRole);

        var result = await _sut.InviteAsync(5, 1, new InviteToOrgRequest("new@relativa.io", OrgRoleId: 7));

        result.Status.Should().Be("Pending");
        result.RoleName.Should().Be("org_admin");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteAsync_ExpiredPendingInvitationExistsForEmail_AutoExpiresOldAndCreatesNew()
    {
        var memberRole = new OrganizationRole { Id = 2, Name = "org_member" };
        var expiredExisting = new OrganizationInvitation
        {
            Id = 5, OrganizationId = 5, Email = "new@relativa.io",
            Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(-3)
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync(memberRole);
        _invitationRepo
            .Setup(r => r.GetPendingByOrgAndEmailAsync(5, "new@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredExisting);

        await _sut.InviteAsync(5, 1, new InviteToOrgRequest("new@relativa.io"));

        expiredExisting.Status.Should().Be("Expired");
        _invitationRepo.Verify(r => r.UpdateAsync(expiredExisting, It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteAsync_EmailBelongsToExistingOrgMember_ThrowsInvalidOperationException()
    {
        var memberRole = new OrganizationRole { Id = 2, Name = "org_member" };
        var existingUser = new User { Id = 20, Email = "member@relativa.io" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync(memberRole);
        _userRepo.Setup(r => r.GetByEmailAsync("member@relativa.io", It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);
        _orgMemberRepo.Setup(r => r.GetAsync(20, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 20, OrganizationId = 5 });

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("member@relativa.io"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This user is already a member of the organization.");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyPendingInvitationsAsync_EmptyOrWhitespaceEmail_ReturnsEmptyWithoutQueryingRepo()
    {
        var result = await _sut.GetMyPendingInvitationsAsync("  ");

        result.Should().BeEmpty();
        _invitationRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyPendingInvitationsAsync_FiltersOutExpiredAndNonPendingInvitations()
    {
        var now = DateTime.UtcNow;
        var invitations = new List<OrganizationInvitation>
        {
            new() { Id = 1, Status = "Pending",  ExpiresAt = now.AddDays(3),  Email = "u@r.io", Token = "t1" },
            new() { Id = 2, Status = "Accepted", ExpiresAt = now.AddDays(3),  Email = "u@r.io", Token = "t2" },
            new() { Id = 3, Status = "Pending",  ExpiresAt = now.AddDays(-1), Email = "u@r.io", Token = "t3" }
        };
        _invitationRepo.Setup(r => r.GetByEmailAsync("u@r.io", It.IsAny<CancellationToken>())).ReturnsAsync(invitations);

        var result = await _sut.GetMyPendingInvitationsAsync("u@r.io");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetByOrganizationAsync_CallerLacksInvitePermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 5,
                Role = new OrganizationRole { Name = "org_viewer", RolePermissions = [] }
            });

        var act = () => _sut.GetByOrganizationAsync(5, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invite_to_org*");
        _invitationRepo.Verify(r => r.GetByOrganizationIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByOrganizationAsync_FiltersPendingNonExpired_ReturnsCorrectSubset()
    {
        var now = DateTime.UtcNow;
        var invitations = new List<OrganizationInvitation>
        {
            new() { Id = 1, Status = "Pending",   ExpiresAt = now.AddDays(3),  Token = "t1", Email = "a@b.io", OrganizationId = 5 },
            new() { Id = 2, Status = "Pending",   ExpiresAt = now.AddDays(-1), Token = "t2", Email = "b@b.io", OrganizationId = 5 },
            new() { Id = 3, Status = "Accepted",  ExpiresAt = now.AddDays(2),  Token = "t3", Email = "c@b.io", OrganizationId = 5 },
            new() { Id = 4, Status = "Cancelled", ExpiresAt = now.AddDays(5),  Token = "t4", Email = "d@b.io", OrganizationId = 5 }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 5, "invite_to_org"));
        _invitationRepo.Setup(r => r.GetByOrganizationIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(invitations);

        var result = await _sut.GetByOrganizationAsync(5, 1);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
        result[0].Status.Should().Be("Pending");
    }
}
