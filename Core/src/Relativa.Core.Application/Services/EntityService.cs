using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Utilities;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class EntityService(
    IEntityRepository entityRepository,
    IWorkspaceAccessEvaluator workspaceAccess,
    IUserRoleWorkspaceRepository memberRepository,
    IValidator<CreateEntityRequest> createValidator,
    IValidator<UpdateEntityRequest> updateValidator,
    IOutboxWriter? auditOutboxWriter = null,
    IEntityRelationshipNotifier? relationshipNotifier = null) : IEntityService
{
    public async Task<EntityPagedResult> GetByWorkspaceAsync(
        int workspaceId,
        int userId,
        int? entityTypeId,
        string? searchQuery,
        int skip,
        int take,
        IReadOnlyList<EntityFilterCondition>? filters = null,
        IReadOnlyList<EntitySortField>? sort = null,
        int? excludeLinkedSourceRelTypeId = null,
        int? excludeLinkedTargetRelTypeId = null,
        CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "view_entities", ct);
        var callerMembership = await GetMembershipOrThrowAsync(userId, workspaceId, ct);

        var resolved = await ResolveFiltersAsync(filters, entityTypeId, userId, workspaceId, ct);

        var (items, total) = await entityRepository.GetByWorkspaceAsync(
            workspaceId,
            userId,
            callerMembership.Role.Priority,
            entityTypeId,
            searchQuery,
            skip,
            take,
            resolved,
            sort ?? [],
            excludeLinkedSourceRelTypeId,
            excludeLinkedTargetRelTypeId,
            ct);

        return new EntityPagedResult(items.Select(MapToListItem).ToList(), total, skip, take);
    }

    public async Task<EntityDetailDto> GetByIdAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "view_entities", ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new EntityNotFoundException(entityId, workspaceId);
        await EnsureCanAccessEntityAsync(userId, workspaceId, entity, ct);

        return MapToDetail(entity);
    }

    public async Task<EntityDetailDto> CreateAsync(int workspaceId, int userId, CreateEntityRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "create_entities", ct);
        await createValidator.ValidateAndThrowAsync(request, ct);

        var typeProperties = await entityRepository.GetTypePropertiesAsync(request.EntityTypeId, ct);
        if (typeProperties.Count == 0)
            throw new KeyNotFoundException($"Entity type {request.EntityTypeId} does not exist or has no properties defined.");

        if (typeProperties.All(tp => tp.Property.IsReadonly))
            throw new ArgumentException($"Entity type {request.EntityTypeId} cannot be created: all properties are read-only.");

        ValidatePropertyPayload(request.Properties, typeProperties);

        var outgoingTypes = await entityRepository.GetOutgoingRelationshipTypesAsync(request.EntityTypeId, ct);
        List<EntityRelationship>? relationshipRows = null;
        if (request.Links is { Count: > 0 })
        {
            relationshipRows = [];
            foreach (var link in request.Links)
            {
                var rt = outgoingTypes.FirstOrDefault(o => o.Id == link.RelationshipTypeId)
                    ?? throw new ArgumentException($"Relationship type {link.RelationshipTypeId} is not valid for this entity type.");

                var target = await entityRepository.GetByIdInWorkspaceAsync(link.TargetEntityId, workspaceId, ct)
                    ?? throw new ArgumentException($"Target entity {link.TargetEntityId} was not found in this workspace.");

                if (target.EntityTypeId != rt.TargetEntityTypeId)
                    throw new ArgumentException($"Target entity {link.TargetEntityId} has the wrong entity type for relationship '{rt.Name}'.");

                relationshipRows.Add(new EntityRelationship
                {
                    RelationshipTypeId = rt.Id,
                    SourceEntityId = 0,
                    TargetEntityId = target.Id,
                });
            }
        }

        var linkTypeIds = new HashSet<int>(relationshipRows?.Select(r => r.RelationshipTypeId) ?? []);
        foreach (var req in outgoingTypes.Where(t => t.IsRequired))
        {
            if (!linkTypeIds.Contains(req.Id))
                throw new ArgumentException($"Required relationship '{req.Name}' is missing.");
        }

        var entity = new Entity { EntityTypeId = request.EntityTypeId, CreatedByUserId = userId, IsArchived = false };
        var propertyValues = BuildPropertyValues(request.Properties, typeProperties);

        var created = await entityRepository.CreateAsync(entity, propertyValues, workspaceId, relationshipRows, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeEntity,
                TargetId: created.Id,
                Action: "entity_created",
                FieldName: null,
                EntityType: request.EntityTypeId.ToString(CultureInfo.InvariantCulture),
                OldValueJson: null,
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(request.Properties)),
            ct);
            await PublishEntityRefreshDomainAsync(
                auditOutboxWriter,
                created.Id,
                created.EntityTypeId,
                workspaceId,
                userId,
                "created",
                ct);
        }

        var detail = await entityRepository.GetByIdInWorkspaceAsync(created.Id, workspaceId, ct);
        return MapToDetail(detail!);
    }

    public async Task<EntityDetailDto> UpdateAsync(int entityId, int workspaceId, int userId, UpdateEntityRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "edit_entities", ct);
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new EntityNotFoundException(entityId, workspaceId);
        await EnsureCanAccessEntityAsync(userId, workspaceId, entity, ct);

        if (entity.IsArchived)
        {
            await RequirePermission(userId, workspaceId, "edit_archived_entities", ct);
        }

        var typeProperties = await entityRepository.GetTypePropertiesAsync(entity.EntityTypeId, ct);
        ValidateRequestPropertyIds(request.Properties, typeProperties);
        var merged = MergePropertyPayload(entity, request.Properties);
        ValidateReadonlyPreserved(entity, merged, typeProperties);
        var readonlyIds = typeProperties
            .Where(tp => tp.Property.IsReadonly)
            .Select(tp => tp.PropertyId)
            .ToHashSet();
        ValidatePropertyPayload(
            merged.Where(p => !readonlyIds.Contains(p.PropertyId)).ToList(),
            typeProperties);

        var propertyValues = BuildPropertyValues(merged, typeProperties);
        await entityRepository.UpdateAsync(entity, propertyValues, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeEntity,
                TargetId: entityId,
                Action: "entity_updated",
                FieldName: "properties",
                EntityType: entity.EntityTypeId.ToString(CultureInfo.InvariantCulture),
                OldValueJson: null,
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(merged)),
            ct);
            await PublishEntityRefreshDomainAsync(
                auditOutboxWriter,
                entityId,
                entity.EntityTypeId,
                workspaceId,
                userId,
                "updated",
                ct);
        }

        var updated = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct);
        return MapToDetail(updated!);
    }

    public async Task ArchiveAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "delete_entities", ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new EntityNotFoundException(entityId, workspaceId);
        await EnsureCanAccessEntityAsync(userId, workspaceId, entity, ct);

        var typeProps = await entityRepository.GetTypePropertiesAsync(entity.EntityTypeId, ct);
        if (typeProps.Count > 0 && typeProps.All(tp => tp.Property.IsReadonly))
            throw new ArgumentException("Cannot delete an entity whose properties are all read-only.");

        await entityRepository.ArchiveAsync(entity.Id, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
            new AuditEventContract(
                EventId: Guid.NewGuid(),
                SchemaVersion: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                SourceService: "core",
                ActorUserId: userId,
                AuditScope: AuditRouting.ScopeEntity,
                TargetId: entityId,
                Action: "entity_archived",
                FieldName: "is_archived",
                EntityType: entity.EntityTypeId.ToString(CultureInfo.InvariantCulture),
                OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = false }),
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = true })),
            ct);
            await PublishEntityRefreshDomainAsync(
                auditOutboxWriter,
                entityId,
                entity.EntityTypeId,
                workspaceId,
                userId,
                "archived",
                ct);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        if (!await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, permission, ct))
            throw new ForbiddenAccessException($"You do not have the '{permission}' permission in this workspace.");
    }

    private async Task<UserRoleWorkspace> GetMembershipOrThrowAsync(int userId, int workspaceId, CancellationToken ct)
    {
        var membership = await memberRepository.GetAsync(userId, workspaceId, ct);
        if (membership?.Role is null)
            throw new ForbiddenAccessException("Access denied");
        return membership;
    }

    private async Task<IReadOnlyList<ResolvedFilterCondition>> ResolveFiltersAsync(
        IReadOnlyList<EntityFilterCondition>? raw,
        int? entityTypeId,
        int userId,
        int workspaceId,
        CancellationToken ct)
    {
        if (raw is null or { Count: 0 }) return [];

        if (entityTypeId is null)
            throw new ArgumentException("entityTypeId is required when filters are specified.");

        var typeProps = await entityRepository.GetTypePropertiesAsync(entityTypeId.Value, ct);
        if (typeProps.Count == 0)
            throw new KeyNotFoundException($"Entity type {entityTypeId} does not exist or has no properties defined.");

        var propMap = typeProps.ToDictionary(tp => tp.PropertyId, tp => tp.Property);
        var hasViewAnalytics = await workspaceAccess.HasWorkspacePermissionAsync(userId, workspaceId, "view_analytics", ct);

        var resolved = new List<ResolvedFilterCondition>();
        foreach (var f in raw)
        {
            if (!propMap.TryGetValue(f.PropertyId, out var prop))
                throw new ArgumentException($"Property {f.PropertyId} does not belong to entity type {entityTypeId}.");

            if (prop.IsReadonly && !hasViewAnalytics)
                continue;

            resolved.Add(ResolveFilter(f, prop));
        }
        return resolved;
    }

    private static ResolvedFilterCondition ResolveFilter(EntityFilterCondition f, Property prop) => prop.DataType switch
    {
        PropertyDataType.String  => new ResolvedFilterCondition(f.PropertyId, prop.DataType, f.Op, f.Value, null, null, null, null),
        PropertyDataType.Int     => new ResolvedFilterCondition(f.PropertyId, prop.DataType, f.Op, null,
            int.TryParse(f.Value, out var i) ? i : throw new ArgumentException($"Property '{prop.Name}' expects an integer filter value."), null, null, null),
        PropertyDataType.Decimal => new ResolvedFilterCondition(f.PropertyId, prop.DataType, f.Op, null, null,
            decimal.TryParse(f.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : throw new ArgumentException($"Property '{prop.Name}' expects a decimal filter value."), null, null),
        PropertyDataType.Bool    => new ResolvedFilterCondition(f.PropertyId, prop.DataType, f.Op, null, null, null,
            bool.TryParse(f.Value, out var b) ? b : throw new ArgumentException($"Property '{prop.Name}' expects a boolean filter value (true/false)."), null),
        PropertyDataType.Date    => new ResolvedFilterCondition(f.PropertyId, prop.DataType, f.Op, null, null, null, null,
            DateOnly.TryParseExact(f.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : throw new ArgumentException($"Property '{prop.Name}' expects a date filter value in yyyy-MM-dd format.")),
        _ => throw new ArgumentException($"Unsupported data type '{prop.DataType}' for filter on property '{prop.Name}'.")
    };

    private async Task EnsureCanAccessEntityAsync(int userId, int workspaceId, Entity entity, CancellationToken ct)
    {
        if (entity.CreatedByUserId == userId)
            return;

        var userIds = new[] { userId, entity.CreatedByUserId };
        var priorities = await memberRepository.GetRolePrioritiesByUserIdsAsync(workspaceId, userIds, ct);
        if (!priorities.TryGetValue(userId, out var callerPriority) ||
            !priorities.TryGetValue(entity.CreatedByUserId, out var creatorPriority))
        {
            throw new ForbiddenAccessException("Access denied");
        }

        // Lower priority value means higher authority.
        if (callerPriority >= creatorPriority)
            throw new ForbiddenAccessException("Access denied");
    }

    /// <summary>
    /// Validates the raw request body: no duplicate property ids, every id belongs to the entity type.
    /// </summary>
    private static void ValidateRequestPropertyIds(
        List<PropertyValueInput> request,
        List<EntityTypeProperty> typeProperties)
    {
        var allowedIds = typeProperties.Select(tp => tp.PropertyId).ToHashSet();

        var duplicates = request.GroupBy(p => p.PropertyId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Duplicate property ids in request: {string.Join(", ", duplicates)}.");

        var unknown = request.Select(p => p.PropertyId).Except(allowedIds).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Properties {string.Join(", ", unknown)} do not belong to this entity type.");
    }

    /// <summary>
    /// For updates, overlays <paramref name="request"/> onto current <see cref="Entity.EntityPropertyValues"/>.
    /// Omitted properties keep their stored values; explicit <c>null</c> clears an optional field (no row written).
    /// </summary>
    private static List<PropertyValueInput> MergePropertyPayload(Entity entity, List<PropertyValueInput> request)
    {
        var reqById = request.ToDictionary(p => p.PropertyId, p => p.Value);
        var existingStrings = entity.EntityPropertyValues.ToDictionary(
            epv => epv.PropertyId,
            epv => ValueToInputString(epv));

        var mergedIds = existingStrings.Keys.Union(reqById.Keys).OrderBy(id => id).ToList();
        var result = new List<PropertyValueInput>(mergedIds.Count);
        foreach (var id in mergedIds)
        {
            if (reqById.TryGetValue(id, out var requested))
                result.Add(new PropertyValueInput(id, requested));
            else if (existingStrings.TryGetValue(id, out var kept))
                result.Add(new PropertyValueInput(id, kept));
        }

        return result;
    }

    private static string? ValueToInputString(EntityPropertyValue pv) => pv.Property.DataType switch
    {
        PropertyDataType.String  => pv.ValueString,
        PropertyDataType.Int     => pv.ValueInt?.ToString(CultureInfo.InvariantCulture),
        PropertyDataType.Decimal => pv.ValueDecimal?.ToString(CultureInfo.InvariantCulture),
        PropertyDataType.Bool    => pv.ValueBool?.ToString(),
        PropertyDataType.Date    => pv.ValueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _                        => null
    };

    private static void ValidateReadonlyPreserved(
        Entity entity,
        List<PropertyValueInput> merged,
        List<EntityTypeProperty> typeProperties)
    {
        foreach (var tp in typeProperties.Where(tp => tp.Property.IsReadonly))
        {
            var mergedEntry = merged.FirstOrDefault(m => m.PropertyId == tp.PropertyId);
            if (mergedEntry is null)
                continue;

            var mergedVal = mergedEntry.Value;
            var existing = entity.EntityPropertyValues.FirstOrDefault(e => e.PropertyId == tp.PropertyId);
            var existingStr = existing is null ? null : ValueToInputString(existing);
            if (!string.Equals(mergedVal, existingStr, StringComparison.Ordinal))
                throw new ArgumentException($"Property '{tp.Property.Name}' is read-only.");
        }
    }

    /// <summary>
    /// Validates that:
    /// - All submitted property ids belong to the entity type.
    /// - All required properties have a non-null value.
    /// - Required String properties have a non-empty, non-whitespace value.
    /// - No duplicate property ids are supplied.
    /// </summary>
    private static void ValidatePropertyPayload(
        List<PropertyValueInput> submitted,
        List<EntityTypeProperty> typeProperties)
    {
        var allowedIds = typeProperties.Select(tp => tp.PropertyId).ToHashSet();
        var requiredIds = typeProperties.Where(tp => tp.IsRequired).Select(tp => tp.PropertyId).ToHashSet();

        var duplicates = submitted.GroupBy(p => p.PropertyId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Duplicate property ids submitted: {string.Join(", ", duplicates)}.");

        var unknown = submitted.Select(p => p.PropertyId).Except(allowedIds).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Properties {string.Join(", ", unknown)} do not belong to this entity type.");

        var submittedIds = submitted.Where(p => p.Value is not null).Select(p => p.PropertyId).ToHashSet();
        var missingRequired = requiredIds.Except(submittedIds).ToList();
        if (missingRequired.Count > 0)
        {
            var details = typeProperties
                .Where(tp => missingRequired.Contains(tp.PropertyId))
                .Select(tp => $"{tp.Property.Name} (propertyId {tp.PropertyId})");
            throw new ArgumentException($"Required properties are missing: {string.Join(", ", details)}.");
        }

        var emptyStringIds = typeProperties
            .Where(tp => tp.IsRequired && tp.Property.DataType == PropertyDataType.String)
            .Where(tp => submitted.Any(p => p.PropertyId == tp.PropertyId && string.IsNullOrWhiteSpace(p.Value)))
            .Select(tp => tp.PropertyId)
            .ToList();

        if (emptyStringIds.Count > 0)
        {
            var emptyDetails = typeProperties
                .Where(tp => emptyStringIds.Contains(tp.PropertyId))
                .Select(tp => $"{tp.Property.Name} (propertyId {tp.PropertyId})");
            throw new ArgumentException($"Required string properties cannot be empty or whitespace: {string.Join(", ", emptyDetails)}.");
        }

        foreach (var tp in typeProperties.Where(tp => tp.Property.IsReadonly))
        {
            var s = submitted.FirstOrDefault(x => x.PropertyId == tp.PropertyId);
            if (s?.Value is not null)
                throw new ArgumentException($"Property '{tp.Property.Name}' is read-only.");
        }
    }

    /// <summary>
    /// Converts the string-based input values to typed <see cref="EntityPropertyValue"/> rows,
    /// choosing the correct value column based on the property's <see cref="PropertyDataType"/>.
    /// </summary>
    private static List<EntityPropertyValue> BuildPropertyValues(
        List<PropertyValueInput> submitted,
        List<EntityTypeProperty> typeProperties)
    {
        var propertyMap = typeProperties.ToDictionary(tp => tp.PropertyId, tp => tp.Property);
        var result = new List<EntityPropertyValue>();

        foreach (var input in submitted.Where(p => p.Value is not null))
        {
            if (!propertyMap.TryGetValue(input.PropertyId, out var prop))
                continue;

            var pv = new EntityPropertyValue { PropertyId = input.PropertyId };

            switch (prop.DataType)
            {
                case PropertyDataType.String:
                    if (prop.AllowedValues.Count > 0
                        && !prop.AllowedValues.Any(av => string.Equals(av.Value, input.Value, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException(
                            $"'{input.Value}' is not a valid value for '{prop.Name}'. " +
                            $"Allowed: {string.Join(", ", prop.AllowedValues.Select(av => av.Value))}.");
                    }
                    pv.ValueString = input.Value;
                    break;

                case PropertyDataType.Int:
                    if (!int.TryParse(input.Value, out var intVal))
                        throw new ArgumentException($"Property '{prop.Name}' expects an integer value.");
                    pv.ValueInt = intVal;
                    break;

                case PropertyDataType.Decimal:
                    if (!decimal.TryParse(input.Value,
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var decVal))
                        throw new ArgumentException($"Property '{prop.Name}' expects a decimal value.");
                    pv.ValueDecimal = decVal;
                    break;

                case PropertyDataType.Bool:
                    if (!bool.TryParse(input.Value, out var boolVal))
                        throw new ArgumentException($"Property '{prop.Name}' expects a boolean value (true/false).");
                    pv.ValueBool = boolVal;
                    break;

                case PropertyDataType.Date:
                    if (!DateOnly.TryParseExact(input.Value, "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var dateVal))
                        throw new ArgumentException($"Property '{prop.Name}' expects a date in yyyy-MM-dd format.");
                    pv.ValueDate = dateVal;
                    break;
            }

            result.Add(pv);
        }

        return result;
    }

    private static EntityListItemDto MapToListItem(Entity e) => new(
        e.Id,
        e.EntityTypeId,
        e.EntityType.Name,
        e.EntityType.DisplayName ?? DisplayNameHelper.Humanize(e.EntityType.Name),
        MapPropertyValues(e));

    private static EntityDetailDto MapToDetail(Entity e)
    {
        const int previewCap = 12;
        return new EntityDetailDto(
            e.Id,
            e.EntityTypeId,
            e.EntityType.Name,
            e.EntityType.DisplayName ?? DisplayNameHelper.Humanize(e.EntityType.Name),
            e.IsArchived,
            MapPropertyValues(e),
            MapOutbound(e, previewCap),
            MapInbound(e, previewCap));
    }

    private static List<EntityRelationshipRefDto> MapOutbound(Entity e, int previewCap) =>
        e.SourceRelationships
            .OrderBy(r => r.Id)
            .Select(r => new EntityRelationshipRefDto(
                r.Id,
                r.RelationshipTypeId,
                r.RelationshipType.Name,
                r.RelationshipType.DisplayName ?? DisplayNameHelper.Humanize(r.RelationshipType.Name),
                r.TargetEntityId,
                r.TargetEntity.EntityType.Name,
                r.TargetEntity.EntityType.DisplayName ?? DisplayNameHelper.Humanize(r.TargetEntity.EntityType.Name),
                MapPreview(r.TargetEntity, previewCap)))
            .ToList();

    private static List<EntityRelationshipRefDto> MapInbound(Entity e, int previewCap) =>
        e.TargetRelationships
            .OrderBy(r => r.Id)
            .Select(r => new EntityRelationshipRefDto(
                r.Id,
                r.RelationshipTypeId,
                r.RelationshipType.Name,
                r.RelationshipType.DisplayName ?? DisplayNameHelper.Humanize(r.RelationshipType.Name),
                r.SourceEntityId,
                r.SourceEntity.EntityType.Name,
                r.SourceEntity.EntityType.DisplayName ?? DisplayNameHelper.Humanize(r.SourceEntity.EntityType.Name),
                MapPreview(r.SourceEntity, previewCap)))
            .ToList();

    private static List<EntityPropertyValueDto> MapPreview(Entity related, int cap) =>
        related.EntityPropertyValues
            .OrderBy(pv => pv.PropertyId)
            .Take(cap)
            .Select(pv => new EntityPropertyValueDto(
                pv.PropertyId,
                pv.Property.Name,
                pv.Property.DisplayName ?? DisplayNameHelper.Humanize(pv.Property.Name),
                pv.Property.DataType.ToString(),
                ResolveValue(pv),
                pv.Property.IsReadonly))
            .ToList();

    private static List<EntityPropertyValueDto> MapPropertyValues(Entity e) =>
        e.EntityPropertyValues
            .OrderBy(pv => pv.PropertyId)
            .Select(pv => new EntityPropertyValueDto(
                pv.PropertyId,
                pv.Property.Name,
                pv.Property.DisplayName ?? DisplayNameHelper.Humanize(pv.Property.Name),
                pv.Property.DataType.ToString(),
                ResolveValue(pv),
                pv.Property.IsReadonly))
            .ToList();

    private static object? ResolveValue(EntityPropertyValue pv) => pv.Property.DataType switch
    {
        PropertyDataType.String  => pv.ValueString,
        PropertyDataType.Int     => (object?)pv.ValueInt,
        PropertyDataType.Decimal => pv.ValueDecimal,
        PropertyDataType.Bool    => pv.ValueBool,
        PropertyDataType.Date    => pv.ValueDate?.ToString("yyyy-MM-dd"),
        _                        => null
    };

    public async Task<EntityRelationshipRefDto> CreateRelationshipAsync(int workspaceId, int userId, CreateEntityRelationshipRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "edit_entities", ct);

        var relType = await entityRepository.GetRelationshipTypeByIdAsync(request.RelationshipTypeId, ct)
            ?? throw new ArgumentException($"Relationship type {request.RelationshipTypeId} does not exist.");

        var source = await entityRepository.GetByIdInWorkspaceAsync(request.SourceEntityId, workspaceId, ct)
            ?? throw new ArgumentException($"Source entity {request.SourceEntityId} not found in workspace {workspaceId}.");

        var target = await entityRepository.GetByIdInWorkspaceAsync(request.TargetEntityId, workspaceId, ct)
            ?? throw new ArgumentException($"Target entity {request.TargetEntityId} not found in workspace {workspaceId}.");

        if (source.IsArchived || target.IsArchived)
            throw new ArgumentException("Cannot create a relationship involving an archived entity.");

        var targetTypeProps = await entityRepository.GetTypePropertiesAsync(target.EntityTypeId, ct);
        if (targetTypeProps.Count > 0 && targetTypeProps.All(tp => tp.Property.IsReadonly))
            throw new ArgumentException("Cannot link an entity whose properties are all read-only.");

        if (source.EntityTypeId != relType.SourceEntityTypeId)
            throw new ArgumentException($"Source entity type does not match relationship type '{relType.Name}'.");

        if (target.EntityTypeId != relType.TargetEntityTypeId)
            throw new ArgumentException($"Target entity type does not match relationship type '{relType.Name}'.");

        if (relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.ManyToOne
            || relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.OneToOne)
        {
            if (await entityRepository.CountRelationshipsBySourceAsync(source.Id, relType.Id, ct) > 0)
                throw new ArgumentException($"Source entity already has a '{relType.Name}' link (cardinality constraint).");
        }

        if (relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.OneToOne)
        {
            if (await entityRepository.CountRelationshipsByTargetAsync(target.Id, relType.Id, ct) > 0)
                throw new ArgumentException($"Target entity already has a '{relType.Name}' link (cardinality constraint).");
        }

        var rel = await entityRepository.AddRelationshipAsync(new Persistence.Entities.EntityRelationship
        {
            SourceEntityId = source.Id,
            TargetEntityId = target.Id,
            RelationshipTypeId = relType.Id,
        }, ct);

        if (relationshipNotifier is not null)
            await relationshipNotifier.NotifyChangedAsync(workspaceId, ct, source.Id, target.Id);

        const int previewCap = 12;
        return new EntityRelationshipRefDto(
            rel.Id,
            relType.Id,
            relType.Name,
            relType.DisplayName ?? DisplayNameHelper.Humanize(relType.Name),
            target.Id,
            relType.TargetEntityType.Name,
            relType.TargetEntityType.DisplayName ?? DisplayNameHelper.Humanize(relType.TargetEntityType.Name),
            MapPreview(target, previewCap));
    }

    public async Task DeleteRelationshipAsync(int workspaceId, int userId, int relationshipId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "edit_entities", ct);

        var rel = await entityRepository.GetRelationshipByIdAsync(relationshipId, ct)
            ?? throw new KeyNotFoundException($"Relationship {relationshipId} not found.");

        if (!rel.SourceEntity.EntityWorkspaces.Any(ew => ew.WorkspaceId == workspaceId))
            throw new ForbiddenAccessException("Relationship does not belong to an entity in this workspace.");

        if (rel.RelationshipType.IsRequired)
        {
            var remaining = await entityRepository.CountRelationshipsBySourceAsync(
                rel.SourceEntityId, rel.RelationshipTypeId, ct);
            if (remaining <= 1)
                throw new ArgumentException(
                    $"Cannot unlink: the entity requires at least one '{rel.RelationshipType.Name}' link.");
        }

        var targetTypeProps = await entityRepository.GetTypePropertiesAsync(rel.TargetEntity.EntityTypeId, ct);
        if (targetTypeProps.Count > 0 && targetTypeProps.All(tp => tp.Property.IsReadonly))
            throw new ArgumentException("Cannot unlink an entity whose properties are all read-only.");

        var sourceId = rel.SourceEntityId;
        var targetId = rel.TargetEntityId;
        await entityRepository.RemoveRelationshipAsync(relationshipId, ct);

        if (relationshipNotifier is not null)
            await relationshipNotifier.NotifyChangedAsync(workspaceId, ct, sourceId, targetId);
    }

    public async Task<EntityRelationshipRefDto> ReassignRelationshipAsync(
        int workspaceId, int userId, int relationshipId,
        ReassignEntityRelationshipRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "edit_entities", ct);

        if ((request.NewSourceEntityId == null) == (request.NewTargetEntityId == null))
            throw new ArgumentException("Exactly one of NewSourceEntityId or NewTargetEntityId must be provided.");

        var rel = await entityRepository.GetRelationshipByIdAsync(relationshipId, ct)
            ?? throw new KeyNotFoundException($"Relationship {relationshipId} not found.");

        if (!rel.SourceEntity.EntityWorkspaces.Any(ew => ew.WorkspaceId == workspaceId))
            throw new ForbiddenAccessException("Relationship does not belong to an entity in this workspace.");

        var relType = rel.RelationshipType;
        EntityRelationshipRefDto result;

        if (request.NewTargetEntityId.HasValue)
        {
            var newTarget = await entityRepository.GetByIdInWorkspaceAsync(request.NewTargetEntityId.Value, workspaceId, ct)
                ?? throw new ArgumentException($"Target entity {request.NewTargetEntityId.Value} not found in workspace.");
            if (newTarget.IsArchived)
                throw new ArgumentException("Cannot link an archived entity.");
            if (newTarget.EntityTypeId != relType.TargetEntityTypeId)
                throw new ArgumentException($"Entity type does not match relationship type '{relType.Name}'.");
            var targetTypeProps = await entityRepository.GetTypePropertiesAsync(newTarget.EntityTypeId, ct);
            if (targetTypeProps.Count > 0 && targetTypeProps.All(tp => tp.Property.IsReadonly))
                throw new ArgumentException("Cannot link an entity whose properties are all read-only.");
            if (relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.OneToOne)
            {
                if (await entityRepository.CountRelationshipsByTargetAsync(newTarget.Id, relType.Id, ct) > 0)
                    throw new ArgumentException($"Target entity already has a '{relType.Name}' link (cardinality constraint).");
            }

            await entityRepository.UpdateRelationshipTargetAsync(relationshipId, newTarget.Id, ct);

            if (auditOutboxWriter is not null)
            {
                await auditOutboxWriter.EnqueueAuditAsync(new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeEntity,
                    TargetId: rel.SourceEntityId,
                    Action: "relationship_reassigned",
                    FieldName: relType.Name,
                    EntityType: relType.SourceEntityTypeId.ToString(CultureInfo.InvariantCulture),
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(rel.TargetEntityId),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(newTarget.Id)), ct);

                await PublishEntityRefreshDomainAsync(
                    auditOutboxWriter, rel.SourceEntityId, relType.SourceEntityTypeId,
                    workspaceId, userId, "updated", ct);
            }

            if (relationshipNotifier is not null)
                await relationshipNotifier.NotifyChangedAsync(workspaceId, ct, rel.SourceEntityId, rel.TargetEntityId, newTarget.Id);

            const int previewCap = 12;
            result = new EntityRelationshipRefDto(rel.Id, relType.Id, relType.Name,
                relType.DisplayName ?? DisplayNameHelper.Humanize(relType.Name),
                newTarget.Id, relType.TargetEntityType.Name,
                relType.TargetEntityType.DisplayName ?? DisplayNameHelper.Humanize(relType.TargetEntityType.Name),
                MapPreview(newTarget, previewCap));
        }
        else
        {
            var newSource = await entityRepository.GetByIdInWorkspaceAsync(request.NewSourceEntityId!.Value, workspaceId, ct)
                ?? throw new ArgumentException($"Source entity {request.NewSourceEntityId.Value} not found in workspace.");
            if (newSource.IsArchived)
                throw new ArgumentException("Cannot link an archived entity.");
            if (newSource.EntityTypeId != relType.SourceEntityTypeId)
                throw new ArgumentException($"Entity type does not match relationship type '{relType.Name}'.");
            var sourceTypeProps = await entityRepository.GetTypePropertiesAsync(newSource.EntityTypeId, ct);
            if (sourceTypeProps.Count > 0 && sourceTypeProps.All(tp => tp.Property.IsReadonly))
                throw new ArgumentException("Cannot link an entity whose properties are all read-only.");
            if (relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.ManyToOne
                || relType.RelationshipCardinality == Persistence.Entities.RelationshipCardinality.OneToOne)
            {
                if (await entityRepository.CountRelationshipsBySourceAsync(newSource.Id, relType.Id, ct) > 0)
                    throw new ArgumentException($"Source entity already has a '{relType.Name}' link (cardinality constraint).");
            }

            await entityRepository.UpdateRelationshipSourceAsync(relationshipId, newSource.Id, ct);

            if (auditOutboxWriter is not null)
            {
                await auditOutboxWriter.EnqueueAuditAsync(new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "core",
                    ActorUserId: userId,
                    AuditScope: AuditRouting.ScopeEntity,
                    TargetId: rel.TargetEntityId,
                    Action: "relationship_reassigned",
                    FieldName: relType.Name,
                    EntityType: relType.TargetEntityTypeId.ToString(CultureInfo.InvariantCulture),
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(rel.SourceEntityId),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(newSource.Id)), ct);

                await PublishEntityRefreshDomainAsync(
                    auditOutboxWriter, rel.TargetEntityId, relType.TargetEntityTypeId,
                    workspaceId, userId, "updated", ct);
            }

            if (relationshipNotifier is not null)
                await relationshipNotifier.NotifyChangedAsync(workspaceId, ct, rel.SourceEntityId, rel.TargetEntityId, newSource.Id);

            const int previewCap = 12;
            result = new EntityRelationshipRefDto(rel.Id, relType.Id, relType.Name,
                relType.DisplayName ?? DisplayNameHelper.Humanize(relType.Name),
                newSource.Id, relType.SourceEntityType.Name,
                relType.SourceEntityType.DisplayName ?? DisplayNameHelper.Humanize(relType.SourceEntityType.Name),
                MapPreview(newSource, previewCap));
        }

        return result;
    }

    private static Task PublishEntityRefreshDomainAsync(
        IOutboxWriter outboxWriter,
        int entityId,
        int entityTypeId,
        int workspaceId,
        int actorUserId,
        string action,
        CancellationToken ct)
    {
        var sagaInstanceId = Guid.NewGuid();
        var envelope = new DomainMessageEnvelope(
            SchemaVersion: MessagingSchemaVersions.V1,
            MessageId: Guid.NewGuid(),
            CorrelationId: sagaInstanceId,
            SagaInstanceId: sagaInstanceId,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: "core",
            PayloadTypeName: DomainPayloadTypes.EntityAnalysisRefreshV1,
            PayloadJson: JsonSerializer.Serialize(new EntityAnalysisRefreshPayloadV1(
                EntityId: entityId,
                EntityTypeId: entityTypeId,
                WorkspaceId: workspaceId,
                ActorUserId: actorUserId,
                Action: action,
                SourceUpdatedAtUtc: DateTimeOffset.UtcNow)));

        return outboxWriter.EnqueueDomainAsync(
            DomainRouting.RoutingKeyCoreEntity(DomainRouting.CoreEntityVerbChanged),
            envelope,
            ct);
    }
}
