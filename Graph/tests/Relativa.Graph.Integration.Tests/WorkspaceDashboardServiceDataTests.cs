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

public sealed class WorkspaceDashboardServiceDataTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("ws_dashboard_data_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private WorkspaceDashboardService _svc = null!;
    private int _analystId, _wsId;
    private int _dealTypeId, _statusPropId, _dealValuePropId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GraphQueryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, MlScoreDto>());
        _svc = new WorkspaceDashboardService(_db, ml, Substitute.For<IMlRecalculationClient>());
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
        var analyst = new User { FirstName = "A", LastName = "Analyst", Email = "a@d.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(analyst);
        await _db.SaveChangesAsync();
        _analystId = analyst.Id;

        var permAnalytics = new Permission { Name = "view_analytics" };
        _db.Permissions.Add(permAnalytics);
        await _db.SaveChangesAsync();
        var role = new WorkspaceRole { Name = "ws_analyst", Priority = 3 };
        _db.WorkspaceRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.WorkspaceRolePermissions.Add(new WorkspaceRolePermission { WsRoleId = role.Id, PermissionId = permAnalytics.Id });

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = analyst.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        _db.EntityTypes.Add(dealType);
        var statusProp = new Property { Name = "status", DataType = PropertyDataType.String };
        var dealValueProp = new Property { Name = "deal_value", DataType = PropertyDataType.Decimal };
        _db.Properties.AddRange(statusProp, dealValueProp);
        await _db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _statusPropId = statusProp.Id; _dealValuePropId = dealValueProp.Id;

        _db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = analyst.Id, WorkspaceId = ws.Id, WsRoleId = role.Id, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        await AddDealAsync("closed", 10000m);
        await AddDealAsync("closed", 30000m);
        await AddDealAsync("revoked", 5000m);
        await AddDealAsync("opened", 8000m);
    }

    private async Task AddDealAsync(string status, decimal value)
    {
        var deal = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = _analystId };
        _db.Entities.Add(deal);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = deal.Id, WorkspaceId = _wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = deal.Id, PropertyId = _statusPropId, ValueString = status },
            new EntityPropertyValue { EntityId = deal.Id, PropertyId = _dealValuePropId, ValueDecimal = value });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_FullAccess_ComputesDealOutcomesAndFinancials()
    {
        var summary = await _svc.GetSummaryAsync(_analystId, _wsId, CancellationToken.None);

        summary.AccessLevel.Should().Be("full");
        summary.TotalDeals.Should().Be(4);
        summary.WonDeals.Should().Be(2);
        summary.LostDeals.Should().Be(1);
        summary.TotalPipelineValue.Should().Be(53000m, "full access exposes the summed deal value across all four deals");
        summary.WinRate.Should().BeApproximately(0.6667, 0.0001, "win rate = 2 won / (2 won + 1 lost)");
    }

    [Fact]
    public async Task GetPipelineAsync_FullAccess_CountsClosedAndRevokedStatuses()
    {
        var pipeline = await _svc.GetPipelineAsync(_analystId, _wsId, CancellationToken.None);

        pipeline.StatusBreakdown["closed"].Should().Be(2);
        pipeline.StatusBreakdown["revoked"].Should().Be(1);
        pipeline.StatusBreakdown["opened"].Should().Be(1);
    }
}
