using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Application.Exceptions;

namespace Relativa.Core.Endpoints;

public static class EntityEndpoints
{
    public static IEndpointRouteBuilder MapEntityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/workspaces/{workspaceId:int}/entities")
            .WithTags("Entities");

        group.MapGet("/", async (
            int workspaceId,
            int? entityTypeId,
            string? q,
            int? skip,
            int? take,
            [FromQuery(Name = "f")] string[]? f,
            [FromQuery(Name = "sort")] string[]? sort,
            int? excludeLinkedSourceRelTypeId,
            int? excludeLinkedTargetRelTypeId,
            IEntityService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var filters = ParseFilters(f);
            var sortFields = ParseSort(sort);
            var result = await service.GetByWorkspaceAsync(
                workspaceId, userId, entityTypeId, q,
                skip ?? 0, take ?? 50,
                filters, sortFields,
                excludeLinkedSourceRelTypeId, excludeLinkedTargetRelTypeId, ct);
            return Results.Ok(result);
        })
        .WithName("ListEntities")
        .Produces<EntityPagedResult>()
        .WithOpenApi(op =>
        {
            op.Summary = "List entities — combined filtering, sorting, and pagination";
            op.Description = """
                Returns non-archived entities visible to the caller within the given workspace.

                ### Visibility (RBAC)
                A user sees only entities they created **or** entities created by users whose workspace role
                has a **lower** priority number (higher authority). Org owners bypass this and see everything.
                Requires the `view_entities` workspace permission.

                ### Combined AND-filters (`f`)
                Repeat the `f` parameter to add multiple conditions — all must match (AND logic).
                Format: `propertyId:op` or `propertyId:op:value`.

                | Data type | Allowed operators |
                |---|---|
                | String | `eq`, `neq`, `contains`, `startsWith` |
                | Int / Decimal | `eq`, `neq`, `gt`, `lt`, `gte`, `lte` |
                | Bool | `eq`, `neq` |
                | Date (yyyy-MM-dd) | `eq`, `neq`, `gt`, `lt`, `gte`, `lte` |

                **RBAC on filters:** conditions targeting `IsReadonly` properties are silently dropped
                for callers without the `view_analytics` permission — no error is returned.

                ### Sorting (`sort`)
                Repeat the `sort` parameter for multi-field ordering (applied left-to-right).
                Format: `propertyId:asc` or `propertyId:desc`. Default order is entity `id` ascending.
                Same RBAC rule applies: readonly-property sort fields are silently dropped without `view_analytics`.

                ### Pagination
                Use `skip` + `take` for offset pagination. The response always includes `total` (count
                of matching rows before pagination), so the client can calculate page count.
                Default: `skip=0`, `take=50`. Max `take=500`.

                ### Error responses
                | Status | Reason |
                |---|---|
                | 400 | Malformed `f` / `sort` string; unknown propertyId for entityType; operator incompatible with data type; unparseable value |
                | 403 | Missing `view_entities` permission or not a workspace member |
                | 404 | `entityTypeId` does not exist (only when `entityTypeId` is supplied with filters) |
                """;

            foreach (var p in op.Parameters)
            {
                p.Description = p.Name switch
                {
                    "workspaceId" =>
                        "ID of the workspace to query. Example: `42`.",
                    "entityTypeId" =>
                        "Limit results to a single entity type. Required when `f` or `sort` are present. Example: `3`.",
                    "q" =>
                        "Free-text search — case-sensitive `contains` across all String property values. Example: `?q=Acme`.",
                    "skip" =>
                        "Offset for pagination. Default: `0`. Example: `?skip=50` (skips the first 50 records).",
                    "take" =>
                        "Page size. Default: `50`, max: `500`. Example: `?take=25`.",
                    "f" =>
                        "Repeatable. Combined AND-filter. Format: `propertyId:op[:value]`.\n\n" +
                        "| Data type | Operators |\n|---|---|\n" +
                        "| String | `eq`, `neq`, `contains`, `startsWith` |\n" +
                        "| Int / Decimal | `eq`, `neq`, `gt`, `lt`, `gte`, `lte` |\n" +
                        "| Bool | `eq`, `neq` |\n" +
                        "| Date (yyyy-MM-dd) | `eq`, `neq`, `gt`, `lt`, `gte`, `lte` |\n\n" +
                        "Examples: `?f=7:gt:0.75` · `?f=5:eq:Acme Corp` · `?f=11:eq:true` · `?f=9:gte:2026-01-01`\n\n" +
                        "**RBAC**: conditions targeting `IsReadonly` properties are silently dropped without `view_analytics`.",
                    "sort" =>
                        "Repeatable. Sort field. Format: `propertyId:asc` or `propertyId:desc`, applied left-to-right.\n\n" +
                        "Example: `?sort=7:desc&sort=5:asc` — by closure_score desc, then name asc.\n\n" +
                        "Default: entity `id` ascending. Same `IsReadonly` RBAC drop rule applies.",
                    "excludeLinkedSourceRelTypeId" =>
                        "Exclude entities already linked as the **source** of this relationship type. Use for link-candidate pickers. Example: `12`.",
                    "excludeLinkedTargetRelTypeId" =>
                        "Exclude entities already linked as the **target** of this relationship type. Example: `15`.",
                    _ => p.Description
                };
            }

            if (op.Responses.TryGetValue("200", out var ok200))
            {
                ok200.Description = "Matching entities with pagination metadata. Empty results return HTTP 200 with `items: []`.";
                if (ok200.Content.TryGetValue("application/json", out var media))
                {
                    media.Example = JsonNode.Parse("""
                        {
                          "items": [
                            {
                              "id": 101,
                              "entityTypeId": 3,
                              "entityTypeName": "Deal",
                              "propertyValues": [
                                { "propertyId": 5, "propertyName": "name",          "dataType": "String",  "value": "Acme Corp",   "isReadonly": false },
                                { "propertyId": 7, "propertyName": "closure_score", "dataType": "Decimal", "value": 0.92,          "isReadonly": true  },
                                { "propertyId": 9, "propertyName": "close_date",    "dataType": "Date",    "value": "2026-06-30",  "isReadonly": false },
                                { "propertyId": 11,"propertyName": "is_priority",   "dataType": "Bool",    "value": true,          "isReadonly": false }
                              ]
                            },
                            {
                              "id": 87,
                              "entityTypeId": 3,
                              "entityTypeName": "Deal",
                              "propertyValues": [
                                { "propertyId": 5, "propertyName": "name",          "dataType": "String",  "value": "Acme Corp Q2", "isReadonly": false },
                                { "propertyId": 7, "propertyName": "closure_score", "dataType": "Decimal", "value": 0.81,           "isReadonly": true  },
                                { "propertyId": 9, "propertyName": "close_date",    "dataType": "Date",    "value": "2026-07-15",   "isReadonly": false },
                                { "propertyId": 11,"propertyName": "is_priority",   "dataType": "Bool",    "value": false,          "isReadonly": false }
                              ]
                            }
                          ],
                          "total": 2,
                          "skip": 0,
                          "take": 50
                        }
                        """);
                }
            }

            return op;
        });

