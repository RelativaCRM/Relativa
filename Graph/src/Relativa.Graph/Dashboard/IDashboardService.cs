using Relativa.Graph.Dashboard.Dto;

namespace Relativa.Graph.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int userId, int organizationId, CancellationToken ct);
    Task<PipelineDto> GetPipelineAsync(int userId, int organizationId, CancellationToken ct);
    Task<RiskDistributionDto> GetRiskDistributionAsync(int userId, int organizationId, CancellationToken ct);
    Task<TrendsDto> GetTrendsAsync(int userId, int organizationId, CancellationToken ct);
    Task<TopEntitiesDto> GetTopEntitiesAsync(int userId, int organizationId, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceComparisonDto>> GetWorkspacesComparisonAsync(int userId, int organizationId, CancellationToken ct);
}
