using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Relativa.Audit.Application.Interfaces;
using Relativa.Audit.Application.Options;
using Relativa.Audit.Application.Services;
using Relativa.Audit.Application.Validators;
using Relativa.Audit.Endpoints;
using Relativa.Audit.Infrastructure.Data;
using Relativa.Audit.Infrastructure.Services;
using Relativa.Audit.Middleware;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/audit-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<AuditDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
    builder.Services.Configure<RabbitMqAuditOptions>(builder.Configuration.GetSection("RabbitMqAudit"));
    builder.Services.Configure<AuditLogReadOptions>(builder.Configuration.GetSection("AuditLogRead"));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AuditDbContext>();

    builder.Services.AddHostedService<AuditEventConsumer>();

    builder.Services.AddValidatorsFromAssemblyContaining<IAuditLogReadService>();

    builder.Services.AddScoped<IAuditLogReadRepository, AuditLogReadRepository>();
    builder.Services.AddScoped<IAuditLogReadService, AuditLogReadService>();

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

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/", () => Results.Ok(new { service = "relativa-audit" }));
    app.MapHealthChecks("/health");
    app.MapAuditLogEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Audit service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
