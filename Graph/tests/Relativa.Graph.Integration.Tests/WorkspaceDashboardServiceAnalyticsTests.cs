using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Caching.Memory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Relativa.Graph.Dashboard;
using Relativa.Graph.Data;
using Relativa.Graph.ML;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class WorkspaceDashboardServiceAnalyticsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("ws_analytics_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private WorkspaceDashboardService _svc = null!;
    private int _managerId, _wsId, _dealTypeId, _clientTypeId, _relTypeId;
    private readonly Dictionary<string, int> _prop = new();
    private readonly DateOnly _thisMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 12);
    private int _d1, _d2, _d3, _d4, _c1;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GraphQueryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        var ml = Substitute.For<IMlScoringClient>();
        var scores = new Dictionary<int, MlScoreDto>
        {
            [_d1] = new(_d1, 0.25, null, null),
            [_d2] = new(_d2, 0.60, null, null),
            [_d3] = new(_d3, 0.95, null, null),
        };
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((IReadOnlyList<int>)ci[0]).Where(scores.ContainsKey).ToDictionary(id => id, id => scores[id]));
        _svc = new WorkspaceDashboardService(
            _db,
            ml,
            Substitute.For<IMlRecalculationClient>(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        var org = new Organization { Name = "Org" };
        _db.Organizations.Add(org);
        var manager = new User { FirstName = "M", LastName = "Manager", Email = "m@a.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(manager);
        await _db.SaveChangesAsync();
        _managerId = manager.Id;

        var pAnalytics = new Permission { Name = "view_analytics" };
        var pTeam = new Permission { Name = "view_team_analytics" };
        _db.Permissions.AddRange(pAnalytics, pTeam);
        await _db.SaveChangesAsync();
        var role = new WorkspaceRole { Name = "ws_manager", Priority = 2 };
        _db.WorkspaceRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.WorkspaceRolePermissions.AddRange(
            new WorkspaceRolePermission { WsRoleId = role.Id, PermissionId = pAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = role.Id, PermissionId = pTeam.Id });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = manager.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        var clientType = new EntityType { Name = "client" };
        _db.EntityTypes.AddRange(dealType, clientType);
        await _db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _clientTypeId = clientType.Id;

        foreach (var (name, type) in new[]
        {
            ("status", PropertyDataType.String), ("deal_value", PropertyDataType.Decimal),
            ("deal_stage", PropertyDataType.String), ("expected_close", PropertyDataType.Date),
            ("title", PropertyDataType.String), ("client_status", PropertyDataType.String),
            ("company_name", PropertyDataType.String), ("client_lifetime_value", PropertyDataType.Decimal),
            ("industry", PropertyDataType.String),
        })
        {
            var p = new Property { Name = name, DataType = type };
            _db.Properties.Add(p);
            await _db.SaveChangesAsync();
            _prop[name] = p.Id;
        }

        var relType = new EntityRelationshipType
        {
            Name = "deal_client", SourceEntityTypeId = dealType.Id, TargetEntityTypeId = clientType.Id,
            RelationshipCardinality = RelationshipCardinality.ManyToOne,
        };
        _db.EntityRelationshipTypes.Add(relType);
        await _db.SaveChangesAsync();
        _relTypeId = relType.Id;

        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = manager.Id, WorkspaceId = ws.Id, WsRoleId = role.Id, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        _d1 = await AddDealAsync("opened", 40000m, "Negotiation", _thisMonth, "A");
        _d2 = await AddDealAsync("pending", 25000m, "Proposal", _thisMonth, "B");
        _d3 = await AddDealAsync("opened", 8000m, "Qualification", _thisMonth, "C");
        _d4 = await AddDealAsync("closed", 15000m, "Negotiation", _thisMonth, "D");
        _c1 = await AddClientAsync("Globex", 90000m, "active");
        await LinkAsync(_d1, _c1);
    }

    private async Task<int> AddDealAsync(string status, decimal value, string stage, DateOnly close, string title)
    {
        var e = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["status"], ValueString = status },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["deal_value"], ValueDecimal = value },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["deal_stage"], ValueString = stage },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["expected_close"], ValueDate = close },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["title"], ValueString = title });
        await _db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<int> AddClientAsync(string company, decimal ltv, string status)
    {
        var e = new Entity { EntityTypeId = _clientTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["company_name"], ValueString = company },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["client_lifetime_value"], ValueDecimal = ltv },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["client_status"], ValueString = status },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["industry"], ValueString = "Tech" });
        await _db.SaveChangesAsync();
        return e.Id;
    }

    private async Task LinkAsync(int dealId, int clientId)
    {
        _db.EntityRelationships.Add(new EntityRelationship { SourceEntityId = dealId, TargetEntityId = clientId, RelationshipTypeId = _relTypeId });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRiskDistributionAsync_ProducesThreePopulatedBuckets()
    {
        var risk = await _svc.GetRiskDistributionAsync(_managerId, _wsId, CancellationToken.None);

        risk.Distribution["high"].Count.Should().Be(1, "the 0.25 active deal is high risk");
        risk.Distribution["medium"].Count.Should().Be(1, "the 0.60 active deal is medium risk");
        risk.Distribution["low"].Count.Should().Be(1, "the 0.95 active deal is low risk");
        risk.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPipelineAsync_GroupsStagesAndStatuses()
    {
        var pipeline = await _svc.GetPipelineAsync(_managerId, _wsId, CancellationToken.None);

        pipeline.Stages.Single(s => s.Name == "Negotiation").Count.Should().Be(2);
        pipeline.StatusBreakdown["opened"].Should().Be(2);
        pipeline.StatusBreakdown["closed"].Should().Be(1);
    }

    [Fact]
    public async Task GetTopEntitiesAsync_RanksDealsAndClients()
    {
        var top = await _svc.GetTopEntitiesAsync(_managerId, _wsId, CancellationToken.None);

        top.TopDeals.First().Value.Should().Be(40000m);
        top.TopClients.First().Name.Should().Be("Globex");
    }

    [Fact]
    public async Task GetMemberActivityAsync_ManagerWithTeamAnalytics_ReturnsMembers()
    {
        var activity = await _svc.GetMemberActivityAsync(_managerId, _wsId, CancellationToken.None);

        activity.Should().ContainSingle(m => m.UserId == _managerId);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesFinancialsWithRealDeals()
    {
        var summary = await _svc.GetSummaryAsync(_managerId, _wsId, CancellationToken.None);

        summary.AccessLevel.Should().Be("full");
        summary.WonDeals.Should().Be(1);
        summary.TotalPipelineValue.Should().Be(88000m);
    }
}