        group.MapGet("/{entityId:int}", async (int workspaceId, int entityId, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.GetByIdAsync(entityId, workspaceId, userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetEntity")
        .WithSummary("Get a single entity by id (404 if not in this workspace)")
        .Produces<EntityDetailDto>();

        group.MapPost("/", async (int workspaceId, CreateEntityRequest request, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.CreateAsync(workspaceId, userId, request, ct);
            return Results.Created($"/api/v1/workspaces/{workspaceId}/entities/{result.Id}", result);
        })
        .WithName("CreateEntity")
        .WithSummary("Create a new entity with property values (atomic transaction)")
        .Produces<EntityDetailDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapPatch("/{entityId:int}", async (int workspaceId, int entityId, UpdateEntityRequest request, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            var result = await service.UpdateAsync(entityId, workspaceId, userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateEntity")
        .WithSummary("Update property values; omitted properties keep their current values. Send propertyId from GET entity or entity-types. Use null value to clear an optional field.")
        .Produces<EntityDetailDto>()
        .ProducesValidationProblem();

        group.MapDelete("/{entityId:int}", async (int workspaceId, int entityId, IEntityService service, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = WorkspaceEndpoints.GetUserId(httpContext);
            await service.ArchiveAsync(entityId, workspaceId, userId, ct);
            return Results.NoContent();
        })
        .WithName("ArchiveEntity")
        .WithSummary("Soft-delete (archive) an entity")
        .Produces(StatusCodes.Status204NoContent);

        return routes;
    }

    private static List<EntityFilterCondition> ParseFilters(string[]? raw)
    {
        if (raw is null or { Length: 0 }) return [];
        var result = new List<EntityFilterCondition>(raw.Length);
        foreach (var s in raw)
        {
            var parts = s.Split(':', 3);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var pid))
                throw new AppException("invalid_filter_format", 400, 
                    $"Invalid filter format '{s}'. Expected propertyId:op or propertyId:op:value.");
            result.Add(new EntityFilterCondition(pid, parts[1], parts.Length == 3 ? parts[2] : null));
        }
        return result;
    }

    private static List<EntitySortField> ParseSort(string[]? raw)
    {
        if (raw is null or { Length: 0 }) return [];
        var result = new List<EntitySortField>(raw.Length);
        foreach (var s in raw)
        {
            var parts = s.Split(':', 2);
            if (!int.TryParse(parts[0], out var pid))
                throw new AppException("invalid_sort_format", 400, 
                    $"Invalid sort format '{s}'. Expected propertyId or propertyId:asc|desc.");
            var dir = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
            if (dir is not "asc" and not "desc")
                throw new AppException("invalid_sort_direction", 400, $"Invalid sort direction '{dir}'. Use 'asc' or 'desc'.");
            result.Add(new EntitySortField(pid, dir));
        }
        return result;
    }
}
