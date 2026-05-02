using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Relativa.Audit.Data;
using Relativa.Audit.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks().AddDbContextCheck<AuditDbContext>();
builder.Services.Configure<RabbitMqAuditOptions>(builder.Configuration.GetSection("RabbitMqAudit"));
builder.Services.AddHostedService<AuditEventConsumer>();

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
        policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "relativa-audit" }));
app.MapHealthChecks("/health");

app.MapGet("/audit-log", async (
        AuditDbContext db,
        string? scope,
        int? targetId,
        int? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct) =>
    {
        scope = scope?.Trim().ToLowerInvariant();
        var fromAt = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toAt = to ?? DateTimeOffset.UtcNow;
        var includeAll = string.IsNullOrWhiteSpace(scope);
        var result = new List<object>();

        if (includeAll || scope == "entity")
        {
            var entityQuery = db.EntityAuditLogs.AsNoTracking()
                .Where(x => x.ChangedAt >= fromAt && x.ChangedAt <= toAt);
            if (targetId.HasValue) entityQuery = entityQuery.Where(x => x.EntityId == targetId.Value);
            if (actorUserId.HasValue) entityQuery = entityQuery.Where(x => x.ChangedById == actorUserId.Value);
            var entityRows = await entityQuery.OrderByDescending(x => x.ChangedAt).Take(500)
                .Select(x => new
                {
                    Scope = "entity",
                    TargetId = x.EntityId,
                    x.Action,
                    x.FieldName,
                    x.ChangedById,
                    x.ChangedAt
                }).ToListAsync(ct);
            if (!includeAll) return Results.Ok(entityRows);
            result.AddRange(entityRows);
        }

        if (includeAll || scope == "workspace")
        {
            var query = db.WorkspaceAuditLogs.AsNoTracking()
                .Where(x => x.ChangedAt >= fromAt && x.ChangedAt <= toAt);
            if (targetId.HasValue) query = query.Where(x => x.WorkspaceId == targetId.Value);
            if (actorUserId.HasValue) query = query.Where(x => x.ChangedById == actorUserId.Value);
            var rows = await query.OrderByDescending(x => x.ChangedAt).Take(500)
                .Select(x => new { Scope = "workspace", TargetId = x.WorkspaceId, x.Action, x.FieldName, x.ChangedById, x.ChangedAt })
                .ToListAsync(ct);
            if (!includeAll) return Results.Ok(rows);
            result.AddRange(rows);
        }

        if (includeAll || scope == "organization")
        {
            var query = db.OrganizationAuditLogs.AsNoTracking()
                .Where(x => x.ChangedAt >= fromAt && x.ChangedAt <= toAt);
            if (targetId.HasValue) query = query.Where(x => x.OrganizationId == targetId.Value);
            if (actorUserId.HasValue) query = query.Where(x => x.ChangedById == actorUserId.Value);
            var rows = await query.OrderByDescending(x => x.ChangedAt).Take(500)
                .Select(x => new { Scope = "organization", TargetId = x.OrganizationId, x.Action, x.FieldName, x.ChangedById, x.ChangedAt })
                .ToListAsync(ct);
            if (!includeAll) return Results.Ok(rows);
            result.AddRange(rows);
        }

        if (includeAll || scope == "user")
        {
            var query = db.UserAuditLogs.AsNoTracking()
                .Where(x => x.ChangedAt >= fromAt && x.ChangedAt <= toAt);
            if (targetId.HasValue) query = query.Where(x => x.TargetUserId == targetId.Value);
            if (actorUserId.HasValue) query = query.Where(x => x.ChangedById == actorUserId.Value);
            var rows = await query.OrderByDescending(x => x.ChangedAt).Take(500)
                .Select(x => new { Scope = "user", TargetId = x.TargetUserId, x.Action, x.FieldName, x.ChangedById, x.ChangedAt })
                .ToListAsync(ct);
            if (!includeAll) return Results.Ok(rows);
            result.AddRange(rows);
        }

        if (includeAll) return Results.Ok(result);
        return Results.BadRequest("Invalid scope. Use entity|workspace|organization|user.");
    })
    .RequireAuthorization("AuditReaders")
    .WithName("GetAuditLog");

app.Run();
