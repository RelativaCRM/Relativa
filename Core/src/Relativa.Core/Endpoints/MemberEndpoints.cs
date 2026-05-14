using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class MemberEndpoints
{
    public static RouteGroupBuilder MapMemberEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/members")
            .WithTags("Members");

        group.MapGet("/", async (int workspaceId, IWorkspaceMemberService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetMembersAsync(workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListMembers")
        .WithSummary("List workspace members")
        .Produces<List<WorkspaceMemberDto>>();

        group.MapPost("/", async (int workspaceId, AddWorkspaceMemberRequest request, IWorkspaceMemberService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.AddMemberAsync(workspaceId, callerUserId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/members/{result.UserId}", result);
        })
        .WithName("AddMember")
        .WithSummary("Add a member to the workspace directly")
        .Produces<WorkspaceMemberDto>(StatusCodes.Status201Created);

        group.MapPut("/{targetUserId:int}/role", async (int workspaceId, int targetUserId, UpdateMemberRoleRequest request, IWorkspaceMemberService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.UpdateRoleAsync(workspaceId, targetUserId, callerUserId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateMemberRole")
        .WithSummary("Change a member's role")
        .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{targetUserId:int}", async (int workspaceId, int targetUserId, IWorkspaceMemberService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.RemoveAsync(workspaceId, targetUserId, callerUserId, ct);
            return Results.NoContent();
        })
        .WithName("RemoveMember")
        .WithSummary("Remove a member from the workspace")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
