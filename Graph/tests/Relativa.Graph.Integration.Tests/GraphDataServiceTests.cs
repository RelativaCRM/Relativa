using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Relativa.Graph.Data;
using Relativa.Graph.Graph;
using Relativa.Graph.ML;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relativa_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public string ConnectionString { get; private set; } = null!;
    public int OrgId { get; private set; }
    public int OrgRoleId { get; private set; }
    public int WsRoleId { get; private set; }
    public int DealEntityTypeId { get; private set; }
    public int ClientEntityTypeId { get; private set; }
    public int DealClientRelTypeId { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        await SeedBackgroundAsync(db);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public GraphQueryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GraphQueryDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    private async Task SeedBackgroundAsync(GraphQueryDbContext db)
    {
        var org     = new Organization { Name = "Test Org", IsArchived = false };
        var orgRole = new OrganizationRole { Name = "org_member", Priority = 5, IsArchived = false };
        var perm    = new Permission { Name = "view_entities", IsArchived = false };
        var wsRole  = new WorkspaceRole { Name = "member", WorkspaceId = null, Priority = 5, IsArchived = false };
        var deal    = new EntityType { Name = "deal" };
        var client  = new EntityType { Name = "client" };

        db.Organizations.Add(org);
        db.OrganizationRoles.Add(orgRole);
        db.Permissions.Add(perm);
        db.WorkspaceRoles.Add(wsRole);
        db.EntityTypes.AddRange(deal, client);
        await db.SaveChangesAsync();

        db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission
        {
            WsRoleId = wsRole.Id, PermissionId = perm.Id
        });
        db.EntityRelationshipTypes.Add(new EntityRelationshipType
        {
            Name = "deal_client",
            SourceEntityTypeId = deal.Id,
            TargetEntityTypeId = client.Id,
            IsRequired = false
        });
        await db.SaveChangesAsync();

        OrgId             = org.Id;
        OrgRoleId         = orgRole.Id;
        WsRoleId          = wsRole.Id;
        DealEntityTypeId  = deal.Id;
        ClientEntityTypeId = client.Id;
        DealClientRelTypeId = db.EntityRelationshipTypes.Single(r => r.Name == "deal_client").Id;
    }
}

[CollectionDefinition("GraphDataService")]
public sealed class GraphDataServiceCollection : ICollectionFixture<GraphDatabaseFixture> { }

[Collection("GraphDataService")]
public sealed class GraphDataServiceTests : IAsyncLifetime
{
    private readonly GraphDatabaseFixture _fixture;
    private GraphQueryDbContext _db = null!;
    private IDbContextTransaction _tx = null!;

    public GraphDataServiceTests(GraphDatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        _tx = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _db.DisposeAsync();
    }

    private async Task<(int UserId, int WorkspaceId)> CreateUserWithWorkspaceAsync()
    {
        var uid  = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            FirstName = uid, LastName = "Test",
            Email = $"{uid}@t.com",
            Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var ws = new Workspace
        {
            Name = $"WS-{uid}", IsArchived = false,
            CreatedByUserId = user.Id, OrganizationId = _fixture.OrgId
        };
        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync();

        _db.UserRoleOrganizations.Add(new UserRoleOrganization
        {
            UserId = user.Id, OrganizationId = _fixture.OrgId, OrgRoleId = _fixture.OrgRoleId,
            JoinedAt = DateTime.UtcNow, IsArchived = false
        });
        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace
        {
            UserId = user.Id, WorkspaceId = ws.Id, WsRoleId = _fixture.WsRoleId,
            JoinedAt = DateTime.UtcNow, IsArchived = false
        });
        await _db.SaveChangesAsync();

