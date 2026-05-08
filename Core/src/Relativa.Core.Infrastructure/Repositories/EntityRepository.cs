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
        int? entityTypeId,
        string? searchQuery,
        int take,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        var query = db.Entities
            .AsNoTracking()
            .Where(e => !e.IsArchived && e.EntityWorkspaces.Any(ew => ew.WorkspaceId == workspaceId));

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

    public async Task ArchiveAsync(Entity entity, CancellationToken ct = default)
    {
        entity.IsArchived = true;
        db.Entities.Update(entity);
        await db.SaveChangesAsync(ct);
    }
}
