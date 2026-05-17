namespace Relativa.Persistence.Contracts;

public static class MlScoringRouting
{
    public const string ExchangeName      = "relativa.graph_ml";
    public const string CommandQueueName  = "ml.graph.score_request.v1";
    public const string CommandRoutingKey = "graph.score.request";
    public const string DlxName          = "relativa.graph_ml.dlx";
    public const string DlqName          = "ml.graph.score_request.v1.failed";
}

public sealed record MlScoreRpcRequestV1(IReadOnlyList<int> EntityIds);

public sealed record MlScoreRpcItemV1(
    int EntityId,
    double? ClosureScore,
    double? ChurnScore,
    string? UnavailableReason);

public sealed record MlScoreRpcReplyV1(
    IReadOnlyList<MlScoreRpcItemV1> Scores,
    string? ErrorMessage);
