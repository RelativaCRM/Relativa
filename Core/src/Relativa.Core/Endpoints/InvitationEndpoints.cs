using System.Security.Claims;
using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this IEndpointRouteBuilder routes)
    {
        var wsGroup = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/invitations")
            .WithTags("Invitations");

        wsGroup.MapPost("/", async (int workspaceId, InviteMemberRequest request, IInvitationService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var result = await service.InviteAsync(workspaceId, userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/invitations/{result.Id}", result);
        })
        .WithName("InviteMember")
        .WithSummary("Invite a user to the workspace by email")
        .Produces<InvitationDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        wsGroup.MapGet("/", async (int workspaceId, IInvitationService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var result = await service.GetPendingAsync(workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListPendingInvitations")
        .WithSummary("List pending invitations for the workspace")
        .Produces<List<InvitationDto>>();

        wsGroup.MapDelete("/{invitationId:int}", async (int workspaceId, int invitationId, IInvitationService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            await service.CancelAsync(workspaceId, invitationId, userId, ct);
            return Results.NoContent();
        })
        .WithName("CancelInvitation")
        .WithSummary("Cancel a pending invitation")
        .Produces(StatusCodes.Status204NoContent);

        var acceptGroup = routes.MapGroup("/api/v1/invitations")
            .WithTags("Invitations");

        acceptGroup.MapPost("/accept", async (AcceptInvitationRequest request, IInvitationService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(user);
            var email = user.FindFirstValue("email")
                ?? throw new UnauthorizedAccessException("Missing email claim.");
            await service.AcceptAsync(userId, email, request, ct);
            return Results.Ok(new { message = "Invitation accepted." });
        })
        .WithName("AcceptInvitation")
        .WithSummary("Accept a workspace invitation by token")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        return wsGroup;
    }
}
