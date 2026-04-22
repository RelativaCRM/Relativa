using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class OrgMemberEndpoints
{
    public static RouteGroupBuilder MapOrgMemberEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/organizations/{organizationId:int}/members")
            .WithTags("Organization Members");

        group.MapGet("/", async (int organizationId, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetMembersAsync(organizationId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListOrgMembers")
        .WithSummary("List organization members")
        .Produces<List<OrgMemberDto>>();

        group.MapDelete("/{userId:int}", async (int organizationId, int userId, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.RemoveMemberAsync(organizationId, userId, callerUserId, ct);
            return Results.NoContent();
        })
        .WithName("RemoveOrgMember")
        .WithSummary("Remove a member from the organization")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{userId:int}/role", async (int organizationId, int userId, ChangeOrgMemberRoleRequest request, IOrganizationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ChangeMemberRoleAsync(organizationId, userId, callerUserId, request, ct);
            return Results.NoContent();
        })
        .WithName("ChangeOrgMemberRole")
        .WithSummary("Change an organization member's role")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
