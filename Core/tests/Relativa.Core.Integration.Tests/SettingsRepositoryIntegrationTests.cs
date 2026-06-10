using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class SettingsRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("settings_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<RelativaDbContext> _opts = null!;
    private int _orgId;
    private int _orgRoleId;
    private int _workspaceId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();

        var org = new Organization { Name = "Acme" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var role = new OrganizationRole { Name = "member", OrganizationId = _orgId, Priority = 3 };
        db.OrganizationRoles.Add(role);
        var user = new User { Email = "a@acme.com", FirstName = "A", LastName = "B", Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _orgRoleId = role.Id;

        var ws = new Workspace { Name = "WS", OrganizationId = _orgId, CreatedByUserId = user.Id };
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync();
        _workspaceId = ws.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private RelativaDbContext Db() => new(_opts);

    [Fact]
    public async Task OrganizationSettings_AddThenGet_RoundTripsWithDefaultRoleIncluded()
    {
        await using (var db = Db())
        {
            await new OrganizationSettingsRepository(db).AddAsync(new OrganizationSettings
            {
                OrganizationId = _orgId,
                Description = "Our org",
                JoinPolicy = "invite_only",
                DefaultOrgRoleId = _orgRoleId,
            });
        }

        await using (var db = Db())
        {
            var loaded = await new OrganizationSettingsRepository(db).GetByOrganizationIdAsync(_orgId);

            loaded.Should().NotBeNull();
            loaded!.JoinPolicy.Should().Be("invite_only");
            loaded.Description.Should().Be("Our org");
            loaded.DefaultOrgRole.Should().NotBeNull("GetByOrganizationIdAsync eager-loads the default org role navigation");
            loaded.DefaultOrgRole!.Id.Should().Be(_orgRoleId);
        }
    }

    [Fact]
    public async Task OrganizationSettings_GetByUnknownOrg_ReturnsNull()
    {
        await using var db = Db();

        var loaded = await new OrganizationSettingsRepository(db).GetByOrganizationIdAsync(999999);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationSettings_Update_PersistsChanges()
    {
        await using (var db = Db())
        {
            await new OrganizationSettingsRepository(db).AddAsync(new OrganizationSettings { OrganizationId = _orgId, JoinPolicy = "open" });
        }

        await using (var db = Db())
        {
            var repo = new OrganizationSettingsRepository(db);
            var settings = await repo.GetByOrganizationIdAsync(_orgId);
            settings!.JoinPolicy = "invite_only";
            await repo.UpdateAsync(settings);
        }

        await using (var db = Db())
        {
            var reloaded = await new OrganizationSettingsRepository(db).GetByOrganizationIdAsync(_orgId);
            reloaded!.JoinPolicy.Should().Be("invite_only");
        }
    }

    [Fact]
    public async Task WorkspaceSettings_AddThenGet_RoundTrips()
    {
        await using (var db = Db())
        {
            await new WorkspaceSettingsRepository(db).AddAsync(new WorkspaceSettings
            {
                WorkspaceId = _workspaceId,
                HighRiskThreshold = 0.8m,
                MediumRiskThreshold = 0.5m,
                Description = "ws settings",
                RiskScoringEnabled = true,
            });
        }

        await using (var db = Db())
        {
            var loaded = await new WorkspaceSettingsRepository(db).GetByWorkspaceIdAsync(_workspaceId);

            loaded.Should().NotBeNull();
            loaded!.HighRiskThreshold.Should().Be(0.8m);
            loaded.MediumRiskThreshold.Should().Be(0.5m);
            loaded.RiskScoringEnabled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task WorkspaceSettings_GetByUnknownWorkspace_ReturnsNull()
    {
        await using var db = Db();

        var loaded = await new WorkspaceSettingsRepository(db).GetByWorkspaceIdAsync(999999);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceSettings_Update_PersistsChanges()
    {
        await using (var db = Db())
        {
            await new WorkspaceSettingsRepository(db).AddAsync(new WorkspaceSettings { WorkspaceId = _workspaceId, HighRiskThreshold = 0.8m, MediumRiskThreshold = 0.5m, RiskScoringEnabled = false });
        }

        await using (var db = Db())
        {
            var repo = new WorkspaceSettingsRepository(db);
            var settings = await repo.GetByWorkspaceIdAsync(_workspaceId);
            settings!.RiskScoringEnabled = true;
            settings.HighRiskThreshold = 0.9m;
            await repo.UpdateAsync(settings);
        }

        await using (var db = Db())
        {
            var reloaded = await new WorkspaceSettingsRepository(db).GetByWorkspaceIdAsync(_workspaceId);
            reloaded!.RiskScoringEnabled.Should().BeTrue();
            reloaded.HighRiskThreshold.Should().Be(0.9m);
        }
    }
}
