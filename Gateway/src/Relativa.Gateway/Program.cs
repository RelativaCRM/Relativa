using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Relativa.Gateway.Middleware;
using Scalar.AspNetCore;
using Serilog;
using Yarp.ReverseProxy.Transforms;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/gateway-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var config = builder.Configuration;

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = config["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!)),
                ValidateLifetime = true
            };
        });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var origins = config.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:5173", "http://localhost:3000"];
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddAuthorization();
    builder.Services.AddReverseProxy()
        .LoadFromConfig(config.GetSection("ReverseProxy"))
        .AddTransforms(transformContext =>
        {
            // The Gateway is the single point of JWT validation. Downstream
            // services trust these headers instead of re-validating tokens,
            // which keeps authentication logic in one place (SRP) and avoids
            // duplicating JWT config across every service.
            //
            // Unconditionally strip any incoming values so a client cannot
            // spoof identity by sending these headers directly. Only values
            // derived from the validated principal are forwarded.
            transformContext.AddRequestTransform(context =>
            {
                context.ProxyRequest.Headers.Remove("X-User-Id");
                context.ProxyRequest.Headers.Remove("X-User-Email");

                var principal = context.HttpContext.User;
                if (principal.Identity?.IsAuthenticated != true)
                {
                    return ValueTask.CompletedTask;
                }

                var sub = principal.FindFirstValue("sub");
                if (!string.IsNullOrEmpty(sub))
                {
                    context.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", sub);
                }

                var email = principal.FindFirstValue("email");
                if (!string.IsNullOrEmpty(email))
                {
                    context.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Email", email);
                }

                return ValueTask.CompletedTask;
            });
        });

    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseCors();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "relativa-gateway" }))
        .AllowAnonymous();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapReverseProxy().RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
