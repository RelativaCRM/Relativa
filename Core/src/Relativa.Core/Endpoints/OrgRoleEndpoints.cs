using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class OrgRoleEndpoints
{
    public static RouteGroupBuilder MapOrgRoleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/organizations/{organizationId:int}/roles")
            .WithTags("Organization Roles");

        group.MapGet("/", async (int organizationId, IOrgRoleService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByOrganizationAsync(organizationId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListOrgRoles")
        .WithSummary("List roles available in the organization")
        .Produces<List<OrgRoleDto>>();

        group.MapPost("/", async (int organizationId, CreateOrgRoleRequest request, IOrgRoleService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateAsync(organizationId, userId, request, ct);
            return Results.Created($"/api/v1/organizations/{organizationId}/roles/{result.Id}", result);
        })
        .WithName("CreateOrgRole")
        .WithSummary("Create a custom role in the organization")
        .Produces<OrgRoleDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapPut("/{roleId:int}", async (int organizationId, int roleId, UpdateOrgRoleRequest request, IOrgRoleService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.UpdateAsync(organizationId, roleId, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateOrgRole")
        .WithSummary("Update a custom organization role")
        .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{roleId:int}", async (int organizationId, int roleId, IOrgRoleService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ArchiveAsync(organizationId, roleId, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveOrgRole")
        .WithSummary("Archive a custom organization role")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
