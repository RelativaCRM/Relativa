var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/login", (LoginRequest? _) => Results.StatusCode(StatusCodes.Status501NotImplemented))
    .WithName("Login");

app.MapPost("/refresh", (RefreshRequest? _) => Results.StatusCode(StatusCodes.Status501NotImplemented))
    .WithName("Refresh");

app.Run();

public sealed record LoginRequest(string? Username, string? Password);

public sealed record RefreshRequest(string? RefreshToken);
