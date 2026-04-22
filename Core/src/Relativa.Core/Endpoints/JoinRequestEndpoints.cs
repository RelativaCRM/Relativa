using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class JoinRequestEndpoints
{
    public static RouteGroupBuilder MapJoinRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var orgGroup = routes.MapGroup("/api/v1/organizations/{organizationId:int}/join-requests")
            .WithTags("Join Requests");

        orgGroup.MapPost("/", async (int organizationId, CreateJoinRequestRequest request, IJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.SubmitAsync(organizationId, userId, request, ct);
            return Results.Created($"/api/v1/organizations/{organizationId}/join-requests/{result.Id}", result);
        })
        .WithName("SubmitJoinRequest")
        .WithSummary("Submit a join request to the organization")
        .Produces<JoinRequestDto>(StatusCodes.Status201Created);

        orgGroup.MapGet("/", async (int organizationId, IJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByOrganizationAsync(organizationId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListJoinRequests")
        .WithSummary("List pending join requests for the organization")
        .Produces<List<JoinRequestDto>>();

        orgGroup.MapPut("/{requestId:int}", async (int organizationId, int requestId, ReviewJoinRequestRequest request, IJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ReviewAsync(organizationId, requestId, userId, request, ct);
            return Results.NoContent();
        })
        .WithName("ReviewJoinRequest")
        .WithSummary("Approve or reject a join request")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        var myGroup = routes.MapGroup("/api/v1/join-requests")
            .WithTags("Join Requests");

        myGroup.MapGet("/mine", async (IJoinRequestService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetMyRequestsAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetMyJoinRequests")
        .WithSummary("Get all join requests submitted by the authenticated user")
        .Produces<List<JoinRequestDto>>();

        return orgGroup;
    }
}
