using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class PermissionRepository(RelativaDbContext db) : IPermissionRepository
{
    public async Task<List<Permission>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Permissions
            .Where(p => !p.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<List<Permission>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await db.Permissions
            .Where(p => idList.Contains(p.Id) && !p.IsArchived)
            .ToListAsync(ct);
    }
}
