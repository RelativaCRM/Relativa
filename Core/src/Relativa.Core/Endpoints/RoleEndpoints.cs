using System.Security.Claims;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this IEndpointRouteBuilder routes)
    {
        var wsGroup = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/roles")
            .WithTags("Roles");

        wsGroup.MapGet("/", async (int workspaceId, IRoleService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var result = await service.GetByWorkspaceAsync(workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListRoles")
        .WithSummary("List roles available in a workspace")
        .Produces<List<RoleDto>>();

        wsGroup.MapPost("/", async (int workspaceId, CreateRoleRequest request, IRoleService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var result = await service.CreateAsync(workspaceId, userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/roles/{result.Id}", result);
        })
        .WithName("CreateRole")
        .WithSummary("Create a custom role in the workspace")
        .Produces<RoleDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        wsGroup.MapPut("/{roleId:int}", async (int workspaceId, int roleId, UpdateRoleRequest request, IRoleService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            await service.UpdateAsync(workspaceId, roleId, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateRole")
        .WithSummary("Update a custom role's name or permissions")
        .Produces(StatusCodes.Status204NoContent);

        wsGroup.MapDelete("/{roleId:int}", async (int workspaceId, int roleId, IRoleService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            await service.ArchiveAsync(workspaceId, roleId, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveRole")
        .WithSummary("Archive a custom role")
        .Produces(StatusCodes.Status204NoContent);

        var permGroup = routes.MapGroup("/api/v1/permissions")
            .WithTags("Permissions");

        permGroup.MapGet("/", async (IRoleService service, CancellationToken ct) =>
        {
            var result = await service.GetAllPermissionsAsync(ct);
            return Results.Ok(result);
        })
        .WithName("ListPermissions")
        .WithSummary("List all available permissions")
        .Produces<List<PermissionDto>>();

        return wsGroup;
    }
}
