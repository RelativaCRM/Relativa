using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Domain.Interfaces;

public interface IUserSettingsRepository
{
    Task<UserSettings?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task AddAsync(UserSettings settings, CancellationToken ct = default);
    Task UpdateAsync(UserSettings settings, CancellationToken ct = default);
}
