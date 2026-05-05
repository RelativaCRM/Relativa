using FluentAssertions;
using Moq;
using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class JoinRequestServiceTests
{
    private readonly Mock<IJoinRequestRepository> _joinRequestRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly JoinRequestService _sut;

    public JoinRequestServiceTests()
    {
        _sut = new JoinRequestService(
            _joinRequestRepo.Object,
            _orgMemberRepo.Object,
            _orgRoleRepo.Object,
            _orgRepo.Object,
            _auditOutboxWriter.Object);
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

    // ── SubmitAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_OrgNotFound_ThrowsKeyNotFoundException()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var act = () => _sut.SubmitAsync(99, 1, new CreateJoinRequestRequest("please"));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Organization not found.");
        _joinRequestRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationJoinRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_OrgIsArchived_ThrowsInvalidOperationException()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 2, IsArchived = true });

        var act = () => _sut.SubmitAsync(2, 1, new CreateJoinRequestRequest(null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This organization is archived.");
    }

    [Fact]
    public async Task SubmitAsync_UserAlreadyMember_ThrowsInvalidOperationException()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 2, IsArchived = false });
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 5, OrganizationId = 2 });

        var act = () => _sut.SubmitAsync(2, 5, new CreateJoinRequestRequest(null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are already a member of this organization.");
    }

    [Fact]
    public async Task SubmitAsync_AlreadyPendingRequest_ThrowsInvalidOperationException()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 2, IsArchived = false });
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);
        _joinRequestRepo
            .Setup(r => r.GetPendingAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationJoinRequest { UserId = 5, OrganizationId = 2, Status = "Pending" });

        var act = () => _sut.SubmitAsync(2, 5, new CreateJoinRequestRequest("again"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already have a pending*");
    }

    [Fact]
    public async Task SubmitAsync_ValidRequest_CreatesRequestWithPendingStatusAndEnqueuesAuditEvent()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 2, IsArchived = false });
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);
        _joinRequestRepo
            .Setup(r => r.GetPendingAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationJoinRequest?)null);

        OrganizationJoinRequest? captured = null;
        _joinRequestRepo
            .Setup(r => r.AddAsync(It.IsAny<OrganizationJoinRequest>(), It.IsAny<CancellationToken>()))
            .Callback<OrganizationJoinRequest, CancellationToken>((jr, _) => captured = jr);

        var result = await _sut.SubmitAsync(2, 5, new CreateJoinRequestRequest("hi there"));

        captured!.Status.Should().Be("Pending");
        captured.Message.Should().Be("hi there");
        captured.UserId.Should().Be(5);
        result.Status.Should().Be("Pending");

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_join_request_submitted" &&
                    e.ActorUserId == 5 &&
                    e.TargetId == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_NullMessage_CreatesRequestSuccessfully()
    {
        _orgRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 2, IsArchived = false });
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);
        _joinRequestRepo
            .Setup(r => r.GetPendingAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationJoinRequest?)null);

        var act = () => _sut.SubmitAsync(2, 5, new CreateJoinRequestRequest(null));

        await act.Should().NotThrowAsync();
        _joinRequestRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationJoinRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetByOrganizationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetByOrganizationAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1,
                OrganizationId = 3,
                Role = new OrganizationRole { Name = "viewer", RolePermissions = [] }
            });

        var act = () => _sut.GetByOrganizationAsync(3, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_join_requests*");
    }

    [Fact]
    public async Task GetByOrganizationAsync_ValidCaller_ReturnsPendingRequestsOnly()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var requests = new List<OrganizationJoinRequest>
        {
            new() { Id = 1, UserId = 10, Status = "Pending", CreatedAt = DateTime.UtcNow,
                User = new User { FirstName = "A", LastName = "B", Email = "a@b.io" } },
            new() { Id = 2, UserId = 11, Status = "Approved", CreatedAt = DateTime.UtcNow,
                User = new User { FirstName = "C", LastName = "D", Email = "c@d.io" } }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByOrganizationIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(requests);

        var result = await _sut.GetByOrganizationAsync(3, 1);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be("Pending");
    }

    // ── ReviewAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1,
                OrganizationId = 3,
                Role = new OrganizationRole { Name = "viewer", RolePermissions = [] }
            });

        var act = () => _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ReviewAsync_RequestNotFound_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationJoinRequest?)null);

        var act = () => _sut.ReviewAsync(3, 99, 1, new ReviewJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Join request not found.");
    }

    [Fact]
    public async Task ReviewAsync_RequestNotInOrg_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var request = new OrganizationJoinRequest { Id = 10, OrganizationId = 99, Status = "Pending" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var act = () => _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Join request not found in this organization.");
    }

    [Fact]
    public async Task ReviewAsync_RequestNotPending_ThrowsInvalidOperationException()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var joinRequest = new OrganizationJoinRequest { Id = 10, OrganizationId = 3, Status = "Approved" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(joinRequest);

        var act = () => _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer pending*Approved*");
    }

    [Fact]
    public async Task ReviewAsync_InvalidDecision_ThrowsArgumentException()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var joinRequest = new OrganizationJoinRequest { Id = 10, OrganizationId = 3, Status = "Pending", UserId = 5 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(joinRequest);

        var act = () => _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Maybe"));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Approved*Rejected*");
    }

    [Fact]
    public async Task ReviewAsync_ApprovedDecision_AddsMemberAndEnqueuesTwoAuditEvents()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var joinRequest = new OrganizationJoinRequest { Id = 10, OrganizationId = 3, Status = "Pending", UserId = 5 };
        var memberRole = new OrganizationRole { Id = 2, Name = "org_member" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(joinRequest);
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync(memberRole);

        await _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Approved"));

        joinRequest.Status.Should().Be("Approved");
        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Once);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e => e.Action == "organization_member_added_via_join_request"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e => e.Action == "organization_join_request_reviewed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_ApprovedDecision_OrgMemberRoleNotFound_ThrowsInvalidOperationException()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var joinRequest = new OrganizationJoinRequest { Id = 10, OrganizationId = 3, Status = "Pending", UserId = 5 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(joinRequest);
        _orgRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);

        var act = () => _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System org_member role not found.");
    }

    [Fact]
    public async Task ReviewAsync_RejectedDecision_UpdatesStatusAndEnqueuesOneAuditEvent()
    {
        var caller = OrgMemberWithPermission(1, 3, "manage_join_requests");
        var joinRequest = new OrganizationJoinRequest { Id = 10, OrganizationId = 3, Status = "Pending", UserId = 5 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(joinRequest);

        await _sut.ReviewAsync(3, 10, 1, new ReviewJoinRequestRequest("Rejected"));

        joinRequest.Status.Should().Be("Rejected");
        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
