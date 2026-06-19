using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Persistence.Entities;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class EntityFilterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("filter_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<RelativaDbContext> _opts = null!;

    private int _userId;
    private int _wsId;
    private int _typeId;
    private int _propStringId;
    private int _propIntId;
    private int _propDecimalId;
    private int _propBoolId;
    private int _propDateId;
    private int _entity1Id;
    private int _entity2Id;
    private int _entity3Id;

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

    private EntityRepository Sut() => new(Db());

    private async Task SeedAsync(RelativaDbContext db)
    {
        var org = new Organization { Name = "Filter Org" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var user = new User { Email = "filter@test.com", FirstName = "Filter", LastName = "User", Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;

        var ws = new Workspace { Name = "Filter WS", OrganizationId = org.Id, CreatedByUserId = _userId };
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync();
        _wsId = ws.Id;

        var wsRole = new WorkspaceRole { Name = "ws_member", Priority = 10 };
        db.WorkspaceRoles.Add(wsRole);
        await db.SaveChangesAsync();
        db.UserRoleWorkspaces.Add(new UserRoleWorkspace
        {
            UserId = _userId, WorkspaceId = _wsId, WsRoleId = wsRole.Id, JoinedAt = DateTime.UtcNow
        });

        var entityType = new EntityType { Name = "filter_type" };
        db.EntityTypes.Add(entityType);
        await db.SaveChangesAsync();
        _typeId = entityType.Id;

        var propStr     = new Property { Name = "str_field",  DataType = PropertyDataType.String };
        var propInt     = new Property { Name = "int_field",  DataType = PropertyDataType.Int };
        var propDec     = new Property { Name = "dec_field",  DataType = PropertyDataType.Decimal };
        var propBool    = new Property { Name = "bool_field", DataType = PropertyDataType.Bool };
        var propDate    = new Property { Name = "date_field", DataType = PropertyDataType.Date };
        db.Properties.AddRange(propStr, propInt, propDec, propBool, propDate);
        await db.SaveChangesAsync();
        _propStringId  = propStr.Id;
        _propIntId     = propInt.Id;
        _propDecimalId = propDec.Id;
        _propBoolId    = propBool.Id;
        _propDateId    = propDate.Id;

        db.EntityTypeProperties.AddRange(
            new EntityTypeProperty { EntityTypeId = _typeId, PropertyId = _propStringId },
            new EntityTypeProperty { EntityTypeId = _typeId, PropertyId = _propIntId },
            new EntityTypeProperty { EntityTypeId = _typeId, PropertyId = _propDecimalId },
            new EntityTypeProperty { EntityTypeId = _typeId, PropertyId = _propBoolId },
            new EntityTypeProperty { EntityTypeId = _typeId, PropertyId = _propDateId }
        );

        var e1 = new Entity { EntityTypeId = _typeId, CreatedByUserId = _userId };
        var e2 = new Entity { EntityTypeId = _typeId, CreatedByUserId = _userId };
        var e3 = new Entity { EntityTypeId = _typeId, CreatedByUserId = _userId };
        db.Entities.AddRange(e1, e2, e3);
        await db.SaveChangesAsync();
        _entity1Id = e1.Id;
        _entity2Id = e2.Id;
        _entity3Id = e3.Id;

        db.EntityWorkspaces.AddRange(
            new EntityWorkspace { EntityId = _entity1Id, WorkspaceId = _wsId },
            new EntityWorkspace { EntityId = _entity2Id, WorkspaceId = _wsId },
            new EntityWorkspace { EntityId = _entity3Id, WorkspaceId = _wsId }
        );

        db.EntityPropertyValues.AddRange(
            new EntityPropertyValue { EntityId = _entity1Id, PropertyId = _propStringId, ValueString = "Alpha" },
            new EntityPropertyValue { EntityId = _entity1Id, PropertyId = _propIntId,    ValueInt    = 10 },
            new EntityPropertyValue { EntityId = _entity1Id, PropertyId = _propDecimalId, ValueDecimal = 1.5m },
            new EntityPropertyValue { EntityId = _entity1Id, PropertyId = _propBoolId,   ValueBool   = true },
            new EntityPropertyValue { EntityId = _entity1Id, PropertyId = _propDateId,   ValueDate   = new DateOnly(2025, 1, 1) },

            new EntityPropertyValue { EntityId = _entity2Id, PropertyId = _propStringId, ValueString = "Beta" },
            new EntityPropertyValue { EntityId = _entity2Id, PropertyId = _propIntId,    ValueInt    = 20 },
            new EntityPropertyValue { EntityId = _entity2Id, PropertyId = _propDecimalId, ValueDecimal = 2.5m },
            new EntityPropertyValue { EntityId = _entity2Id, PropertyId = _propBoolId,   ValueBool   = false },
            new EntityPropertyValue { EntityId = _entity2Id, PropertyId = _propDateId,   ValueDate   = new DateOnly(2025, 6, 1) },

            new EntityPropertyValue { EntityId = _entity3Id, PropertyId = _propStringId, ValueString = "Alpha Extra" },
            new EntityPropertyValue { EntityId = _entity3Id, PropertyId = _propIntId,    ValueInt    = 30 },
            new EntityPropertyValue { EntityId = _entity3Id, PropertyId = _propDecimalId, ValueDecimal = 3.5m },
            new EntityPropertyValue { EntityId = _entity3Id, PropertyId = _propBoolId,   ValueBool   = true },
            new EntityPropertyValue { EntityId = _entity3Id, PropertyId = _propDateId,   ValueDate   = new DateOnly(2026, 1, 1) }
        );

        await db.SaveChangesAsync();
    }

    private (List<Entity> Items, int Total) GetAll(
        int? entityTypeId = null,
        string? search = null,
        int skip = 0,
        int take = 100,
        IReadOnlyList<ResolvedFilterCondition>? filters = null,
        IReadOnlyList<EntitySortField>? sort = null) =>
        Sut().GetByWorkspaceAsync(
            _wsId, _userId, requesterRolePriority: 999,
            entityTypeId, search, skip, take,
            filters ?? [],
            sort ?? []).GetAwaiter().GetResult();

    [Fact]
    public async Task NoFilters_ReturnsAllNonArchivedWorkspaceEntities()
    {
        var (items, total) = GetAll();

        total.Should().Be(3);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task EntityTypeFilter_ReturnsOnlyMatchingType()
    {
        var otherType = new EntityType { Name = "other_type" };
        await using (var db = Db())
        {
            db.EntityTypes.Add(otherType);
            await db.SaveChangesAsync();
        }

        var (items, total) = GetAll(entityTypeId: _typeId);

        total.Should().Be(3);
        items.Should().AllSatisfy(e => e.EntityTypeId.Should().Be(_typeId));
    }

    [Fact]
    public void SearchQuery_ReturnsEntitiesContainingString()
    {
        var (items, total) = GetAll(search: "Alpha");

        total.Should().Be(2);
        items.Should().AllSatisfy(e =>
            e.EntityPropertyValues.Any(v => v.ValueString != null && v.ValueString.Contains("Alpha"))
                .Should().BeTrue());
    }

    [Fact]
    public void StringFilter_Eq_MatchesExact()
    {
        var filters = new[] { new ResolvedFilterCondition(_propStringId, PropertyDataType.String, "eq", "Beta", null, null, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(1);
        items[0].Id.Should().Be(_entity2Id);
    }

    [Fact]
    public void StringFilter_Contains_MatchesPartial()
    {
        var filters = new[] { new ResolvedFilterCondition(_propStringId, PropertyDataType.String, "contains", "Alpha", null, null, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
    }

    [Fact]
    public void StringFilter_StartsWith_MatchesPrefix()
    {
        var filters = new[] { new ResolvedFilterCondition(_propStringId, PropertyDataType.String, "startswith", "Alpha", null, null, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Should().AllSatisfy(e =>
            e.EntityPropertyValues.Any(v => v.ValueString != null && v.ValueString.StartsWith("Alpha"))
                .Should().BeTrue());
    }

    [Fact]
    public void StringFilter_Neq_ExcludesExact()
    {
        var filters = new[] { new ResolvedFilterCondition(_propStringId, PropertyDataType.String, "neq", "Beta", null, null, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Should().NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void IntFilter_Gt_ReturnsMatchingRows()
    {
        var filters = new[] { new ResolvedFilterCondition(_propIntId, PropertyDataType.Int, "gt", null, 15, null, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Select(e => e.Id).Should().BeEquivalentTo(new[] { _entity2Id, _entity3Id });
    }

    [Fact]
    public void DecimalFilter_Lt_ReturnsMatchingRows()
    {
        var filters = new[] { new ResolvedFilterCondition(_propDecimalId, PropertyDataType.Decimal, "lt", null, null, 3.0m, null, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Select(e => e.Id).Should().BeEquivalentTo(new[] { _entity1Id, _entity2Id });
    }

    [Fact]
    public void BoolFilter_Eq_ReturnsMatchingRows()
    {
        var filters = new[] { new ResolvedFilterCondition(_propBoolId, PropertyDataType.Bool, "eq", null, null, null, true, null) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Select(e => e.Id).Should().BeEquivalentTo(new[] { _entity1Id, _entity3Id });
    }

    [Fact]
    public void DateFilter_Gte_ReturnsMatchingRows()
    {
        var filters = new[] { new ResolvedFilterCondition(_propDateId, PropertyDataType.Date, "gte", null, null, null, null, new DateOnly(2025, 6, 1)) };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(2);
        items.Select(e => e.Id).Should().BeEquivalentTo(new[] { _entity2Id, _entity3Id });
    }

    [Fact]
    public void MultipleFilters_AndLogic_NarrowsResult()
    {
        var filters = new ResolvedFilterCondition[]
        {
            new(_propStringId,  PropertyDataType.String,  "contains", "Alpha", null,  null,  null,  null),
            new(_propIntId,     PropertyDataType.Int,     "gt",       null,    25,    null,  null,  null),
        };
        var (items, total) = GetAll(filters: filters);

        total.Should().Be(1);
        items[0].Id.Should().Be(_entity3Id);
    }

    [Fact]
    public void Sort_Ascending_OrdersCorrectly()
    {
        var sort = new[] { new EntitySortField(_propStringId, "asc") };
        var (items, _) = GetAll(sort: sort);

        var values = items
            .Select(e => e.EntityPropertyValues.First(v => v.PropertyId == _propStringId).ValueString)
            .ToList();
        values.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_Descending_OrdersCorrectly()
    {
        var sort = new[] { new EntitySortField(_propStringId, "desc") };
        var (items, _) = GetAll(sort: sort);

        var values = items
            .Select(e => e.EntityPropertyValues.First(v => v.PropertyId == _propStringId).ValueString)
            .ToList();
        values.Should().BeInDescendingOrder();
    }

    [Fact]
    public void Pagination_SkipAndTake_ReturnsCorrectSlice()
    {
        var (page1, total) = GetAll(skip: 0, take: 2);
        var (page2, _)     = GetAll(skip: 2, take: 2);

        total.Should().Be(3);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(1);
        page1.Select(e => e.Id).Should().NotIntersectWith(page2.Select(e => e.Id));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesAllPropertyValues()
    {
        await using var db = Db();
        var repo   = new EntityRepository(db);
        var entity = await db.Entities.FindAsync(_entity1Id);

        var newValues = new List<EntityPropertyValue>
        {
            new() { PropertyId = _propStringId, ValueString = "Replaced" },
        };

        await repo.UpdateAsync(entity!, newValues);

        var stored = await Db().EntityPropertyValues
            .Where(v => v.EntityId == _entity1Id)
            .ToListAsync();

        stored.Should().HaveCount(1);
        stored[0].ValueString.Should().Be("Replaced");
    }

    [Fact]
    public async Task EntityTypeRepository_GetAllWithPropertiesAsync_ReturnsTypesWithProperties()
    {
        var repo   = new EntityTypeRepository(Db());
        var result = await repo.GetAllWithPropertiesAsync();

        result.Should().NotBeEmpty();
        var filterType = result.FirstOrDefault(t => t.Id == _typeId);
        filterType.Should().NotBeNull();
        filterType!.EntityTypeProperties.Should().HaveCount(5);
    }

    private ResolvedFilterCondition IntF(string op, int v) => new(_propIntId, PropertyDataType.Int, op, null, v, null, null, null);
    private ResolvedFilterCondition DecF(string op, decimal v) => new(_propDecimalId, PropertyDataType.Decimal, op, null, null, v, null, null);
    private ResolvedFilterCondition BoolF(string op, bool v) => new(_propBoolId, PropertyDataType.Bool, op, null, null, null, v, null);
    private ResolvedFilterCondition DateF(string op, DateOnly v) => new(_propDateId, PropertyDataType.Date, op, null, null, null, null, v);

    [Fact]
    public void IntFilter_Eq_MatchesValue()
    {
        var (items, _) = GetAll(filters: [IntF("eq", 20)]);
        items.Should().ContainSingle().Which.Id.Should().Be(_entity2Id);
    }

    [Fact]
    public void IntFilter_Neq_ExcludesValue()
    {
        var (items, _) = GetAll(filters: [IntF("neq", 20)]);
        items.Should().NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void IntFilter_Lt_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [IntF("lt", 25)]);
        items.Should().Contain(e => e.Id == _entity2Id).And.NotContain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void IntFilter_Gte_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [IntF("gte", 20)]);
        items.Should().Contain(e => e.Id == _entity2Id).And.Contain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void IntFilter_Lte_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [IntF("lte", 20)]);
        items.Should().Contain(e => e.Id == _entity2Id).And.NotContain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void DecimalFilter_Eq_MatchesValue()
    {
        var (items, _) = GetAll(filters: [DecF("eq", 2.5m)]);
        items.Should().ContainSingle().Which.Id.Should().Be(_entity2Id);
    }

    [Fact]
    public void DecimalFilter_Neq_ExcludesValue()
    {
        var (items, _) = GetAll(filters: [DecF("neq", 2.5m)]);
        items.Should().NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void DecimalFilter_Gt_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DecF("gt", 2.5m)]);
        items.Should().Contain(e => e.Id == _entity3Id).And.NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void DecimalFilter_Gte_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DecF("gte", 2.5m)]);
        items.Should().Contain(e => e.Id == _entity2Id).And.Contain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void DecimalFilter_Lte_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DecF("lte", 2.5m)]);
        items.Should().Contain(e => e.Id == _entity2Id).And.NotContain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void BoolFilter_Neq_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [BoolF("neq", true)]);
        items.Should().Contain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void DateFilter_Eq_MatchesValue()
    {
        var (items, _) = GetAll(filters: [DateF("eq", new DateOnly(2025, 6, 1))]);
        items.Should().ContainSingle().Which.Id.Should().Be(_entity2Id);
    }

    [Fact]
    public void DateFilter_Neq_ExcludesValue()
    {
        var (items, _) = GetAll(filters: [DateF("neq", new DateOnly(2025, 6, 1))]);
        items.Should().NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void DateFilter_Gt_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DateF("gt", new DateOnly(2025, 6, 1))]);
        items.Should().Contain(e => e.Id == _entity3Id).And.NotContain(e => e.Id == _entity2Id);
    }

    [Fact]
    public void DateFilter_Lt_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DateF("lt", new DateOnly(2025, 6, 1))]);
        items.Should().NotContain(e => e.Id == _entity2Id).And.NotContain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void DateFilter_Lte_ReturnsMatching()
    {
        var (items, _) = GetAll(filters: [DateF("lte", new DateOnly(2025, 6, 1))]);
        items.Should().Contain(e => e.Id == _entity2Id).And.NotContain(e => e.Id == _entity3Id);
    }

    [Fact]
    public void MultiFieldSort_AppliesThenBy()
    {
        var sort = new[]
        {
            new EntitySortField(_propBoolId, "asc"),
            new EntitySortField(_propStringId, "desc"),
        };
        var (items, total) = GetAll(sort: sort);

        total.Should().Be(3);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExcludeLinkedSourceRelType_OmitsLinkedEntities()
    {
        int relTypeId;
        await using (var db = Db())
        {
            var rt = new EntityRelationshipType
            {
                Name = "src_link", SourceEntityTypeId = _typeId, TargetEntityTypeId = _typeId,
            };
            db.Set<EntityRelationshipType>().Add(rt);
            await db.SaveChangesAsync();
            relTypeId = rt.Id;
            db.Set<EntityRelationship>().Add(new EntityRelationship
            {
                SourceEntityId = _entity1Id, TargetEntityId = _entity2Id, RelationshipTypeId = relTypeId,
            });
            await db.SaveChangesAsync();
        }

        var (items, _) = Sut().GetByWorkspaceAsync(
            _wsId, _userId, 999, null, null, 0, 100, [], [],
            excludeLinkedSourceRelTypeId: relTypeId).GetAwaiter().GetResult();

        items.Should().NotContain(e => e.Id == _entity1Id);
    }

    [Fact]
    public async Task ExcludeLinkedTargetRelType_OmitsLinkedEntities()
    {
        int relTypeId;
        await using (var db = Db())
        {
            var rt = new EntityRelationshipType
            {
                Name = "tgt_link", SourceEntityTypeId = _typeId, TargetEntityTypeId = _typeId,
            };
            db.Set<EntityRelationshipType>().Add(rt);
            await db.SaveChangesAsync();
            relTypeId = rt.Id;
            db.Set<EntityRelationship>().Add(new EntityRelationship
            {
                SourceEntityId = _entity1Id, TargetEntityId = _entity3Id, RelationshipTypeId = relTypeId,
            });
            await db.SaveChangesAsync();
        }

        var (items, _) = Sut().GetByWorkspaceAsync(
            _wsId, _userId, 999, null, null, 0, 100, [], [],
            excludeLinkedTargetRelTypeId: relTypeId).GetAwaiter().GetResult();

        items.Should().NotContain(e => e.Id == _entity3Id);
    }
}
