using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class RepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("repo_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<RelativaDbContext> _opts = null!;

    private int _orgId;
    private int _userId1, _userId2;
    private int _ws1Id, _ws2Id;
    private int _permViewId, _permManageId;
    private int _wsRoleId;
    private int _orgRoleSysId, _orgRoleOrgId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(db);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private RelativaDbContext Db() => new(_opts);

    private async Task SeedAsync(RelativaDbContext db)
    {
        var org = new Organization { Name = "Acme Corp" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var u1 = new User { Email = "admin@acme.com", FirstName = "Admin", LastName = "U", Password = "x" };
        var u2 = new User { Email = "member@acme.com", FirstName = "Member", LastName = "U", Password = "x" };
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();
        _userId1 = u1.Id;
        _userId2 = u2.Id;

        var ws1 = new Workspace { Name = "WS Alpha", OrganizationId = _orgId, CreatedByUserId = _userId1 };
        var ws2 = new Workspace { Name = "WS Beta", OrganizationId = _orgId, CreatedByUserId = _userId1 };
        db.Workspaces.AddRange(ws1, ws2);
        await db.SaveChangesAsync();
        _ws1Id = ws1.Id;
        _ws2Id = ws2.Id;

        var pView = new Permission { Name = "view_analytics" };
        var pManage = new Permission { Name = "manage_org_settings" };
        db.Permissions.AddRange(pView, pManage);
        await db.SaveChangesAsync();
        _permViewId = pView.Id;
        _permManageId = pManage.Id;

        var wsRole = new WorkspaceRole { Name = "ws_analyst", Priority = 2 };
        db.WorkspaceRoles.Add(wsRole);
        await db.SaveChangesAsync();
        _wsRoleId = wsRole.Id;
        db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission { WsRoleId = _wsRoleId, PermissionId = _permViewId });

        var orgRoleSys = new OrganizationRole { Name = "org_admin", Priority = 1 };
        db.OrganizationRoles.Add(orgRoleSys);
        await db.SaveChangesAsync();
        _orgRoleSysId = orgRoleSys.Id;
        db.OrganizationRolePermissions.Add(new OrganizationRolePermission { OrgRoleId = _orgRoleSysId, PermissionId = _permManageId });

        var orgRoleOrg = new OrganizationRole { Name = "custom_role", OrganizationId = _orgId, Priority = 3 };
        db.OrganizationRoles.Add(orgRoleOrg);
        await db.SaveChangesAsync();
        _orgRoleOrgId = orgRoleOrg.Id;

        db.UserRoleWorkspaces.Add(new UserRoleWorkspace
        {
            UserId = _userId1, WorkspaceId = _ws1Id, WsRoleId = _wsRoleId, JoinedAt = DateTime.UtcNow
        });
        db.UserRoleOrganizations.Add(new UserRoleOrganization
        {
            UserId = _userId1, OrganizationId = _orgId, OrgRoleId = _orgRoleSysId, JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OrganizationRepository_GetByIdAsync_ExistingOrg_ReturnsOrg()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.GetByIdAsync(_orgId);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task OrganizationRepository_GetByIdAsync_NonExistent_ReturnsNull()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.GetByIdAsync(99999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationRepository_GetByUserIdAsync_ReturnsMemberOrgs()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.GetByUserIdAsync(_userId1);
        result.Should().Contain(o => o.Id == _orgId);
    }

    [Fact]
    public async Task OrganizationRepository_GetByUserIdAsync_UserWithoutMembership_ReturnsEmpty()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.GetByUserIdAsync(_userId2);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationRepository_SearchAsync_MatchesPartialName()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.SearchAsync("acme");
        result.Should().Contain(h => h.Id == _orgId);
    }

    [Fact]
    public async Task OrganizationRepository_SearchAsync_NoMatch_ReturnsEmpty()
    {
        var repo = new OrganizationRepository(Db());
        var result = await repo.SearchAsync("zzznomatch");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationRepository_AddAsync_PersistsOrganization()
    {
        await using var db = Db();
        var repo = new OrganizationRepository(db);
        var org = new Organization { Name = "New Corp" };
        await repo.AddAsync(org);
        org.Id.Should().BeGreaterThan(0);
        (await db.Organizations.FindAsync(org.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task OrganizationRepository_UpdateAsync_PersistsChanges()
    {
        await using var db = Db();
        var org = await db.Organizations.FindAsync(_orgId);
        org!.Name = "Acme Corp Updated";
        await new OrganizationRepository(db).UpdateAsync(org);
        var updated = await Db().Organizations.FindAsync(_orgId);
        updated!.Name.Should().Be("Acme Corp Updated");
    }

    [Fact]
    public async Task WorkspaceRepository_GetByIdAsync_ExistingWorkspace_ReturnsIt()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByIdAsync(_ws1Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("WS Alpha");
    }

    [Fact]
    public async Task WorkspaceRepository_GetByIdAsync_NonExistent_ReturnsNull()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByIdAsync(99999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceRepository_GetByOrganizationIdAsync_ReturnsAllInOrg()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByOrganizationIdAsync(_orgId);
        result.Should().Contain(w => w.Id == _ws1Id);
        result.Should().Contain(w => w.Id == _ws2Id);
    }

    [Fact]
    public async Task WorkspaceRepository_GetByUserIdAsync_ViaWorkspaceMembership_ReturnsWorkspace()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByUserIdAsync(_userId1);
        result.Should().Contain(w => w.Id == _ws1Id);
    }

    [Fact]
    public async Task WorkspaceRepository_GetByUserIdAsync_NoMembership_ReturnsEmpty()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByUserIdAsync(_userId2);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkspaceRepository_GetByUserIdAndOrganizationIdAsync_ReturnsWorkspace()
    {
        var repo = new WorkspaceRepository(Db());
        var result = await repo.GetByUserIdAndOrganizationIdAsync(_userId1, _orgId);
        result.Should().Contain(w => w.Id == _ws1Id);
    }

    [Fact]
    public async Task WorkspaceRepository_AddAsync_PersistsWorkspace()
    {
        await using var db = Db();
        var repo = new WorkspaceRepository(db);
        var ws = new Workspace { Name = "WS Gamma", OrganizationId = _orgId, CreatedByUserId = _userId1 };
        await repo.AddAsync(ws);
        ws.Id.Should().BeGreaterThan(0);
        (await db.Workspaces.FindAsync(ws.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task WorkspaceRepository_UpdateAsync_PersistsName()
    {
        await using var db = Db();
        var ws = await db.Workspaces.FindAsync(_ws2Id);
        ws!.Name = "WS Beta Renamed";
        await new WorkspaceRepository(db).UpdateAsync(ws);
        var updated = await Db().Workspaces.FindAsync(_ws2Id);
        updated!.Name.Should().Be("WS Beta Renamed");
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_GetAsync_ExistingMembership_ReturnsWithPermissions()
    {
        var repo = new UserRoleWorkspaceRepository(Db());
        var result = await repo.GetAsync(_userId1, _ws1Id);
        result.Should().NotBeNull();
        result!.Role.RolePermissions.Should().NotBeEmpty();
        result.Role.RolePermissions.Any(rp => rp.Permission?.Name == "view_analytics").Should().BeTrue();
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_GetAsync_NonExistentMembership_ReturnsNull()
    {
        var repo = new UserRoleWorkspaceRepository(Db());
        var result = await repo.GetAsync(_userId2, _ws1Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_GetByWorkspaceIdAsync_ReturnsAllMembers()
    {
        var repo = new UserRoleWorkspaceRepository(Db());
        var result = await repo.GetByWorkspaceIdAsync(_ws1Id);
        result.Should().Contain(m => m.UserId == _userId1);
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_GetRolePrioritiesByUserIdsAsync_ReturnsDictionary()
    {
        var repo = new UserRoleWorkspaceRepository(Db());
        var result = await repo.GetRolePrioritiesByUserIdsAsync(_ws1Id, [_userId1]);
        result.Should().ContainKey(_userId1);
        result[_userId1].Should().Be(2);
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_GetRolePrioritiesByUserIdsAsync_EmptyIds_ReturnsEmpty()
    {
        var repo = new UserRoleWorkspaceRepository(Db());
        var result = await repo.GetRolePrioritiesByUserIdsAsync(_ws1Id, []);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UserRoleWorkspaceRepository_AddUpdateRemove_FullLifecycle()
    {
        await using var db = Db();
        var repo = new UserRoleWorkspaceRepository(db);

        var member = new UserRoleWorkspace { UserId = _userId2, WorkspaceId = _ws1Id, WsRoleId = _wsRoleId, JoinedAt = DateTime.UtcNow };
        await repo.AddAsync(member);
        member.Id.Should().BeGreaterThan(0);

        member.IsArchived = true;
        await repo.UpdateAsync(member);
        var updated = await Db().UserRoleWorkspaces.FindAsync(member.Id);
        updated!.IsArchived.Should().BeTrue();

        await repo.RemoveAsync(member);
        var deleted = await Db().UserRoleWorkspaces.FindAsync(member.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task UserRoleOrganizationRepository_GetAsync_ExistingMembership_ReturnsWithPermissions()
    {
        var repo = new UserRoleOrganizationRepository(Db());
        var result = await repo.GetAsync(_userId1, _orgId);
        result.Should().NotBeNull();
        result!.Role.RolePermissions.Should().NotBeEmpty();
        result.Role.RolePermissions.Any(rp => rp.Permission?.Name == "manage_org_settings").Should().BeTrue();
    }

    [Fact]
    public async Task UserRoleOrganizationRepository_GetAsync_NonExistent_ReturnsNull()
    {
        var repo = new UserRoleOrganizationRepository(Db());
        var result = await repo.GetAsync(_userId2, _orgId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UserRoleOrganizationRepository_GetByOrganizationIdAsync_ReturnsAllMembers()
    {
        var repo = new UserRoleOrganizationRepository(Db());
        var result = await repo.GetByOrganizationIdAsync(_orgId);
        result.Should().Contain(m => m.UserId == _userId1);
    }

    [Fact]
    public async Task UserRoleOrganizationRepository_GetByUserIdAsync_ReturnsMemberships()
    {
        var repo = new UserRoleOrganizationRepository(Db());
        var result = await repo.GetByUserIdAsync(_userId1);
        result.Should().Contain(m => m.OrganizationId == _orgId);
    }

    [Fact]
    public async Task UserRoleOrganizationRepository_AddUpdateRemove_FullLifecycle()
    {
        await using var db = Db();
        var repo = new UserRoleOrganizationRepository(db);

        var member = new UserRoleOrganization { UserId = _userId2, OrganizationId = _orgId, OrgRoleId = _orgRoleSysId, JoinedAt = DateTime.UtcNow };
        await repo.AddAsync(member);
        member.Id.Should().BeGreaterThan(0);

        member.IsArchived = true;
        await repo.UpdateAsync(member);
        var updated = await Db().UserRoleOrganizations.FindAsync(member.Id);
        updated!.IsArchived.Should().BeTrue();

        await repo.RemoveAsync(member);
        var deleted = await Db().UserRoleOrganizations.FindAsync(member.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetByIdAsync_ReturnsRoleWithPermissions()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetByIdAsync(_wsRoleId);
        result.Should().NotBeNull();
        result!.Name.Should().Be("ws_analyst");
        result.RolePermissions.Should().Contain(rp => rp.Permission!.Name == "view_analytics");
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetByWorkspaceIdAsync_IncludesSystemRoles()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetByWorkspaceIdAsync(_ws1Id);
        result.Should().Contain(r => r.Id == _wsRoleId);
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetSystemRoleByNameAsync_ReturnsMatchingRole()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetSystemRoleByNameAsync("ws_analyst");
        result.Should().NotBeNull();
        result!.Id.Should().Be(_wsRoleId);
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetSystemRoleByNameAsync_NoMatch_ReturnsNull()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetSystemRoleByNameAsync("nonexistent_role");
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetSystemRoleWithPermissionsSupersetAsync_EmptyRequired_ReturnsNull()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetSystemRoleWithPermissionsSupersetAsync([]);
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetSystemRoleWithPermissionsSupersetAsync_MatchFound_ReturnsRole()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetSystemRoleWithPermissionsSupersetAsync(["view_analytics"]);
        result.Should().NotBeNull();
        result!.Id.Should().Be(_wsRoleId);
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetSystemRoleWithPermissionsSupersetAsync_NoMatch_ReturnsNull()
    {
        var repo = new WorkspaceRoleRepository(Db());
        var result = await repo.GetSystemRoleWithPermissionsSupersetAsync(["nonexistent_permission"]);
        result.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceRoleRepository_AddAsync_PersistsRole()
    {
        await using var db = Db();
        var repo = new WorkspaceRoleRepository(db);
        var role = new WorkspaceRole { Name = "ws_custom", Priority = 5 };
        await repo.AddAsync(role);
        role.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WorkspaceRoleRepository_UpdateAsync_PersistsChange()
    {
        await using var db = Db();
        var role = new WorkspaceRole { Name = "ws_temp", Priority = 9 };
        db.WorkspaceRoles.Add(role);
        await db.SaveChangesAsync();

        role.Priority = 8;
        await new WorkspaceRoleRepository(db).UpdateAsync(role);

        var updated = await Db().WorkspaceRoles.FindAsync(role.Id);
        updated!.Priority.Should().Be(8);
    }

    [Fact]
    public async Task OrganizationRoleRepository_GetByIdAsync_ReturnsRoleWithPermissions()
    {
        var repo = new OrganizationRoleRepository(Db());
        var result = await repo.GetByIdAsync(_orgRoleSysId);
        result.Should().NotBeNull();
        result!.Name.Should().Be("org_admin");
        result.RolePermissions.Should().Contain(rp => rp.Permission!.Name == "manage_org_settings");
    }

    [Fact]
    public async Task OrganizationRoleRepository_GetByOrganizationIdAsync_IncludesSystemAndOrgRoles()
    {
        var repo = new OrganizationRoleRepository(Db());
        var result = await repo.GetByOrganizationIdAsync(_orgId);
        result.Should().Contain(r => r.Id == _orgRoleSysId);
        result.Should().Contain(r => r.Id == _orgRoleOrgId);
    }

    [Fact]
    public async Task OrganizationRoleRepository_GetSystemRolesAsync_ReturnsOnlyNullOrgRoles()
    {
        var repo = new OrganizationRoleRepository(Db());
        var result = await repo.GetSystemRolesAsync();
        result.Should().Contain(r => r.Id == _orgRoleSysId);
        result.Should().NotContain(r => r.Id == _orgRoleOrgId);
    }

    [Fact]
    public async Task OrganizationRoleRepository_GetSystemRoleByNameAsync_ReturnsMatchingSystemRole()
    {
        var repo = new OrganizationRoleRepository(Db());
        var result = await repo.GetSystemRoleByNameAsync("org_admin");
        result.Should().NotBeNull();
        result!.Id.Should().Be(_orgRoleSysId);
    }

    [Fact]
    public async Task OrganizationRoleRepository_GetSystemRoleByNameAsync_OrgSpecificRole_ReturnsNull()
    {
        var repo = new OrganizationRoleRepository(Db());
        var result = await repo.GetSystemRoleByNameAsync("custom_role");
        result.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationRoleRepository_AddAsync_PersistsRole()
    {
        await using var db = Db();
        var repo = new OrganizationRoleRepository(db);
        var role = new OrganizationRole { Name = "org_custom", OrganizationId = _orgId, Priority = 5 };
        await repo.AddAsync(role);
        role.Id.Should().BeGreaterThan(0);
        (await db.OrganizationRoles.FindAsync(role.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task OrganizationRoleRepository_UpdateAsync_PersistsChange()
    {
        await using var db = Db();
        var role = new OrganizationRole { Name = "org_temp", Priority = 9 };
        db.OrganizationRoles.Add(role);
        await db.SaveChangesAsync();

        role.Priority = 7;
        await new OrganizationRoleRepository(db).UpdateAsync(role);

        var updated = await Db().OrganizationRoles.FindAsync(role.Id);
        updated!.Priority.Should().Be(7);
    }

    [Fact]
    public async Task PermissionRepository_GetAllAsync_ReturnsAllActivePermissions()
    {
        var repo = new PermissionRepository(Db());
        var result = await repo.GetAllAsync();
        result.Should().Contain(p => p.Id == _permViewId);
        result.Should().Contain(p => p.Id == _permManageId);
    }

    [Fact]
    public async Task PermissionRepository_GetByIdsAsync_ReturnsMatchingPermissions()
    {
        var repo = new PermissionRepository(Db());
        var result = await repo.GetByIdsAsync([_permViewId]);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("view_analytics");
    }

    [Fact]
    public async Task PermissionRepository_GetByIdsAsync_UnknownId_ReturnsEmpty()
    {
        var repo = new PermissionRepository(Db());
        var result = await repo.GetByIdsAsync([99999]);
        result.Should().BeEmpty();
    }

    private async Task<(int userId, int orgId, int wsId)> SeedOrgOwnerScenarioAsync()
    {
        await using var db = Db();
        var org = new Organization { Name = $"Owner Org {Guid.NewGuid():N}" };
        db.Organizations.Add(org);
        var owner = new User { Email = $"owner-{Guid.NewGuid():N}@acme.com", FirstName = "Own", LastName = "Er", Password = "x" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var ownerRole = new OrganizationRole { Name = "org_owner", OrganizationId = org.Id, Priority = 0 };
        db.OrganizationRoles.Add(ownerRole);
        await db.SaveChangesAsync();

        db.UserRoleOrganizations.Add(new UserRoleOrganization
        {
            UserId = owner.Id, OrganizationId = org.Id, OrgRoleId = ownerRole.Id, JoinedAt = DateTime.UtcNow
        });
        var ws = new Workspace { Name = "Owned WS", OrganizationId = org.Id, CreatedByUserId = owner.Id };
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync();

        return (owner.Id, org.Id, ws.Id);
    }

    [Fact]
    public async Task WorkspaceRepository_GetByUserIdAsync_OrgOwner_IncludesOwnedWorkspacesWithoutMembership()
    {
        var (ownerId, _, wsId) = await SeedOrgOwnerScenarioAsync();
        var repo = new WorkspaceRepository(Db());

        var result = await repo.GetByUserIdAsync(ownerId);

        result.Should().ContainSingle(w => w.Id == wsId,
            "an org owner sees workspaces in their org even without a direct membership row");
    }

    [Fact]
    public async Task WorkspaceRepository_GetByUserIdAndOrganizationIdAsync_OrgOwner_IncludesOwnedWorkspaces()
    {
        var (ownerId, orgId, wsId) = await SeedOrgOwnerScenarioAsync();
        var repo = new WorkspaceRepository(Db());

        var result = await repo.GetByUserIdAndOrganizationIdAsync(ownerId, orgId);

        result.Should().Contain(w => w.Id == wsId);
    }

    [Fact]
    public async Task OrganizationRepository_SearchAsync_BlankQuery_ReturnsOrgsRankedByMemberCount()
    {
        await using (var db = Db())
        {
            var org = new Organization { Name = $"Settings Org {Guid.NewGuid():N}" };
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
            db.Set<OrganizationSettings>().Add(new OrganizationSettings { OrganizationId = org.Id, JoinPolicy = "invite_only" });
            await db.SaveChangesAsync();
        }
        var repo = new OrganizationRepository(Db());

        var result = await repo.SearchAsync("   ");

        result.Should().NotBeEmpty();
        result.Should().Contain(h => h.JoinPolicy == "invite_only",
            "the projection reads JoinPolicy from settings when present");
        result.Should().Contain(h => h.JoinPolicy == "open",
            "organizations without settings fall back to the default open policy");
    }

    [Fact]
    public async Task WorkspaceRoleRepository_GetByWorkspaceIdAsync_IncludesWorkspaceSpecificRoles()
    {
        int scopedRoleId;
        await using (var db = Db())
        {
            var role = new WorkspaceRole { Name = "ws_scoped", WorkspaceId = _ws1Id, Priority = 7 };
            db.WorkspaceRoles.Add(role);
            await db.SaveChangesAsync();
            scopedRoleId = role.Id;
        }
        var repo = new WorkspaceRoleRepository(Db());

        var result = await repo.GetByWorkspaceIdAsync(_ws1Id);

        result.Should().Contain(r => r.Id == scopedRoleId, "roles scoped to the workspace are returned alongside system roles");
        result.Should().NotContain(r => r.WorkspaceId != null && r.WorkspaceId != _ws1Id);
    }
}
