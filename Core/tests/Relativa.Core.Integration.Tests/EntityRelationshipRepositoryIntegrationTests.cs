using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class EntityRelationshipRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("entity_rel_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private DbContextOptions<RelativaDbContext> _opts = null!;
    private int _dealTypeId, _clientTypeId, _relTypeId, _deal1, _client1, _client2, _wsId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<RelativaDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();

        var org = new Organization { Name = "Org" };
        db.Organizations.Add(org);
        var user = new User { Email = "u@test.com", FirstName = "U", LastName = "Ser", Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ws = new Workspace { Name = "WS", OrganizationId = org.Id, CreatedByUserId = user.Id };
        db.Workspaces.Add(ws);
        var dealType = new EntityType { Name = "deal", IsStandalone = true };
        var clientType = new EntityType { Name = "client", IsStandalone = true };
        db.EntityTypes.AddRange(dealType, clientType);
        await db.SaveChangesAsync();
        _wsId = ws.Id; _dealTypeId = dealType.Id; _clientTypeId = clientType.Id;

        var relType = new EntityRelationshipType
        {
            Name = "deal_client",
            SourceEntityTypeId = dealType.Id,
            TargetEntityTypeId = clientType.Id,
            RelationshipCardinality = RelationshipCardinality.ManyToOne,
        };
        db.EntityRelationshipTypes.Add(relType);
        await db.SaveChangesAsync();
        _relTypeId = relType.Id;

        var deal1 = new Entity { EntityTypeId = dealType.Id, CreatedByUserId = user.Id };
        var client1 = new Entity { EntityTypeId = clientType.Id, CreatedByUserId = user.Id };
        var client2 = new Entity { EntityTypeId = clientType.Id, CreatedByUserId = user.Id };
        db.Entities.AddRange(deal1, client1, client2);
        await db.SaveChangesAsync();
        _deal1 = deal1.Id; _client1 = client1.Id; _client2 = client2.Id;

        db.Set<EntityWorkspace>().AddRange(
            new EntityWorkspace { EntityId = deal1.Id, WorkspaceId = ws.Id },
            new EntityWorkspace { EntityId = client1.Id, WorkspaceId = ws.Id },
            new EntityWorkspace { EntityId = client2.Id, WorkspaceId = ws.Id });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private RelativaDbContext Db() => new(_opts);
    private EntityRepository Sut(RelativaDbContext db) => new(db);

    private async Task<int> AddRelationshipAsync(int sourceId, int targetId)
    {
        await using var db = Db();
        var rel = await Sut(db).AddRelationshipAsync(new EntityRelationship
        {
            SourceEntityId = sourceId, TargetEntityId = targetId, RelationshipTypeId = _relTypeId,
        });
        return rel.Id;
    }

    [Fact]
    public async Task GetEntityTypeByIdAsync_ReturnsType()
    {
        await using var db = Db();
        (await Sut(db).GetEntityTypeByIdAsync(_dealTypeId))!.Name.Should().Be("deal");
    }

    [Fact]
    public async Task GetOutgoingRelationshipTypesAsync_ReturnsOnlyTypesWhoseSourceMatches()
    {
        await using var db = Db();
        var repo = Sut(db);

        (await repo.GetOutgoingRelationshipTypesAsync(_dealTypeId)).Should().ContainSingle(rt => rt.Name == "deal_client");
        (await repo.GetOutgoingRelationshipTypesAsync(_clientTypeId)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetRelationshipTypeByIdAsync_IncludesSourceAndTargetEntityTypes()
    {
        await using var db = Db();
        var relType = await Sut(db).GetRelationshipTypeByIdAsync(_relTypeId);

        relType.Should().NotBeNull();
        relType!.SourceEntityType.Name.Should().Be("deal");
        relType.TargetEntityType.Name.Should().Be("client");
    }

    [Fact]
    public async Task AddRelationship_ThenGetById_HydratesAllNavigations()
    {
        var relId = await AddRelationshipAsync(_deal1, _client1);

        await using var db = Db();
        var rel = await Sut(db).GetRelationshipByIdAsync(relId);

        rel.Should().NotBeNull();
        rel!.RelationshipType.SourceEntityType.Name.Should().Be("deal");
        rel.RelationshipType.TargetEntityType.Name.Should().Be("client");
        rel.SourceEntity.EntityWorkspaces.Should().Contain(ew => ew.WorkspaceId == _wsId,
            "GetRelationshipByIdAsync must include the source entity's workspace links for the in-workspace guard");
        rel.TargetEntity.Id.Should().Be(_client1);
    }

    [Fact]
    public async Task CountRelationships_BySourceAndByTarget_AreAccurate()
    {
        await AddRelationshipAsync(_deal1, _client1);

        await using var db = Db();
        var repo = Sut(db);
        (await repo.CountRelationshipsBySourceAsync(_deal1, _relTypeId)).Should().Be(1);
        (await repo.CountRelationshipsByTargetAsync(_client1, _relTypeId)).Should().Be(1);
        (await repo.CountRelationshipsByTargetAsync(_client2, _relTypeId)).Should().Be(0);
    }

    [Fact]
    public async Task UpdateRelationshipTarget_RepointsTheLink()
    {
        var relId = await AddRelationshipAsync(_deal1, _client1);

        await using (var db = Db())
        {
            await Sut(db).UpdateRelationshipTargetAsync(relId, _client2);
        }

        await using var verify = Db();
        (await Sut(verify).GetRelationshipByIdAsync(relId))!.TargetEntityId.Should().Be(_client2);
    }

    [Fact]
    public async Task UpdateRelationshipSource_RepointsTheLink()
    {
        var relId = await AddRelationshipAsync(_deal1, _client1);
        await using var db = Db();
        var user = await db.Users.FirstAsync();
        var deal2 = new Entity { EntityTypeId = _dealTypeId, CreatedByUserId = user.Id };
        db.Entities.Add(deal2);
        await db.SaveChangesAsync();

        await Sut(db).UpdateRelationshipSourceAsync(relId, deal2.Id);

        await using var verify = Db();
        (await Sut(verify).GetRelationshipByIdAsync(relId))!.SourceEntityId.Should().Be(deal2.Id);
    }

    [Fact]
    public async Task RemoveRelationship_DeletesIt()
    {
        var relId = await AddRelationshipAsync(_deal1, _client1);

        await using (var db = Db())
        {
            await Sut(db).RemoveRelationshipAsync(relId);
        }

        await using var verify = Db();
        (await Sut(verify).GetRelationshipByIdAsync(relId)).Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_SetsEntityIsArchivedTrue()
    {
        await using (var db = Db())
        {
            await Sut(db).ArchiveAsync(_deal1);
        }

        await using var verify = Db();
        (await verify.Entities.AsNoTracking().SingleAsync(e => e.Id == _deal1)).IsArchived.Should().BeTrue();
    }
}
