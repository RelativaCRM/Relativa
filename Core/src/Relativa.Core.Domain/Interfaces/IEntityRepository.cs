using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IEntityRepository
{
    Task<List<EntityTypeProperty>> GetTypePropertiesAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<Entity>> GetByWorkspaceAsync(int workspaceId, CancellationToken ct = default);
    Task<Entity?> GetByIdInWorkspaceAsync(int entityId, int workspaceId, CancellationToken ct = default);
    Task<Entity> CreateAsync(Entity entity, List<EntityPropertyValue> propertyValues, int workspaceId, CancellationToken ct = default);
    Task UpdateAsync(Entity entity, List<EntityPropertyValue> newPropertyValues, CancellationToken ct = default);
    Task ArchiveAsync(Entity entity, CancellationToken ct = default);
}
