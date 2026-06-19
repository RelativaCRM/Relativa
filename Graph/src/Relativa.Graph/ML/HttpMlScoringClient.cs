using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Relativa.Graph.ML;

file record ScoreBatchRequest([property: JsonPropertyName("entity_ids")] IReadOnlyList<int> EntityIds);

file record ScoreBatchItem(
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("closure_score")] double? ClosureScore,
    [property: JsonPropertyName("churn_score")] double? ChurnScore,
    [property: JsonPropertyName("unavailable_reason")] string? UnavailableReason
);

public sealed class HttpMlScoringClient(HttpClient http, ILogger<HttpMlScoringClient> logger) : IMlScoringClient
{
    public async Task<IReadOnlyDictionary<int, MlScoreDto>> ScoreBatchAsync(
        IReadOnlyList<int> dealEntityIds,
        CancellationToken ct = default)
    {
        if (dealEntityIds.Count == 0)
            return new Dictionary<int, MlScoreDto>();

        try
        {
            var response = await http.PostAsJsonAsync(
                "/api/ml/score/batch",
                new ScoreBatchRequest(dealEntityIds),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ML score/batch returned {Status} — graph renders without highlights", response.StatusCode);
                return new Dictionary<int, MlScoreDto>();
            }

            var items = await response.Content.ReadFromJsonAsync<List<ScoreBatchItem>>(ct);
            if (items is null)
                return new Dictionary<int, MlScoreDto>();

            return items.ToDictionary(
                i => i.EntityId,
                i => new MlScoreDto(i.EntityId, i.ClosureScore, i.ChurnScore, i.UnavailableReason));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ML API unavailable — graph renders without highlights");
            return new Dictionary<int, MlScoreDto>();
        }
    }
}
