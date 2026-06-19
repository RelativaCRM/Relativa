using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<User?> GetByExternalLoginAsync(string provider, string subject, CancellationToken ct = default);
    Task AddExternalLoginAsync(UserExternalLogin login, CancellationToken ct = default);
    Task<User?> GetByResetTokenAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<UserBackupCode>> GetActiveBackupCodesAsync(int userId, CancellationToken ct = default);
    Task ReplaceBackupCodesAsync(int userId, IReadOnlyList<UserBackupCode> codes, CancellationToken ct = default);
    Task ConsumeBackupCodeAsync(UserBackupCode code, CancellationToken ct = default);
    Task<IReadOnlyList<UserEmail>> GetEmailsAsync(int userId, CancellationToken ct = default);
    Task<UserEmail?> GetEmailAsync(int userId, string address, CancellationToken ct = default);
    Task<bool> EmailExistsAnywhereAsync(string address, CancellationToken ct = default);
    Task AddEmailAsync(UserEmail email, CancellationToken ct = default);
    Task UpdateEmailAsync(UserEmail email, CancellationToken ct = default);
    Task RemoveEmailAsync(UserEmail email, CancellationToken ct = default);
    Task ReleaseExternalIdentifiersAsync(int userId, CancellationToken ct = default);
}
