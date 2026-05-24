namespace Relativa.Graph.Graph;

public static class GraphQueryEndpoints
{
    private static readonly HashSet<string> ValidRiskLevels =
        new(StringComparer.OrdinalIgnoreCase) { "high", "medium", "low" };

    public static void MapGraphQueryEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/graph", async (
                int organizationId,
                string? riskLevel,
                HttpContext ctx,
                IGraphDataService svc,
                CancellationToken ct) =>
            {
                int userId;
                try
                {
                    userId = GetUserIdOrThrow(ctx);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Unauthorized();
                }

                if (riskLevel is not null && !ValidRiskLevels.Contains(riskLevel))
                    return Results.BadRequest(
                        $"Invalid risk_level '{riskLevel}'. Allowed values: high, medium, low.");

                var result = await svc.BuildGraphAsync(userId, organizationId, riskLevel, ct);
                return Results.Ok(result);
            })
            .WithTags("Graph")
            .WithName("GetGraph")
            .WithSummary("Returns graph nodes and edges for the authenticated user within the specified organization.");
    }

    private static int GetUserIdOrThrow(HttpContext ctx)
    {
        var v = ctx.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(v) || !int.TryParse(v, out var id))
            throw new UnauthorizedAccessException("Missing or invalid X-User-Id header.");
        return id;
    }
}
