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

public sealed class DashboardServiceEdgeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("dashboard_edge_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private DashboardService _svc = null!;
    private int _orgId, _adminId, _basicUserId, _wsId, _dealTypeId, _clientTypeId, _relTypeId;
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
        _svc = new DashboardService(_db, ml);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _postgres.DisposeAsync(); }

    private async Task SeedAsync()
    {
        var org = new Organization { Name = "Org" };
        _db.Organizations.Add(org);
        var admin = new User { FirstName = "O", LastName = "Owner", Email = "o@e.com", Password = "x", CreatedAt = DateTime.UtcNow };
        var basic = new User { FirstName = "B", LastName = "Basic", Email = "b@e.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.AddRange(admin, basic);
        await _db.SaveChangesAsync();
        _orgId = org.Id; _adminId = admin.Id; _basicUserId = basic.Id;

        var pManage = new Permission { Name = "manage_org_settings" };
        var pBasic = new Permission { Name = "view_basic_stats" };
        _db.Permissions.AddRange(pManage, pBasic);
        await _db.SaveChangesAsync();
        var orgRole = new OrganizationRole { Name = "admin", OrganizationId = org.Id, Priority = 1 };
        _db.OrganizationRoles.Add(orgRole);
        await _db.SaveChangesAsync();
        _db.OrganizationRolePermissions.Add(new OrganizationRolePermission { OrgRoleId = orgRole.Id, PermissionId = pManage.Id });
        _db.UserRoleOrganizations.Add(new UserRoleOrganization { UserId = admin.Id, OrganizationId = org.Id, OrgRoleId = orgRole.Id, JoinedAt = DateTime.UtcNow });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = admin.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        var clientType = new EntityType { Name = "client" };
        _db.EntityTypes.AddRange(dealType, clientType);
        await _db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _clientTypeId = clientType.Id;

        var wsViewer = new WorkspaceRole { Name = "viewer", Priority = 4 };
        _db.WorkspaceRoles.Add(wsViewer);
        await _db.SaveChangesAsync();
        _db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission { WsRoleId = wsViewer.Id, PermissionId = pBasic.Id });
        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = basic.Id, WorkspaceId = ws.Id, WsRoleId = wsViewer.Id, JoinedAt = DateTime.UtcNow });
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
        var dUnnamedClient = await AddDealAsync(("status", "opened"), ("deal_value", 8000m));
        await AddDealAsync();
        await AddDealAsync(("status", "on_hold"), ("deal_value", 3000m));
        _dClosed = await AddDealAsync(("status", "closed"), ("deal_value", 20000m), ("expected_close", _thisMonth));

        _cName = await AddClientAsync(("name", "Bob"), ("client_lifetime_value", 50000m), ("client_status", "active"));
        _cNoName = await AddClientAsync(("client_lifetime_value", 10000m));
        await AddClientAsync(("first_name", "Carol"), ("client_lifetime_value", 20000m));

        await LinkAsync(_dScored, _cName);
        await LinkAsync(dUnnamedClient, _cNoName);
    }

    private async Task<int> AddDealAsync(params (string name, object value)[] props)
    {
        var e = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _adminId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        await AddValuesAsync(e.Id, props);
        return e.Id;
    }

    private async Task<int> AddClientAsync(params (string name, object value)[] props)
    {
        var e = new Entity { EntityTypeId = _clientTypeId, CreatedByUserId = _adminId };
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
    public async Task GetSummaryAsync_BasicTierUser_ReturnsBasicAccessWithNullFinancials()
    {
        var summary = await _svc.GetSummaryAsync(_basicUserId, _orgId, CancellationToken.None);

        summary.AccessLevel.Should().Be("basic", "a view_basic_stats-only user gets the limited basic dashboard");
        summary.TotalDeals.Should().Be(5);
    }

    [Fact]
    public async Task GetSummaryAsync_MissingAndUnknownStatusDeals_CountAsOpen()
    {
        var summary = await _svc.GetSummaryAsync(_adminId, _orgId, CancellationToken.None);

        summary.WonDeals.Should().Be(1, "only the explicitly 'closed' deal is won");
        summary.OpenDeals.Should().Be(4, "deals with missing status and the unknown 'on_hold' status fall through to open");
    }

    [Fact]
    public async Task GetRiskDistributionAsync_ExcludesActiveDealsWithoutMlScores()
    {
        var risk = await _svc.GetRiskDistributionAsync(_adminId, _orgId, CancellationToken.None);

        risk.Items.Should().ContainSingle("only the one active deal that has an ML score is scored")
            .Which.ClientName.Should().Be("Bob", "the client name falls back to the 'name' property when company_name is absent");
    }

    [Fact]
    public async Task GetTopEntitiesAsync_FallsBackToPlaceholderNameForUnnamedClients()
    {
        var top = await _svc.GetTopEntitiesAsync(_adminId, _orgId, CancellationToken.None);

        top.TopClients.Should().Contain(c => c.Name == $"Client #{_cNoName}",
            "a client with no company_name/name resolves to a stable placeholder");
    }
}
