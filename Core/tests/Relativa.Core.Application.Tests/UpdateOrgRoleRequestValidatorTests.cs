using FluentAssertions;
using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.Validators;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class UpdateOrgRoleRequestValidatorTests
{
    private readonly UpdateOrgRoleRequestValidator _sut = new();

    [Fact]
    public void AllFieldsNull_IsValid()
    {
        _sut.Validate(new UpdateOrgRoleRequest(null, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NameOverMaxLength_IsInvalid()
    {
        var request = new UpdateOrgRoleRequest(new string('x', 101), null, null);

        _sut.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void PriorityBelowOne_IsInvalid()
    {
        var result = _sut.Validate(new UpdateOrgRoleRequest("Manager", null, 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Priority must be at least 1"));
    }

    [Fact]
    public void ValidNameAndPriority_IsValid()
    {
        _sut.Validate(new UpdateOrgRoleRequest("Manager", null, 5)).IsValid.Should().BeTrue();
    }
}
