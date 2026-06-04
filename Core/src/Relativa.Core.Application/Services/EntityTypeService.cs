using Relativa.Core.Application.DTOs.EntityType;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Utilities;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class EntityTypeService(IEntityTypeRepository entityTypeRepository) : IEntityTypeService
{
    public async Task<List<EntityTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var types = await entityTypeRepository.GetAllWithPropertiesAsync(ct);

        return types.Select(et => new EntityTypeDto(
            et.Id,
            et.Name,
            et.DisplayName ?? DisplayNameHelper.Humanize(et.Name),
            et.IsStandalone,
            et.SourceRelationshipTypes
                .OrderBy(rt => rt.Id)
                .Select(rt => new OutgoingRelationshipDto(
                    rt.Id,
                    rt.Name,
                    rt.DisplayName ?? DisplayNameHelper.Humanize(rt.Name),
                    rt.TargetEntityTypeId,
                    rt.TargetEntityType.Name,
                    rt.TargetEntityType.DisplayName ?? DisplayNameHelper.Humanize(rt.TargetEntityType.Name),
                    rt.IsRequired,
                    rt.RelationshipCardinality.ToDatabaseValue()))
                .ToList(),
            et.TargetRelationshipTypes
                .OrderBy(rt => rt.Id)
                .Select(rt => new IncomingRelationshipDto(
                    rt.Id,
                    rt.Name,
                    rt.DisplayName ?? DisplayNameHelper.Humanize(rt.Name),
                    rt.SourceEntityTypeId,
                    rt.SourceEntityType.Name,
                    rt.SourceEntityType.DisplayName ?? DisplayNameHelper.Humanize(rt.SourceEntityType.Name),
                    rt.IsRequired,
                    rt.RelationshipCardinality.ToDatabaseValue()))
                .ToList(),
            et.EntityTypeProperties
                .OrderBy(etp => etp.PropertyId)
                .Select(etp => new EntityTypePropertyDto(
                    etp.PropertyId,
                    etp.Property.Name,
                    etp.Property.DisplayName ?? DisplayNameHelper.Humanize(etp.Property.Name),
                    etp.Property.DataType.ToString(),
                    etp.IsRequired,
                    etp.Property.IsReadonly,
                    etp.Property.AllowedValues
                        .Select(av => new AllowedValueDto(av.Value, av.DisplayName ?? DisplayNameHelper.Humanize(av.Value)))
                        .ToList()))
                .ToList()))
            .ToList();
    }
}
