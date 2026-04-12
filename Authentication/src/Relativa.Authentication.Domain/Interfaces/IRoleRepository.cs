using Relativa.Authentication.Domain.Entities;

namespace Relativa.Authentication.Domain.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Role?> GetDefaultRoleAsync(CancellationToken ct = default);
}
