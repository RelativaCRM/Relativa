using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class EntityService(
    IEntityRepository entityRepository,
    IUserRoleWorkspaceRepository memberRepository,
    IValidator<CreateEntityRequest> createValidator,
    IValidator<UpdateEntityRequest> updateValidator,
    IOutboxWriter? auditOutboxWriter = null) : IEntityService
{
    public async Task<List<EntityListItemDto>> GetByWorkspaceAsync(int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "view_entities", ct);

        var entities = await entityRepository.GetByWorkspaceAsync(workspaceId, ct);
        return entities.Select(MapToListItem).ToList();
    }

    public async Task<EntityDetailDto> GetByIdAsync(int entityId, int workspaceId, int userId, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "view_entities", ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found in workspace {workspaceId}.");

        return MapToDetail(entity);
    }

    public async Task<EntityDetailDto> CreateAsync(int workspaceId, int userId, CreateEntityRequest request, CancellationToken ct = default)
    {
        await RequirePermission(userId, workspaceId, "manage_entities", ct);
        await createValidator.ValidateAndThrowAsync(request, ct);

        var typeProperties = await entityRepository.GetTypePropertiesAsync(request.EntityTypeId, ct);
        if (typeProperties.Count == 0)
            throw new KeyNotFoundException($"Entity type {request.EntityTypeId} does not exist or has no properties defined.");

        ValidatePropertyPayload(request.Properties, typeProperties);

        var entity = new Entity { EntityTypeId = request.EntityTypeId, IsArchived = false };
        var propertyValues = BuildPropertyValues(request.Properties, typeProperties);

        var created = await entityRepository.CreateAsync(entity, propertyValues, workspaceId, ct);

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
        await RequirePermission(userId, workspaceId, "manage_entities", ct);
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found in workspace {workspaceId}.");

        if (entity.IsArchived)
            throw new ArgumentException("Cannot update an archived entity.");

        var typeProperties = await entityRepository.GetTypePropertiesAsync(entity.EntityTypeId, ct);
        ValidateRequestPropertyIds(request.Properties, typeProperties);
        var merged = MergePropertyPayload(entity, request.Properties);
        ValidatePropertyPayload(merged, typeProperties);

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
        await RequirePermission(userId, workspaceId, "manage_entities", ct);

        var entity = await entityRepository.GetByIdInWorkspaceAsync(entityId, workspaceId, ct)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found in workspace {workspaceId}.");

        await entityRepository.ArchiveAsync(entity, ct);

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

    private async Task<UserRoleWorkspace> RequireMembership(int userId, int workspaceId, CancellationToken ct)
    {
        return await memberRepository.GetAsync(userId, workspaceId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this workspace.");
    }

    private async Task RequirePermission(int userId, int workspaceId, string permission, CancellationToken ct)
    {
        var membership = await RequireMembership(userId, workspaceId, ct);
        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this workspace.");
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

    /// <summary>
    /// Validates that:
    /// - All submitted property ids belong to the entity type.
    /// - All required properties have a non-null value.
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
        MapPropertyValues(e));

    private static EntityDetailDto MapToDetail(Entity e) => new(
        e.Id,
        e.EntityTypeId,
        e.EntityType.Name,
        MapPropertyValues(e));

    private static List<EntityPropertyValueDto> MapPropertyValues(Entity e) =>
        e.EntityPropertyValues
            .OrderBy(pv => pv.PropertyId)
            .Select(pv => new EntityPropertyValueDto(
                pv.PropertyId,
                pv.Property.Name,
                pv.Property.DataType.ToString(),
                ResolveValue(pv)))
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
