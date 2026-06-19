using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Relativa.Graph.Data;
using Relativa.Graph.ML;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("graph_api_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public int OrgId { get; private set; }
    public int OrgAdminUserId { get; private set; }
    public int AnalyticsUserId { get; private set; }
    public int OutsiderUserId { get; private set; }
    public int WorkspaceId { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GraphQueryDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IMlScoringClient>();
            var ml = Substitute.For<IMlScoringClient>();
            ml.ScoreBatchAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<int, MlScoreDto>());
            services.AddSingleton(ml);

            services.RemoveAll<IMlRecalculationClient>();
            services.AddSingleton(Substitute.For<IMlRecalculationClient>());
        });
    }

    private async Task SeedAsync(GraphQueryDbContext db)
    {
        var org = new Organization { Name = "Api Org", IsArchived = false };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        OrgId = org.Id;

        var admin = new User { FirstName = "Olha", LastName = "Owner", Email = "owner@api.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var analytics = new User { FirstName = "Ana", LastName = "Analyst", Email = "analyst@api.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        var outsider = new User { FirstName = "Ostap", LastName = "Outside", Email = "outsider@api.com", Password = "x", CreatedAt = DateTime.UtcNow, IsArchived = false };
        db.Users.AddRange(admin, analytics, outsider);
        await db.SaveChangesAsync();
        OrgAdminUserId = admin.Id;
        AnalyticsUserId = analytics.Id;
        OutsiderUserId = outsider.Id;

        var permManage = new Permission { Name = "manage_org_settings", IsArchived = false };
        var permAnalytics = new Permission { Name = "view_analytics", IsArchived = false };
        var permBasic = new Permission { Name = "view_basic_stats", IsArchived = false };
        db.Permissions.AddRange(permManage, permAnalytics, permBasic);
        await db.SaveChangesAsync();

        var orgAdminRole = new OrganizationRole { Name = "org_admin", OrganizationId = org.Id, Priority = 1, IsArchived = false };
        db.OrganizationRoles.Add(orgAdminRole);
        await db.SaveChangesAsync();
        db.OrganizationRolePermissions.Add(new OrganizationRolePermission { OrgRoleId = orgAdminRole.Id, PermissionId = permManage.Id });
        db.UserRoleOrganizations.Add(new UserRoleOrganization { UserId = admin.Id, OrganizationId = org.Id, OrgRoleId = orgAdminRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false });
        await db.SaveChangesAsync();

        var wsAnalystRole = new WorkspaceRole { Name = "ws_analyst", WorkspaceId = null, Priority = 3, IsArchived = false };
        db.WorkspaceRoles.Add(wsAnalystRole);
        await db.SaveChangesAsync();
        db.WorkspaceRolePermissions.AddRange(
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permAnalytics.Id },
            new WorkspaceRolePermission { WsRoleId = wsAnalystRole.Id, PermissionId = permBasic.Id });
        await db.SaveChangesAsync();

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = admin.Id, IsArchived = false };
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync();
        WorkspaceId = ws.Id;

        db.UserRoleWorkspaces.Add(new UserRoleWorkspace { UserId = analytics.Id, WorkspaceId = ws.Id, WsRoleId = wsAnalystRole.Id, JoinedAt = DateTime.UtcNow, IsArchived = false });
        await db.SaveChangesAsync();
    }
}
