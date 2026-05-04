using Microsoft.AspNetCore.Http;
using Relativa.Authentication.Application.DTOs;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class OrganizationUserEndpoints
{
    public static RouteGroupBuilder MapOrganizationUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/organizations/{organizationId:int}/users")
            .WithTags("Organization Users");

        group.MapPost("/", async (int organizationId, CreateOrgUserRequest request, IOrganizationUserAdminService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateOrgUserAsync(organizationId, callerUserId, request, ct);
            return Results.Created($"/api/v1/organizations/{organizationId}/users/{result.Id}", result);
        })
        .WithName("CreateOrgUser")
        .WithSummary("Create a user account and add them to the organization with a selected role (defaults to member)")
        .Produces<RegisterResponseDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status409Conflict);

        group.MapPatch("/{userId:int}", async (int organizationId, int userId, UpdateOrgUserProfileRequest request, IOrganizationUserAdminService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.UpdateOtherUserProfileAsync(organizationId, userId, callerUserId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateOrgUserProfile")
        .WithSummary("Update another organization member's profile (first and last name)")
        .Produces<UserProfileDto>()
        .ProducesValidationProblem();

        group.MapDelete("/{userId:int}", async (int organizationId, int userId, IOrganizationUserAdminService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.DeleteOrgUserAsync(organizationId, userId, callerUserId, ct);
            return Results.NoContent();
        })
        .WithName("DeleteOrgUser")
        .WithSummary("Archive a user account (organization-scoped permission)")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
