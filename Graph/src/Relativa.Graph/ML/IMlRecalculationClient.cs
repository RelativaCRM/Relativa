namespace Relativa.Graph.ML;

public interface IMlRecalculationClient
{
    Task EnqueueAsync(IReadOnlyList<int> dealEntityIds, int requestedByUserId, int? workspaceId = null, CancellationToken ct = default);
}
