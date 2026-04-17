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
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<OrganizationInvitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    public async Task<List<OrganizationInvitation>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Include(i => i.Organization)
            .Where(i => i.OrganizationId == organizationId)
            .ToListAsync(ct);
    }

    public async Task<List<OrganizationInvitation>> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await db.OrganizationInvitations
            .Where(i => i.Email.ToLower() == email.ToLower() && i.Status == "Pending")
            .ToListAsync(ct);
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
