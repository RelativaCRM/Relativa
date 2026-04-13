using System.Security.Claims;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces");

        group.MapPost("/", async (CreateWorkspaceRequest request, IWorkspaceService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.CreateAsync(userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{result.Id}", result);
        })
        .WithName("CreateWorkspace")
        .WithSummary("Create a new workspace")
        .Produces<WorkspaceDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/", async (IWorkspaceService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.GetByUserAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListWorkspaces")
        .WithSummary("List workspaces for the authenticated user")
        .Produces<List<WorkspaceDto>>();

        group.MapGet("/{id:int}", async (int id, IWorkspaceService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.GetByIdAsync(id, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWorkspace")
        .WithSummary("Get workspace details")
        .Produces<WorkspaceDto>();

        group.MapPut("/{id:int}", async (int id, UpdateWorkspaceRequest request, IWorkspaceService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            await service.UpdateAsync(id, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateWorkspace")
        .WithSummary("Update workspace details")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        group.MapDelete("/{id:int}", async (int id, IWorkspaceService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            await service.ArchiveAsync(id, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveWorkspace")
        .WithSummary("Archive a workspace")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    internal static int GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return int.Parse(sub);
    }
}
