using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Relativa.Audit.Application.DTOs;
using Relativa.Audit.Application.Interfaces;
using Relativa.Audit.Application.Options;
using Relativa.Audit.Application.Services;
using Relativa.Audit.Application.Validators;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Relativa.Audit.Application.Tests;

public sealed class AuditLogReadServiceTests
{
    private readonly Mock<IAuditLogReadRepository> _repo = new();
    private readonly Mock<IValidator<GetAuditLogQuery>> _validator = new();
    private readonly AuditLogReadService _sut;

    private static readonly AuditLogListResponse EmptyResponse = new([], 0, 1, 20, null);

    public AuditLogReadServiceTests()
    {
        _sut = new AuditLogReadService(
            _repo.Object,
            _validator.Object,
            OptionsFactory.Create(new AuditLogReadOptions { DefaultDateRangeDays = 30 }));
    }

    private void SetupValidQuery() =>
        _validator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<GetAuditLogQuery>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupCommonRepoMocks()
    {
        _repo.Setup(r => r.EnsureResourcesExistAsync(It.IsAny<GetAuditLogQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.EnsureRbacAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.BuildFilterContextAsync(It.IsAny<GetAuditLogQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditFilterContextDto?)null);
    }

    [Fact]
    public async Task GetAsync_ValidationFails_ThrowsValidationException()
    {
        var sut = new AuditLogReadService(
            _repo.Object,
            new GetAuditLogQueryValidator(),
            OptionsFactory.Create(new AuditLogReadOptions { DefaultDateRangeDays = 30 }));

        var q = new GetAuditLogQuery("bad", null, null, null, 1, 20, null, null, null, null, null, null);

        var act = () => sut.GetAsync(q, 1, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task GetAsync_EntityScope_DelegatesToRepository()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 1, 20, 10, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                null, 10, null, null, 3, 0, 20, 1, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        var result = await _sut.GetAsync(q, callerUserId: 99, CancellationToken.None);

        result.Should().BeSameAs(EmptyResponse);
        _repo.Verify(r => r.EnsureResourcesExistAsync(q, "entity", It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.EnsureRbacAsync(99, "entity", 3, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WorkspaceScope_DelegatesToWorkspaceScopeMethod()
    {
        var q = new GetAuditLogQuery("workspace", null, null, null, 1, 20, null, null, 5, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetWorkspaceScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                null, null, 5, 0, 20, 1, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        var result = await _sut.GetAsync(q, callerUserId: 42, CancellationToken.None);

        result.Should().BeSameAs(EmptyResponse);
        _repo.Verify(r => r.GetWorkspaceScopeAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            null, null, 5, 0, 20, 1, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_OrganizationScope_DelegatesToOrganizationScopeMethod()
    {
        var q = new GetAuditLogQuery("organization", null, null, null, 1, 20, null, null, null, 7, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetOrganizationScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                null, null, 7, 0, 20, 1, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        var result = await _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        result.Should().BeSameAs(EmptyResponse);
        _repo.Verify(r => r.GetOrganizationScopeAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            null, null, 7, 0, 20, 1, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_UserScope_DelegatesToUserScopeMethod()
    {
        var q = new GetAuditLogQuery("user", null, null, null, 1, 20, null, null, null, null, null, 11);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetUserScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                null, null, 11, It.IsAny<int>(), 0, 20, 1, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        var result = await _sut.GetAsync(q, callerUserId: 99, CancellationToken.None);

        result.Should().BeSameAs(EmptyResponse);
        _repo.Verify(r => r.GetUserScopeAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            null, null, 11, 99, 0, 20, 1, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_NullDateRange_AppliesDefaultDays()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 1, 20, null, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditFilterContextDto?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        await _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        _repo.Verify(r => r.GetEntityScopeAsync(
            It.Is<DateTimeOffset>(from => from <= DateTimeOffset.UtcNow.AddDays(-29)),
            It.Is<DateTimeOffset>(to => to >= DateTimeOffset.UtcNow.AddSeconds(-5)),
            It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditFilterContextDto?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_CategoryNormalized_DispatchesCorrectly()
    {
        var q = new GetAuditLogQuery("ENTITY", null, null, null, 1, 20, null, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditFilterContextDto?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        await _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        _repo.Verify(r => r.GetEntityScopeAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditFilterContextDto?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_PageTwoWithPageSize10_CalculatesSkipCorrectly()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 2, 10, null, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                null, null, null, null, 3, 10, 10, 2, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        await _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        _repo.Verify(r => r.GetEntityScopeAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            null, null, null, null, 3, 10, 10, 2, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_UnknownCategoryThatPassesValidation_ThrowsInvalidCategory()
    {
        var q = new GetAuditLogQuery("system", null, null, null, 1, 20, null, null, null, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();

        var act = () => _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        (await act.Should().ThrowAsync<Relativa.Audit.Application.Exceptions.AppException>())
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAsync_ExplicitDateRange_PassesProvidedDatesThrough()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var q = new GetAuditLogQuery("entity", from, to, null, 1, 20, null, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                from, to, It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<AuditFilterContextDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        await _sut.GetAsync(q, callerUserId: 1, CancellationToken.None);

        _repo.Verify(r => r.GetEntityScopeAsync(
            from, to, It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<AuditFilterContextDto?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_AlwaysCallsEnsureRbacWithCallerUserId()
    {
        var q = new GetAuditLogQuery("entity", null, null, null, 1, 20, null, null, 3, null, null, null);
        SetupValidQuery();
        SetupCommonRepoMocks();
        _repo.Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AuditFilterContextDto?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResponse);

        await _sut.GetAsync(q, callerUserId: 77, CancellationToken.None);

        _repo.Verify(r => r.EnsureRbacAsync(77, "entity", 3, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
