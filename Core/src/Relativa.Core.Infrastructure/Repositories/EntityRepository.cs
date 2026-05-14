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
            .Where(etp => etp.EntityTypeId == entityTypeId)
            .ToListAsync(ct);
    }

    public async Task<List<Entity>> GetByWorkspaceAsync(
        int workspaceId,
        int requesterUserId,
        int requesterRolePriority,
        int? entityTypeId,
        string? searchQuery,
        int take,
        CancellationToken ct = default)
    {
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

        return await query
            .Include(e => e.EntityType)
            .Include(e => e.EntityPropertyValues)
                .ThenInclude(epv => epv.Property)
            .OrderBy(e => e.Id)
            .Take(take)
            .ToListAsync(ct);
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
            .Include(r => r.SourceEntity)
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

    public Task<int> CountRelationshipsBySourceAsync(int sourceEntityId, int relationshipTypeId, CancellationToken ct = default) =>
        db.EntityRelationships
            .CountAsync(r => r.SourceEntityId == sourceEntityId && r.RelationshipTypeId == relationshipTypeId, ct);

    public Task<int> CountRelationshipsByTargetAsync(int targetEntityId, int relationshipTypeId, CancellationToken ct = default) =>
        db.EntityRelationships
            .CountAsync(r => r.TargetEntityId == targetEntityId && r.RelationshipTypeId == relationshipTypeId, ct);
}
