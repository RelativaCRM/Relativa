using System.Security.Claims;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class MemberEndpoints
{
    public static RouteGroupBuilder MapMemberEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/members")
            .WithTags("Members");

        group.MapGet("/", async (int workspaceId, IWorkspaceMemberService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var result = await service.GetMembersAsync(workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListMembers")
        .WithSummary("List workspace members")
        .Produces<List<WorkspaceMemberDto>>();

        group.MapPut("/{targetUserId:int}/role", async (int workspaceId, int targetUserId, UpdateMemberRoleRequest request, IWorkspaceMemberService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(user);
            await service.UpdateRoleAsync(workspaceId, targetUserId, callerUserId, request, ct);
            return Results.NoContent();
        })
        .WithName("UpdateMemberRole")
        .WithSummary("Change a member's role")
        .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{targetUserId:int}", async (int workspaceId, int targetUserId, IWorkspaceMemberService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var callerUserId = WorkspaceEndpoints.GetUserId(user);
            await service.RemoveAsync(workspaceId, targetUserId, callerUserId, ct);
            return Results.NoContent();
        })
        .WithName("RemoveMember")
        .WithSummary("Remove a member from the workspace")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