        return (user.Id, ws.Id);
    }

    private static IMlScoringClient EmptyMl()
    {
        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>());
        return ml;
    }

    [Fact]
    public async Task BuildGraphAsync_UnknownUser_DoesNotThrowAndReturnsEmptyGraph()
    {
        var svc = new GraphDataService(_db, EmptyMl());

        var act = () => svc.BuildGraphAsync(999999, _fixture.OrgId, null, CancellationToken.None);

        var assertion = await act.Should().NotThrowAsync(
            "an unknown userId is a valid input — the SUT must return empty, not throw");
        assertion.Which.Nodes.Should().BeEmpty();
        assertion.Which.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGraphAsync_UserWithNoEntities_ReturnsExactlyUserAndWorkspaceNodes()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Should().HaveCount(2, "exactly one user_self node and one workspace node");
        result.Nodes.Should().ContainSingle(n => n.Type == "user_self" && n.ResourceId == userId);
        result.Nodes.Should().ContainSingle(n => n.Type == "workspace"  && n.ResourceId == wsId);
        result.Edges.Should().HaveCount(1);
        result.Edges.Single().Type.Should().Be("user_workspace");
    }

    [Fact]
    public async Task BuildGraphAsync_ArchivedEntity_IsExcludedFromGraph()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var archived = new Entity
        {
            EntityTypeId = _fixture.ClientEntityTypeId, CreatedByUserId = userId, IsArchived = true
        };
        _db.Entities.Add(archived);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.Add(new EntityWorkspace { EntityId = archived.Id, WorkspaceId = wsId });
        await _db.SaveChangesAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Should().NotContain(
            n => n.ResourceType == "entity" && n.ResourceId == archived.Id,
            "archived entities must never appear in the graph regardless of workspace membership");
    }

    [Theory]
    [InlineData("high",   32.9, true,  "32.9 is just below the 33.0 lower bound of medium — qualifies as high risk")]
    [InlineData("high",   33.0, false, "33.0 is the inclusive lower bound of medium — not high risk")]
    [InlineData("medium", 33.0, true,  "33.0 is the inclusive lower bound of medium risk")]
    [InlineData("medium", 32.9, false, "32.9 falls below medium's lower bound of 33.0")]
    [InlineData("medium", 66.9, true,  "66.9 is just below the 67.0 upper bound — still medium")]
    [InlineData("medium", 67.0, false, "67.0 is the inclusive lower bound of low — exits medium")]
    [InlineData("low",    67.0, true,  "67.0 is the exact inclusive lower bound of low risk")]
    [InlineData("low",    66.9, false, "66.9 is below the 67.0 threshold — not low risk")]
    [InlineData(null,     50.0, true,  "no filter applied — deal always included regardless of score")]
    public async Task BuildGraphAsync_RiskLevelFilter_IncludesOrExcludesDealAtThresholdBoundary(
        string? riskLevel, double closureScore, bool expectDealIncluded, string reason)
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var deal = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        _db.Entities.Add(deal);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.Add(new EntityWorkspace { EntityId = deal.Id, WorkspaceId = wsId });
        await _db.SaveChangesAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>
            {
                [deal.Id] = new MlScoreDto(deal.Id, closureScore, 0.0, null)
            });

        var result = await new GraphDataService(_db, ml)
            .BuildGraphAsync(userId, _fixture.OrgId, riskLevel, CancellationToken.None);

        result.Nodes.Any(n => n.ResourceType == "entity" && n.ResourceId == deal.Id)
            .Should().Be(expectDealIncluded, reason);
    }

    [Fact]
    public async Task BuildGraphAsync_MlClientThrows_ReturnsDealNodesWithoutHighlightTags()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var deal1 = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        var deal2 = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        _db.Entities.AddRange(deal1, deal2);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.AddRange(
            new EntityWorkspace { EntityId = deal1.Id, WorkspaceId = wsId },
            new EntityWorkspace { EntityId = deal2.Id, WorkspaceId = wsId });
        await _db.SaveChangesAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<int, MlScoreDto>>(
                new HttpRequestException("ML service unavailable")));

        var result = await new GraphDataService(_db, ml)
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        var entityNodes = result.Nodes.Where(n => n.ResourceType == "entity").ToList();
        entityNodes.Should().HaveCount(2, "both deals must appear even when ML is unavailable");
        entityNodes.Should().AllSatisfy(n =>
            n.HighlightTag.Should().BeNull("no scores available to classify when ML service fails"));
    }

    [Fact]
    public async Task BuildGraphAsync_MlClientThrowsWithNonNullRiskFilter_ExcludesAllDeals()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var deal = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        _db.Entities.Add(deal);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.Add(new EntityWorkspace { EntityId = deal.Id, WorkspaceId = wsId });
        await _db.SaveChangesAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<int, MlScoreDto>>(
                new HttpRequestException("ML service unavailable")));

        var result = await new GraphDataService(_db, ml)
            .BuildGraphAsync(userId, _fixture.OrgId, "high", CancellationToken.None);

        result.Nodes.Should().NotContain(
            n => n.ResourceType == "entity",
            "empty ML scores against a non-null risk filter removes all deals from the graph");
    }

    [Fact]
    public async Task BuildGraphAsync_TwoDealsWithScores_AssignsBestAndWorstTagsWithWorkspaceEdges()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var bestDeal  = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        var worstDeal = new Entity { EntityTypeId = _fixture.DealEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        _db.Entities.AddRange(bestDeal, worstDeal);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.AddRange(
            new EntityWorkspace { EntityId = bestDeal.Id,  WorkspaceId = wsId },
            new EntityWorkspace { EntityId = worstDeal.Id, WorkspaceId = wsId });
        await _db.SaveChangesAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>
            {
                [bestDeal.Id]  = new MlScoreDto(bestDeal.Id,  90.0, 5.0,  null),
                [worstDeal.Id] = new MlScoreDto(worstDeal.Id, 10.0, 80.0, null)
            });

        var result = await new GraphDataService(_db, ml)
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Single(n => n.ResourceType == "entity" && n.ResourceId == bestDeal.Id)
            .HighlightTag.Should().Be("best_deal");
        result.Nodes.Single(n => n.ResourceType == "entity" && n.ResourceId == worstDeal.Id)
            .HighlightTag.Should().Be("worst_deal");

        var wsEdges = result.Edges.Where(e => e.Type == "workspace_entity").ToList();
        wsEdges.Should().HaveCount(2, "each deal must have exactly one workspace→entity edge");
        wsEdges.Should().ContainSingle(e => e.To == $"entity:{bestDeal.Id}");
        wsEdges.Should().ContainSingle(e => e.To == $"entity:{worstDeal.Id}");
    }

    [Fact]
    public async Task BuildGraphAsync_DealLinkedToClient_ProducesEntityEntityEdgeAndBothEntityNodes()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var deal   = new Entity { EntityTypeId = _fixture.DealEntityTypeId,   CreatedByUserId = userId, IsArchived = false };
        var client = new Entity { EntityTypeId = _fixture.ClientEntityTypeId, CreatedByUserId = userId, IsArchived = false };
        _db.Entities.AddRange(deal, client);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.AddRange(
            new EntityWorkspace { EntityId = deal.Id,   WorkspaceId = wsId },
            new EntityWorkspace { EntityId = client.Id, WorkspaceId = wsId });
        _db.EntityRelationships.Add(new EntityRelationship
        {
            SourceEntityId = deal.Id, TargetEntityId = client.Id,
            RelationshipTypeId = _fixture.DealClientRelTypeId
        });
        await _db.SaveChangesAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Should().Contain(n => n.ResourceType == "entity" && n.ResourceId == deal.Id,
            "deal node must be present for the edge to be meaningful");
        result.Nodes.Should().Contain(n => n.ResourceType == "entity" && n.ResourceId == client.Id,
            "client node must be present for the edge to be meaningful");

        var entityEntityEdges = result.Edges.Where(e => e.Type == "entity_entity").ToList();
        entityEntityEdges.Should().HaveCount(1, "exactly one deal→client relationship edge");
        entityEntityEdges.Single().From.Should().Be($"entity:{deal.Id}");
        entityEntityEdges.Single().To.Should().Be($"entity:{client.Id}");
    }

    [Fact]
    public async Task BuildGraphAsync_UserInMultipleWorkspaces_ReturnsOneNodeAndOneEdgePerWorkspace()
    {
        var uid  = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            FirstName = uid, LastName = "Multi",
            Email = $"{uid}@t.com",
            Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var wsA = new Workspace { Name = $"WS-A-{uid}", IsArchived = false, CreatedByUserId = user.Id, OrganizationId = _fixture.OrgId };
        var wsB = new Workspace { Name = $"WS-B-{uid}", IsArchived = false, CreatedByUserId = user.Id, OrganizationId = _fixture.OrgId };
        _db.Workspaces.AddRange(wsA, wsB);
        await _db.SaveChangesAsync();

        _db.UserRoleOrganizations.Add(new UserRoleOrganization
        {
            UserId = user.Id, OrganizationId = _fixture.OrgId, OrgRoleId = _fixture.OrgRoleId,
            JoinedAt = DateTime.UtcNow, IsArchived = false
        });
        _db.UserRoleWorkspaces.AddRange(
            new UserRoleWorkspace { UserId = user.Id, WorkspaceId = wsA.Id, WsRoleId = _fixture.WsRoleId, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleWorkspace { UserId = user.Id, WorkspaceId = wsB.Id, WsRoleId = _fixture.WsRoleId, JoinedAt = DateTime.UtcNow, IsArchived = false });
        await _db.SaveChangesAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(user.Id, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Should().HaveCount(3, "one user_self node plus one node per workspace");
        result.Nodes.Should().ContainSingle(n => n.Type == "user_self");
        result.Nodes.Should().ContainSingle(n => n.Type == "workspace" && n.ResourceId == wsA.Id);
        result.Nodes.Should().ContainSingle(n => n.Type == "workspace" && n.ResourceId == wsB.Id);

        var wsEdges = result.Edges.Where(e => e.Type == "user_workspace").ToList();
        wsEdges.Should().HaveCount(2, "one edge per workspace, no duplicates");
        wsEdges.Should().ContainSingle(e => e.To == $"workspace:{wsA.Id}");
        wsEdges.Should().ContainSingle(e => e.To == $"workspace:{wsB.Id}");
    }

    [Fact]
    public async Task BuildGraphAsync_OrgMemberManager_IncludesPeersWithEditDeleteAndExcludesArchivedUsers()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var focal = new User { FirstName = uid, LastName = "Admin", Email = uid + "@t.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        _db.Users.Add(focal);
        await _db.SaveChangesAsync();
        var userId = focal.Id;

        var perms = new[] { "remove_org_members", "edit_other_org_users_profile", "delete_org_users" }
            .Select(n => new Permission { Name = n }).ToArray();
        _db.Permissions.AddRange(perms);
        var adminRole = new OrganizationRole { Name = "org_admin_" + Guid.NewGuid().ToString("N")[..6], Priority = 1 };
        _db.OrganizationRoles.Add(adminRole);
        await _db.SaveChangesAsync();
        foreach (var p in perms)
            _db.Set<OrganizationRolePermission>().Add(new OrganizationRolePermission { OrgRoleId = adminRole.Id, PermissionId = p.Id });
        _db.UserRoleOrganizations.Add(new UserRoleOrganization
        {
            UserId = userId, OrganizationId = _fixture.OrgId, OrgRoleId = adminRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false
        });

        var peer  = new User { FirstName = "Peer",  LastName = "Visible",  Email = Guid.NewGuid().ToString("N")[..8] + "@t.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var ghost = new User { FirstName = "Ghost", LastName = "Archived", Email = Guid.NewGuid().ToString("N")[..8] + "@t.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = true };
        _db.Users.AddRange(peer, ghost);
        await _db.SaveChangesAsync();
        _db.UserRoleOrganizations.AddRange(
            new UserRoleOrganization { UserId = peer.Id,  OrganizationId = _fixture.OrgId, OrgRoleId = _fixture.OrgRoleId, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleOrganization { UserId = ghost.Id, OrganizationId = _fixture.OrgId, OrgRoleId = _fixture.OrgRoleId, JoinedAt = DateTime.UtcNow, IsArchived = false });
        await _db.SaveChangesAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        var peerNode = result.Nodes.SingleOrDefault(n => n.Type == "user" && n.ResourceId == peer.Id);
        peerNode.Should().NotBeNull("a member-manager sees other org members as graph nodes");
        peerNode!.Permissions.Should().Contain(new[] { "view", "edit", "delete" },
            "edit_other_org_users_profile and delete_org_users surface as edit/delete on peer nodes");
        result.Nodes.Should().NotContain(n => n.ResourceId == ghost.Id,
            "an archived user is filtered out even when their membership row is active");
        result.Edges.Should().ContainSingle(e => e.Type == "user_user" && e.To == $"user:{peer.Id}");
    }

    [Fact]
    public async Task BuildGraphAsync_FullWorkspacePermissionsAndLabelledEntity_SetsManageEditDeleteAndResolvesLabel()
    {
        var uid  = Guid.NewGuid().ToString("N")[..8];
        var user = new User { FirstName = uid, LastName = "Perms", Email = uid + "@t.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var ws = new Workspace { Name = "WS-" + uid, IsArchived = false, CreatedByUserId = user.Id, OrganizationId = _fixture.OrgId };
        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync();

        var viewPerm = await _db.Permissions.FirstAsync(p => p.Name == "view_entities");
        var extraPerms = new[] { "manage_ws_settings", "edit_entities", "delete_entities" }
            .Select(n => new Permission { Name = n }).ToArray();
        _db.Permissions.AddRange(extraPerms);
        var wsRole = new WorkspaceRole { Name = "ws_admin_" + uid, Priority = 1, IsArchived = false };
        _db.WorkspaceRoles.Add(wsRole);
        await _db.SaveChangesAsync();
        foreach (var p in extraPerms.Append(viewPerm))
            _db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission { WsRoleId = wsRole.Id, PermissionId = p.Id });
        _db.UserRoleOrganizations.Add(new UserRoleOrganization { UserId = user.Id, OrganizationId = _fixture.OrgId, OrgRoleId = _fixture.OrgRoleId, JoinedAt = DateTime.UtcNow, IsArchived = false });
        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = user.Id, WorkspaceId = ws.Id, WsRoleId = wsRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false });
        await _db.SaveChangesAsync();

        var nameProp = new Property { Name = "name", DataType = PropertyDataType.String };
        _db.Properties.Add(nameProp);
        await _db.SaveChangesAsync();
        var client = new Entity { EntityTypeId = _fixture.ClientEntityTypeId, CreatedByUserId = user.Id, IsArchived = false };
        _db.Entities.Add(client);
        await _db.SaveChangesAsync();
        _db.EntityWorkspaces.Add(new EntityWorkspace { EntityId = client.Id, WorkspaceId = ws.Id });
        _db.EntityPropertyValues.Add(new EntityPropertyValue { EntityId = client.Id, PropertyId = nameProp.Id, ValueString = "Acme Corp" });
        await _db.SaveChangesAsync();

        var result = await new GraphDataService(_db, EmptyMl())
            .BuildGraphAsync(user.Id, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Single(n => n.Type == "workspace" && n.ResourceId == ws.Id)
            .Permissions.Should().Contain("manage", "manage_ws_settings elevates the workspace node to manageable");
        var entityNode = result.Nodes.Single(n => n.ResourceType == "entity" && n.ResourceId == client.Id);
        entityNode.Permissions.Should().Contain(new[] { "view", "edit", "delete" });
        entityNode.Label.Should().Be("Acme Corp", "the 'name' property resolves the node label via priority order");
    }

    [Fact]
    public async Task BuildGraphAsync_ClientsWithLinkedScoredDeals_AssignBestAndWorstClientHighlights()
    {
        var (userId, wsId) = await CreateUserWithWorkspaceAsync();

        var ltvProp = new Property { Name = "client_lifetime_value", DataType = PropertyDataType.Decimal };
        _db.Properties.Add(ltvProp);
        await _db.SaveChangesAsync();

        async Task<(int DealId, int ClientId)> AddPairAsync(decimal ltv)
        {
            var deal   = new Entity { EntityTypeId = _fixture.DealEntityTypeId,   CreatedByUserId = userId, IsArchived = false };
            var client = new Entity { EntityTypeId = _fixture.ClientEntityTypeId, CreatedByUserId = userId, IsArchived = false };
            _db.Entities.AddRange(deal, client);
            await _db.SaveChangesAsync();
            _db.EntityWorkspaces.AddRange(
                new EntityWorkspace { EntityId = deal.Id,   WorkspaceId = wsId },
                new EntityWorkspace { EntityId = client.Id, WorkspaceId = wsId });
            _db.EntityRelationships.Add(new EntityRelationship { SourceEntityId = deal.Id, TargetEntityId = client.Id, RelationshipTypeId = _fixture.DealClientRelTypeId });
            _db.EntityPropertyValues.Add(new EntityPropertyValue { EntityId = client.Id, PropertyId = ltvProp.Id, ValueDecimal = ltv });
            await _db.SaveChangesAsync();
            return (deal.Id, client.Id);
        }

        var (goodDeal, goodClient) = await AddPairAsync(500000m);
        var (badDeal,  badClient)  = await AddPairAsync(1000m);

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>
            {
                [goodDeal] = new MlScoreDto(goodDeal, 90.0,  5.0, null),
                [badDeal]  = new MlScoreDto(badDeal,  10.0, 80.0, null),
            });

        var result = await new GraphDataService(_db, ml)
            .BuildGraphAsync(userId, _fixture.OrgId, null, CancellationToken.None);

        result.Nodes.Single(n => n.ResourceType == "entity" && n.ResourceId == goodClient)
            .HighlightTag.Should().Be("best_client",
            "high closure, low churn and large LTV yield the top composite client score");
        result.Nodes.Single(n => n.ResourceType == "entity" && n.ResourceId == badClient)
            .HighlightTag.Should().Be("worst_client");
    }
}
