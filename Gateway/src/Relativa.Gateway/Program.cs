using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Relativa.Gateway.Middleware;
using Relativa.Gateway.OpenApi;
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
            // SignalR sends the JWT as ?access_token= because WebSocket doesn't support
            // custom headers. Read the token from the query string for hub routes.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/graph/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowAnyOriginForDev = config.GetValue<bool>("Cors:AllowAnyOriginForDev");
            var origins = config.GetSection("Cors:Origins").Get<string[]>()
                ?? [];

            if (allowAnyOriginForDev)
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                return;
            }

            if (origins.Length == 0)
            {
                Log.Warning(
                    "CORS is enabled at gateway but Cors:Origins is empty and Cors:AllowAnyOriginForDev is false. " +
                    "Cross-origin browser requests will be blocked.");
                return;
            }

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddHttpClient("openapi")
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

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
    app.MapAggregatedOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Relativa API";
        options.OpenApiRoutePattern = "/openapi/aggregated.json";
    });

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
