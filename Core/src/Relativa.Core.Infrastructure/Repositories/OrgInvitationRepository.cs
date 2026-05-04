using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class OrgInvitationRepository(RelativaDbContext db) : IOrgInvitationRepository
{
    public async Task<OrganizationInvitation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.Role)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<OrganizationInvitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.Role)
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    public async Task<List<OrganizationInvitation>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.Role)
            .Where(i => i.OrganizationId == organizationId)
            .ToListAsync(ct);
    }

    public async Task<List<OrganizationInvitation>> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return [];

        var normalized = email.Trim().ToLowerInvariant();
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.Role)
            .Where(i => i.Email == normalized && i.Status == "Pending")
            .ToListAsync(ct);
    }

    public async Task<OrganizationInvitation?> GetPendingByOrgAndEmailAsync(int organizationId, string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await db.OrganizationInvitations
            .Include(i => i.Role)
            .FirstOrDefaultAsync(
                i => i.OrganizationId == organizationId
                     && i.Email == normalized
                     && i.Status == "Pending",
                ct);
    }

    public async Task AddAsync(OrganizationInvitation invitation, CancellationToken ct = default)
    {
        db.OrganizationInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrganizationInvitation invitation, CancellationToken ct = default)
    {
        db.OrganizationInvitations.Update(invitation);
        await db.SaveChangesAsync(ct);
    }
}
