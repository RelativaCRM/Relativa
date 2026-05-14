using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class JoinRequestRepository(RelativaDbContext db) : IJoinRequestRepository
{
    public async Task<OrganizationJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.OrganizationJoinRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<OrganizationJoinRequest>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.OrganizationJoinRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .Where(r => r.OrganizationId == organizationId && r.Status == "Pending")
            .ToListAsync(ct);
    }

    public async Task<List<OrganizationJoinRequest>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await db.OrganizationJoinRequests
            .Include(r => r.Organization)
            .Include(r => r.User)
            .Include(r => r.ReviewedBy)
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<OrganizationJoinRequest?> GetPendingAsync(int userId, int organizationId, CancellationToken ct = default)
    {
        return await db.OrganizationJoinRequests
            .FirstOrDefaultAsync(r => r.UserId == userId && r.OrganizationId == organizationId && r.Status == "Pending", ct);
    }

    public async Task AddAsync(OrganizationJoinRequest request, CancellationToken ct = default)
    {
        db.OrganizationJoinRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrganizationJoinRequest request, CancellationToken ct = default)
    {
        db.OrganizationJoinRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }
}
