using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class EntityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relativa_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private RelativaDbContext _db = null!;
    private EntityRepository _repo = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new RelativaDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        await SeedAsync();

        _repo = new EntityRepository(_db);
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

        var user = new User
        {
            FirstName  = "Test",
            LastName   = "User",
            Email      = "test@relativa.com",
            Password   = "hashed",
            CreatedAt  = DateTime.UtcNow,
            IsArchived = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var clientType = new EntityType { Name = "client" };
        var dealType   = new EntityType { Name = "deal" };
        _db.EntityTypes.AddRange(clientType, dealType);
        await _db.SaveChangesAsync();

        var firstName  = new Property { Name = "first_name",    DataType = PropertyDataType.String };
        var lastName   = new Property { Name = "last_name",     DataType = PropertyDataType.String };
        var dealValue  = new Property { Name = "deal_value",    DataType = PropertyDataType.Decimal };
        var closeDate  = new Property { Name = "expected_close",DataType = PropertyDataType.Date };
        _db.Properties.AddRange(firstName, lastName, dealValue, closeDate);
        await _db.SaveChangesAsync();

        _db.EntityTypeProperties.AddRange(
            new EntityTypeProperty { EntityTypeId = clientType.Id, PropertyId = firstName.Id, IsRequired = true },
            new EntityTypeProperty { EntityTypeId = clientType.Id, PropertyId = lastName.Id,  IsRequired = true },
            new EntityTypeProperty { EntityTypeId = dealType.Id,   PropertyId = dealValue.Id, IsRequired = false },
            new EntityTypeProperty { EntityTypeId = dealType.Id,   PropertyId = closeDate.Id, IsRequired = false }
        );

        var workspace = new Workspace { Name = "Test WS",  IsArchived = false, CreatedByUserId = user.Id, OrganizationId = org.Id };
        var otherWs   = new Workspace { Name = "Other WS", IsArchived = false, CreatedByUserId = user.Id, OrganizationId = org.Id };
        _db.Workspaces.AddRange(workspace, otherWs);

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_ClientEntity_CreatesEntityWorkspaceRow()
    {
        var workspaceId = _db.Workspaces.Single(w => w.Name == "Test WS").Id;
        var typeProps   = await _repo.GetTypePropertiesAsync(
            _db.EntityTypes.Single(t => t.Name == "client").Id);

        var entity = new Entity
        {
            EntityTypeId = _db.EntityTypes.Single(t => t.Name == "client").Id,
            IsArchived   = false
        };
        var pvs = new List<EntityPropertyValue>
        {
            new() { PropertyId = typeProps.Single(p => p.Property.Name == "first_name").PropertyId, ValueString = "Ivan" },
            new() { PropertyId = typeProps.Single(p => p.Property.Name == "last_name").PropertyId,  ValueString = "Franko" }
        };

        var created = await _repo.CreateAsync(entity, pvs, workspaceId);

        var link = await _db.EntityWorkspaces
            .FirstOrDefaultAsync(ew => ew.EntityId == created.Id && ew.WorkspaceId == workspaceId);

        link.Should().NotBeNull("entity_workspace FK row must exist after CreateAsync");
    }

    [Fact]
    public async Task CreateAsync_DealEntity_PersistsDecimalAndDatePropertyValues()
    {
        var workspaceId = _db.Workspaces.Single(w => w.Name == "Test WS").Id;
        var dealTypeId  = _db.EntityTypes.Single(t => t.Name == "deal").Id;
        var typeProps   = await _repo.GetTypePropertiesAsync(dealTypeId);

        var entity = new Entity { EntityTypeId = dealTypeId, IsArchived = false };
        var pvs = new List<EntityPropertyValue>
        {
            new()
            {
                PropertyId   = typeProps.Single(p => p.Property.Name == "deal_value").PropertyId,
                ValueDecimal = 9500.75m
            },
            new()
            {
                PropertyId = typeProps.Single(p => p.Property.Name == "expected_close").PropertyId,
                ValueDate  = new DateOnly(2026, 6, 30)
            }
        };

        var created = await _repo.CreateAsync(entity, pvs, workspaceId);

        var stored = await _db.EntityPropertyValues
            .Where(epv => epv.EntityId == created.Id)
            .ToListAsync();

        stored.Should().HaveCount(2);
        stored.Single(v => v.ValueDecimal.HasValue).ValueDecimal.Should().Be(9500.75m);
        stored.Single(v => v.ValueDate.HasValue).ValueDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task GetByIdInWorkspaceAsync_EntityInDifferentWorkspace_ReturnsNull()
    {
        var workspaceId = _db.Workspaces.Single(w => w.Name == "Test WS").Id;
        var otherWsId   = _db.Workspaces.Single(w => w.Name == "Other WS").Id;
        var typeProps   = await _repo.GetTypePropertiesAsync(
            _db.EntityTypes.Single(t => t.Name == "client").Id);

        var entity = new Entity
        {
            EntityTypeId = _db.EntityTypes.Single(t => t.Name == "client").Id,
            IsArchived   = false
        };
        var pvs = new List<EntityPropertyValue>
        {
            new() { PropertyId = typeProps.Single(p => p.Property.Name == "first_name").PropertyId, ValueString = "Test" },
            new() { PropertyId = typeProps.Single(p => p.Property.Name == "last_name").PropertyId,  ValueString = "User" }
        };
        var created = await _repo.CreateAsync(entity, pvs, workspaceId);

        var result = await _repo.GetByIdInWorkspaceAsync(created.Id, otherWsId);

        result.Should().BeNull("entity belongs to a different workspace — isolation must be enforced");
    }

    [Fact]
    public async Task CreateAsync_OnTransactionFailure_DoesNotLeaveOrphanedEntityRow()
    {
        var workspaceId = _db.Workspaces.Single(w => w.Name == "Test WS").Id;
        var clientTypeId = _db.EntityTypes.Single(t => t.Name == "client").Id;
        var typeProps    = await _repo.GetTypePropertiesAsync(clientTypeId);

        var entityCountBefore = await _db.Entities.CountAsync(e => e.EntityTypeId == clientTypeId);

        var pvWithInvalidPropertyId = new List<EntityPropertyValue>
        {
            new() { PropertyId = 999999, ValueString = "orphan" }
        };

        var entity = new Entity { EntityTypeId = clientTypeId, IsArchived = false };

        await _repo.Invoking(r => r.CreateAsync(entity, pvWithInvalidPropertyId, workspaceId))
            .Should().ThrowAsync<Exception>("FK violation on property_id=999999 must roll back the transaction");

        var entityCountAfter = await _db.Entities.CountAsync(e => e.EntityTypeId == clientTypeId);
        entityCountAfter.Should().Be(entityCountBefore, "transaction rollback must remove the orphaned entity row");
    }
}
