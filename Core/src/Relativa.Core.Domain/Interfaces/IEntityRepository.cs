using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IEntityRepository
{
    Task<EntityType?> GetEntityTypeByIdAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<EntityRelationshipType>> GetOutgoingRelationshipTypesAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<EntityTypeProperty>> GetTypePropertiesAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<Entity>> GetByWorkspaceAsync(
        int workspaceId,
        int? entityTypeId,
        string? searchQuery,
        int take,
        CancellationToken ct = default);
    Task<Entity?> GetByIdInWorkspaceAsync(int entityId, int workspaceId, CancellationToken ct = default);
    Task<Entity> CreateAsync(
        Entity entity,
        List<EntityPropertyValue> propertyValues,
        int workspaceId,
        IReadOnlyList<EntityRelationship>? relationships,
        CancellationToken ct = default);
    Task UpdateAsync(Entity entity, List<EntityPropertyValue> newPropertyValues, CancellationToken ct = default);
    Task ArchiveAsync(Entity entity, CancellationToken ct = default);
}
