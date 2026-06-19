namespace Relativa.Graph.ML;

public interface IMlScoringClient
{
    Task<IReadOnlyDictionary<int, MlScoreDto>> ScoreBatchAsync(
        IReadOnlyList<int> dealEntityIds,
        CancellationToken ct = default);
}
