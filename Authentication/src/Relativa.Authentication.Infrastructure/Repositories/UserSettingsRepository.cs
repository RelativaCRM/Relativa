using Microsoft.EntityFrameworkCore;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Infrastructure.Repositories;

public sealed class UserSettingsRepository(AuthDbContext db) : IUserSettingsRepository
{
    public Task<UserSettings?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task AddAsync(UserSettings settings, CancellationToken ct = default)
    {
        db.UserSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserSettings settings, CancellationToken ct = default)
    {
        db.UserSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
