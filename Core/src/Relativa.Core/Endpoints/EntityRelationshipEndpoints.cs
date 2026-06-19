using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class EntityRelationshipEndpoints
{
    public static IEndpointRouteBuilder MapEntityRelationshipEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/entity-relationships")
            .WithTags("Entity Relationships");

        group.MapPost("/", async (
            int workspaceId,
            CreateEntityRelationshipRequest request,
            IEntityService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateRelationshipAsync(workspaceId, userId, request, ct);
            return Results.Created(
                $"/api/v1/workspaces/{workspaceId}/entity-relationships/{result.RelationshipId}",
                result);
        })
        .WithName("CreateEntityRelationship")
        .WithSummary("Link two existing entities via a relationship type")
        .Produces<EntityRelationshipRefDto>(StatusCodes.Status201Created);

        group.MapDelete("/{relationshipId:int}", async (
            int workspaceId,
            int relationshipId,
            IEntityService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.DeleteRelationshipAsync(workspaceId, userId, relationshipId, ct);
            return Results.NoContent();
        })
        .WithName("DeleteEntityRelationship")
        .WithSummary("Remove a relationship between two entities (blocked for required relationships)")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{relationshipId:int}", async (
            int workspaceId,
            int relationshipId,
            ReassignEntityRelationshipRequest request,
            IEntityService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.ReassignRelationshipAsync(workspaceId, userId, relationshipId, request, ct);
            return Results.Ok(result);
        })
        .WithName("ReassignEntityRelationship")
        .WithSummary("Swap the source or target of an existing relationship (safe for required relationships)")
        .Produces<EntityRelationshipRefDto>(StatusCodes.Status200OK);

        return routes;
    }
}
