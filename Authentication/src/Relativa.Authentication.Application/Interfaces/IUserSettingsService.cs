using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Interfaces;

public interface IUserSettingsService
{
    Task<UserSettingsDto> GetMySettingsAsync(int userId, CancellationToken ct = default);
    Task<UserSettingsDto> UpdateMySettingsAsync(int userId, UpdateUserSettingsRequest request, CancellationToken ct = default);
}
