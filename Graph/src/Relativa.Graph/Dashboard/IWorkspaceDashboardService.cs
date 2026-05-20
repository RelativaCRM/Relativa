using Relativa.Graph.Dashboard.Dto;

namespace Relativa.Graph.Dashboard;

public interface IWorkspaceDashboardService
{
    Task<WorkspaceSummaryDto> GetSummaryAsync(int userId, int workspaceId, CancellationToken ct);
    Task<PipelineDto> GetPipelineAsync(int userId, int workspaceId, CancellationToken ct);
    Task<RiskDistributionDto> GetRiskDistributionAsync(int userId, int workspaceId, CancellationToken ct);
    Task<TrendsDto> GetTrendsAsync(int userId, int workspaceId, CancellationToken ct);
    Task<TopEntitiesDto> GetTopEntitiesAsync(int userId, int workspaceId, CancellationToken ct);
    Task<IReadOnlyList<MemberActivityDto>> GetMemberActivityAsync(int userId, int workspaceId, CancellationToken ct);
}
