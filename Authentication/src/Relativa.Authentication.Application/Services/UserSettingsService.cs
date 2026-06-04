using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Services;

public sealed class UserSettingsService(
    IUserSettingsRepository userSettingsRepository,
    IValidator<UpdateUserSettingsRequest> updateSettingsValidator) : IUserSettingsService
{
    public async Task<UserSettingsDto> GetMySettingsAsync(int userId, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(userId, ct);
        return new UserSettingsDto(settings.Locale);
    }

    public async Task<UserSettingsDto> UpdateMySettingsAsync(int userId, UpdateUserSettingsRequest request, CancellationToken ct = default)
    {
        await updateSettingsValidator.ValidateAndThrowAsync(request, ct);

        var settings = await GetOrCreateSettingsAsync(userId, ct);
        settings.Locale = request.Locale;
        await userSettingsRepository.UpdateAsync(settings, ct);

        return new UserSettingsDto(settings.Locale);
    }

    private async Task<UserSettings> GetOrCreateSettingsAsync(int userId, CancellationToken ct)
    {
        var settings = await userSettingsRepository.GetByUserIdAsync(userId, ct);
        if (settings is not null) return settings;

        settings = new UserSettings { UserId = userId };
        await userSettingsRepository.AddAsync(settings, ct);
        return settings;
    }
}
