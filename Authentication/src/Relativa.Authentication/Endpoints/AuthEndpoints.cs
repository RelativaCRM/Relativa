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

        return group;
    }
}
