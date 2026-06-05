using Microsoft.AspNetCore.Http;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Endpoints;

public static class SupportEndpoints
{
    public static RouteGroupBuilder MapSupportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/support")
            .WithTags("Support");

        group.MapPost("/contact", async (SupportContactRequest request, ISupportService supportService, HttpContext http, CancellationToken ct) =>
        {
            await supportService.SendContactAsync(request, ClientIp.From(http), ct);
            return Results.NoContent();
        })
        .WithName("SupportContact")
        .WithSummary("Send a support message to the development team")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        return group;
    }
}
