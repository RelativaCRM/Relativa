using DotNet.Testcontainers.Builders;
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

public sealed class WorkspaceDashboardServiceEdgeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("ws_dashboard_edge_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private WorkspaceDashboardService _svc = null!;
    private int _managerId, _basicId, _wsId, _dealTypeId, _clientTypeId, _relTypeId;
    private readonly Dictionary<string, int> _prop = new();
    private readonly DateOnly _thisMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 10);
    private int _dScored, _dClosed, _cName, _cNoName;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GraphQueryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        var ml = Substitute.For<IMlScoringClient>();
        var scores = new Dictionary<int, MlScoreDto> { [_dScored] = new(_dScored, 0.30, null, null), [_dClosed] = new(_dClosed, 0.90, null, null) };
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((IReadOnlyList<int>)ci[0]).Where(scores.ContainsKey).ToDictionary(id => id, id => scores[id]));
        _svc = new WorkspaceDashboardService(_db, ml, Substitute.For<IMlRecalculationClient>());
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _postgres.DisposeAsync(); }

    private async Task SeedAsync()
    {
        var org = new Organization { Name = "Org" };
        _db.Organizations.Add(org);
        var manager = new User { FirstName = "M", LastName = "Manager", Email = "m@e.com", Password = "x", CreatedAt = DateTime.UtcNow };
        var basic = new User { FirstName = "B", LastName = "Basic", Email = "b@e.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.AddRange(manager, basic);
        await _db.SaveChangesAsync();
        _managerId = manager.Id; _basicId = basic.Id;

        var pAnalytics = new Permission { Name = "view_analytics" };
        var pTeam = new Permission { Name = "view_team_analytics" };
        var pBasic = new Permission { Name = "view_basic_stats" };
        _db.Permissions.AddRange(pAnalytics, pTeam, pBasic);
        await _db.SaveChangesAsync();
        var mgrRole = new WorkspaceRole { Name = "manager", Priority = 2 };
        var viewerRole = new WorkspaceRole { Name = "viewer", Priority = 4 };
        _db.WorkspaceRoles.AddRange(mgrRole, viewerRole);
        await _db.SaveChangesAsync();
        _db.WorkspaceRolePermissions.AddRange(
            new WorkspaceRolePermission { WsRoleId = mgrRole.Id, PermissionId = pAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = mgrRole.Id, PermissionId = pTeam.Id },
            new WorkspaceRolePermission { WsRoleId = viewerRole.Id, PermissionId = pBasic.Id });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = manager.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        var clientType = new EntityType { Name = "client" };
        _db.EntityTypes.AddRange(dealType, clientType);
        await _db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _clientTypeId = clientType.Id;

        _db.UserRoleWorkspaces.AddRange(
            new UserRoleWorkspace { UserId = manager.Id, WorkspaceId = ws.Id, WsRoleId = mgrRole.Id, JoinedAt = DateTime.UtcNow },
            new UserRoleWorkspace { UserId = basic.Id, WorkspaceId = ws.Id, WsRoleId = viewerRole.Id, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        foreach (var (name, type) in new[]
        {
            ("status", PropertyDataType.String), ("deal_value", PropertyDataType.Decimal), ("deal_stage", PropertyDataType.String),
            ("expected_close", PropertyDataType.Date), ("title", PropertyDataType.String), ("client_status", PropertyDataType.String),
            ("company_name", PropertyDataType.String), ("name", PropertyDataType.String), ("first_name", PropertyDataType.String),
            ("client_lifetime_value", PropertyDataType.Decimal), ("industry", PropertyDataType.String),
        })
        {
            var p = new Property { Name = name, DataType = type };
            _db.Properties.Add(p);
            await _db.SaveChangesAsync();
            _prop[name] = p.Id;
        }

        var relType = new EntityRelationshipType { Name = "deal_client", SourceEntityTypeId = dealType.Id, TargetEntityTypeId = clientType.Id, RelationshipCardinality = RelationshipCardinality.ManyToOne };
        _db.EntityRelationshipTypes.Add(relType);
        await _db.SaveChangesAsync();
        _relTypeId = relType.Id;

        _dScored = await AddDealAsync(("status", "opened"), ("deal_value", 10000m), ("deal_stage", "Negotiation"), ("expected_close", _thisMonth), ("title", "Scored"));
        await AddDealAsync(("status", "opened"), ("deal_value", 8000m));
        await AddDealAsync();
        await AddDealAsync(("status", "on_hold"), ("deal_value", 3000m));
        _dClosed = await AddDealAsync(("status", "closed"), ("deal_value", 20000m), ("expected_close", _thisMonth));

        _cName = await AddClientAsync(("name", "Bob"), ("client_lifetime_value", 50000m), ("client_status", "active"));
        _cNoName = await AddClientAsync(("client_lifetime_value", 10000m));
        await LinkAsync(_dScored, _cName);
    }

    private async Task<int> AddDealAsync(params (string name, object value)[] props)
    {
        var e = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        await AddValuesAsync(e.Id, props);
        return e.Id;
    }

    private async Task<int> AddClientAsync(params (string name, object value)[] props)
    {
        var e = new Entity { EntityTypeId = _clientTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        await AddValuesAsync(e.Id, props);
        return e.Id;
    }

    private async Task AddValuesAsync(int entityId, (string name, object value)[] props)
    {
        foreach (var (name, value) in props)
        {
            var pv = new EntityPropertyValue { EntityId = entityId, PropertyId = _prop[name] };
            switch (value)
            {
                case string s: pv.ValueString = s; break;
                case decimal d: pv.ValueDecimal = d; break;
                case DateOnly dt: pv.ValueDate = dt; break;
            }
            _db.EntityPropertyValues.Add(pv);
        }
        await _db.SaveChangesAsync();
    }

    private async Task LinkAsync(int dealId, int clientId)
    {
        _db.EntityRelationships.Add(new EntityRelationship { SourceEntityId = dealId, TargetEntityId = clientId, RelationshipTypeId = _relTypeId });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_BasicTier_ReturnsBasicWithNullFinancials()
    {
        var summary = await _svc.GetSummaryAsync(_basicId, _wsId, CancellationToken.None);

        summary.AccessLevel.Should().Be("basic");
        summary.TotalPipelineValue.Should().BeNull("basic tier hides financial aggregates");
        summary.WonDeals.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_MissingAndUnknownStatus_CountAsOpen()
    {
        var summary = await _svc.GetSummaryAsync(_managerId, _wsId, CancellationToken.None);

        summary.WonDeals.Should().Be(1);
        summary.OpenDeals.Should().Be(4);
    }

    [Fact]
    public async Task GetRiskDistributionAsync_ExcludesUnscoredActiveDeals_AndFallsBackClientName()
    {
        var risk = await _svc.GetRiskDistributionAsync(_managerId, _wsId, CancellationToken.None);

        risk.Items.Should().ContainSingle().Which.ClientName.Should().Be("Bob");
    }

    [Fact]
    public async Task GetTopEntitiesAsync_PlaceholderNameForUnnamedClient()
    {
        var top = await _svc.GetTopEntitiesAsync(_managerId, _wsId, CancellationToken.None);

        top.TopClients.Should().Contain(c => c.Name == $"Client #{_cNoName}");
    }

    [Fact]
    public async Task GetMemberActivityAsync_Manager_ListsActiveMembersWithRoles()
    {
        var activity = await _svc.GetMemberActivityAsync(_managerId, _wsId, CancellationToken.None);

        activity.Should().Contain(m => m.UserId == _managerId && m.RoleName == "manager");
        activity.Should().Contain(m => m.UserId == _basicId);
    }
}
