using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class UserRepositoryAuthFeaturesIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth_features_test")
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

        var user = new User { Email = "primary@test.com", FirstName = "Prim", LastName = "Ary", Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuthDbContext Db() => new(_opts);
    private UserRepository Sut(AuthDbContext db) => new(db);

    [Fact]
    public async Task AddExternalLogin_ThenGetByExternalLogin_RoundTrips()
    {
        await using (var db = Db())
        {
            await Sut(db).AddExternalLoginAsync(new UserExternalLogin { UserId = _userId, Provider = "google", Subject = "sub-1", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            var user = await Sut(db).GetByExternalLoginAsync("google", "sub-1");
            user.Should().NotBeNull();
            user!.Id.Should().Be(_userId);
        }
    }

    [Fact]
    public async Task GetByExternalLogin_Unknown_ReturnsNull()
    {
        await using var db = Db();

        (await Sut(db).GetByExternalLoginAsync("google", "does-not-exist")).Should().BeNull();
    }

    [Fact]
    public async Task ReleaseExternalIdentifiers_RemovesLoginsAndEmails()
    {
        await using (var db = Db())
        {
            var repo = Sut(db);
            await repo.AddExternalLoginAsync(new UserExternalLogin { UserId = _userId, Provider = "google", Subject = "sub-2", CreatedAt = DateTime.UtcNow });
            await repo.AddEmailAsync(new UserEmail { UserId = _userId, Address = "extra@test.com", IsVerified = true, Source = "manual", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            await Sut(db).ReleaseExternalIdentifiersAsync(_userId);
        }

        await using (var db = Db())
        {
            (await Sut(db).GetByExternalLoginAsync("google", "sub-2")).Should().BeNull();
            (await Sut(db).GetEmailsAsync(_userId)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ReplaceBackupCodes_ThenGetActive_ReturnsStoredCodes()
    {
        await using (var db = Db())
        {
            await Sut(db).ReplaceBackupCodesAsync(_userId,
            [
                new UserBackupCode { UserId = _userId, CodeHash = "h1", CreatedAt = DateTime.UtcNow },
                new UserBackupCode { UserId = _userId, CodeHash = "h2", CreatedAt = DateTime.UtcNow },
            ]);
        }

        await using (var db = Db())
        {
            (await Sut(db).GetActiveBackupCodesAsync(_userId)).Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task ConsumeBackupCode_ExcludesItFromActive()
    {
        await using (var db = Db())
        {
            await Sut(db).ReplaceBackupCodesAsync(_userId,
            [
                new UserBackupCode { UserId = _userId, CodeHash = "use-me", CreatedAt = DateTime.UtcNow },
                new UserBackupCode { UserId = _userId, CodeHash = "keep-me", CreatedAt = DateTime.UtcNow },
            ]);
        }

        await using (var db = Db())
        {
            var repo = Sut(db);
            var code = (await repo.GetActiveBackupCodesAsync(_userId)).First(c => c.CodeHash == "use-me");
            await repo.ConsumeBackupCodeAsync(code);
        }

        await using (var db = Db())
        {
            var active = await Sut(db).GetActiveBackupCodesAsync(_userId);
            active.Should().ContainSingle().Which.CodeHash.Should().Be("keep-me");
        }
    }

    [Fact]
    public async Task ReplaceBackupCodes_DiscardsPreviousCodes()
    {
        await using (var db = Db())
        {
            await Sut(db).ReplaceBackupCodesAsync(_userId, [new UserBackupCode { UserId = _userId, CodeHash = "old", CreatedAt = DateTime.UtcNow }]);
        }

        await using (var db = Db())
        {
            await Sut(db).ReplaceBackupCodesAsync(_userId, [new UserBackupCode { UserId = _userId, CodeHash = "new", CreatedAt = DateTime.UtcNow }]);
        }

        await using (var db = Db())
        {
            var active = await Sut(db).GetActiveBackupCodesAsync(_userId);
            active.Should().ContainSingle().Which.CodeHash.Should().Be("new");
        }
    }

    [Fact]
    public async Task AddEmail_GetEmail_And_GetEmails_Work()
    {
        await using (var db = Db())
        {
            await Sut(db).AddEmailAsync(new UserEmail { UserId = _userId, Address = "second@test.com", IsVerified = false, Source = "manual", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            var repo = Sut(db);
            (await repo.GetEmailsAsync(_userId)).Should().ContainSingle(e => e.Address == "second@test.com");
            (await repo.GetEmailAsync(_userId, "second@test.com")).Should().NotBeNull();
            (await repo.GetEmailAsync(_userId, "missing@test.com")).Should().BeNull();
        }
    }

    [Fact]
    public async Task EmailExistsAnywhere_TrueForPrimaryAndSecondaryAddresses()
    {
        await using (var db = Db())
        {
            await Sut(db).AddEmailAsync(new UserEmail { UserId = _userId, Address = "alias@test.com", IsVerified = true, Source = "manual", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            var repo = Sut(db);
            (await repo.EmailExistsAnywhereAsync("primary@test.com")).Should().BeTrue("the primary user email counts");
            (await repo.EmailExistsAnywhereAsync("alias@test.com")).Should().BeTrue("a secondary UserEmail also counts");
            (await repo.EmailExistsAnywhereAsync("nobody@test.com")).Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateEmail_PersistsVerification()
    {
        await using (var db = Db())
        {
            await Sut(db).AddEmailAsync(new UserEmail { UserId = _userId, Address = "verifyme@test.com", IsVerified = false, Source = "manual", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            var repo = Sut(db);
            var email = await repo.GetEmailAsync(_userId, "verifyme@test.com");
            email!.IsVerified = true;
            await repo.UpdateEmailAsync(email);
        }

        await using (var db = Db())
        {
            (await Sut(db).GetEmailAsync(_userId, "verifyme@test.com"))!.IsVerified.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RemoveEmail_DeletesIt()
    {
        await using (var db = Db())
        {
            await Sut(db).AddEmailAsync(new UserEmail { UserId = _userId, Address = "removeme@test.com", IsVerified = true, Source = "manual", CreatedAt = DateTime.UtcNow });
        }

        await using (var db = Db())
        {
            var repo = Sut(db);
            var email = await repo.GetEmailAsync(_userId, "removeme@test.com");
            await repo.RemoveEmailAsync(email!);
        }

        await using (var db = Db())
        {
            (await Sut(db).GetEmailAsync(_userId, "removeme@test.com")).Should().BeNull();
        }
    }
}
