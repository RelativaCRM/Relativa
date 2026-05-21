using Relativa.Graph.Dashboard.Dto;

namespace Relativa.Graph.Dashboard;

public static class WorkspaceDashboardEndpoints
{
    public static void MapWorkspaceDashboardEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/dashboard/workspace/{workspaceId:int}")
            .WithTags("WorkspaceDashboard");

        group.MapGet("/summary", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetSummaryAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardSummary")
        .WithSummary("KPI summary for a single workspace (permission-filtered).");

        group.MapGet("/pipeline", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetPipelineAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardPipeline")
        .WithSummary("Deal pipeline funnel for a single workspace.");

        group.MapGet("/risk-distribution", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetRiskDistributionAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardRiskDistribution")
        .WithSummary("ML-scored risk distribution for a single workspace.");

        group.MapGet("/trends", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetTrendsAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardTrends")
        .WithSummary("6-month rolling deal trends for a single workspace.");

        group.MapGet("/top-entities", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetTopEntitiesAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardTopEntities")
        .WithSummary("Top deals and clients in a single workspace.");

        group.MapGet("/member-activity", async (
            int workspaceId,
            HttpContext ctx,
            IWorkspaceDashboardService svc,
            CancellationToken ct) =>
        {
            var userId = GetUserIdOrThrow(ctx);
            var result = await svc.GetMemberActivityAsync(userId, workspaceId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceDashboardMemberActivity")
        .WithSummary("Member activity statistics for a single workspace (requires view_team_analytics).");
    }

    private static int GetUserIdOrThrow(HttpContext ctx)
    {
        var v = ctx.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(v) || !int.TryParse(v, out var id))
            throw new UnauthorizedAccessException("Missing or invalid X-User-Id header.");
        return id;
    }
}
