using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Interfaces;

public interface IEmailAddressService
{
    Task<IReadOnlyList<UserEmailDto>> ListAsync(int userId, CancellationToken ct = default);
    Task AddAsync(int userId, string address, CancellationToken ct = default);
    Task VerifyAsync(int userId, string address, string code, CancellationToken ct = default);
    Task ResendAsync(int userId, string address, CancellationToken ct = default);
    Task SetPrimaryAsync(int userId, string address, CancellationToken ct = default);
    Task RemoveAsync(int userId, string address, CancellationToken ct = default);
}
