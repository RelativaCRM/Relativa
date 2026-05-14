using Relativa.Core.Application.DTOs.Workspace;

namespace Relativa.Core.Application.Interfaces;

public interface IWorkspaceService
{
    Task<WorkspaceDto> CreateAsync(int userId, CreateWorkspaceRequest request, CancellationToken ct = default);
    Task<List<WorkspaceDto>> GetByUserAsync(int userId, int? organizationId, CancellationToken ct = default);
    Task<WorkspaceDto> GetByIdAsync(int workspaceId, int userId, CancellationToken ct = default);
    Task UpdateAsync(int workspaceId, int userId, UpdateWorkspaceRequest request, CancellationToken ct = default);
    Task ArchiveAsync(int workspaceId, int userId, CancellationToken ct = default);
}
