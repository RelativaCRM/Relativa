using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = false,
            RequireExpirationTime = false,
            SignatureValidator = (token, _) => new JsonWebToken(token),
        };
        options.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AuditReaders",
        policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim("role", "Admin")
                || ctx.User.HasClaim("role", "Analyst")));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "relativa-audit" }));

app.MapGet("/audit-log", () => Results.Ok(Array.Empty<object>()))
    .RequireAuthorization("AuditReaders")
    .WithName("GetAuditLog");

app.Run();
