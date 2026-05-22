using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class EntityEndpoints
{
    public static IEndpointRouteBuilder MapEntityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/entities")
            .WithTags("Entities");

        group.MapGet("/", async (
            int workspaceId,
            int? entityTypeId,
            string? q,
            int? take,
            int? excludeLinkedSourceRelTypeId,
            int? excludeLinkedTargetRelTypeId,
            IEntityService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByWorkspaceAsync(workspaceId, userId, entityTypeId, q, take ?? 500, excludeLinkedSourceRelTypeId, excludeLinkedTargetRelTypeId, ct);
            return Results.Ok(result);
        })
        .WithName("ListEntities")
        .WithSummary("List non-archived entities; optional entityTypeId filter, q search on string values, take (default 500, max 500).")
        .Produces<List<EntityListItemDto>>();

        group.MapGet("/{entityId:int}", async (int workspaceId, int entityId, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByIdAsync(entityId, workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetEntity")
        .WithSummary("Get a single entity by id (404 if not in this workspace)")
        .Produces<EntityDetailDto>();

        group.MapPost("/", async (int workspaceId, CreateEntityRequest request, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateAsync(workspaceId, userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/entities/{result.Id}", result);
        })
        .WithName("CreateEntity")
        .WithSummary("Create a new entity with property values (atomic transaction)")
        .Produces<EntityDetailDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapPatch("/{entityId:int}", async (int workspaceId, int entityId, UpdateEntityRequest request, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.UpdateAsync(entityId, workspaceId, userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateEntity")
        .WithSummary("Update property values; omitted properties keep their current values. Send propertyId from GET entity or entity-types. Use null value to clear an optional field.")
        .Produces<EntityDetailDto>()
        .ProducesValidationProblem();

        group.MapDelete("/{entityId:int}", async (int workspaceId, int entityId, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ArchiveAsync(entityId, workspaceId, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveEntity")
        .WithSummary("Soft-delete (archive) an entity")
        .Produces(StatusCodes.Status204NoContent);

        return routes;
    }
}
