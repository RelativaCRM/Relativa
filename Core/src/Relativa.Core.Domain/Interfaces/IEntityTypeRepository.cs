using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IEntityTypeRepository
{
    Task<List<EntityType>> GetAllWithPropertiesAsync(CancellationToken ct = default);
}
