namespace Relativa.Core.Application.DTOs.Entity;

public sealed record CreateEntityRequest(
    int EntityTypeId,
    List<PropertyValueInput> Properties);
