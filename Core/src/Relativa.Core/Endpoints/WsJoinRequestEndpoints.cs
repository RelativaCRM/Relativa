using Relativa.Core.Application.DTOs.WsJoinRequest;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class WsJoinRequestEndpoints
{
    public static RouteGroupBuilder MapWsJoinRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var wsGroup = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/join-requests")
            .WithTags("Workspace Join Requests");

        wsGroup.MapPost("/", async (int workspaceId, CreateWsJoinRequestRequest request, IWsJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.SubmitAsync(workspaceId, userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/join-requests/{result.Id}", result);
        })
        .WithName("SubmitWsJoinRequest")
        .WithSummary("Submit a request to join the workspace. Requires membership in the parent organization.")
        .Produces<WsJoinRequestDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        wsGroup.MapGet("/", async (int workspaceId, IWsJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByWorkspaceAsync(workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListWsJoinRequests")
        .WithSummary("List pending join requests for the workspace. Requires 'manage_ws_join_requests' permission.")
        .Produces<List<WsJoinRequestDto>>();

        wsGroup.MapPut("/{requestId:int}", async (int workspaceId, int requestId, ReviewWsJoinRequestRequest request, IWsJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ReviewAsync(workspaceId, requestId, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("ReviewWsJoinRequest")
        .WithSummary("Approve or reject a workspace join request. Requires 'manage_ws_join_requests' permission.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        var myGroup = routes.MapGroup("/api/v1/workspace-join-requests")
            .WithTags("Workspace Join Requests");

        myGroup.MapGet("/mine", async (IWsJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetMyRequestsAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetMyWsJoinRequests")
        .WithSummary("Get all workspace join requests submitted by the authenticated user")
        .Produces<List<WsJoinRequestDto>>();

        return wsGroup;
    }
}
