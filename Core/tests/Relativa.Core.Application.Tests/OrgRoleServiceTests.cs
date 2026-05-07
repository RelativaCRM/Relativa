using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application;
using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrgRoleServiceTests
{
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IValidator<CreateOrgRoleRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateOrgRoleRequest>> _updateValidator = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly OrgRoleService _sut;

    public OrgRoleServiceTests()
    {
        _sut = new OrgRoleService(
            _orgRoleRepo.Object,
            _permissionRepo.Object,
            _orgMemberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _auditOutboxWriter.Object);

        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateOrgRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateOrgRoleRequest>>(), It.IsAny<CancellationToken>()))
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
                Priority = OrganizationRolePriorityTiers.Admin,
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
            Role = new OrganizationRole { Name = "org_viewer", Priority = OrganizationRolePriorityTiers.Member, RolePermissions = [] }
        };

    // ── GetByOrganizationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetByOrganizationAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.GetByOrganizationAsync(2, 5);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this organization.");
    }

    [Fact]
    public async Task GetByOrganizationAsync_ValidMember_ReturnsOnlyActiveRoles()
    {
        var member = OrgMemberNoPermissions(1, 3);
        var roles = new List<OrganizationRole>
        {
            new() { Id = 1, Name = "org_owner", OrganizationId = null, Priority = OrganizationRolePriorityTiers.Owner, IsArchived = false, RolePermissions = [] },
            new() { Id = 2, Name = "archived-role", OrganizationId = 3, Priority = 5, IsArchived = true, RolePermissions = [] },
            new() { Id = 3, Name = "custom-reviewer", OrganizationId = 3, Priority = 3, IsArchived = false, RolePermissions = [] }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByOrganizationIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await _sut.GetByOrganizationAsync(3, 1);

        result.Should().HaveCount(2);
        result.Should().NotContain(r => r.Name == "archived-role");
    }

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var member = OrgMemberNoPermissions(1, 4);
        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        var act = () => _sut.CreateAsync(4, 1, new CreateOrgRoleRequest("read-only", [1], OrganizationRolePriorityTiers.CustomRoleMinimum));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_org_roles*");
        _orgRoleRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InvalidPermissionIds_ThrowsArgumentException()
    {
        var member = OrgMemberWithPermission(1, 4, "manage_org_roles");

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _permissionRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Permission { Id = 1, Name = "view_only" }]);

        var act = () => _sut.CreateAsync(4, 1, new CreateOrgRoleRequest("bad-role", [1, 999], OrganizationRolePriorityTiers.CustomRoleMinimum));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("One or more permission IDs are invalid.");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesRoleWithPermissionsAndEnqueuesAuditEvent()
    {
        var member = OrgMemberWithPermission(1, 4, "manage_org_roles");
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "view_reports" },
            new() { Id = 2, Name = "export_data" }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _permissionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(permissions);

        var result = await _sut.CreateAsync(4, 1, new CreateOrgRoleRequest("analyst", [1, 2], 3));

        result.Name.Should().Be("analyst");
        result.Priority.Should().Be(3);
        result.IsSystem.Should().BeFalse();
        result.Permissions.Should().HaveCount(2);
        _orgRoleRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationRole>(), It.IsAny<CancellationToken>()), Times.Once);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_role_created" &&
                    e.TargetId == 4 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var member = OrgMemberNoPermissions(1, 5);
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        var act = () => _sut.UpdateAsync(5, 3, 1, new UpdateOrgRoleRequest("renamed", null, null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateAsync_RoleNotFound_ThrowsKeyNotFoundException()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);

        var act = () => _sut.UpdateAsync(5, 99, 1, new UpdateOrgRoleRequest("renamed", null, null));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found.");
    }

    [Fact]
    public async Task UpdateAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var systemRole = new OrganizationRole { Id = 1, Name = "org_owner", OrganizationId = null, IsArchived = false, RolePermissions = [] };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(systemRole);

        var act = () => _sut.UpdateAsync(5, 1, 1, new UpdateOrgRoleRequest("hacked", null, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be modified.");
        _orgRoleRepo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RoleFromAnotherOrg_ThrowsKeyNotFoundException()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var foreignRole = new OrganizationRole { Id = 8, Name = "custom", OrganizationId = 99, IsArchived = false, RolePermissions = [] };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.UpdateAsync(5, 8, 1, new UpdateOrgRoleRequest("renamed", null, null));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found in this organization.");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesNameAndEnqueuesAuditEvent()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var role = new OrganizationRole { Id = 9, Name = "old-name", OrganizationId = 5, IsArchived = false, RolePermissions = [] };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        await _sut.UpdateAsync(5, 9, 1, new UpdateOrgRoleRequest("new-name", null, null));

        role.Name.Should().Be("new-name");
        _orgRoleRepo.Verify(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_role_updated"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── ArchiveAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var member = OrgMemberNoPermissions(1, 5);
        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        var act = () => _sut.ArchiveAsync(5, 3, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ArchiveAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var systemRole = new OrganizationRole { Id = 2, Name = "org_member", OrganizationId = null };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(systemRole);

        var act = () => _sut.ArchiveAsync(5, 2, 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be deleted.");
    }

    [Fact]
    public async Task ArchiveAsync_RoleFromAnotherOrg_ThrowsKeyNotFoundException()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var foreignRole = new OrganizationRole { Id = 8, Name = "custom", OrganizationId = 99 };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.ArchiveAsync(5, 8, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found in this organization.");
    }

    [Fact]
    public async Task ArchiveAsync_ValidRequest_SetsIsArchivedAndEnqueuesAuditEvent()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var role = new OrganizationRole { Id = 9, Name = "old-analyst", OrganizationId = 5, IsArchived = false };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        await _sut.ArchiveAsync(5, 9, 1);

        role.IsArchived.Should().BeTrue();
        _orgRoleRepo.Verify(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.Action == "organization_role_archived" &&
                    e.TargetId == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithPermissionIds_ClearsAndRebuildsPermissions()
    {
        var member = OrgMemberWithPermission(1, 5, "manage_org_roles");
        var role = new OrganizationRole { Id = 9, Name = "analyst", OrganizationId = 5, IsArchived = false, RolePermissions = [] };
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "view_reports" },
            new() { Id = 2, Name = "export_data" }
        };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _orgRoleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _permissionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(permissions);

        await _sut.UpdateAsync(5, 9, 1, new UpdateOrgRoleRequest(null, [1, 2], null));

        role.RolePermissions.Should().HaveCount(2);
        role.RolePermissions.Should().Contain(rp => rp.PermissionId == 1);
        role.RolePermissions.Should().Contain(rp => rp.PermissionId == 2);
        _permissionRepo.Verify(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
