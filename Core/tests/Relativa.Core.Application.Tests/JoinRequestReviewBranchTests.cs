using FluentAssertions;
using Moq;
using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class JoinRequestReviewBranchTests
{
    private readonly Mock<IJoinRequestRepository> _joinRequestRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IOrganizationSettingsRepository> _orgSettingsRepo = new();
    private readonly Mock<IOutboxWriter> _audit = new();
    private readonly JoinRequestService _sut;

    private const int Org = 1, Caller = 7, Requester = 20, ReqId = 55;

    public JoinRequestReviewBranchTests()
    {
        _audit.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Caller, "manage_join_requests"));
        _orgMemberRepo.Setup(r => r.GetAsync(Requester, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync(Pending());
        _sut = new JoinRequestService(_joinRequestRepo.Object, _orgMemberRepo.Object, _orgRoleRepo.Object, _orgRepo.Object, _orgSettingsRepo.Object, _audit.Object);
    }

    private static UserRoleOrganization Member(int userId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            OrganizationId = Org,
            Role = new OrganizationRole { Name = "r", RolePermissions = permissions.Select(p => new OrganizationRolePermission { Permission = new Permission { Name = p } }).ToList() },
        };

    private static OrganizationJoinRequest Pending() => new() { Id = ReqId, UserId = Requester, OrganizationId = Org, Status = "Pending" };

    private async Task<string> ReviewCode(string decision) =>
        (await Assert.ThrowsAsync<AppException>(() => _sut.ReviewAsync(Org, ReqId, Caller, new ReviewJoinRequestRequest(decision)))).Code;

    [Fact]
    public async Task Review_CallerNotMember_Throws()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        (await ReviewCode("Approved")).Should().Be("not_org_member");
    }

    [Fact]
    public async Task Review_CallerLacksPermission_Throws()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Caller, "view_audit_log"));
        (await ReviewCode("Approved")).Should().Be("permission_denied");
    }

    [Fact]
    public async Task Review_RequestNotFound_Throws()
    {
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationJoinRequest?)null);
        (await ReviewCode("Approved")).Should().Be("join_request_not_found");
    }

    [Fact]
    public async Task Review_RequestBelongsToDifferentOrg_Throws()
    {
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationJoinRequest { Id = ReqId, UserId = Requester, OrganizationId = 999, Status = "Pending" });
        (await ReviewCode("Approved")).Should().Be("join_request_not_found");
    }

    [Fact]
    public async Task Review_NotPending_Throws()
    {
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationJoinRequest { Id = ReqId, UserId = Requester, OrganizationId = Org, Status = "Approved" });
        (await ReviewCode("Approved")).Should().Be("join_request_not_pending");
    }

    [Fact]
    public async Task Review_Approved_RequesterAlreadyMember_Throws()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(Requester, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Requester));
        (await ReviewCode("Approved")).Should().Be("already_org_member");
    }

    [Fact]
    public async Task Review_Approved_ConfiguredDefaultRoleMissing_Throws()
    {
        _orgSettingsRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationSettings { OrganizationId = Org, DefaultOrgRoleId = 5 });
        _orgRoleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);
        (await ReviewCode("Approved")).Should().Be("default_org_role_not_found");
    }

    [Fact]
    public async Task Review_Approved_NoSystemRoleFallback_Throws()
    {
        _orgSettingsRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationSettings?)null);
        _orgRoleRepo.Setup(r => r.GetSystemRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        (await ReviewCode("Approved")).Should().Be("default_org_role_not_found");
    }

    [Fact]
    public async Task Review_InvalidDecision_Throws()
    {
        (await ReviewCode("Maybe")).Should().Be("invalid_decision");
    }

    [Fact]
    public async Task Review_Approved_AddsMembershipAndUpdatesRequest()
    {
        _orgSettingsRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationSettings { OrganizationId = Org, DefaultOrgRoleId = 5 });
        _orgRoleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationRole { Id = 5, Name = "member" });

        await _sut.ReviewAsync(Org, ReqId, Caller, new ReviewJoinRequestRequest("Approved"));

        _orgMemberRepo.Verify(r => r.AddAsync(It.Is<UserRoleOrganization>(m => m.UserId == Requester && m.OrgRoleId == 5), It.IsAny<CancellationToken>()), Times.Once);
        _joinRequestRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationJoinRequest>(j => j.Status == "Approved" && j.ReviewedByUserId == Caller), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_Rejected_UpdatesRequestWithoutAddingMember()
    {
        await _sut.ReviewAsync(Org, ReqId, Caller, new ReviewJoinRequestRequest("Rejected"));

        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Never);
        _joinRequestRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationJoinRequest>(j => j.Status == "Rejected"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_NotFound_Throws()
    {
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationJoinRequest?)null);
        (await Assert.ThrowsAsync<AppException>(() => _sut.CancelMineAsync(ReqId, Requester))).Code.Should().Be("join_request_not_found");
    }

    [Fact]
    public async Task Cancel_NotOwnRequest_Throws()
    {
        (await Assert.ThrowsAsync<AppException>(() => _sut.CancelMineAsync(ReqId, 999))).Code.Should().Be("cancel_own_join_only");
    }

    [Fact]
    public async Task Cancel_NotPending_Throws()
    {
        _joinRequestRepo.Setup(r => r.GetByIdAsync(ReqId, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationJoinRequest { Id = ReqId, UserId = Requester, OrganizationId = Org, Status = "Rejected" });
        (await Assert.ThrowsAsync<AppException>(() => _sut.CancelMineAsync(ReqId, Requester))).Code.Should().Be("join_request_not_pending");
    }

    [Fact]
    public async Task Cancel_Pending_SetsCancelledAndUpdates()
    {
        await _sut.CancelMineAsync(ReqId, Requester);

        _joinRequestRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationJoinRequest>(j => j.Status == "Cancelled"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
