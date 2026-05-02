using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (LoginRequestDto request, IAuthService authService, CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithSummary("Authenticate a user and return a JWT access token")
        .AllowAnonymous()
        .Produces<LoginResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/register", async (RegisterRequestDto request, IAuthService authService, CancellationToken ct) =>
        {
            var result = await authService.RegisterAsync(request, ct);
            return Results.Created($"/api/v1/auth/users/{result.Id}", result);
        })
        .WithName("Register")
        .WithSummary("Register a new user")
        .AllowAnonymous()
        .Produces<RegisterResponseDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/me", async (IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await authService.GetProfileAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetProfile")
        .WithSummary("Get the current user's profile")
        .RequireAuthorization()
        .Produces<UserProfileDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me", async (UpdateMyProfileRequest request, IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await authService.UpdateMyProfileAsync(userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateMyProfile")
        .WithSummary("Update the current user's profile (first and last name)")
        .RequireAuthorization()
        .Produces<UserProfileDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/me", async (IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            await authService.DeleteMyAccountAsync(userId, ct);
            return Results.NoContent();
        })
        .WithName("DeleteMyAccount")
        .WithSummary("Archive the current user's account")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }
}
