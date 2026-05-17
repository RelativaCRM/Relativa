using Relativa.Core.Application.DTOs.Entity;

namespace Relativa.Core.Application.Interfaces;

public interface IEntityService
{
    Task<List<EntityListItemDto>> GetByWorkspaceAsync(
        int workspaceId,
        int userId,
        int? entityTypeId,
        string? searchQuery,
        int take,
        CancellationToken ct = default);
    Task<EntityDetailDto> GetByIdAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default);
    Task<EntityDetailDto> CreateAsync(int workspaceId, int userId, CreateEntityRequest request, CancellationToken ct = default);
    Task<EntityDetailDto> UpdateAsync(int entityId, int workspaceId, int userId, UpdateEntityRequest request, CancellationToken ct = default);
    Task ArchiveAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default);
    Task<EntityRelationshipRefDto> CreateRelationshipAsync(int workspaceId, int userId, CreateEntityRelationshipRequest request, CancellationToken ct = default);
    Task DeleteRelationshipAsync(int workspaceId, int userId, int relationshipId, CancellationToken ct = default);
}
