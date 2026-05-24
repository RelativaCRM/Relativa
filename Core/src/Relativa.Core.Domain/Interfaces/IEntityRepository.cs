using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IEntityRepository
{
    Task<EntityType?> GetEntityTypeByIdAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<EntityRelationshipType>> GetOutgoingRelationshipTypesAsync(int entityTypeId, CancellationToken ct = default);
    Task<List<EntityTypeProperty>> GetTypePropertiesAsync(int entityTypeId, CancellationToken ct = default);
    Task<(List<Entity> Items, int Total)> GetByWorkspaceAsync(
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
        CancellationToken ct = default);
    Task<Entity?> GetByIdInWorkspaceAsync(int entityId, int workspaceId, CancellationToken ct = default);
    Task<Entity> CreateAsync(
        Entity entity,
        List<EntityPropertyValue> propertyValues,
        int workspaceId,
        IReadOnlyList<EntityRelationship>? relationships,
        CancellationToken ct = default);
    Task UpdateAsync(Entity entity, List<EntityPropertyValue> newPropertyValues, CancellationToken ct = default);
    Task SetArchivedStateAsync(int entityId, bool isArchived, CancellationToken ct = default);
    Task ArchiveAsync(int entityId, CancellationToken ct = default);

    // Relationship management
    Task<EntityRelationshipType?> GetRelationshipTypeByIdAsync(int relationshipTypeId, CancellationToken ct = default);
    Task<EntityRelationship?> GetRelationshipByIdAsync(int relationshipId, CancellationToken ct = default);
    Task<EntityRelationship> AddRelationshipAsync(EntityRelationship relationship, CancellationToken ct = default);
    Task RemoveRelationshipAsync(int relationshipId, CancellationToken ct = default);
    Task<int> CountRelationshipsBySourceAsync(int sourceEntityId, int relationshipTypeId, CancellationToken ct = default);
    Task<int> CountRelationshipsByTargetAsync(int targetEntityId, int relationshipTypeId, CancellationToken ct = default);
}
