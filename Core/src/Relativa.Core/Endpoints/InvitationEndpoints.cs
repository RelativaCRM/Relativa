using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this IEndpointRouteBuilder routes)
    {
        var invGroup = routes.MapGroup("/api/v1/invitations")
            .WithTags("Invitations");

        invGroup.MapPost("/accept-org", async (AcceptOrgInvitationRequest request, IOrgInvitationService orgService, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var email = WorkspaceEndpoints.GetUserEmail(httpContext);
            await orgService.AcceptAsync(userId, email, request.Token, ct);
            return Results.Ok(new { message = "Organization invitation accepted." });
        })
        .WithName("AcceptOrgInvitation")
        .WithSummary("Accept an organization invitation by token")
        .Produces(StatusCodes.Status200OK);

        invGroup.MapGet("/mine", async (IOrgInvitationService orgService, HttpContext httpContext, CancellationToken ct) =>
        {
            var email = WorkspaceEndpoints.GetUserEmail(httpContext);
            var list = await orgService.GetMyPendingInvitationsAsync(email, ct);
            return Results.Ok(new MyInvitationsDto(list));
        })
        .WithName("GetMyInvitations")
        .WithSummary("Get pending organization invitations for the authenticated user")
        .Produces<MyInvitationsDto>();

        invGroup.MapGet("/mine/organization", async (IOrgInvitationService orgService, HttpContext httpContext, CancellationToken ct) =>
        {
            var email = WorkspaceEndpoints.GetUserEmail(httpContext);
            var result = await orgService.GetMyPendingInvitationsAsync(email, ct);
            return Results.Ok(result);
        })
        .WithName("GetMyOrganizationInvitations")
        .WithSummary("Get pending organization invitations for the authenticated user")
        .Produces<List<OrgInvitationDto>>();

        return invGroup;
    }
}
