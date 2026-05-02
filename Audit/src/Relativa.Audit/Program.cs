using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Relativa.Audit.Data;
using Relativa.Audit.Endpoints;
using Relativa.Audit.Middleware;
using Relativa.Audit.Services;
using Relativa.Audit.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks().AddDbContextCheck<AuditDbContext>();
builder.Services.Configure<RabbitMqAuditOptions>(builder.Configuration.GetSection("RabbitMqAudit"));
builder.Services.Configure<AuditLogReadOptions>(builder.Configuration.GetSection("AuditLogRead"));
builder.Services.AddHostedService<AuditEventConsumer>();

builder.Services.AddScoped<AuditLogReadService>();
builder.Services.AddScoped<IValidator<GetAuditLogQuery>, GetAuditLogQueryValidator>();

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            ValidateLifetime = true,
            RequireExpirationTime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AuditReaders",
        policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "relativa-audit" }));
app.MapHealthChecks("/health");
app.MapAuditLogEndpoints();

app.Run();
