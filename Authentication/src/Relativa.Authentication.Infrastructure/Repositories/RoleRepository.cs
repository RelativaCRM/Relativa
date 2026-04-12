using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Relativa.Persistence.Entities;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Infrastructure.Data;

namespace Relativa.Authentication.Infrastructure.Repositories;

public sealed class RoleRepository(AuthDbContext db, IOptions<AuthOptions> authOptions) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Roles.FirstOrDefaultAsync(r => r.Id == id && !r.IsArchived, ct);
    }

    public async Task<Role?> GetDefaultRoleAsync(CancellationToken ct = default)
    {
        var defaultRoleId = authOptions.Value.DefaultRoleId;
        return await GetByIdAsync(defaultRoleId, ct);
    }
}

public sealed class AuthOptions
{
    public int DefaultRoleId { get; set; }
}
