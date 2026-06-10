using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Relativa.Graph;
using Relativa.Graph.Dashboard;
using Relativa.Graph.Data;
using Relativa.Graph.ML;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class DashboardServiceAnalyticsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("dashboard_analytics_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private DashboardService _svc = null!;
    private int _orgId, _adminId, _wsId, _dealTypeId, _clientTypeId, _relTypeId, _userId;
    private readonly Dictionary<string, int> _prop = new();
    private readonly DateOnly _thisMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 15);
    private int _d1, _d2, _d3, _d4, _d5, _c1, _c2;

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
            [_d1] = new(_d1, 0.30, null, null),
            [_d2] = new(_d2, 0.55, null, null),
            [_d3] = new(_d3, 0.85, null, null),
            [_d4] = new(_d4, 0.90, null, null),
            [_d5] = new(_d5, 0.40, null, null),
        };
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((IReadOnlyList<int>)ci[0]).Where(scores.ContainsKey).ToDictionary(id => id, id => scores[id]));
        _svc = new DashboardService(_db, ml);
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
        var admin = new User { FirstName = "O", LastName = "Owner", Email = "o@a.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        _orgId = org.Id; _adminId = admin.Id; _userId = admin.Id;

        var perm = new Permission { Name = "manage_org_settings" };
        _db.Permissions.Add(perm);
        await _db.SaveChangesAsync();
        var role = new OrganizationRole { Name = "org_admin", OrganizationId = org.Id, Priority = 1 };
        _db.OrganizationRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.OrganizationRolePermissions.Add(new OrganizationRolePermission { OrgRoleId = role.Id, PermissionId = perm.Id });
        _db.UserRoleOrganizations.Add(new UserRoleOrganization { UserId = admin.Id, OrganizationId = org.Id, OrgRoleId = role.Id, JoinedAt = DateTime.UtcNow });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = admin.Id };
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

        _d1 = await AddDealAsync("opened", 50000m, "Negotiation", _thisMonth, "Alpha");
        _d2 = await AddDealAsync("pending", 30000m, "Proposal", _thisMonth, "Beta");
        _d3 = await AddDealAsync("opened", 10000m, "Qualification", _thisMonth.AddMonths(2), "Gamma");
        _d4 = await AddDealAsync("closed", 20000m, "Negotiation", _thisMonth, "Delta");
        _d5 = await AddDealAsync("revoked", 5000m, "Prospecting", _thisMonth, "Epsilon");
        _c1 = await AddClientAsync("Acme", 100000m, "active");
        _c2 = await AddClientAsync("Beta Inc", 40000m, "inactive");

        await LinkAsync(_d1, _c1);
        await LinkAsync(_d2, _c2);
    }

    private async Task<int> AddDealAsync(string status, decimal value, string stage, DateOnly close, string title)
    {
        var e = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _userId };
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
        var e = new Entity { EntityTypeId = _clientTypeId, CreatedByUserId = _userId };
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
    public async Task GetRiskDistributionAsync_BucketsActiveDealsByClosureScore()
    {
        var risk = await _svc.GetRiskDistributionAsync(_adminId, _orgId, CancellationToken.None);

        risk.Distribution["high"].Count.Should().Be(1, "only the 0.30-score active deal is high risk (< 0.4)");
        risk.Distribution["medium"].Count.Should().Be(1, "the 0.55-score deal is medium (0.4–0.7)");
        risk.Distribution["low"].Count.Should().Be(1, "the 0.85-score deal is low (> 0.7)");
        risk.Items.Should().HaveCount(3, "closed and revoked deals are not scored — only opened/pending");
        risk.Items.Should().BeInAscendingOrder(i => i.Score);
        risk.Items.Should().Contain(i => i.ClientName == "Acme");
    }

    [Fact]
    public async Task GetPipelineAsync_GroupsStagesAndStatusesAndConversion()
    {
        var pipeline = await _svc.GetPipelineAsync(_adminId, _orgId, CancellationToken.None);

        pipeline.Stages.Single(s => s.Name == "Negotiation").Count.Should().Be(2);
        pipeline.Stages.Single(s => s.Name == "Prospecting").Count.Should().Be(1);
        pipeline.StatusBreakdown["opened"].Should().Be(2);
        pipeline.StatusBreakdown["pending"].Should().Be(1);
        pipeline.StatusBreakdown["closed"].Should().Be(1);
        pipeline.StatusBreakdown["revoked"].Should().Be(1);
        pipeline.ConversionRate.Should().BeApproximately(0.5, 0.0001, "1 won / (1 won + 1 lost)");
    }

    [Fact]
    public async Task GetTopEntitiesAsync_RanksDealsByValueAndClientsByLifetimeValue()
    {
        var top = await _svc.GetTopEntitiesAsync(_adminId, _orgId, CancellationToken.None);

        top.TopDeals.First().Title.Should().Be("Alpha", "Alpha has the highest deal value (50000)");
        top.TopDeals.First().Value.Should().Be(50000m);
        top.TopClients.First().Name.Should().Be("Acme", "Acme has the highest lifetime value (100000)");
    }

    [Fact]
    public async Task GetTrendsAsync_AttributesWonAndLostToExpectedCloseMonth()
    {
        var trends = await _svc.GetTrendsAsync(_adminId, _orgId, CancellationToken.None);

        trends.Months.Should().HaveCount(6);
        trends.Months.Sum(m => m.ClosedWon).Should().Be(1, "exactly one closed (won) deal closes within the window");
        trends.Months.Sum(m => m.ClosedLost).Should().Be(1, "exactly one revoked (lost) deal");
    }

    [Fact]
    public async Task GetSummaryAsync_CountsDealsClosingThisMonth()
    {
        var summary = await _svc.GetSummaryAsync(_adminId, _orgId, CancellationToken.None);

        summary.DealsClosingThisMonth.Should().Be(4, "four deals have an expected_close date in the current month");
        summary.OpenDeals.Should().Be(3);
        summary.TotalDealValue.Should().Be(115000m);
    }

    [Fact]
    public async Task GetWorkspacesComparisonAsync_AdminSeesPerWorkspaceWinRateClientsAndTopStage()
    {
        var comparison = await _svc.GetWorkspacesComparisonAsync(_adminId, _orgId, CancellationToken.None);

        var ws = comparison.Single(c => c.WorkspaceId == _wsId);
        ws.DealCount.Should().Be(5);
        ws.ClientCount.Should().Be(2);
        ws.MemberCount.Should().Be(0, "the org admin holds an org role but no direct workspace membership row");
        ws.WinRate.Should().BeApproximately(0.5, 0.0001, "one closed-won over one won plus one revoked-lost");
        ws.TopStage.Should().Be("Negotiation", "two deals share the Negotiation stage, more than any other");
        ws.PipelineValue.Should().Be(115000m);
    }

    [Fact]
    public async Task GetWorkspacesComparisonAsync_WithoutManageOrgSettings_ThrowsForbidden()
    {
        var act = () => _svc.GetWorkspacesComparisonAsync(999999, _orgId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "the cross-workspace comparison is an org-owner view gated by manage_org_settings");
    }

    [Fact]
    public async Task GetTrendsAsync_IgnoresDealsWithMissingOrPastCloseDate()
    {
        var pastDeal = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _userId };
        _db.Entities.Add(pastDeal);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = pastDeal.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = pastDeal.Id, PropertyId = _prop["status"], ValueString = "closed" },
            new EntityPropertyValue { EntityId = pastDeal.Id, PropertyId = _prop["deal_value"], ValueDecimal = 99999m },
            new EntityPropertyValue { EntityId = pastDeal.Id, PropertyId = _prop["expected_close"], ValueDate = _thisMonth.AddMonths(-10) });

        var datelessDeal = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _userId };
        _db.Entities.Add(datelessDeal);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = datelessDeal.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = datelessDeal.Id, PropertyId = _prop["status"], ValueString = "opened" },
            new EntityPropertyValue { EntityId = datelessDeal.Id, PropertyId = _prop["deal_value"], ValueDecimal = 12345m });
        await _db.SaveChangesAsync();

        var trends = await _svc.GetTrendsAsync(_adminId, _orgId, CancellationToken.None);

        trends.Months.Sum(m => m.NewDeals).Should().Be(4,
            "only the four current-month deals are in-window; the long-past and date-less deals are skipped");
        trends.Months.Sum(m => m.WonRevenue).Should().Be(20000m,
            "the out-of-window closed deal's revenue must not leak into the rolling window");
    }
}
