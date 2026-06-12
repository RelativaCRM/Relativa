using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class UserSettingsRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth_settings_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<AuthDbContext> _opts = null!;
    private int _userId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();

        var user = new User { Email = "settings@test.com", FirstName = "Set", LastName = "Tings", Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuthDbContext Db() => new(_opts);

    [Fact]
    public async Task GetByUserIdAsync_NoSettings_ReturnsNull()
    {
        await using var db = Db();

        var result = await new UserSettingsRepository(db).GetByUserIdAsync(_userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ThenGet_RoundTripsLocale()
    {
        await using (var db = Db())
        {
            await new UserSettingsRepository(db).AddAsync(new UserSettings { UserId = _userId, Locale = "uk" });
        }

        await using (var db = Db())
        {
            var result = await new UserSettingsRepository(db).GetByUserIdAsync(_userId);

            result.Should().NotBeNull();
            result!.Locale.Should().Be("uk");
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsNewLocale()
    {
        await using (var db = Db())
        {
            await new UserSettingsRepository(db).AddAsync(new UserSettings { UserId = _userId, Locale = "en" });
        }

        await using (var db = Db())
        {
            var repo = new UserSettingsRepository(db);
            var settings = await repo.GetByUserIdAsync(_userId);
            settings!.Locale = "uk";
            await repo.UpdateAsync(settings);
        }

        await using (var db = Db())
        {
            var reloaded = await new UserSettingsRepository(db).GetByUserIdAsync(_userId);
            reloaded!.Locale.Should().Be("uk");
        }
    }
}
