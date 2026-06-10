using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrgInvitationMappingBranchTests
{
    private readonly Mock<IOrgInvitationRepository> _invitationRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IValidator<InviteToOrgRequest>> _inviteValidator = new();
    private readonly Mock<IOutboxWriter> _audit = new();

    private const int Org = 1, Caller = 7;

    public OrgInvitationMappingBranchTests()
    {
        _inviteValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteToOrgRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private OrgInvitationService Sut(bool withAudit = true) =>
        new(_invitationRepo.Object, _orgMemberRepo.Object, _orgRoleRepo.Object, _userRepo.Object,
            _inviteValidator.Object, withAudit ? _audit.Object : null);

    private void CallerCanInvite() =>
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(
            new UserRoleOrganization
            {
                UserId = Caller, OrganizationId = Org,
                Role = new OrganizationRole { Name = "admin", RolePermissions =
                    [new OrganizationRolePermission { Permission = new Permission { Name = "invite_to_org" } }] }
            });

    private static OrganizationInvitation Pending(int id, OrganizationRole? role) =>
        new()
        {
            Id = id, OrganizationId = Org, Email = "a@test.io", Token = "t" + id,
            Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(3),
            Organization = new Organization { Id = Org, Name = "Org" }, Role = role!,
        };

    [Fact]
    public async Task Decline_TokenNotFound_Throws()
    {
        _invitationRepo.Setup(r => r.GetByTokenAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationInvitation?)null);

        await Sut().Invoking(s => s.DeclineAsync(5, "a@test.io", "missing"))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "invitation_not_found_or_expired");
    }

    [Fact]
    public async Task GetMyPending_MapsRolePresentAndAbsent()
    {
        var withRole = Pending(1, new OrganizationRole { Name = "org_admin", DisplayName = null });
        var noRole = Pending(2, null);
        _invitationRepo.Setup(r => r.GetByEmailAsync("a@test.io", It.IsAny<CancellationToken>())).ReturnsAsync([withRole, noRole]);

        var result = await Sut().GetMyPendingInvitationsAsync("a@test.io");

        result.Should().HaveCount(2);
        result.Single(r => r.Id == 1).RoleDisplayName.Should().Be("Org Admin");
        result.Single(r => r.Id == 2).RoleName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByOrganization_MapsRolePresentAndAbsent()
    {
        CallerCanInvite();
        var withRole = Pending(1, new OrganizationRole { Name = "org_admin", DisplayName = "Admin" });
        var noRole = Pending(2, null);
        _invitationRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync([withRole, noRole]);

        var result = await Sut().GetByOrganizationAsync(Org, Caller);

        result.Should().HaveCount(2);
        result.Single(r => r.Id == 1).RoleDisplayName.Should().Be("Admin");
        result.Single(r => r.Id == 2).RoleName.Should().BeEmpty();
    }

    [Fact]
    public async Task Invite_NoRequestedRoleAndNoSystemRole_Throws()
    {
        CallerCanInvite();
        _orgRoleRepo.Setup(r => r.GetSystemRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await Sut().Invoking(s => s.InviteAsync(Org, Caller, new InviteToOrgRequest("new@test.io")))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "default_org_role_not_found");
    }

    [Fact]
    public async Task Invite_ExpiredPendingInvitationExists_AutoExpiresAndCreatesNew()
    {
        CallerCanInvite();
        _orgRoleRepo.Setup(r => r.GetSystemRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OrganizationRole { Id = 9, Name = "org_member", Priority = 5 }]);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var expired = new OrganizationInvitation
        {
            Id = 88, OrganizationId = Org, Email = "new@test.io", Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), Token = "old",
        };
        _invitationRepo.Setup(r => r.GetPendingByOrgAndEmailAsync(Org, "new@test.io", It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        await Sut().InviteAsync(Org, Caller, new InviteToOrgRequest("new@test.io"));

        expired.Status.Should().Be("Expired");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invite_WithoutAuditWriter_SucceedsWithoutEnqueue()
    {
        CallerCanInvite();
        _orgRoleRepo.Setup(r => r.GetSystemRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OrganizationRole { Id = 9, Name = "org_member", Priority = 5 }]);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _invitationRepo.Setup(r => r.GetPendingByOrgAndEmailAsync(Org, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationInvitation?)null);

        var result = await Sut(withAudit: false).InviteAsync(Org, Caller, new InviteToOrgRequest("new@test.io"));

        result.Status.Should().Be("Pending");
        _audit.Verify(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invite_EmailBelongsToExistingMember_Throws()
    {
        CallerCanInvite();
        _orgRoleRepo.Setup(r => r.GetSystemRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OrganizationRole { Id = 9, Name = "org_member", Priority = 5 }]);
        _userRepo.Setup(r => r.GetByEmailAsync("new@test.io", It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = 30, Email = "new@test.io" });
        _orgMemberRepo.Setup(r => r.GetAsync(30, Org, It.IsAny<CancellationToken>())).ReturnsAsync(
            new UserRoleOrganization { UserId = 30, OrganizationId = Org });

        await Sut().Invoking(s => s.InviteAsync(Org, Caller, new InviteToOrgRequest("new@test.io")))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "already_org_member");
    }
}
