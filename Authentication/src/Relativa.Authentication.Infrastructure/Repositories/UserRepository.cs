using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Entities;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Infrastructure.Data;

namespace Relativa.Authentication.Infrastructure.Repositories;

public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsArchived, ct);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email == email, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
