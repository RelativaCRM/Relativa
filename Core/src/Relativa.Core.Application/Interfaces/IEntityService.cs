using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Domain.Interfaces;

namespace Relativa.Core.Application.Interfaces;

public interface IEntityService
{
    Task<EntityPagedResult> GetByWorkspaceAsync(
        int workspaceId,
        int userId,
        int? entityTypeId,
        string? searchQuery,
        int skip,
        int take,
        IReadOnlyList<EntityFilterCondition>? filters = null,
        IReadOnlyList<EntitySortField>? sort = null,
        int? excludeLinkedSourceRelTypeId = null,
        int? excludeLinkedTargetRelTypeId = null,
        CancellationToken ct = default);
    Task<EntityDetailDto> GetByIdAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default);
    Task<EntityDetailDto> CreateAsync(int workspaceId, int userId, CreateEntityRequest request, CancellationToken ct = default);
    Task<EntityDetailDto> UpdateAsync(int entityId, int workspaceId, int userId, UpdateEntityRequest request, CancellationToken ct = default);
    Task ArchiveAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default);
    Task<EntityRelationshipRefDto> CreateRelationshipAsync(int workspaceId, int userId, CreateEntityRelationshipRequest request, CancellationToken ct = default);
    Task DeleteRelationshipAsync(int workspaceId, int userId, int relationshipId, CancellationToken ct = default);
    Task<EntityRelationshipRefDto> ReassignRelationshipAsync(int workspaceId, int userId, int relationshipId, ReassignEntityRelationshipRequest request, CancellationToken ct = default);
}
