using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Audit.Application.Exceptions;
using Relativa.Audit.Application.Validators;
using Relativa.Audit.Infrastructure.Data;
using Relativa.Audit.Infrastructure.Services;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Audit.Integration.Tests;

public sealed class AuditLogReadRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("audit_rbac_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<AuditDbContext> _opts = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _opts = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new AuditDbContext(_opts);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE entity_audit_log DROP CONSTRAINT IF EXISTS fk_entity_audit_log_entities;
            ALTER TABLE entity_audit_log DROP CONSTRAINT IF EXISTS fk_entity_audit_log_users;
            ALTER TABLE workspace_audit_log DROP CONSTRAINT IF EXISTS fk_workspace_audit_log_workspaces;
            ALTER TABLE workspace_audit_log DROP CONSTRAINT IF EXISTS fk_workspace_audit_log_users;
            ALTER TABLE organization_audit_log DROP CONSTRAINT IF EXISTS fk_organization_audit_log_organizations;
            ALTER TABLE organization_audit_log DROP CONSTRAINT IF EXISTS fk_organization_audit_log_users;
            ALTER TABLE user_audit_log DROP CONSTRAINT IF EXISTS fk_user_audit_log_target_users;
            ALTER TABLE user_audit_log DROP CONSTRAINT IF EXISTS fk_user_audit_log_users;
        ");

        await SeedBaseDataAsync(db);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuditLogReadRepository Sut() => new(new AuditDbContext(_opts));

    private AuditDbContext Db() => new(_opts);

    private static async Task SeedBaseDataAsync(AuditDbContext db)
    {
        db.Set<Organization>().Add(new Organization { Id = 1, Name = "Test Org" });
        db.Set<User>().AddRange(
            new User { Id = 1, Email = "admin@test.com", FirstName = "Admin", LastName = "U", Password = "x" },
            new User { Id = 2, Email = "target@test.com", FirstName = "Target", LastName = "U", Password = "x" },
            new User { Id = 3, Email = "noperm@test.com", FirstName = "NoPerm", LastName = "U", Password = "x" },
            new User { Id = 10, Email = "isolated@test.com", FirstName = "Isolated", LastName = "U", Password = "x" });
        db.Set<Workspace>().Add(new Workspace { Id = 1, Name = "WS", OrganizationId = 1, CreatedByUserId = 1 });
        db.Set<Permission>().AddRange(
            new Permission { Id = 1, Name = "view_analytics" },
            new Permission { Id = 2, Name = "manage_org_settings" });
        db.Set<WorkspaceRole>().AddRange(
            new WorkspaceRole { Id = 1, Name = "analyst", Priority = 1 },
            new WorkspaceRole { Id = 2, Name = "member", Priority = 2 });
        db.Set<WorkspaceRolePermission>().Add(
            new WorkspaceRolePermission { Id = 1, WsRoleId = 1, PermissionId = 1 });
        db.Set<OrganizationRole>().AddRange(
            new OrganizationRole { Id = 1, Name = "org_admin", OrganizationId = 1, Priority = 1 },
            new OrganizationRole { Id = 2, Name = "org_member", OrganizationId = 1, Priority = 2 });
        db.Set<OrganizationRolePermission>().Add(
            new OrganizationRolePermission { Id = 1, OrgRoleId = 1, PermissionId = 2 });
        db.Set<UserRoleWorkspace>().AddRange(
            new UserRoleWorkspace { Id = 1, UserId = 1, WorkspaceId = 1, WsRoleId = 1, JoinedAt = DateTime.UtcNow },
            new UserRoleWorkspace { Id = 2, UserId = 3, WorkspaceId = 1, WsRoleId = 2, JoinedAt = DateTime.UtcNow });
        db.Set<UserRoleOrganization>().AddRange(
            new UserRoleOrganization { Id = 1, UserId = 1, OrganizationId = 1, OrgRoleId = 1, JoinedAt = DateTime.UtcNow },
            new UserRoleOrganization { Id = 2, UserId = 3, OrganizationId = 1, OrgRoleId = 2, JoinedAt = DateTime.UtcNow });
        db.Set<EntityType>().Add(new EntityType { Id = 1, Name = "Client" });
        db.Set<Entity>().AddRange(
            new Entity { Id = 1, EntityTypeId = 1, CreatedByUserId = 1 },
            new Entity { Id = 2, EntityTypeId = 1, CreatedByUserId = 1 });
        db.Set<EntityWorkspace>().Add(new EntityWorkspace { Id = 1, EntityId = 1, WorkspaceId = 1 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EnsureRbacAsync_EntityScope_UserWithViewAnalytics_DoesNotThrow()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 1, "entity", workspaceId: 1, organizationId: null, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureRbacAsync_EntityScope_UserWithoutViewAnalytics_ThrowsForbidden()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 3, "entity", workspaceId: 1, organizationId: null, CancellationToken.None);
        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task EnsureRbacAsync_WorkspaceScope_UserWithViewAnalytics_DoesNotThrow()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 1, "workspace", workspaceId: 1, organizationId: null, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureRbacAsync_OrgScope_UserWithManageOrgSettings_DoesNotThrow()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 1, "organization", workspaceId: null, organizationId: 1, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureRbacAsync_OrgScope_UserWithoutManageOrgSettings_ThrowsForbidden()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 3, "organization", workspaceId: null, organizationId: 1, CancellationToken.None);
        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task EnsureRbacAsync_UserScope_AlwaysAllowed()
    {
        var act = () => Sut().EnsureRbacAsync(callerUserId: 99, "user", workspaceId: null, organizationId: null, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_WorkspaceMissing_ThrowsKeyNotFound()
    {
        var q = new GetAuditLogQuery("workspace", null, null, null, 0, 10, null, null, 9999, null, null, null);
        var act = () => Sut().EnsureResourcesExistAsync(q, "workspace", CancellationToken.None);
        await act.Should().ThrowAsync<AppException>().WithMessage("*9999*");
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_EntityNotLinkedToWorkspace_ThrowsKeyNotFound()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 0, 10, 2, null, 1, null, null, null);
        var act = () => Sut().EnsureResourcesExistAsync(q, "entity", CancellationToken.None);
        await act.Should().ThrowAsync<AppException>().WithMessage("*not linked*");
    }

    [Fact]
    public async Task GetWorkspaceScopeAsync_ReturnsOnlyLogsForRequestedWorkspace()
    {
        var wsIdA = 100;
        var wsIdB = 101;
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.WorkspaceAuditLogs.AddRange(
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "created", WorkspaceId = wsIdA, ChangedAt = now },
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "created", WorkspaceId = wsIdA, ChangedAt = now },
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "created", WorkspaceId = wsIdB, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetWorkspaceScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1), null, null,
            wsIdA, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(2);
        result.Data.Should().AllSatisfy(x => x.EntityTypeCategory.Should().Be("workspace"));
    }

    [Fact]
    public async Task GetWorkspaceScopeAsync_ActionFilter_NarrowsResults()
    {
        var wsId = 102;
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.WorkspaceAuditLogs.AddRange(
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "created", WorkspaceId = wsId, ChangedAt = now },
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "updated", WorkspaceId = wsId, ChangedAt = now },
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "updated", WorkspaceId = wsId, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetWorkspaceScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1), "created", null,
            wsId, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetUserScopeAsync_TargetUserOutsideVisibleSet_ThrowsForbidden()
    {
        var act = () => Sut().GetUserScopeAsync(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1),
            null, null, targetUserIdFilter: 2,
            callerUserId: 10, 0, 10, 0, null, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task GetUserScopeAsync_NoTargetFilter_ReturnsOnlyVisibleLogs()
    {
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.UserAuditLogs.AddRange(
                new UserAuditLog { Id = Guid.NewGuid(), Action = "login", TargetUserId = 10, ChangedAt = now },
                new UserAuditLog { Id = Guid.NewGuid(), Action = "login", TargetUserId = 2, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetUserScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            null, null, null,
            callerUserId: 10, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].TargetUser!.Id.Should().Be(10);
    }

    [Fact]
    public async Task GetEntityScopeAsync_ReturnsOnlyLogsLinkedToWorkspace()
    {
        var action = $"ws_isolation_{Guid.NewGuid():N}";
        var now    = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.EntityAuditLogs.AddRange(
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 1, ChangedAt = now },
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 2, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].Entity!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetEntityScopeAsync_ActionFilter_NarrowsResults()
    {
        var actionA = $"ent_scopeA_{Guid.NewGuid():N}";
        var actionB = $"ent_scopeB_{Guid.NewGuid():N}";
        var now     = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.EntityAuditLogs.AddRange(
                new EntityAuditLog { Id = Guid.NewGuid(), Action = actionA, EntityId = 1, ChangedAt = now },
                new EntityAuditLog { Id = Guid.NewGuid(), Action = actionB, EntityId = 1, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            actionA, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].Action.Should().Be(actionA);
    }

    [Fact]
    public async Task GetOrganizationScopeAsync_ReturnsOnlyLogsForOrg()
    {
        var now    = DateTimeOffset.UtcNow;
        var orgIdA = 200;
        var orgIdB = 201;

        await using (var db = Db())
        {
            db.OrganizationAuditLogs.AddRange(
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_created", OrganizationId = orgIdA, ChangedAt = now },
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_created", OrganizationId = orgIdA, ChangedAt = now },
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_created", OrganizationId = orgIdB, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetOrganizationScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            null, null, orgIdA, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetOrganizationScopeAsync_ActorFilter_NarrowsResults()
    {
        var now   = DateTimeOffset.UtcNow;
        var orgId = 202;

        await using (var db = Db())
        {
            db.OrganizationAuditLogs.AddRange(
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_updated", OrganizationId = orgId, ChangedById = 1, ChangedAt = now },
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_updated", OrganizationId = orgId, ChangedById = 2, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetOrganizationScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            null, actorUserId: 1, orgId, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task BuildFilterContextAsync_WorkspaceScope_ReturnsWorkspaceAndOrgNames()
    {
        var q = new GetAuditLogQuery("workspace", null, null, null, 1, 10, null, null, 1, null, null, null);

        var result = await Sut().BuildFilterContextAsync(q, "workspace", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Workspace.Should().NotBeNull();
        result.Workspace!.Name.Should().Be("WS");
        result.Workspace.OrganizationName.Should().Be("Test Org");
    }

    [Fact]
    public async Task BuildFilterContextAsync_OrgScope_ReturnsOrgName()
    {
        var q = new GetAuditLogQuery("organization", null, null, null, 1, 10, null, null, null, 1, null, null);

        var result = await Sut().BuildFilterContextAsync(q, "organization", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Organization.Should().NotBeNull();
        result.Organization!.Name.Should().Be("Test Org");
    }

    [Fact]
    public async Task BuildFilterContextAsync_EntityScope_ReturnsWorkspaceContext()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 1, 10, null, null, 1, null, null, null);

        var result = await Sut().BuildFilterContextAsync(q, "entity", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Workspace.Should().NotBeNull();
        result.Workspace!.Name.Should().Be("WS");
    }

    [Fact]
    public async Task BuildFilterContextAsync_UserScope_ReturnsNull()
    {
        var q = new GetAuditLogQuery("user", null, null, null, 1, 10, null, null, null, null, null, 2);

        var result = await Sut().BuildFilterContextAsync(q, "user", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_AllResourcesExist_DoesNotThrow()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 0, 10, 1, null, 1, 1, null, 2);
        var act = () => Sut().EnsureResourcesExistAsync(q, "entity", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_OrganizationMissing_Throws()
    {
        var q = new GetAuditLogQuery("organization", null, null, null, 0, 10, null, null, null, 9999, null, null);
        var act = () => Sut().EnsureResourcesExistAsync(q, "organization", CancellationToken.None);
        await act.Should().ThrowAsync<AppException>().WithMessage("*organization*");
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_EntityMissing_Throws()
    {
        var q = new GetAuditLogQuery("user", null, null, null, 0, 10, 9999, null, null, null, null, null);
        var act = () => Sut().EnsureResourcesExistAsync(q, "user", CancellationToken.None);
        await act.Should().ThrowAsync<AppException>().WithMessage("*entity*");
    }

    [Fact]
    public async Task EnsureResourcesExistAsync_TargetUserMissing_Throws()
    {
        var q = new GetAuditLogQuery("user", null, null, null, 0, 10, null, null, null, null, null, 9999);
        var act = () => Sut().EnsureResourcesExistAsync(q, "user", CancellationToken.None);
        await act.Should().ThrowAsync<AppException>().WithMessage("*9999*");
    }

    [Fact]
    public async Task GetWorkspaceScopeAsync_ActorFilter_NarrowsResults()
    {
        var wsId = 103;
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.WorkspaceAuditLogs.AddRange(
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "updated", WorkspaceId = wsId, ChangedById = 1, ChangedAt = now },
                new WorkspaceAuditLog { Id = Guid.NewGuid(), Action = "updated", WorkspaceId = wsId, ChangedById = 2, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetWorkspaceScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1), null, actorUserId: 1,
            wsId, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetOrganizationScopeAsync_ActionFilter_NarrowsResults()
    {
        var orgId = 203;
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.OrganizationAuditLogs.AddRange(
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_created", OrganizationId = orgId, ChangedAt = now },
                new OrganizationAuditLog { Id = Guid.NewGuid(), Action = "org_updated", OrganizationId = orgId, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetOrganizationScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1), "org_created", null,
            orgId, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetUserScopeAsync_TargetActionAndActorFilters_NarrowResults()
    {
        var now = DateTimeOffset.UtcNow;
        var action = $"user_filter_{Guid.NewGuid():N}";

        await using (var db = Db())
        {
            db.UserAuditLogs.AddRange(
                new UserAuditLog { Id = Guid.NewGuid(), Action = action, TargetUserId = 10, ChangedById = 10, ChangedAt = now },
                new UserAuditLog { Id = Guid.NewGuid(), Action = action, TargetUserId = 10, ChangedById = 2, ChangedAt = now },
                new UserAuditLog { Id = Guid.NewGuid(), Action = "other", TargetUserId = 10, ChangedById = 10, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetUserScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, actorUserId: 10, targetUserIdFilter: 10,
            callerUserId: 10, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetEntityScopeAsync_EntityIdDomainTypeAndActorFilters_Narrow()
    {
        var action = $"ent_multi_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.EntityAuditLogs.AddRange(
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 1, EntityType = "1", ChangedById = 1, ChangedAt = now },
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 1, EntityType = "1", ChangedById = 2, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, entityId: 1, domainEntityType: "1", actorUserId: 1,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].Entity!.EntityTypeName.Should().Be("Client");
    }

    [Fact]
    public async Task GetEntityScopeAsync_DeletedEntity_MapsEntityDeletedAndParsesTypeFromEvent()
    {
        var action = $"ent_deleted_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE entity_workspace DROP CONSTRAINT IF EXISTS fk_ew_entity;");
            db.Set<EntityWorkspace>().Add(new EntityWorkspace { Id = 500, EntityId = 500, WorkspaceId = 1 });
            db.EntityAuditLogs.Add(
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 500, EntityType = "1", ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].EntityDeleted.Should().BeTrue();
        result.Data[0].Entity!.Id.Should().Be(500);
    }

    [Fact]
    public async Task GetEntityScopeAsync_WithPropertyDefinitions_BuildsPropertyChanges()
    {
        var action = $"ent_props_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.Set<Property>().Add(new Property { Id = 600, Name = "amount", DataType = PropertyDataType.Int });
            db.Set<EntityTypeProperty>().Add(new EntityTypeProperty { EntityTypeId = 1, PropertyId = 600 });
            db.EntityAuditLogs.Add(new EntityAuditLog
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityId = 1,
                EntityType = "1",
                ChangedAt = now,
                OldValue = JsonDocument.Parse("[{\"propertyId\":600,\"value\":\"10\"}]"),
                NewValue = JsonDocument.Parse("[{\"propertyId\":600,\"value\":\"20\"}]"),
            });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        var change = result.Data[0].PropertyChanges.Should().ContainSingle().Subject;
        change.PropertyId.Should().Be(600);
        change.OldValue.Should().Be("10");
        change.NewValue.Should().Be("20");
    }

    [Fact]
    public async Task GetEntityScopeAsync_MalformedPropertyJson_YieldsNoPropertyChanges()
    {
        var action = $"ent_badjson_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.Set<Property>().Add(new Property { Id = 601, Name = "score", DataType = PropertyDataType.Int });
            db.Set<EntityTypeProperty>().Add(new EntityTypeProperty { EntityTypeId = 1, PropertyId = 601 });
            db.EntityAuditLogs.Add(new EntityAuditLog
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityId = 1,
                EntityType = "1",
                ChangedAt = now,
                NewValue = JsonDocument.Parse("{\"notAnArray\":true}"),
            });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].PropertyChanges.Should().BeNull();
    }

    [Fact]
    public async Task GetEntityScopeAsync_ActorIdWithoutUserRow_MapsActorWithIdOnly()
    {
        var action = $"ent_actor_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = Db())
        {
            db.EntityAuditLogs.Add(
                new EntityAuditLog { Id = Guid.NewGuid(), Action = action, EntityId = 1, EntityType = "1", ChangedById = 9999, ChangedAt = now });
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetEntityScopeAsync(
            now.AddMinutes(-1), now.AddMinutes(1),
            action, null, null, null,
            workspaceId: 1, 0, 10, 0, null, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Data[0].Actor.Should().NotBeNull();
        result.Data[0].Actor!.UserId.Should().Be(9999);
        result.Data[0].Actor!.Email.Should().BeNull();
    }
}
