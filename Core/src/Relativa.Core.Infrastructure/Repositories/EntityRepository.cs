using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class EntityRepository(RelativaDbContext db) : IEntityRepository
{
    public async Task<List<EntityTypeProperty>> GetTypePropertiesAsync(int entityTypeId, CancellationToken ct = default)
    {
        return await db.EntityTypeProperties
            .AsNoTracking()
            .Include(etp => etp.Property)
            .Where(etp => etp.EntityTypeId == entityTypeId)
            .ToListAsync(ct);
    }

    public async Task<List<Entity>> GetByWorkspaceAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.Entities
            .AsNoTracking()
            .Where(e => !e.IsArchived && e.EntityWorkspaces.Any(ew => ew.WorkspaceId == workspaceId))
            .Include(e => e.EntityType)
            .Include(e => e.EntityPropertyValues)
                .ThenInclude(epv => epv.Property)
            .OrderBy(e => e.Id)
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
            .Where(e => e.Id == entityId)
            .Include(e => e.EntityType)
            .Include(e => e.EntityPropertyValues)
                .ThenInclude(epv => epv.Property)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Entity> CreateAsync(Entity entity, List<EntityPropertyValue> propertyValues, int workspaceId, CancellationToken ct = default)
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
