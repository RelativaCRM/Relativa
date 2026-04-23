using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class EntityTypeRepository(RelativaDbContext db) : IEntityTypeRepository
{
    public async Task<List<EntityType>> GetAllWithPropertiesAsync(CancellationToken ct = default)
    {
        return await db.EntityTypes
            .AsNoTracking()
            .Include(et => et.EntityTypeProperties)
                .ThenInclude(etp => etp.Property)
            .OrderBy(et => et.Id)
            .ToListAsync(ct);
    }
}
