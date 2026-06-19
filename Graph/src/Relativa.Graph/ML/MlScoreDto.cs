namespace Relativa.Graph.ML;

public record MlScoreDto(
    int EntityId,
    double? ClosureScore,
    double? ChurnScore,
    string? UnavailableReason
);
