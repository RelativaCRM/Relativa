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

public sealed class WorkspaceDashboardServiceTrendsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("ws_trends_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private IMlRecalculationClient _mlRecalc = null!;
    private WorkspaceDashboardService _svc = null!;
    private int _managerId, _wsId, _dealTypeId, _analysisTypeId, _analysisRelTypeId;
    private readonly Dictionary<string, int> _prop = new();

    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateOnly InWindow = new(Now.Year, Now.Month, 15);
    private static readonly DateOnly OutOfWindow = new DateOnly(Now.Year, Now.Month, 15).AddMonths(-8);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GraphQueryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>());
        _mlRecalc = Substitute.For<IMlRecalculationClient>();
        _svc = new WorkspaceDashboardService(
            _db,
            ml,
            _mlRecalc,
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
        var manager = new User { FirstName = "M", LastName = "Manager", Email = "m@a.com", Password = "x", CreatedAt = Now };
        _db.Users.Add(manager);
        await _db.SaveChangesAsync();
        _managerId = manager.Id;

        var pAnalytics = new Permission { Name = "view_analytics" };
        _db.Permissions.Add(pAnalytics);
        await _db.SaveChangesAsync();
        var role = new WorkspaceRole { Name = "ws_manager", Priority = 2 };
        _db.WorkspaceRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission { WsRoleId = role.Id, PermissionId = pAnalytics.Id });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = manager.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        var analysisType = new EntityType { Name = "deal_analysis" };
        _db.EntityTypes.AddRange(dealType, analysisType);
        await _db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _analysisTypeId = analysisType.Id;

        foreach (var (name, type) in new[]
        {
            ("status", PropertyDataType.String), ("deal_value", PropertyDataType.Decimal),
            ("expected_close", PropertyDataType.Date), ("title", PropertyDataType.String),
        })
        {
            var p = new Property { Name = name, DataType = type };
            _db.Properties.Add(p);
            await _db.SaveChangesAsync();
            _prop[name] = p.Id;
        }

        var analysisRel = new EntityRelationshipType
        {
            Name = "deal_analysis", SourceEntityTypeId = dealType.Id, TargetEntityTypeId = analysisType.Id,
            RelationshipCardinality = RelationshipCardinality.OneToOne,
        };
        _db.EntityRelationshipTypes.Add(analysisRel);
        await _db.SaveChangesAsync();
        _analysisRelTypeId = analysisRel.Id;

        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = manager.Id, WorkspaceId = ws.Id, WsRoleId = role.Id, JoinedAt = Now });
        await _db.SaveChangesAsync();
    }

    private async Task<int> AddDealAsync(string status, decimal value, DateOnly? close)
    {
        var e = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(e);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = e.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["status"], ValueString = status },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["deal_value"], ValueDecimal = value },
            new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["title"], ValueString = $"Deal {status}" });
        if (close is not null)
            _db.EntityPropertyValues.Add(new EntityPropertyValue { EntityId = e.Id, PropertyId = _prop["expected_close"], ValueDate = close });
        await _db.SaveChangesAsync();
        return e.Id;
    }

    [Fact]
    public async Task GetTrendsAsync_BucketsDealsByCloseMonthAndStatusAcrossEveryBranch()
    {
        await AddDealAsync("closed", 10000m, InWindow);
        await AddDealAsync("revoked", 5000m, InWindow);
        await AddDealAsync("opened", 7000m, InWindow);
        await AddDealAsync("closed", 9999m, OutOfWindow);
        await AddDealAsync("opened", 3000m, close: null);

        var trends = await _svc.GetTrendsAsync(_managerId, _wsId, CancellationToken.None);

        trends.Months.Should().HaveCount(6, "the dashboard reports a rolling six-month window");
        var current = trends.Months[^1];
        current.NewDeals.Should().Be(3, "only the three in-window deals count; the out-of-window and date-less deals are skipped");
        current.ClosedWon.Should().Be(1);
        current.ClosedLost.Should().Be(1);
        current.WonRevenue.Should().Be(10000m);
        current.ActiveValue.Should().Be(7000m, "the opened in-window deal contributes to active value, not revenue");
    }

    [Fact]
    public async Task GetTrendsAsync_NoDeals_ReturnsSixZeroedMonths()
    {
        var trends = await _svc.GetTrendsAsync(_managerId, _wsId, CancellationToken.None);

        trends.Months.Should().HaveCount(6);
        trends.Months.Should().OnlyContain(m => m.NewDeals == 0 && m.WonRevenue == 0m);
    }

    [Fact]
    public async Task GetRiskDistribution_UnscoredActiveDeals_ProvisionsMissingDealAnalysisEntitiesAndEnqueuesRecalc()
    {
        var alreadyLinked = await AddDealAsync("opened", 12000m, InWindow);
        var missing = await AddDealAsync("pending", 6000m, InWindow);

        var existingAnalysis = new Entity { EntityTypeId = _analysisTypeId, CreatedByUserId = _managerId };
        _db.Entities.Add(existingAnalysis);
        await _db.SaveChangesAsync();
        _db.EntityRelationships.Add(new EntityRelationship
        {
            SourceEntityId = alreadyLinked,
            TargetEntityId = existingAnalysis.Id,
            RelationshipTypeId = _analysisRelTypeId,
        });
        await _db.SaveChangesAsync();

        await _svc.GetRiskDistributionAsync(_managerId, _wsId, CancellationToken.None);

        var analysisLinks = await _db.EntityRelationships
            .Where(er => er.RelationshipTypeId == _analysisRelTypeId)
            .Select(er => er.SourceEntityId)
            .ToListAsync();
        analysisLinks.Should().Contain(missing, "the unscored deal with no analysis entity must be provisioned one");
        analysisLinks.Should().Contain(alreadyLinked, "the pre-existing link must be left intact, not duplicated");
        analysisLinks.Count(id => id == alreadyLinked).Should().Be(1, "an already-linked deal is not re-provisioned");

        await _mlRecalc.Received(1).EnqueueAsync(
            Arg.Is<IReadOnlyList<int>>(ids => ids.Contains(missing)), _managerId, _wsId);
    }
}
