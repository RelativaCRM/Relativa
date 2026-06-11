using FluentAssertions;
using FluentValidation;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Application.Validators;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class UserSettingsServiceTests
{
    private readonly Mock<IUserSettingsRepository> _repo = new();
    private readonly UpdateUserSettingsRequestValidator _validator = new();
    private readonly UserSettingsService _sut;

    public UserSettingsServiceTests()
    {
        _sut = new UserSettingsService(_repo.Object, _validator);
    }

    [Fact]
    public async Task GetMySettingsAsync_ExistingSettings_ReturnsLocale()
    {
        _repo.Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettings { UserId = 1, Locale = "uk" });

        var result = await _sut.GetMySettingsAsync(1);

        result.Locale.Should().Be("uk");
    }

    [Fact]
    public async Task GetMySettingsAsync_NoSettings_CreatesDefault()
    {
        _repo.Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((UserSettings?)null);

        var result = await _sut.GetMySettingsAsync(1);

        result.Locale.Should().Be("en");
        _repo.Verify(r => r.AddAsync(It.Is<UserSettings>(s => s.UserId == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMySettingsAsync_ValidLocale_PersistsAndReturns()
    {
        var settings = new UserSettings { UserId = 1, Locale = "en" };
        _repo.Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(settings);

        var result = await _sut.UpdateMySettingsAsync(1, new UpdateUserSettingsRequest("uk"));

        result.Locale.Should().Be("uk");
        settings.Locale.Should().Be("uk");
        _repo.Verify(r => r.UpdateAsync(settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMySettingsAsync_InvalidLocale_ThrowsValidation()
    {
        var act = () => _sut.UpdateMySettingsAsync(1, new UpdateUserSettingsRequest("ENGLISH"));

        await act.Should().ThrowAsync<ValidationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateMySettingsAsync_EmptyLocale_ThrowsValidation()
    {
        var act = () => _sut.UpdateMySettingsAsync(1, new UpdateUserSettingsRequest(""));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
