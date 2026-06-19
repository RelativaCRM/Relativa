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

public sealed class DashboardServicePermissionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relativa_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private GraphQueryDbContext _db = null!;
    private DashboardService _svc = null!;

    private int _organizationId;
    private int _orgAdminUserId;
    private int _analyticsUserId;
    private int _basicUserId;
    private int _outsiderUserId;
    private int _archivedAdminUserId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<GraphQueryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new GraphQueryDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        await SeedAsync();

        var mlScoring = Substitute.For<IMlScoringClient>();
        mlScoring
            .ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MlScoreDto>());

        _svc = new DashboardService(_db, mlScoring);

        await _svc.GetSummaryAsync(_orgAdminUserId, _organizationId, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        var org = new Organization { Name = "Test Org", IsArchived = false };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        _organizationId = org.Id;

        var orgAdmin     = new User { FirstName = "Olha",  LastName = "Owner",   Email = "owner@test.com",    Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var analyticsU   = new User { FirstName = "Ana",   LastName = "Analyst", Email = "analyst@test.com",  Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var basicU       = new User { FirstName = "Borys", LastName = "Basic",   Email = "basic@test.com",    Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var outsider     = new User { FirstName = "Ostap", LastName = "Outside", Email = "outsider@test.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var archivedAdmin = new User { FirstName = "Fedir", LastName = "Former",  Email = "former@test.com",   Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        _db.Users.AddRange(orgAdmin, analyticsU, basicU, outsider, archivedAdmin);
        await _db.SaveChangesAsync();

        _orgAdminUserId      = orgAdmin.Id;
        _analyticsUserId     = analyticsU.Id;
        _basicUserId         = basicU.Id;
        _outsiderUserId      = outsider.Id;
        _archivedAdminUserId = archivedAdmin.Id;

        var permManageOrg     = new Permission { Name = "manage_org_settings", IsArchived = false };
        var permViewAnalytics = new Permission { Name = "view_analytics",      IsArchived = false };
        var permViewBasic     = new Permission { Name = "view_basic_stats",    IsArchived = false };
        _db.Permissions.AddRange(permManageOrg, permViewAnalytics, permViewBasic);
        await _db.SaveChangesAsync();

        var orgAdminRole = new OrganizationRole { Name = "org_admin", OrganizationId = org.Id, Priority = 1, IsArchived = false };
        _db.OrganizationRoles.Add(orgAdminRole);
        await _db.SaveChangesAsync();

        _db.OrganizationRolePermissions.Add(
            new OrganizationRolePermission { OrgRoleId = orgAdminRole.Id, PermissionId = permManageOrg.Id });
        await _db.SaveChangesAsync();

        _db.UserRoleOrganizations.AddRange(
            new UserRoleOrganization { UserId = orgAdmin.Id,      OrganizationId = org.Id, OrgRoleId = orgAdminRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleOrganization { UserId = archivedAdmin.Id, OrganizationId = org.Id, OrgRoleId = orgAdminRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = true });
        await _db.SaveChangesAsync();

        var wsAnalystRole = new WorkspaceRole { Name = "ws_analyst", WorkspaceId = null, Priority = 3, IsArchived = false };
        var wsViewerRole  = new WorkspaceRole { Name = "ws_viewer",  WorkspaceId = null, Priority = 4, IsArchived = false };
        _db.WorkspaceRoles.AddRange(wsAnalystRole, wsViewerRole);
        await _db.SaveChangesAsync();

        _db.WorkspaceRolePermissions.AddRange(
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permViewAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permViewBasic.Id },
            new WorkspaceRolePermission { WsRoleId = wsViewerRole.Id,  PermissionId = permViewBasic.Id });
        await _db.SaveChangesAsync();

        var ws = new Workspace { Name = "WS One", IsArchived = false, CreatedByUserId = orgAdmin.Id, OrganizationId = org.Id };
        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync();

        _db.UserRoleWorkspaces.AddRange(
            new UserRoleWorkspace { UserId = analyticsU.Id, WorkspaceId = ws.Id, WsRoleId = wsAnalystRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleWorkspace { UserId = basicU.Id,     WorkspaceId = ws.Id, WsRoleId = wsViewerRole.Id,  JoinedAt = DateTime.UtcNow, IsArchived = false });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_OrgAdmin_ReturnsFullOrgAccessLevel()
    {
        var result = await _svc.GetSummaryAsync(_orgAdminUserId, _organizationId, CancellationToken.None);

        result.AccessLevel.Should().Be("full_org",
            "manage_org_settings grants org-wide dashboard access across every workspace");
        result.TotalWorkspaces.Should().Be(1,
            "the org has exactly one non-archived workspace");
    }

    [Fact]
    public async Task GetSummaryAsync_WorkspaceAnalyticsUser_ReturnsFull()
    {
        var result = await _svc.GetSummaryAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        result.AccessLevel.Should().Be("full",
            "view_analytics in a workspace yields full (workspace-scoped) analytics access");
    }

    [Fact]
    public async Task GetSummaryAsync_BasicStatsUser_ReturnsBasic()
    {
        var result = await _svc.GetSummaryAsync(_basicUserId, _organizationId, CancellationToken.None);

        result.AccessLevel.Should().Be("basic",
            "only view_basic_stats must downgrade the summary to the basic tier");
    }

    [Fact]
    public async Task GetSummaryAsync_Outsider_ThrowsForbidden()
    {
        var act = () => _svc.GetSummaryAsync(_outsiderUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a user with no org or workspace permissions has no dashboard access at all");
    }

    [Fact]
    public async Task GetSummaryAsync_ArchivedOrgMembership_DoesNotGrantFullOrg()
    {
        var act = () => _svc.GetSummaryAsync(_archivedAdminUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked org-admin assignment (IsArchived=true) must not grant org-wide access, and the user has no workspace roles to fall back on");
    }

    [Fact]
    public async Task GetWorkspacesComparisonAsync_OrgAdmin_ReturnsOneRowPerWorkspace()
    {
        var result = await _svc.GetWorkspacesComparisonAsync(_orgAdminUserId, _organizationId, CancellationToken.None);

        result.Should().HaveCount(1,
            "comparison returns one row per non-archived workspace in the org");
    }

    [Fact]
    public async Task GetWorkspacesComparisonAsync_NonAdmin_ThrowsForbidden()
    {
        var act = () => _svc.GetWorkspacesComparisonAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "workspace comparison is an org-level feature gated strictly behind manage_org_settings");
    }

    [Fact]
    public async Task GetPipelineAsync_AnalyticsUser_ReturnsFourFixedStages()
    {
        var result = await _svc.GetPipelineAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        result.Stages.Select(s => s.Name).Should()
            .Equal("Prospecting", "Qualification", "Proposal", "Negotiation");
        result.StatusBreakdown.Keys.Should()
            .BeEquivalentTo(new[] { "opened", "pending", "closed", "revoked" });
    }

    [Fact]
    public async Task GetPipelineAsync_OrgAdmin_IsAllowed()
    {
        var result = await _svc.GetPipelineAsync(_orgAdminUserId, _organizationId, CancellationToken.None);

        result.Stages.Should().HaveCount(4,
            "org admins bypass the workspace-level analytics check and still get the full pipeline shape");
    }

    [Fact]
    public async Task GetPipelineAsync_BasicUser_ThrowsForbidden()
    {
        var act = () => _svc.GetPipelineAsync(_basicUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "pipeline requires view_analytics — a basic-stats-only user must be denied");
    }

    [Fact]
    public async Task GetRiskDistributionAsync_AnalyticsUser_ReturnsThreeBuckets()
    {
        var result = await _svc.GetRiskDistributionAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        result.Distribution.Keys.Should().BeEquivalentTo(new[] { "high", "medium", "low" });
    }

    [Fact]
    public async Task GetRiskDistributionAsync_BasicUser_ThrowsForbidden()
    {
        var act = () => _svc.GetRiskDistributionAsync(_basicUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "risk distribution requires view_analytics");
    }

    [Fact]
    public async Task GetTrendsAsync_AnalyticsUser_ReturnsSixRollingMonths()
    {
        var result = await _svc.GetTrendsAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        result.Months.Should().HaveCount(6,
            "trends always covers a fixed 6-month rolling window");
    }

    [Fact]
    public async Task GetTopEntitiesAsync_AnalyticsUser_ReturnsCappedLists()
    {
        var result = await _svc.GetTopEntitiesAsync(_analyticsUserId, _organizationId, CancellationToken.None);

        result.TopDeals.Should().HaveCountLessThanOrEqualTo(10);
        result.TopClients.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task GetTopEntitiesAsync_Outsider_ThrowsForbidden()
    {
        var act = () => _svc.GetTopEntitiesAsync(_outsiderUserId, _organizationId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "top entities requires view_analytics and the outsider has none");
    }
}
