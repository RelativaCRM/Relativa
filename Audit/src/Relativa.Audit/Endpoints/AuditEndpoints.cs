using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.IdentityModel.JsonWebTokens;
using Relativa.Audit.DTOs;
using Relativa.Audit.Services;
using Relativa.Audit.Validation;

namespace Relativa.Audit.Endpoints;

public static class AuditEndpoints
{
    public const string UserIdHeader = "X-User-Id";

    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/audit-log", GetAuditLog)
            .WithName("GetAuditLog")
            .WithSummary("Paginated audit log with filters and enriched report context")
            .RequireAuthorization("AuditReaders")
            .Produces<AuditLogListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        routes.MapGet("/entities/{entityId:int}/audit-log", GetEntityAuditLog)
            .WithName("GetEntityAuditLog")
            .WithSummary("Entity-scoped audit log (requires entity_type=entity context via query)")
            .RequireAuthorization("AuditReaders")
            .Produces<AuditLogListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
    }

    private static async Task<IResult> GetAuditLog(
        HttpContext httpContext,
        AuditLogReadService service,
        string? entity_type,
        string? scope,
        DateTimeOffset? date_from,
        DateTimeOffset? from,
        DateTimeOffset? date_to,
        DateTimeOffset? to,
        string? action,
        int? index,
        int? page_size,
        int? entity_id,
        int? targetId,
        string? domain_entity_type,
        int? workspace_id,
        int? organization_id,
        int? actor_user_id,
        int? actorUserId,
        int? target_user_id,
        CancellationToken cancellationToken)
    {
        var q = BuildQuery(
            entity_type,
            scope,
            date_from,
            from,
            date_to,
            to,
            action,
            index,
            page_size,
            entity_id,
            targetId,
            domain_entity_type,
            workspace_id,
            organization_id,
            actor_user_id,
            actorUserId,
            target_user_id,
            null);

        var userId = GetCallerUserId(httpContext);
        var result = await service.GetAsync(q, userId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEntityAuditLog(
        HttpContext httpContext,
        AuditLogReadService service,
        int entityId,
        DateTimeOffset? date_from,
        DateTimeOffset? from,
        DateTimeOffset? date_to,
        DateTimeOffset? to,
        string? action,
        int? index,
        int? page_size,
        string? domain_entity_type,
        int? workspace_id,
        int? actor_user_id,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        var df = date_from ?? from;
        var dt = date_to ?? to;

        var q = BuildQuery(
            entity_type: "entity",
            scope: null,
            date_from: df,
            from: null,
            date_to: dt,
            to: null,
            action,
            index,
            page_size,
            entity_id: entityId,
            targetId: null,
            domain_entity_type,
            workspace_id,
            organization_id: null,
            actor_user_id,
            actorUserId,
            target_user_id: null,
            forcedEntityId: entityId);

        var userId = GetCallerUserId(httpContext);
        var result = await service.GetAsync(q, userId, cancellationToken);
        return Results.Ok(result);
    }

    internal static GetAuditLogQuery BuildQuery(
        string? entity_type,
        string? scope,
        DateTimeOffset? date_from,
        DateTimeOffset? from,
        DateTimeOffset? date_to,
        DateTimeOffset? to,
        string? action,
        int? index,
        int? page_size,
        int? entity_id,
        int? targetId,
        string? domain_entity_type,
        int? workspace_id,
        int? organization_id,
        int? actor_user_id,
        int? actorUserId,
        int? target_user_id,
        int? forcedEntityId)
    {
        var cat = (entity_type ?? scope)?.Trim();
        if (string.IsNullOrEmpty(cat))
        {
            throw new ValidationException([
                new ValidationFailure("entity_type", "entity_type (or scope) is required.")
            ]);
        }

        var df = date_from ?? from;
        var dt = date_to ?? to;
        var idx = index ?? 1;
        var ps = page_size ?? 20;
        var eid = forcedEntityId ?? entity_id ?? (cat.Trim().ToLowerInvariant() == "entity" ? targetId : null);
        var actor = actor_user_id ?? actorUserId;

        return new GetAuditLogQuery(
            cat,
            df,
            dt,
            action,
            idx,
            ps,
            eid,
            domain_entity_type,
            workspace_id,
            organization_id,
            actor,
            target_user_id);
    }

    internal static int GetCallerUserId(HttpContext httpContext)
    {
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!string.IsNullOrEmpty(sub) && int.TryParse(sub, out var id))
                return id;
        }

        var header = httpContext.Request.Headers[UserIdHeader].ToString();
        if (!string.IsNullOrEmpty(header) && int.TryParse(header, out var headerId))
            return headerId;

        throw new UnauthorizedAccessException("Missing or invalid user identity (JWT sub or X-User-Id).");
    }
}
