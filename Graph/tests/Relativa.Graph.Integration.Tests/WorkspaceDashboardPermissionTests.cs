using System.Diagnostics;
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

public sealed class WorkspaceDashboardPermissionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relativa_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private GraphQueryDbContext _db = null!;
    private WorkspaceDashboardService _svc = null!;

    private int _analystUserId;
    private int _managerUserId;
    private int _outsiderUserId;
    private int _archivedMemberUserId;
    private int _targetWorkspaceId;
    private int _archivedWorkspaceId;

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

        _svc = new WorkspaceDashboardService(_db, mlScoring, Substitute.For<IMlRecalculationClient>());

        await _svc.GetSummaryAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);
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

        var analystUser        = new User { FirstName = "Ana",     LastName = "Analyst", Email = "analyst@test.com",       Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var managerUser        = new User { FirstName = "Mark",    LastName = "Manager", Email = "manager@test.com",       Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var outsiderUser       = new User { FirstName = "Outside", LastName = "User",    Email = "outsider@test.com",      Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var archivedMemberUser = new User { FirstName = "Former",  LastName = "Member",  Email = "former.member@test.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        _db.Users.AddRange(analystUser, managerUser, outsiderUser, archivedMemberUser);
        await _db.SaveChangesAsync();

        _analystUserId        = analystUser.Id;
        _managerUserId        = managerUser.Id;
        _outsiderUserId       = outsiderUser.Id;
        _archivedMemberUserId = archivedMemberUser.Id;

        var permViewAnalytics     = new Permission { Name = "view_analytics",      IsArchived = false };
        var permViewBasic         = new Permission { Name = "view_basic_stats",    IsArchived = false };
        var permViewTeamAnalytics = new Permission { Name = "view_team_analytics", IsArchived = false };
        _db.Permissions.AddRange(permViewAnalytics, permViewBasic, permViewTeamAnalytics);
        await _db.SaveChangesAsync();

        var wsAnalystRole = new WorkspaceRole { Name = "ws_analyst", WorkspaceId = null, Priority = 3, IsArchived = false };
        var wsManagerRole = new WorkspaceRole { Name = "ws_manager", WorkspaceId = null, Priority = 2, IsArchived = false };
        _db.WorkspaceRoles.AddRange(wsAnalystRole, wsManagerRole);
        await _db.SaveChangesAsync();

        _db.WorkspaceRolePermissions.AddRange(
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permViewAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permViewBasic.Id },
            new WorkspaceRolePermission { WsRoleId = wsManagerRole.Id, PermissionId = permViewAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = wsManagerRole.Id, PermissionId = permViewBasic.Id },
            new WorkspaceRolePermission { WsRoleId = wsManagerRole.Id, PermissionId = permViewTeamAnalytics.Id }
        );
        await _db.SaveChangesAsync();

        var targetWs   = new Workspace { Name = "Target WS",   IsArchived = false, CreatedByUserId = analystUser.Id,  OrganizationId = org.Id };
        var otherWs    = new Workspace { Name = "Other WS",    IsArchived = false, CreatedByUserId = outsiderUser.Id, OrganizationId = org.Id };
        var archivedWs = new Workspace { Name = "Archived WS", IsArchived = true,  CreatedByUserId = analystUser.Id,  OrganizationId = org.Id };
        _db.Workspaces.AddRange(targetWs, otherWs, archivedWs);
        await _db.SaveChangesAsync();

        _targetWorkspaceId   = targetWs.Id;
        _archivedWorkspaceId = archivedWs.Id;

        _db.UserRoleWorkspaces.AddRange(
            new UserRoleWorkspace { UserId = analystUser.Id,        WorkspaceId = targetWs.Id,   WsRoleId = wsAnalystRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleWorkspace { UserId = managerUser.Id,        WorkspaceId = targetWs.Id,   WsRoleId = wsManagerRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleWorkspace { UserId = outsiderUser.Id,       WorkspaceId = otherWs.Id,    WsRoleId = wsManagerRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false },
            new UserRoleWorkspace { UserId = archivedMemberUser.Id, WorkspaceId = targetWs.Id,   WsRoleId = wsAnalystRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = true  },
            new UserRoleWorkspace { UserId = analystUser.Id,        WorkspaceId = archivedWs.Id, WsRoleId = wsAnalystRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false }
        );
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_AnalystMember_ReturnsFull()
    {
        var result = await _svc.GetSummaryAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);

        result.WorkspaceId.Should().Be(_targetWorkspaceId);
        result.AccessLevel.Should().Be("full",
            "analyst has view_analytics so the service must return full-access summary");
    }

    [Fact]
    public async Task GetSummaryAsync_ManagerMember_ReturnsFull()
    {
        var result = await _svc.GetSummaryAsync(_managerUserId, _targetWorkspaceId, CancellationToken.None);

        result.WorkspaceId.Should().Be(_targetWorkspaceId);
        result.AccessLevel.Should().Be("full",
            "manager has view_analytics so summary access level must also be full");
    }

    [Fact]
    public async Task GetPipelineAsync_AnalystMember_ReturnsPipeline()
    {
        var result = await _svc.GetPipelineAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);

        result.Should().NotBeNull("analyst has view_analytics — pipeline must be accessible");
    }

    [Fact]
    public async Task GetPipelineAsync_ManagerMember_ReturnsPipeline()
    {
        var result = await _svc.GetPipelineAsync(_managerUserId, _targetWorkspaceId, CancellationToken.None);

        result.Should().NotBeNull("manager has view_analytics — pipeline must be accessible");
    }

    [Fact]
    public async Task GetRiskDistributionAsync_AnalystMember_ReturnsDistribution()
    {
        var result = await _svc.GetRiskDistributionAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);

        result.Should().NotBeNull("analyst has view_analytics — risk distribution must be accessible");
    }

    [Fact]
    public async Task GetTrendsAsync_AnalystMember_ReturnsTrends()
    {
        var result = await _svc.GetTrendsAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);

        result.Should().NotBeNull("analyst has view_analytics — trends must be accessible");
    }

    [Fact]
    public async Task GetSummaryAsync_NonMember_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetSummaryAsync(_outsiderUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "user with no role in this workspace must be denied at the permission guard");
    }

    [Fact]
    public async Task GetPipelineAsync_NonMember_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetPipelineAsync(_outsiderUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "user with no role in this workspace must be denied at the permission guard");
    }

    [Fact]
    public async Task GetRiskDistributionAsync_NonMember_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetRiskDistributionAsync(_outsiderUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "user with no role in this workspace must be denied at the permission guard");
    }

    [Fact]
    public async Task GetTrendsAsync_NonMember_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetTrendsAsync(_outsiderUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "user with no role in this workspace must be denied at the permission guard");
    }

    [Fact]
    public async Task GetSummaryAsync_ArchivedMembership_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetSummaryAsync(_archivedMemberUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked workspace assignment (IsArchived=true) must not grant dashboard access");
    }

    [Fact]
    public async Task GetPipelineAsync_ArchivedMembership_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetPipelineAsync(_archivedMemberUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked workspace assignment must not grant access to any guarded endpoint");
    }

    [Fact]
    public async Task GetTrendsAsync_ArchivedMembership_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetTrendsAsync(_archivedMemberUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked workspace assignment must not grant access to any guarded endpoint");
    }

    [Fact]
    public async Task GetRiskDistributionAsync_ArchivedMembership_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetRiskDistributionAsync(_archivedMemberUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked workspace assignment must not grant access to any guarded endpoint");
    }

    [Fact]
    public async Task GetMemberActivityAsync_ArchivedMembership_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetMemberActivityAsync(_archivedMemberUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "a revoked workspace assignment must not grant access to any guarded endpoint");
    }

    [Fact]
    public async Task GetSummaryAsync_ArchivedWorkspace_ThrowsWorkspaceNotFoundException()
    {
        var act = () => _svc.GetSummaryAsync(_analystUserId, _archivedWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<WorkspaceNotFoundException>(
            "the workspace lookup filters IsArchived=true — the domain contract is WorkspaceNotFoundException, not a raw dictionary miss");
    }

    [Fact]
    public async Task GetMemberActivityAsync_AnalystMember_ThrowsForbiddenAccessException()
    {
        var act = () => _svc.GetMemberActivityAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>(
            "analyst lacks view_team_analytics — member activity endpoint must be gated");
    }

    [Fact]
    public async Task GetMemberActivityAsync_ManagerMember_ReturnsList()
    {
        var result = await _svc.GetMemberActivityAsync(_managerUserId, _targetWorkspaceId, CancellationToken.None);

        result.Should().NotBeNull("manager has view_team_analytics — member activity must be accessible");
    }

    [Fact]
    public async Task GetSummaryAsync_WarmPath_MaxOf3Calls_CompletesWithin500ms()
    {
        var samples = new long[3];
        for (var i = 0; i < samples.Length; i++)
        {
            var sw = Stopwatch.StartNew();
            await _svc.GetSummaryAsync(_analystUserId, _targetWorkspaceId, CancellationToken.None);
            samples[i] = sw.ElapsedMilliseconds;
        }

        samples.Max().Should().BeLessThanOrEqualTo(500,
            $"worst of 3 warm-path calls was {samples.Max()}ms — exceeds the 500ms SLA (samples: {string.Join(", ", samples)})");
    }
}
