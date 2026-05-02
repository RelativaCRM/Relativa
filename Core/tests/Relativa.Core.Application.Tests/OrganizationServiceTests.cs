using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrganizationServiceTests
{
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IValidator<CreateOrganizationRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateOrganizationRequest>> _updateValidator = new();
    private readonly Mock<IAuditOutboxWriter> _auditOutboxWriter = new();
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        _sut = new OrganizationService(
            _orgRepo.Object,
            _orgMemberRepo.Object,
            _orgRoleRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _auditOutboxWriter.Object);

        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateOrganizationRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateOrganizationRequest>>(), It.IsAny<CancellationToken>()))
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

    private UserRoleOrganization OrgMemberNoPermissions(int userId, int orgId) =>
        new()
        {
            UserId = userId,
            OrganizationId = orgId,
            Role = new OrganizationRole { Name = "org_viewer", RolePermissions = [] }
        };

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrgAndAssignsCallerAsOwner()
    {
        var ownerRole = new OrganizationRole { Id = 1, Name = "org_owner" };

        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerRole);

        Organization? capturedOrg = null;
        _orgRepo
            .Setup(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .Callback<Organization, CancellationToken>((o, _) => capturedOrg = o);

        UserRoleOrganization? capturedMembership = null;
        _orgMemberRepo
            .Setup(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()))
            .Callback<UserRoleOrganization, CancellationToken>((m, _) => capturedMembership = m);

        var result = await _sut.CreateAsync(42, new CreateOrganizationRequest("Relativa Inc."));

        result.Name.Should().Be("Relativa Inc.");
        result.UserRole.Should().Be("org_owner");
        result.MemberCount.Should().Be(1);
        capturedOrg!.IsArchived.Should().BeFalse();
        capturedMembership!.UserId.Should().Be(42);
        capturedMembership.OrgRoleId.Should().Be(ownerRole.Id);
    }

    [Fact]
    public async Task CreateAsync_OrgOwnerRoleNotFound_ThrowsInvalidOperationException()
    {
        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationRole?)null);

        var act = () => _sut.CreateAsync(1, new CreateOrganizationRequest("Orphan Org"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System org_owner role not found.");
        _orgRepo.Verify(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationException()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateOrganizationRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Name", "Name is required.") }));

        var act = () => _sut.CreateAsync(1, new CreateOrganizationRequest(""));

        await act.Should().ThrowAsync<ValidationException>();
        _orgRoleRepo.Verify(r => r.GetSystemRoleByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_EnqueuesOrganizationCreatedAuditEvent()
    {
        var ownerRole = new OrganizationRole { Id = 1, Name = "org_owner" };
        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerRole);

        await _sut.CreateAsync(7, new CreateOrganizationRequest("Audit Corp"));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_created" &&
                    e.ActorUserId == 7 &&
                    e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetByIdAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.GetByIdAsync(5, 99);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this organization.");
    }

    [Fact]
    public async Task GetByIdAsync_OrgNotFound_ThrowsKeyNotFoundException()
    {
        var member = OrgMemberNoPermissions(1, 5);

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _orgRepo
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);
        _orgMemberRepo
            .Setup(r => r.GetByOrganizationIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _sut.GetByIdAsync(5, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Organization not found.");
    }

    [Fact]
    public async Task GetByIdAsync_ValidMember_ReturnsOrgWithCorrectMemberCount()
    {
        var member = OrgMemberNoPermissions(1, 5);
        var org = new Organization { Id = 5, Name = "Relativa" };
        var allMembers = new List<UserRoleOrganization>
        {
            new() { IsArchived = false },
            new() { IsArchived = false },
            new() { IsArchived = true }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(org);
        _orgMemberRepo.Setup(r => r.GetByOrganizationIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(allMembers);

        var result = await _sut.GetByIdAsync(5, 1);

        result.Id.Should().Be(5);
        result.Name.Should().Be("Relativa");
        result.MemberCount.Should().Be(3);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var member = OrgMemberNoPermissions(3, 10);

        _orgMemberRepo.Setup(r => r.GetAsync(3, 10, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        var act = () => _sut.UpdateAsync(10, 3, new UpdateOrganizationRequest("New Name"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_org_settings*");
        _orgRepo.Verify(r => r.UpdateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_OrgNotFound_ThrowsKeyNotFoundException()
    {
        var member = OrgMemberWithPermission(1, 3, "manage_org_settings");

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((Organization?)null);

        var act = () => _sut.UpdateAsync(3, 1, new UpdateOrganizationRequest("Ghost"));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Organization not found.");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_EnqueuesAuditEventWithOldAndNewName()
    {
        var org = new Organization { Id = 3, Name = "Old Corp" };
        var member = OrgMemberWithPermission(1, 3, "manage_org_settings");

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(org);

        await _sut.UpdateAsync(3, 1, new UpdateOrganizationRequest("New Corp"));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_updated" &&
                    e.FieldName == "name" &&
                    e.OldValueJson!.Contains("Old Corp") &&
                    e.NewValueJson!.Contains("New Corp")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetMembersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembersAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.GetMembersAsync(2, 5);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetMembersAsync_ValidMember_ReturnsOnlyActiveMembers()
    {
        var caller = OrgMemberNoPermissions(1, 2);
        var members = new List<UserRoleOrganization>
        {
            new()
            {
                UserId = 1, IsArchived = false, JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Taras", LastName = "K", Email = "t@r.io" },
                Role = new OrganizationRole { Name = "org_owner" }
            },
            new()
            {
                UserId = 2, IsArchived = true, JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Ivan", LastName = "P", Email = "i@r.io" },
                Role = new OrganizationRole { Name = "org_member" }
            }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetByOrganizationIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await _sut.GetMembersAsync(2, 1);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("t@r.io");
    }

    // ── RemoveMemberAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMemberAsync_SelfRemove_SkipsPermissionCheckAndRemovesMember()
    {
        var member = OrgMemberNoPermissions(3, 6);

        _orgMemberRepo.Setup(r => r.GetAsync(3, 6, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        await _sut.RemoveMemberAsync(6, 3, 3);

        _orgMemberRepo.Verify(r => r.RemoveAsync(member, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_CallerLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var caller = OrgMemberNoPermissions(1, 6);

        _orgMemberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var act = () => _sut.RemoveMemberAsync(6, 99, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _orgMemberRepo.Verify(r => r.RemoveAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMemberAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 6, "remove_org_members");

        _orgMemberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(77, 6, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.RemoveMemberAsync(6, 77, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this organization.");
    }

    [Fact]
    public async Task RemoveMemberAsync_ValidRequest_EnqueuesOrganizationMemberRemovedAuditEvent()
    {
        var caller = OrgMemberWithPermission(1, 6, "remove_org_members");
        var target = OrgMemberNoPermissions(77, 6);

        _orgMemberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(77, 6, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await _sut.RemoveMemberAsync(6, 77, 1);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_member_removed" &&
                    e.TargetId == 6 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── ChangeMemberRoleAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ChangeMemberRoleAsync_CallerLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var caller = OrgMemberNoPermissions(1, 4);

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var act = () => _sut.ChangeMemberRoleAsync(4, 2, 1, new ChangeOrgMemberRoleRequest(3));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*assign_org_roles*");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        var caller = OrgMemberWithPermission(1, 4, "assign_org_roles");

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(99, 4, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.ChangeMemberRoleAsync(4, 99, 1, new ChangeOrgMemberRoleRequest(3));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this organization.");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_RoleNotFound_ThrowsArgumentException()
    {
        var caller = OrgMemberWithPermission(1, 4, "assign_org_roles");
        var target = OrgMemberNoPermissions(2, 4);

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);

        var act = () => _sut.ChangeMemberRoleAsync(4, 2, 1, new ChangeOrgMemberRoleRequest(99));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not exist.");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_RoleFromAnotherOrg_ThrowsArgumentException()
    {
        var caller = OrgMemberWithPermission(1, 4, "assign_org_roles");
        var target = OrgMemberNoPermissions(2, 4);
        var foreignRole = new OrganizationRole { Id = 7, Name = "custom", OrganizationId = 99 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.ChangeMemberRoleAsync(4, 2, 1, new ChangeOrgMemberRoleRequest(7));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not belong to this organization.");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_ValidRequest_ChangesRoleAndEnqueuesAuditEvent()
    {
        var caller = OrgMemberWithPermission(1, 4, "assign_org_roles");
        var target = OrgMemberNoPermissions(2, 4);
        var role = new OrganizationRole { Id = 5, Name = "org_manager", OrganizationId = 4 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orgMemberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        await _sut.ChangeMemberRoleAsync(4, 2, 1, new ChangeOrgMemberRoleRequest(5));

        target.OrgRoleId.Should().Be(5);
        _orgMemberRepo.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_member_role_changed" &&
                    e.TargetId == 4 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
