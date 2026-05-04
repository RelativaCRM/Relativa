using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Relativa.Audit.Application.DTOs;
using Relativa.Audit.Application.Interfaces;
using Relativa.Audit.Application.Options;
using Relativa.Audit.Application.Services;
using Relativa.Audit.Application.Validators;
using Xunit;

namespace Relativa.Audit.Application.Tests;

public sealed class AuditLogReadServiceTests
{
    private readonly Mock<IAuditLogReadRepository> _repo = new();
    private readonly Mock<IValidator<GetAuditLogQuery>> _validator = new();
    private readonly AuditLogReadService _sut;

    public AuditLogReadServiceTests()
    {
        _sut = new AuditLogReadService(
            _repo.Object,
            _validator.Object,
            OptionsFactory.Create(new AuditLogReadOptions { DefaultDateRangeDays = 30 }));
    }

    private void SetupValidQuery()
    {
        _validator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<GetAuditLogQuery>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    [Fact]
    public async Task GetAsync_EntityScope_DelegatesToRepository()
    {
        var q = new GetAuditLogQuery(
            "entity",
            null,
            null,
            null,
            1,
            20,
            10,
            null,
            3,
            null,
            null,
            null);

        SetupValidQuery();
        _repo.Setup(r => r.EnsureResourcesExistAsync(It.IsAny<GetAuditLogQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.EnsureRbacAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.BuildFilterContextAsync(It.IsAny<GetAuditLogQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditFilterContextDto?)null);

        var expected = new AuditLogListResponse([], 0, 1, 20, null);
        _repo
            .Setup(r => r.GetEntityScopeAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                null,
                10,
                null,
                null,
                3,
                0,
                20,
                1,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetAsync(q, callerUserId: 99, CancellationToken.None);

        result.Should().BeSameAs(expected);
        _repo.Verify(r => r.EnsureResourcesExistAsync(q, "entity", It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.EnsureRbacAsync(99, "entity", 3, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
