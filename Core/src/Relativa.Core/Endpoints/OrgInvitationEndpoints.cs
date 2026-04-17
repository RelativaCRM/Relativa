using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Endpoints;

public static class OrgInvitationEndpoints
{
    public static RouteGroupBuilder MapOrgInvitationEndpoints(this IEndpointRouteBuilder routes)
    {
        var orgGroup = routes.MapGroup("/api/v1/organizations/{organizationId:int}/invitations")
            .WithTags("Organization Invitations");

        orgGroup.MapPost("/", async (int organizationId, InviteToOrgRequest request, IOrgInvitationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.InviteAsync(organizationId, userId, request, ct);
            return Results.Created($"/api/v1/organizations/{organizationId}/invitations/{result.Id}", result);
        })
        .WithName("InviteToOrg")
        .WithSummary("Invite a user to the organization by email")
        .Produces<OrgInvitationDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        orgGroup.MapGet("/", async (int organizationId, IOrgInvitationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByOrganizationAsync(organizationId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ListOrgInvitations")
        .WithSummary("List pending invitations for the organization")
        .Produces<List<OrgInvitationDto>>();

        orgGroup.MapDelete("/{invitationId:int}", async (int organizationId, int invitationId, IOrgInvitationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.CancelAsync(organizationId, invitationId, userId, ct);
            return Results.NoContent();
        })
        .WithName("CancelOrgInvitation")
        .WithSummary("Cancel a pending organization invitation")
        .Produces(StatusCodes.Status204NoContent);

        var acceptGroup = routes.MapGroup("/api/v1/invitations")
            .WithTags("Invitations");

        acceptGroup.MapPost("/accept-org", async (AcceptOrgInvitationRequest request, IOrgInvitationService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var email = WorkspaceEndpoints.GetUserEmail(httpContext);
            await service.AcceptAsync(userId, email, request.Token, ct);
            return Results.Ok(new { message = "Organization invitation accepted." });
        })
        .WithName("AcceptOrgInvitation")
        .WithSummary("Accept an organization invitation by token")
        .Produces(StatusCodes.Status200OK);

        return orgGroup;
    }
}
