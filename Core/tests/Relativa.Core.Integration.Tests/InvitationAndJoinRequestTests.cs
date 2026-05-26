using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class InvitationAndJoinRequestTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("inv_jr_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<RelativaDbContext> _opts = null!;

    private int _orgId;
    private int _userId1;
    private int _userId2;
    private int _orgRoleId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(db);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private RelativaDbContext Db() => new(_opts);

    private async Task SeedAsync(RelativaDbContext db)
    {
        var org = new Organization { Name = "Inv Org" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var u1 = new User { Email = "inviter@test.com", FirstName = "A", LastName = "B", Password = "x" };
        var u2 = new User { Email = "joiner@test.com",  FirstName = "C", LastName = "D", Password = "x" };
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();
        _userId1 = u1.Id;
        _userId2 = u2.Id;

        var role = new OrganizationRole { Name = "org_member", OrganizationId = _orgId, Priority = 5 };
        db.OrganizationRoles.Add(role);
        await db.SaveChangesAsync();
        _orgRoleId = role.Id;
    }

    private OrgInvitationRepository InvRepo() => new(Db());
    private JoinRequestRepository JrRepo()    => new(Db());


    [Fact]
    public async Task OrgInvitation_GetById_ReturnsInvitationWithOrgAndRole()
    {
        var inv = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = "a@b.com", OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var db = Db()) { db.OrganizationInvitations.Add(inv); await db.SaveChangesAsync(); }

        var result = await InvRepo().GetByIdAsync(inv.Id);

        result.Should().NotBeNull();
        result!.Organization.Should().NotBeNull();
        result.Role.Should().NotBeNull();
    }

    [Fact]
    public async Task OrgInvitation_GetByToken_Found_ReturnsInvitation()
    {
        var token = Guid.NewGuid().ToString();
        var inv = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = "token@b.com", OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = token,
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var db = Db()) { db.OrganizationInvitations.Add(inv); await db.SaveChangesAsync(); }

        var result = await InvRepo().GetByTokenAsync(token);

        result.Should().NotBeNull();
        result!.Token.Should().Be(token);
    }

    [Fact]
    public async Task OrgInvitation_GetByToken_NotFound_ReturnsNull()
    {
        var result = await InvRepo().GetByTokenAsync("no-such-token-xyz");
        result.Should().BeNull();
    }

    [Fact]
    public async Task OrgInvitation_GetByEmail_NormalizesAndFiltersNonPending()
    {
        var email = $"mixed.{Guid.NewGuid():N}@org.com";
        var invPending  = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = email, OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        var invAccepted = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = email, OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Accepted", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var db = Db())
        {
            db.OrganizationInvitations.AddRange(invPending, invAccepted);
            await db.SaveChangesAsync();
        }

        var result = await InvRepo().GetByEmailAsync($"  {email.ToUpperInvariant()}  ");

        result.Should().HaveCount(1);
        result[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task OrgInvitation_GetByEmail_EmptyInput_ReturnsEmpty()
    {
        var result = await InvRepo().GetByEmailAsync("   ");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OrgInvitation_GetPendingByOrgAndEmail_MatchNormalized()
    {
        var email = $"pending.{Guid.NewGuid():N}@org.com";
        var inv = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = email, OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var db = Db()) { db.OrganizationInvitations.Add(inv); await db.SaveChangesAsync(); }

        var result = await InvRepo().GetPendingByOrgAndEmailAsync(_orgId, $"  {email.ToUpperInvariant()}  ");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task OrgInvitation_AddAsync_PersistsInvitation()
    {
        var inv = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = "new@inv.com", OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await InvRepo().AddAsync(inv);

        inv.Id.Should().BeGreaterThan(0);
        (await Db().OrganizationInvitations.FindAsync(inv.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task OrgInvitation_UpdateAsync_ChangesStatus()
    {
        var inv = new OrganizationInvitation
        {
            OrganizationId = _orgId, Email = "update@inv.com", OrgRoleId = _orgRoleId,
            InvitedByUserId = _userId1, Token = Guid.NewGuid().ToString(),
            Status = "Pending", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var db = Db()) { db.OrganizationInvitations.Add(inv); await db.SaveChangesAsync(); }

        await using (var db = Db())
        {
            var stored = await db.OrganizationInvitations.FindAsync(inv.Id);
            stored!.Status = "Accepted";
            await new OrgInvitationRepository(db).UpdateAsync(stored);
        }

        var updated = await Db().OrganizationInvitations.FindAsync(inv.Id);
        updated!.Status.Should().Be("Accepted");
    }


    [Fact]
    public async Task JoinRequest_GetByOrganizationId_ReturnsPendingOnly()
    {
        var jr1 = new OrganizationJoinRequest
        {
            UserId = _userId1, OrganizationId = _orgId, Status = "Pending", CreatedAt = DateTime.UtcNow
        };
        var jr2 = new OrganizationJoinRequest
        {
            UserId = _userId2, OrganizationId = _orgId, Status = "Approved", CreatedAt = DateTime.UtcNow
        };
        await using (var db = Db()) { db.OrganizationJoinRequests.AddRange(jr1, jr2); await db.SaveChangesAsync(); }

        var result = await JrRepo().GetByOrganizationIdAsync(_orgId);

        result.Should().OnlyContain(r => r.Status == "Pending");
    }

    [Fact]
    public async Task JoinRequest_GetByUserId_ReturnsAllStatuses()
    {
        var uid = _userId1;
        var jr1 = new OrganizationJoinRequest
        {
            UserId = uid, OrganizationId = _orgId, Status = "Pending", CreatedAt = DateTime.UtcNow
        };
        var jr2 = new OrganizationJoinRequest
        {
            UserId = uid, OrganizationId = _orgId, Status = "Rejected", CreatedAt = DateTime.UtcNow
        };
        await using (var db = Db()) { db.OrganizationJoinRequests.AddRange(jr1, jr2); await db.SaveChangesAsync(); }

        var result = await JrRepo().GetByUserIdAsync(uid);

        result.Count(r => r.UserId == uid && r.Status == "Pending").Should().BeGreaterThanOrEqualTo(1);
        result.Count(r => r.UserId == uid && r.Status == "Rejected").Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task JoinRequest_GetPendingAsync_Found()
    {
        var jr = new OrganizationJoinRequest
        {
            UserId = _userId1, OrganizationId = _orgId, Status = "Pending", CreatedAt = DateTime.UtcNow
        };
        await using (var db = Db()) { db.OrganizationJoinRequests.Add(jr); await db.SaveChangesAsync(); }

        var result = await JrRepo().GetPendingAsync(_userId1, _orgId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task JoinRequest_GetPendingAsync_NotFound_ReturnsNull()
    {
        var result = await JrRepo().GetPendingAsync(99999, 99999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task JoinRequest_AddAsync_PersistsRequest()
    {
        var jr = new OrganizationJoinRequest
        {
            UserId = _userId2, OrganizationId = _orgId, Status = "Pending", CreatedAt = DateTime.UtcNow
        };

        await JrRepo().AddAsync(jr);

        jr.Id.Should().BeGreaterThan(0);
        (await Db().OrganizationJoinRequests.FindAsync(jr.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task JoinRequest_UpdateAsync_UpdatesStatus()
    {
        var jr = new OrganizationJoinRequest
        {
            UserId = _userId2, OrganizationId = _orgId, Status = "Pending", CreatedAt = DateTime.UtcNow
        };
        await using (var db = Db()) { db.OrganizationJoinRequests.Add(jr); await db.SaveChangesAsync(); }

        await using (var db = Db())
        {
            var stored = await db.OrganizationJoinRequests.FindAsync(jr.Id);
            stored!.Status = "Approved";
            await new JoinRequestRepository(db).UpdateAsync(stored);
        }

        var updated = await Db().OrganizationJoinRequests.FindAsync(jr.Id);
        updated!.Status.Should().Be("Approved");
    }
}
