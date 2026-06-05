using Relativa.Authentication.Application.DTOs;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Interfaces;

public interface ITwoFactorService
{
    Task<TwoFactorStatusDto> GetStatusAsync(int userId, CancellationToken ct = default);
    Task<TwoFactorSetupDto> StartSetupAsync(int userId, CancellationToken ct = default);
    Task<TwoFactorEnableResultDto> EnableAsync(int userId, string code, CancellationToken ct = default);
    Task DisableAsync(int userId, string code, CancellationToken ct = default);
    Task<TwoFactorBackupCodesDto> RegenerateBackupCodesAsync(int userId, string code, CancellationToken ct = default);
    Task<TwoFactorMasterCodeDto> RegenerateMasterCodeAsync(int userId, string code, CancellationToken ct = default);
    Task<bool> VerifyAsync(User user, string code, CancellationToken ct = default);
}
