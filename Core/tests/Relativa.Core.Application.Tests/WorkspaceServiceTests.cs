using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceServiceTests
{
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly WorkspaceAccessEvaluator _workspaceAccessEvaluator;
    private readonly Mock<IValidator<CreateWorkspaceRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateWorkspaceRequest>> _updateValidator = new();
    private readonly Mock<IValidator<UpdateWorkspaceSettingsRequest>> _updateSettingsValidator = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly Mock<IWorkspaceSettingsRepository> _workspaceSettingsRepo = new();
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        _workspaceAccessEvaluator = new WorkspaceAccessEvaluator(
            _memberRepo.Object,
            _orgMemberRepo.Object,
            _workspaceRepo.Object,
            _roleRepo.Object);

        _sut = new WorkspaceService(
            _workspaceRepo.Object,
            _memberRepo.Object,
            _roleRepo.Object,
            _orgMemberRepo.Object,
            _workspaceAccessEvaluator,
            _workspaceSettingsRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _updateSettingsValidator.Object,
            _auditOutboxWriter.Object);

        _updateSettingsValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceSettingsRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private void SetupValidCreate() =>
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidUpdate() =>
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
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

    private UserRoleWorkspace AdminMember(int userId, int workspaceId) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole { Name = "ws_admin", RolePermissions = [] }
        };

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

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesWorkspaceAndAddsCreatorAsAdmin()
    {
        var request = new CreateWorkspaceRequest("Kyiv Sales Team", 10);
        var adminRole = new WorkspaceRole { Id = 1, Name = "ws_admin" };
        var orgMember = OrgMemberWithPermission(42, 10, "create_workspaces");

        SetupValidCreate();
        _orgMemberRepo.Setup(r => r.GetAsync(42, 10, It.IsAny<CancellationToken>())).ReturnsAsync(orgMember);
        _roleRepo.Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>())).ReturnsAsync(adminRole);

        Workspace? capturedWorkspace = null;
        _workspaceRepo
            .Setup(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((w, _) => capturedWorkspace = w);

        UserRoleWorkspace? capturedMember = null;
        _memberRepo
            .Setup(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()))
            .Callback<UserRoleWorkspace, CancellationToken>((m, _) => capturedMember = m);

        _memberRepo
            .Setup(r => r.GetAsync(42, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int u, int wid, CancellationToken _) => new UserRoleWorkspace
            {
                UserId = u,
                WorkspaceId = wid,
                Role = new WorkspaceRole
                {
                    Name = "ws_admin",
                    RolePermissions =
                    [
                        new WorkspaceRolePermission { Permission = new Permission { Name = "create_entities" } },
                        new WorkspaceRolePermission { Permission = new Permission { Name = "view_entities" } }
                    ]
                }
            });

        var result = await _sut.CreateAsync(42, request);

        result.Name.Should().Be(request.Name);
        result.UserRole.Should().Be("ws_admin");
        result.MemberCount.Should().Be(1);
        result.MyPermissions.Should().Contain("create_entities");
        result.MyPermissions.Should().Contain("view_entities");
        capturedWorkspace!.CreatedByUserId.Should().Be(42);
        capturedMember!.UserId.Should().Be(42);
        capturedMember.WsRoleId.Should().Be(adminRole.Id);
    }

    [Fact]
    public async Task CreateAsync_UserNotInOrg_ThrowsUnauthorizedAccessException()
    {
        SetupValidCreate();
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.CreateAsync(1, new CreateWorkspaceRequest("Team", 5));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("*not a member of this organization*");
        _workspaceRepo.Verify(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UserLacksCreateWorkspacesPermission_ThrowsUnauthorizedAccessException()
    {
        SetupValidCreate();
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 5,
                Role = new OrganizationRole { Name = "org_viewer", RolePermissions = [] }
            });

        var act = () => _sut.CreateAsync(1, new CreateWorkspaceRequest("Team", 5));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("*create_workspaces*");
        _workspaceRepo.Verify(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AdminRoleNotFound_ThrowsInvalidOperationException()
    {
        var request = new CreateWorkspaceRequest("Lviv Team", 1);
        var orgMember = OrgMemberWithPermission(1, 1, "create_workspaces");

        SetupValidCreate();
        _orgMemberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(orgMember);
        _roleRepo.Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.CreateAsync(1, request);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("System ws_admin role not found.");
        _workspaceRepo.Verify(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationException()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Name", "Workspace name is required.") }));

        var act = () => _sut.CreateAsync(1, new CreateWorkspaceRequest("", 1));

        await act.Should().ThrowAsync<ValidationException>();
        _roleRepo.Verify(r => r.GetSystemRoleByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_EnqueuesWorkspaceCreatedAuditEvent()
    {
        var adminRole = new WorkspaceRole { Id = 1, Name = "ws_admin" };
        var orgMember = OrgMemberWithPermission(7, 1, "create_workspaces");

        SetupValidCreate();
        _orgMemberRepo.Setup(r => r.GetAsync(7, 1, It.IsAny<CancellationToken>())).ReturnsAsync(orgMember);
        _roleRepo.Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>())).ReturnsAsync(adminRole);
        _memberRepo
            .Setup(r => r.GetAsync(7, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int u, int wid, CancellationToken _) => new UserRoleWorkspace
            {
                UserId = u,
                WorkspaceId = wid,
                Role = new WorkspaceRole
                {
                    Name = "ws_admin",
                    RolePermissions =
                    [
                        new WorkspaceRolePermission { Permission = new Permission { Name = "create_entities" } }
                    ]
                }
            });

        await _sut.CreateAsync(7, new CreateWorkspaceRequest("Audit Team", 1));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_created" &&
                    e.ActorUserId == 7 &&
                    e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullAuditWriter_CompletesWithoutError()
    {
        var sut = new WorkspaceService(
            _workspaceRepo.Object, _memberRepo.Object, _roleRepo.Object,
            _orgMemberRepo.Object, _workspaceAccessEvaluator,
            _workspaceSettingsRepo.Object, _createValidator.Object, _updateValidator.Object, null);

        SetupValidCreate();
        _orgMemberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberWithPermission(1, 1, "create_workspaces"));
        _roleRepo.Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceRole { Id = 1, Name = "ws_admin" });
        _memberRepo
            .Setup(r => r.GetAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int u, int wid, CancellationToken _) => new UserRoleWorkspace
            {
                UserId = u,
                WorkspaceId = wid,
                Role = new WorkspaceRole { Name = "ws_admin", RolePermissions = [] }
            });

        var act = () => sut.CreateAsync(1, new CreateWorkspaceRequest("No Audit", 1));

        await act.Should().NotThrowAsync();
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.GetByIdAsync(5, 99);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetByIdAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 5));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Workspace?)null);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var act = () => _sut.GetByIdAsync(5, 1);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace not found.");
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        SetupValidUpdate();
        _memberRepo.Setup(r => r.GetAsync(3, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 3, WorkspaceId = 10,
                Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
            });

        var act = () => _sut.UpdateAsync(10, 3, new UpdateWorkspaceRequest("New Name"));

        await act.Should().ThrowAsync<AppException>();
        _workspaceRepo.Verify(r => r.UpdateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        SetupValidUpdate();
        _memberRepo.Setup(r => r.GetAsync(2, 7, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(2, 7, "manage_ws_settings"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Workspace?)null);

        var act = () => _sut.UpdateAsync(7, 2, new UpdateWorkspaceRequest("Renamed"));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_EnqueuesAuditEventWithOldAndNewName()
    {
        var workspace = new Workspace { Id = 7, Name = "Old Name", IsArchived = false };

        SetupValidUpdate();
        _memberRepo.Setup(r => r.GetAsync(2, 7, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(2, 7, "manage_ws_settings"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.UpdateAsync(7, 2, new UpdateWorkspaceRequest("New Name"));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_updated" &&
                    e.FieldName == "name" &&
                    e.OldValueJson!.Contains("Old Name") &&
                    e.NewValueJson!.Contains("New Name")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── ArchiveAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_AdminArchivesWorkspace_SetsIsArchivedTrue()
    {
        var workspace = new Workspace { Id = 3, Name = "Odesa Pipeline", IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 3));
        _workspaceRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.ArchiveAsync(3, 1);

        workspace.IsArchived.Should().BeTrue();
        _workspaceRepo.Verify(r => r.UpdateAsync(workspace, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_AdminArchivesWorkspace_EnqueuesAuditEvent()
    {
        var workspace = new Workspace { Id = 3, Name = "Odesa Pipeline", IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 3));
        _workspaceRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.ArchiveAsync(3, 1);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_archived" &&
                    e.TargetId == 3 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_NonAdminRole_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(5, 3, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(5, 3, "manage_ws_settings"));

        var act = () => _sut.ArchiveAsync(3, 5);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Only workspace admins or organization owners can archive a workspace.");
        _workspaceRepo.Verify(r => r.UpdateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_OrganizationOwner_NotWorkspaceMember_ArchivesWorkspace()
    {
        var workspace = new Workspace { Id = 81, OrganizationId = 501, Name = "Owner Bypass", IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(600, 81, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);
        _workspaceRepo.Setup(r => r.GetByIdAsync(81, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo
            .Setup(r => r.GetAsync(600, 501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 600,
                OrganizationId = 501,
                Role = new OrganizationRole { Name = WorkspaceAccessEvaluator.OrgOwnerRoleName }
            });

        await _sut.ArchiveAsync(81, 600);

        workspace.IsArchived.Should().BeTrue();
        _workspaceRepo.Verify(r => r.UpdateAsync(workspace, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByUserAsync_WithOrgFilter_UserNotMemberOfOrg_ThrowsForbiddenAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(7, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.GetByUserAsync(7, organizationId: 3);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("*not a member of this organization*");
        _workspaceRepo.Verify(r => r.GetByUserIdAndOrganizationIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByUserAsync_WithOrgFilter_ReturnsMemberCountAndUserRolePerWorkspace()
    {
        var orgMember = OrgMemberWithPermission(1, 2, "view_workspaces");
        var ws = new Workspace { Id = 10, OrganizationId = 2, Name = "Sales" };

        _orgMemberRepo.Setup(r => r.GetAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(orgMember);
        _workspaceRepo
            .Setup(r => r.GetByUserIdAndOrganizationIdAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ws]);
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 10));
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AdminMember(1, 10), AdminMember(2, 10)]);

        var result = await _sut.GetByUserAsync(1, organizationId: 2);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(10);
        result[0].MemberCount.Should().Be(2);
        result[0].UserRole.Should().Be("ws_admin");
    }

    [Fact]
    public async Task GetByUserAsync_WithoutOrgFilter_ReturnsAllUserWorkspacesAcrossOrgs()
    {
        var ws1 = new Workspace { Id = 1, OrganizationId = 10, Name = "Alpha" };
        var ws2 = new Workspace { Id = 2, OrganizationId = 20, Name = "Beta" };

        _workspaceRepo.Setup(r => r.GetByUserIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([ws1, ws2]);
        _memberRepo.Setup(r => r.GetAsync(5, 1, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(5, 1));
        _memberRepo.Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(5, 2));
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([AdminMember(5, 1)]);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([AdminMember(5, 2), AdminMember(6, 2)]);

        var result = await _sut.GetByUserAsync(5, organizationId: null);

        result.Should().HaveCount(2);
        result.Should().Contain(w => w.Id == 1 && w.MemberCount == 1);
        result.Should().Contain(w => w.Id == 2 && w.MemberCount == 2);
    }

    [Fact]
    public async Task GetByIdAsync_ValidMember_ReturnsDtoWithMemberCountAndRole()
    {
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Sales", IsArchived = false };
        var membership = AdminMember(1, 10);

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AdminMember(1, 10), AdminMember(2, 10), AdminMember(3, 10)]);

        var result = await _sut.GetByIdAsync(10, 1);

        result.Id.Should().Be(10);
        result.Name.Should().Be("Sales");
        result.MemberCount.Should().Be(3);
        result.UserRole.Should().Be("ws_admin");
    }

    // ── GetSettingsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_UserNotMember_ThrowsForbiddenAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(99, 10, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.GetSettingsAsync(10, 99);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 10));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceSettings?)null);

        var act = () => _sut.GetSettingsAsync(10, 1);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace settings not found.");
    }

    [Fact]
    public async Task GetSettingsAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 10));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m });
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((Workspace?)null);

        var act = () => _sut.GetSettingsAsync(10, 1);

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task GetSettingsAsync_ValidMember_ReturnsMappedSettingsDto()
    {
        var settings = new WorkspaceSettings
        {
            Id = 1,
            WorkspaceId = 10,
            HighRiskThreshold = 0.8m,
            MediumRiskThreshold = 0.5m,
            RiskScoringEnabled = true,
            Description = "Sales workspace"
        };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Sales WS" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 10));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        var result = await _sut.GetSettingsAsync(10, 1);

        result.WorkspaceId.Should().Be(10);
        result.Name.Should().Be("Sales WS");
        result.HighRiskThreshold.Should().Be(0.8m);
        result.MediumRiskThreshold.Should().Be(0.5m);
        result.RiskScoringEnabled.Should().BeTrue();
        result.Description.Should().Be("Sales workspace");
    }

    [Fact]
    public async Task GetSettingsAsync_ValidMember_EnqueuesAuditReadEvent()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Sales WS" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMember(1, 10));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.GetSettingsAsync(10, 1);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_settings_read" &&
                    e.TargetId == 10 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── UpdateSettingsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSettingsAsync_UserLacksPermission_ThrowsForbiddenAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 1, WorkspaceId = 10,
                Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
            });

        var act = () => _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("Name", null, 0.7m, 0.4m, false));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage($"*{WorkspacePermissions.ManageWsSettings}*");
        _workspaceSettingsRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSettingsAsync_SettingsNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceSettings?)null);

        var act = () => _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("Name", null, 0.7m, 0.4m, false));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace settings not found.");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m });
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((Workspace?)null);

        var act = () => _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("Name", null, 0.7m, 0.4m, false));

        await act.Should().ThrowAsync<AppException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidationFails_ThrowsValidationExceptionBeforeAnyRepoCall()
    {
        _updateSettingsValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceSettingsRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(new[] { new ValidationFailure("HighRiskThreshold", "Invalid.") }));

        var act = () => _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("Name", null, 1.5m, 0.4m, false));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _workspaceSettingsRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidRequest_AppliesAllFieldChanges()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m, RiskScoringEnabled = false };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Old Name" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("New Name", "Desc", 0.9m, 0.6m, true));

        workspace.Name.Should().Be("New Name");
        settings.Description.Should().Be("Desc");
        settings.HighRiskThreshold.Should().Be(0.9m);
        settings.MediumRiskThreshold.Should().Be(0.6m);
        settings.RiskScoringEnabled.Should().BeTrue();
        _workspaceRepo.Verify(r => r.UpdateAsync(workspace, It.IsAny<CancellationToken>()), Times.Once);
        _workspaceSettingsRepo.Verify(r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidRequest_EnqueuesAuditEventWithOldAndNewValues()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m, RiskScoringEnabled = false };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Old Name" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("New Name", null, 0.9m, 0.6m, true));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_settings_updated" &&
                    e.TargetId == 10 &&
                    e.ActorUserId == 1 &&
                    e.OldValueJson!.Contains("Old Name") &&
                    e.NewValueJson!.Contains("New Name")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidRequest_EnqueuesDomainSettingsUpdatedEvent()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "WS Name" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        await _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("WS Name", null, 0.7m, 0.4m, false));

        _auditOutboxWriter.Verify(
            x => x.EnqueueDomainAsync(
                It.IsAny<string>(),
                It.Is<DomainMessageEnvelope>(e => e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettingsAsync_NullAuditWriter_PersistsChangesWithoutEnqueue()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m, RiskScoringEnabled = false };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "Old Name" };

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);

        var sut = new WorkspaceService(
            _workspaceRepo.Object,
            _memberRepo.Object,
            _roleRepo.Object,
            _orgMemberRepo.Object,
            _workspaceAccessEvaluator,
            _workspaceSettingsRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _updateSettingsValidator.Object,
            null);

        await sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("New Name", "Desc", 0.9m, 0.6m, true));

        workspace.Name.Should().Be("New Name");
        settings.HighRiskThreshold.Should().Be(0.9m);
        _workspaceSettingsRepo.Verify(r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(x => x.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidRequest_DomainPayloadCarriesWorkspaceOrgAndActor()
    {
        var settings = new WorkspaceSettings { Id = 1, WorkspaceId = 10, HighRiskThreshold = 0.7m, MediumRiskThreshold = 0.4m };
        var workspace = new Workspace { Id = 10, OrganizationId = 2, Name = "WS" };
        DomainMessageEnvelope? captured = null;

        _memberRepo.Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 10, WorkspacePermissions.ManageWsSettings));
        _workspaceSettingsRepo.Setup(r => r.GetByWorkspaceIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        _workspaceRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _auditOutboxWriter
            .Setup(x => x.EnqueueDomainAsync(It.IsAny<string>(), It.IsAny<DomainMessageEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<string, DomainMessageEnvelope, CancellationToken>((_, env, _) => captured = env);

        await _sut.UpdateSettingsAsync(10, 1, new UpdateWorkspaceSettingsRequest("WS", null, 0.7m, 0.4m, false));

        captured.Should().NotBeNull();
        captured!.PayloadTypeName.Should().Be(DomainPayloadTypes.WorkspaceSettingsUpdatedV1);
        var payload = JsonSerializer.Deserialize<WorkspaceSettingsUpdatedPayloadV1>(captured.PayloadJson);
        payload!.WorkspaceId.Should().Be(10);
        payload.OrganizationId.Should().Be(2);
        payload.ActorUserId.Should().Be(1);
    }
}
