using Relativa.Graph.Dashboard.Dto;

namespace Relativa.Graph.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/dashboard").WithTags("Dashboard");

        group.MapGet("/summary", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetSummaryAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardSummary")
        .WithSummary("KPI summary cards for the dashboard.");

        group.MapGet("/pipeline", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetPipelineAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardPipeline")
        .WithSummary("Deal pipeline funnel data grouped by stage.");

        group.MapGet("/risk-distribution", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetRiskDistributionAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardRiskDistribution")
        .WithSummary("ML-scored risk distribution of active deals.");

        group.MapGet("/trends", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetTrendsAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardTrends")
        .WithSummary("6-month rolling deal trends (new, won, lost, revenue).");

        group.MapGet("/top-entities", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetTopEntitiesAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardTopEntities")
        .WithSummary("Top 10 deals by value and top 10 clients by lifetime value.");

        group.MapGet("/workspaces-comparison", async (
            int organizationId,
            HttpContext ctx,
            IDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetWorkspacesComparisonAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("GetDashboardWorkspacesComparison")
        .WithSummary("Per-workspace KPI comparison for org admins/owners.");
    }

    private static int GetUserIdOrThrow(HttpContext ctx)
    {
        var v = ctx.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(v) || !int.TryParse(v, out var id))
            throw new UnauthorizedAccessException("Missing or invalid X-User-Id header.");
        return id;
    }
}
