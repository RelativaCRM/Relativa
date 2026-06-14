using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class EntityRepository(RelativaDbContext db) : IEntityRepository
{
    public Task<EntityType?> GetEntityTypeByIdAsync(int entityTypeId, CancellationToken ct = default) =>
        db.EntityTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityTypeId, ct);

    public Task<List<EntityRelationshipType>> GetOutgoingRelationshipTypesAsync(int entityTypeId, CancellationToken ct = default) =>
        db.EntityRelationshipTypes
            .AsNoTracking()
            .Where(rt => rt.SourceEntityTypeId == entityTypeId)
            .ToListAsync(ct);

    public async Task<List<EntityTypeProperty>> GetTypePropertiesAsync(int entityTypeId, CancellationToken ct = default)
    {
        return await db.EntityTypeProperties
            .AsNoTracking()
            .Include(etp => etp.Property)
                .ThenInclude(p => p.AllowedValues)
            .Where(etp => etp.EntityTypeId == entityTypeId)
            .ToListAsync(ct);
    }

    public async Task<(List<Entity> Items, int Total)> GetByWorkspaceAsync(
        int workspaceId,
        int requesterUserId,
        int requesterRolePriority,
        int? entityTypeId,
        string? searchQuery,
        int skip,
        int take,
        IReadOnlyList<ResolvedFilterCondition> filters,
        IReadOnlyList<EntitySortField> sort,
        int? excludeLinkedSourceRelTypeId = null,
        int? excludeLinkedTargetRelTypeId = null,
        CancellationToken ct = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 500);

        var query = db.Entities
            .AsNoTracking()
            .Where(e => !e.IsArchived && e.EntityWorkspaces.Any(ew => ew.WorkspaceId == workspaceId))
            .Where(e =>
                e.CreatedByUserId == requesterUserId
                || db.UserRoleWorkspaces.Any(urw =>
                    urw.WorkspaceId == workspaceId
                    && !urw.IsArchived
                    && urw.UserId == e.CreatedByUserId
                    && urw.Role.Priority > requesterRolePriority));

        if (entityTypeId is > 0)
            query = query.Where(e => e.EntityTypeId == entityTypeId.Value);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var pattern = searchQuery.Trim();
            query = query.Where(e => e.EntityPropertyValues.Any(epv =>
                epv.ValueString != null && epv.ValueString.Contains(pattern)));
        }

        if (excludeLinkedSourceRelTypeId is > 0)
            query = query.Where(e => !db.EntityRelationships
                .Any(r => r.SourceEntityId == e.Id && r.RelationshipTypeId == excludeLinkedSourceRelTypeId.Value));

        if (excludeLinkedTargetRelTypeId is > 0)
            query = query.Where(e => !db.EntityRelationships
                .Any(r => r.TargetEntityId == e.Id && r.RelationshipTypeId == excludeLinkedTargetRelTypeId.Value));

        // Combined AND-logic property filters
        foreach (var f in filters)
        {
            var pid = f.PropertyId;
            query = (f.DataType, f.Op) switch
            {
                (PropertyDataType.String,  "eq")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueString == f.StringValue)),
                (PropertyDataType.String,  "neq")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueString != f.StringValue)),
                (PropertyDataType.String,  "contains")   => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueString != null && v.ValueString.Contains(f.StringValue!))),
                (PropertyDataType.String,  "startswith")  => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueString != null && v.ValueString.StartsWith(f.StringValue!))),
                (PropertyDataType.Int,     "eq")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt == f.IntValue)),
                (PropertyDataType.Int,     "neq")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt != f.IntValue)),
                (PropertyDataType.Int,     "gt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt > f.IntValue)),
                (PropertyDataType.Int,     "lt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt < f.IntValue)),
                (PropertyDataType.Int,     "gte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt >= f.IntValue)),
                (PropertyDataType.Int,     "lte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueInt <= f.IntValue)),
                (PropertyDataType.Decimal, "eq")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal == f.DecimalValue)),
                (PropertyDataType.Decimal, "neq")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal != f.DecimalValue)),
                (PropertyDataType.Decimal, "gt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal > f.DecimalValue)),
                (PropertyDataType.Decimal, "lt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal < f.DecimalValue)),
                (PropertyDataType.Decimal, "gte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal >= f.DecimalValue)),
                (PropertyDataType.Decimal, "lte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDecimal <= f.DecimalValue)),
                (PropertyDataType.Bool,    "eq")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueBool == f.BoolValue)),
                (PropertyDataType.Bool,    "neq")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueBool != f.BoolValue)),
                (PropertyDataType.Date,    "eq")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate == f.DateValue)),
                (PropertyDataType.Date,    "neq")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate != f.DateValue)),
                (PropertyDataType.Date,    "gt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate > f.DateValue)),
                (PropertyDataType.Date,    "lt")         => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate < f.DateValue)),
                (PropertyDataType.Date,    "gte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate >= f.DateValue)),
                (PropertyDataType.Date,    "lte")        => query.Where(e => e.EntityPropertyValues.Any(v => v.PropertyId == pid && v.ValueDate <= f.DateValue)),
                _ => query
            };
        }

        // Build ordered query (first field uses OrderBy, subsequent use ThenBy)
        IOrderedQueryable<Entity> ordered;
        if (sort is { Count: > 0 })
        {
            ordered = ApplySort(query, sort[0], isFirst: true);
            foreach (var s in sort.Skip(1))
                ordered = ApplySort(ordered, s, isFirst: false);
        }
        else
        {
            ordered = query.OrderBy(e => e.Id);
        }

        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Include(e => e.EntityType)
            .Include(e => e.EntityPropertyValues)
                .ThenInclude(epv => epv.Property)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    private static IOrderedQueryable<Entity> ApplySort(IQueryable<Entity> query, EntitySortField s, bool isFirst)
    {
        var pid = s.PropertyId;
        var desc = s.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
        return (isFirst, desc) switch
        {
            (true,  true)  => query.OrderByDescending(e => e.EntityPropertyValues
                .Where(v => v.PropertyId == pid).Select(v => v.ValueString).FirstOrDefault()),
            (true,  false) => query.OrderBy(e => e.EntityPropertyValues
                .Where(v => v.PropertyId == pid).Select(v => v.ValueString).FirstOrDefault()),
            (false, true)  => ((IOrderedQueryable<Entity>)query).ThenByDescending(e => e.EntityPropertyValues
                .Where(v => v.PropertyId == pid).Select(v => v.ValueString).FirstOrDefault()),
            (false, false) => ((IOrderedQueryable<Entity>)query).ThenBy(e => e.EntityPropertyValues
                .Where(v => v.PropertyId == pid).Select(v => v.ValueString).FirstOrDefault()),
        };
    }

    public async Task<Entity?> GetByIdInWorkspaceAsync(int entityId, int workspaceId, CancellationToken ct = default)
    {
        var link = await db.EntityWorkspaces
            .AsNoTracking()
            .Where(ew => ew.EntityId == entityId && ew.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(ct);

        if (link is null)
            return null;

        return await db.Entities
            .AsNoTracking()
            .AsSplitQuery()
            .Where(e => e.Id == entityId)
            .Include(e => e.EntityType)
            .Include(e => e.EntityPropertyValues)
                .ThenInclude(epv => epv.Property)
            .Include(e => e.SourceRelationships)
                .ThenInclude(r => r.RelationshipType)
            .Include(e => e.SourceRelationships)
                .ThenInclude(r => r.TargetEntity)
                    .ThenInclude(t => t.EntityType)
            .Include(e => e.SourceRelationships)
                .ThenInclude(r => r.TargetEntity)
                    .ThenInclude(t => t.EntityPropertyValues)
                        .ThenInclude(epv => epv.Property)
            .Include(e => e.TargetRelationships)
                .ThenInclude(r => r.RelationshipType)
            .Include(e => e.TargetRelationships)
                .ThenInclude(r => r.SourceEntity)
                    .ThenInclude(s => s.EntityType)
            .Include(e => e.TargetRelationships)
                .ThenInclude(r => r.SourceEntity)
                    .ThenInclude(s => s.EntityPropertyValues)
                        .ThenInclude(epv => epv.Property)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Entity> CreateAsync(
        Entity entity,
        List<EntityPropertyValue> propertyValues,
        int workspaceId,
        IReadOnlyList<EntityRelationship>? relationships,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.Entities.Add(entity);
        await db.SaveChangesAsync(ct);

        foreach (var pv in propertyValues)
        {
            pv.EntityId = entity.Id;
        }

        db.EntityPropertyValues.AddRange(propertyValues);

        db.EntityWorkspaces.Add(new EntityWorkspace { EntityId = entity.Id, WorkspaceId = workspaceId });

        if (relationships is { Count: > 0 })
        {
            foreach (var rel in relationships)
            {
                rel.SourceEntityId = entity.Id;
            }

            db.EntityRelationships.AddRange(relationships);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return entity;
    }

    public async Task UpdateAsync(Entity entity, List<EntityPropertyValue> newPropertyValues, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var existing = await db.EntityPropertyValues
            .Where(epv => epv.EntityId == entity.Id)
            .ToListAsync(ct);
        db.EntityPropertyValues.RemoveRange(existing);
        await db.SaveChangesAsync(ct);

        foreach (var pv in newPropertyValues)
        {
            pv.EntityId = entity.Id;
        }

        db.EntityPropertyValues.AddRange(newPropertyValues);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SetArchivedStateAsync(int entityId, bool isArchived, CancellationToken ct = default)
    {
        await db.Entities
            .Where(e => e.Id == entityId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, isArchived), ct);
    }

    public Task ArchiveAsync(int entityId, CancellationToken ct = default) =>
        SetArchivedStateAsync(entityId, true, ct);

    public Task<EntityRelationshipType?> GetRelationshipTypeByIdAsync(int relationshipTypeId, CancellationToken ct = default) =>
        db.EntityRelationshipTypes
            .AsNoTracking()
            .Include(rt => rt.SourceEntityType)
            .Include(rt => rt.TargetEntityType)
            .FirstOrDefaultAsync(rt => rt.Id == relationshipTypeId, ct);

    public Task<EntityRelationship?> GetRelationshipByIdAsync(int relationshipId, CancellationToken ct = default) =>
        db.EntityRelationships
            .AsNoTracking()
            .Include(r => r.RelationshipType)
                .ThenInclude(rt => rt.SourceEntityType)
            .Include(r => r.RelationshipType)
                .ThenInclude(rt => rt.TargetEntityType)
            .Include(r => r.SourceEntity)
                .ThenInclude(e => e.EntityWorkspaces)
            .Include(r => r.TargetEntity)
                .ThenInclude(e => e.EntityWorkspaces)
            .FirstOrDefaultAsync(r => r.Id == relationshipId, ct);

    public async Task<EntityRelationship> AddRelationshipAsync(EntityRelationship relationship, CancellationToken ct = default)
    {
        db.EntityRelationships.Add(relationship);
        await db.SaveChangesAsync(ct);
        return relationship;
    }

    public async Task RemoveRelationshipAsync(int relationshipId, CancellationToken ct = default)
    {
        await db.EntityRelationships
            .Where(r => r.Id == relationshipId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await tx.CommitAsync(ct);
        return result;
    }

    public Task UpdateRelationshipSourceAsync(int relationshipId, int newSourceEntityId, CancellationToken ct = default)
        => db.EntityRelationships
            .Where(r => r.Id == relationshipId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.SourceEntityId, newSourceEntityId), ct);

    public Task UpdateRelationshipTargetAsync(int relationshipId, int newTargetEntityId, CancellationToken ct = default)
        => db.EntityRelationships
            .Where(r => r.Id == relationshipId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TargetEntityId, newTargetEntityId), ct);

    public Task<int> CountRelationshipsBySourceAsync(int sourceEntityId, int relationshipTypeId, CancellationToken ct = default) =>
        db.EntityRelationships
            .CountAsync(r => r.SourceEntityId == sourceEntityId && r.RelationshipTypeId == relationshipTypeId, ct);

    public Task<int> CountRelationshipsByTargetAsync(int targetEntityId, int relationshipTypeId, CancellationToken ct = default) =>
        db.EntityRelationships
            .CountAsync(r => r.TargetEntityId == targetEntityId && r.RelationshipTypeId == relationshipTypeId, ct);
}
