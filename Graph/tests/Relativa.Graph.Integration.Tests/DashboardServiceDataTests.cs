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

public sealed class DashboardServiceDataTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("dashboard_data_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private GraphQueryDbContext _db = null!;
    private DashboardService _svc = null!;
    private int _orgId, _adminId;

    private int _dealTypeId, _clientTypeId;
    private int _statusPropId, _dealValuePropId, _clientStatusPropId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<GraphQueryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();

        var ml = Substitute.For<IMlScoringClient>();
        ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<int, MlScoreDto>());
        _svc = new DashboardService(_db, ml);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        var org = new Organization { Name = "Org", IsArchived = false };
        _db.Organizations.Add(org);
        var admin = new User { FirstName = "O", LastName = "Owner", Email = "owner@d.com", Password = "x", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        _orgId = org.Id; _adminId = admin.Id;

        var permManage = new Permission { Name = "manage_org_settings" };
        _db.Permissions.Add(permManage);
        await _db.SaveChangesAsync();
        var role = new OrganizationRole { Name = "org_admin", OrganizationId = org.Id, Priority = 1 };
        _db.OrganizationRoles.Add(role);
        await _db.SaveChangesAsync();
        _db.OrganizationRolePermissions.Add(new OrganizationRolePermission { OrgRoleId = role.Id, PermissionId = permManage.Id });
        _db.UserRoleOrganizations.Add(new UserRoleOrganization { UserId = admin.Id, OrganizationId = org.Id, OrgRoleId = role.Id, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = admin.Id };
        _db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal" };
        var clientType = new EntityType { Name = "client" };
        _db.EntityTypes.AddRange(dealType, clientType);
        var statusProp = new Property { Name = "status", DataType = PropertyDataType.String };
        var dealValueProp = new Property { Name = "deal_value", DataType = PropertyDataType.Decimal };
        var clientStatusProp = new Property { Name = "client_status", DataType = PropertyDataType.String };
        _db.Properties.AddRange(statusProp, dealValueProp, clientStatusProp);
        await _db.SaveChangesAsync();
        _dealTypeId = dealType.Id; _clientTypeId = clientType.Id;
        _statusPropId = statusProp.Id; _dealValuePropId = dealValueProp.Id; _clientStatusPropId = clientStatusProp.Id;

        await AddDealAsync(ws.Id, admin.Id, "closed", 10000m);
        await AddDealAsync(ws.Id, admin.Id, "closed", 20000m);
        await AddDealAsync(ws.Id, admin.Id, "revoked", 5000m);
        await AddClientAsync(ws.Id, admin.Id, "active");
    }

    private async Task AddDealAsync(int wsId, int userId, string status, decimal value)
    {
        var deal = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = userId };
        _db.Entities.Add(deal);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = deal.Id, WorkspaceId = wsId });
        _db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = deal.Id, PropertyId = _statusPropId, ValueString = status },
            new EntityPropertyValue { EntityId = deal.Id, PropertyId = _dealValuePropId, ValueDecimal = value });
        await _db.SaveChangesAsync();
    }

    private async Task AddClientAsync(int wsId, int userId, string status)
    {
        var client = new Entity { EntityTypeId = _clientTypeId, CreatedByUserId = userId };
        _db.Entities.Add(client);
        await _db.SaveChangesAsync();
        _db.Set<EntityWorkspace>().Add(new EntityWorkspace { EntityId = client.Id, WorkspaceId = wsId });
        _db.EntityPropertyValues.Add(new EntityPropertyValue { EntityId = client.Id, PropertyId = _clientStatusPropId, ValueString = status });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_AggregatesDealOutcomesValueAndWinRate()
    {
        var summary = await _svc.GetSummaryAsync(_adminId, _orgId, CancellationToken.None);

        summary.AccessLevel.Should().Be("full_org");
        summary.TotalDeals.Should().Be(3);
        summary.WonDeals.Should().Be(2, "two deals have status 'closed'");
        summary.LostDeals.Should().Be(1, "one deal has status 'revoked'");
        summary.TotalDealValue.Should().Be(35000m);
        summary.WinRate.Should().BeApproximately(0.6667, 0.0001, "win rate = won / (won + lost) = 2/3");
        summary.AvgDealSize.Should().Be(11666.67m, "average = total value 35000 / 3 deals, rounded to 2 dp");
    }

    [Fact]
    public async Task GetSummaryAsync_CountsClientsAndActiveClients()
    {
        var summary = await _svc.GetSummaryAsync(_adminId, _orgId, CancellationToken.None);

        summary.TotalClients.Should().Be(1);
        summary.ActiveClients.Should().Be(1, "the single client has client_status 'active'");
    }

    [Fact]
    public async Task GetPipelineAsync_BreaksDownDealStatusesIncludingClosedAndRevoked()
    {
        var pipeline = await _svc.GetPipelineAsync(_adminId, _orgId, CancellationToken.None);

        pipeline.StatusBreakdown["closed"].Should().Be(2);
        pipeline.StatusBreakdown["revoked"].Should().Be(1);
        pipeline.ConversionRate.Should().BeApproximately(0.6667, 0.0001);
    }
}
