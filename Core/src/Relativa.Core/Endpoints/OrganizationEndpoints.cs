using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/organizations")
            .WithTags("Organizations");

        group.MapPost("/", async (CreateOrganizationRequest request, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateAsync(userId, request, ct);
            return Results.Created($"/api/v1/organizations/{result.Id}", result);
        })
        .WithName("CreateOrganization")
        .WithSummary("Create a new organization")
        .Produces<OrganizationDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/", async (IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByUserAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListOrganizations")
        .WithSummary("List organizations for the authenticated user")
        .Produces<List<OrganizationDto>>();

        group.MapGet("/search", async (string q, IOrganizationService service, CancellationToken ct) =>
        {
            var result = await service.SearchAsync(q, ct);
            return Results.Ok(result);
        })
        .WithName("SearchOrganizations")
        .WithSummary("Search organizations by name")
        .Produces<List<OrganizationSearchResultDto>>();

        group.MapGet("/{id:int}", async (int id, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByIdAsync(id, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetOrganization")
        .WithSummary("Get organization details")
        .Produces<OrganizationDto>();

        group.MapPut("/{id:int}", async (int id, UpdateOrganizationRequest request, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.UpdateAsync(id, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateOrganization")
        .WithSummary("Update organization details")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        return group;
    }
}
