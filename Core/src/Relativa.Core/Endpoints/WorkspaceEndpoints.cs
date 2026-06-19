using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces");

        group.MapPost("/", async (CreateWorkspaceRequest request, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            var result = await service.CreateAsync(userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{result.Id}", result);
        })
        .WithName("CreateWorkspace")
        .WithSummary("Create a new workspace")
        .Produces<WorkspaceDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/", async (int? organizationId, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            var result = await service.GetByUserAsync(userId, organizationId, ct);
            return Results.Ok(result);
        })
        .WithName("ListWorkspaces")
        .WithSummary("List workspaces for the authenticated user; optional organizationId limits to that org (caller must be an org member)")
        .Produces<List<WorkspaceDto>>()
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:int}", async (int id, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            var result = await service.GetByIdAsync(id, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspace")
        .WithSummary("Get workspace details")
        .Produces<WorkspaceDto>();

        group.MapPut("/{id:int}", async (int id, UpdateWorkspaceRequest request, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            await service.UpdateAsync(id, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateWorkspace")
        .WithSummary("Update workspace details")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        group.MapDelete("/{id:int}", async (int id, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            await service.ArchiveAsync(id, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveWorkspace")
        .WithSummary("Archive a workspace")
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/{id:int}/settings", async (int id, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            var result = await service.GetSettingsAsync(id, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspaceSettings")
        .WithSummary("Get settings for a workspace (any workspace member)")
        .Produces<WorkspaceSettingsDto>();

        group.MapPut("/{id:int}/settings", async (int id, UpdateWorkspaceSettingsRequest request, IWorkspaceService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            await service.UpdateSettingsAsync(id, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateWorkspaceSettings")
        .WithSummary("Update settings for a workspace (requires manage_ws_settings)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        return group;
    }

    // Identity is injected by the Gateway after it validates the JWT. Core
    // does not parse tokens; it trusts the X-User-Id / X-User-Email headers
    // because downstream services are not reachable outside the Gateway's
    // network. A missing header means the request bypassed the Gateway.
    internal const string UserIdHeader = "X-User-Id";
    internal const string UserEmailHeader = "X-User-Email";

    internal static int GetUserId(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers[UserIdHeader].ToString();
        if (string.IsNullOrEmpty(value))
        {
            throw new UnauthorizedAccessException($"Missing {UserIdHeader} header.");
        }

        if (!int.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException($"Invalid {UserIdHeader} header.");
        }

        return userId;
    }

    internal static string GetUserEmail(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers[UserEmailHeader].ToString();
        if (string.IsNullOrEmpty(value))
        {
            throw new UnauthorizedAccessException($"Missing {UserEmailHeader} header.");
        }

        return value;
    }
}
