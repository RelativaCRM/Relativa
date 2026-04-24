using Relativa.Core.Application.DTOs.EntityType;

namespace Relativa.Core.Application.Interfaces;

public interface IEntityTypeService
{
    Task<List<EntityTypeDto>> GetAllAsync(CancellationToken ct = default);
}
