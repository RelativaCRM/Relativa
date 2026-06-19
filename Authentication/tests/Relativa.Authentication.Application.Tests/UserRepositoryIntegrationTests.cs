using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class UserRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth_user_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<AuthDbContext> _opts = null!;

    private int _activeUserId;
    private int _archivedUserId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(db);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuthDbContext Db() => new(_opts);

    private UserRepository Sut() => new(Db());

    private async Task SeedAsync(AuthDbContext db)
    {
        var active = new User
        {
            Email      = "active@test.com",
            FirstName  = "Active",
            LastName   = "User",
            Password   = "hashed",
            IsArchived = false,
        };
        var archived = new User
        {
            Email      = "archived@test.com",
            FirstName  = "Archived",
            LastName   = "User",
            Password   = "hashed",
            IsArchived = true,
        };
        db.Users.AddRange(active, archived);
        await db.SaveChangesAsync();
        _activeUserId   = active.Id;
        _archivedUserId = archived.Id;
    }

    [Fact]
    public async Task GetById_ActiveUser_Returns()
    {
        var result = await Sut().GetByIdAsync(_activeUserId);
        result.Should().NotBeNull();
        result!.Email.Should().Be("active@test.com");
    }

    [Fact]
    public async Task GetById_ArchivedUser_ReturnsNull()
    {
        var result = await Sut().GetByIdAsync(_archivedUserId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmail_ActiveUser_Returns()
    {
        var result = await Sut().GetByEmailAsync("active@test.com");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByEmail_ArchivedUser_ReturnsNull()
    {
        var result = await Sut().GetByEmailAsync("archived@test.com");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ActiveEmail_ReturnsTrue()
    {
        var result = await Sut().ExistsAsync("active@test.com");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ArchivedEmail_ReturnsFalse()
    {
        var result = await Sut().ExistsAsync("archived@test.com");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_PersistsUser()
    {
        await using var db = Db();
        var repo = new UserRepository(db);
        var user = new User
        {
            Email      = "new@test.com",
            FirstName  = "New",
            LastName   = "User",
            Password   = "hashed",
            IsArchived = false,
        };

        await repo.AddAsync(user);

        user.Id.Should().BeGreaterThan(0);
        (await Db().Users.FindAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        await using var db = Db();
        var user = await db.Users.FindAsync(_activeUserId);
        user!.FirstName = "Updated";
        await new UserRepository(db).UpdateAsync(user);

        var refreshed = await Db().Users.FindAsync(_activeUserId);
        refreshed!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task GetByResetToken_ValidToken_ReturnsUser()
    {
        var token   = "valid-reset-token-123";
        var expires = DateTime.UtcNow.AddHours(1);

        await using (var db = Db())
        {
            var user = await db.Users.FindAsync(_activeUserId);
            user!.PasswordResetToken        = token;
            user.PasswordResetTokenExpiresAt = expires;
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetByResetTokenAsync(token);

        result.Should().NotBeNull();
        result!.Id.Should().Be(_activeUserId);
    }

    [Fact]
    public async Task GetByResetToken_ExpiredToken_ReturnsNull()
    {
        var token   = "expired-reset-token-456";
        var expires = DateTime.UtcNow.AddHours(-1);

        await using (var db = Db())
        {
            var user = await db.Users.FindAsync(_activeUserId);
            user!.PasswordResetToken        = token;
            user.PasswordResetTokenExpiresAt = expires;
            await db.SaveChangesAsync();
        }

        var result = await Sut().GetByResetTokenAsync(token);

        result.Should().BeNull();
    }
}
