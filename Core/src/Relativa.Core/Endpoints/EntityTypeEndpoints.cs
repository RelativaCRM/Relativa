using Relativa.Core.Application.DTOs.EntityType;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class EntityTypeEndpoints
{
    public static IEndpointRouteBuilder MapEntityTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/entity-types")
            .WithTags("EntityTypes");

        group.MapGet("/", async (IEntityTypeService service, CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return Results.Ok(result);
        })
        .WithName("ListEntityTypes")
        .WithSummary("List all entity types with their property definitions")
        .Produces<List<EntityTypeDto>>();

        return routes;
    }
}
