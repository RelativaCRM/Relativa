using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class UserRoleOrganizationRepository(RelativaDbContext db) : IUserRoleOrganizationRepository
{
    public async Task<UserRoleOrganization?> GetAsync(int userId, int organizationId, CancellationToken ct = default)
    {
        return await db.UserRoleOrganizations
            .Include(uro => uro.User)
            .Include(uro => uro.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(uro => uro.UserId == userId && uro.OrganizationId == organizationId && !uro.IsArchived, ct);
    }

    public async Task<List<UserRoleOrganization>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.UserRoleOrganizations
            .Include(uro => uro.User)
            .Include(uro => uro.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(uro => uro.OrganizationId == organizationId && !uro.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<List<UserRoleOrganization>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserRoleOrganizations
            .Include(uro => uro.User)
            .Include(uro => uro.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(uro => uro.UserId == userId && !uro.IsArchived)
            .ToListAsync(ct);
    }

    public async Task AddAsync(UserRoleOrganization member, CancellationToken ct = default)
    {
        db.UserRoleOrganizations.Add(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserRoleOrganization member, CancellationToken ct = default)
    {
        db.UserRoleOrganizations.Update(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(UserRoleOrganization member, CancellationToken ct = default)
    {
        db.UserRoleOrganizations.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}
