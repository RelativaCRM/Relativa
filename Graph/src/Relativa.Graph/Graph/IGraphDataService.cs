namespace Relativa.Graph.Graph;

public interface IGraphDataService
{
    Task<GraphResponseDto> BuildGraphAsync(int userId, int organizationId, string? riskLevel, CancellationToken ct);
}
