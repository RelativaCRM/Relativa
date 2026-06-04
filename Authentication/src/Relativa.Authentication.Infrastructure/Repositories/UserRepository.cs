using Microsoft.EntityFrameworkCore;
using Relativa.Persistence.Entities;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Infrastructure.Data;

namespace Relativa.Authentication.Infrastructure.Repositories;

public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Users
            .Include(u => u.Settings)
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsArchived, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await db.Users
            .Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsArchived, ct);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email == email && !u.IsArchived, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<User?> GetByExternalLoginAsync(string provider, string subject, CancellationToken ct = default)
    {
        return await db.UserExternalLogins
            .Where(l => l.Provider == provider && l.Subject == subject && !l.User.IsArchived)
            .Select(l => l.User)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddExternalLoginAsync(UserExternalLogin login, CancellationToken ct = default)
    {
        db.UserExternalLogins.Add(login);
        await db.SaveChangesAsync(ct);
    }

    public async Task<User?> GetByResetTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(
            u => u.PasswordResetToken == tokenHash
              && u.PasswordResetTokenExpiresAt > DateTime.UtcNow
              && !u.IsArchived,
            ct);
    }

    public async Task<IReadOnlyList<UserBackupCode>> GetActiveBackupCodesAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserBackupCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .ToListAsync(ct);
    }

    public async Task ReplaceBackupCodesAsync(int userId, IReadOnlyList<UserBackupCode> codes, CancellationToken ct = default)
    {
        var existing = db.UserBackupCodes.Where(c => c.UserId == userId);
        db.UserBackupCodes.RemoveRange(existing);
        if (codes.Count > 0)
        {
            db.UserBackupCodes.AddRange(codes);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ConsumeBackupCodeAsync(UserBackupCode code, CancellationToken ct = default)
    {
        code.UsedAt = DateTime.UtcNow;
        db.UserBackupCodes.Update(code);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserEmail>> GetEmailsAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserEmails
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<UserEmail?> GetEmailAsync(int userId, string address, CancellationToken ct = default)
    {
        return await db.UserEmails.FirstOrDefaultAsync(e => e.UserId == userId && e.Address == address, ct);
    }

    public async Task<bool> EmailExistsAnywhereAsync(string address, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email == address && !u.IsArchived, ct)
            || await db.UserEmails.AnyAsync(e => e.Address == address, ct);
    }

    public async Task AddEmailAsync(UserEmail email, CancellationToken ct = default)
    {
        db.UserEmails.Add(email);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateEmailAsync(UserEmail email, CancellationToken ct = default)
    {
        db.UserEmails.Update(email);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveEmailAsync(UserEmail email, CancellationToken ct = default)
    {
        db.UserEmails.Remove(email);
        await db.SaveChangesAsync(ct);
    }
}
