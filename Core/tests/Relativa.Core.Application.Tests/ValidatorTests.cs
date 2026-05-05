using FluentValidation.TestHelper;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Validators;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class CreateOrganizationRequestValidatorTests
{
    private readonly CreateOrganizationRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateOrganizationRequest("Acme Corp"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new CreateOrganizationRequest(name))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateOrganizationRequest(new string('A', 200)))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new CreateOrganizationRequest(new string('A', 201)))
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public sealed class UpdateOrganizationRequestValidatorTests
{
    private readonly UpdateOrganizationRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateOrganizationRequest("Updated Name"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new UpdateOrganizationRequest(name))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateOrganizationRequest(new string('B', 200)))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateOrganizationRequest(new string('B', 201)))
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public sealed class CreateWorkspaceRequestValidatorTests
{
    private readonly CreateWorkspaceRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateWorkspaceRequest("Engineering", 1))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new CreateWorkspaceRequest(name, 1))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateWorkspaceRequest(new string('W', 200), 1))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new CreateWorkspaceRequest(new string('W', 201), 1))
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public sealed class UpdateWorkspaceRequestValidatorTests
{
    private readonly UpdateWorkspaceRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateWorkspaceRequest("Design"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new UpdateWorkspaceRequest(name))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateWorkspaceRequest(new string('X', 200)))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateWorkspaceRequest(new string('X', 201)))
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public sealed class CreateRoleRequestValidatorTests
{
    private readonly CreateRoleRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateRoleRequest("Editor", [1, 2]))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new CreateRoleRequest(name, [1]))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateRoleRequest(new string('R', 100), [1]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new CreateRoleRequest(new string('R', 101), [1]))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void EmptyPermissionIds_FailsValidation() =>
        _sut.TestValidate(new CreateRoleRequest("Admin", []))
            .ShouldHaveValidationErrorFor(x => x.PermissionIds);

    [Fact]
    public void NullPermissionIds_FailsValidation() =>
        _sut.TestValidate(new CreateRoleRequest("Admin", null!))
            .ShouldHaveValidationErrorFor(x => x.PermissionIds);
}

public sealed class CreateOrgRoleRequestValidatorTests
{
    private readonly CreateOrgRoleRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateOrgRoleRequest("Manager", [3]))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_FailsValidation(string name) =>
        _sut.TestValidate(new CreateOrgRoleRequest(name, [1]))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void NameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateOrgRoleRequest(new string('O', 100), [1]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new CreateOrgRoleRequest(new string('O', 101), [1]))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void EmptyPermissionIds_FailsValidation() =>
        _sut.TestValidate(new CreateOrgRoleRequest("Lead", []))
            .ShouldHaveValidationErrorFor(x => x.PermissionIds);

    [Fact]
    public void NullPermissionIds_FailsValidation() =>
        _sut.TestValidate(new CreateOrgRoleRequest("Lead", null!))
            .ShouldHaveValidationErrorFor(x => x.PermissionIds);
}

public sealed class UpdateMemberRoleRequestValidatorTests
{
    private readonly UpdateMemberRoleRequestValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void PositiveRoleId_ReturnsNoErrors(int roleId) =>
        _sut.TestValidate(new UpdateMemberRoleRequest(roleId))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveRoleId_FailsValidation(int roleId) =>
        _sut.TestValidate(new UpdateMemberRoleRequest(roleId))
            .ShouldHaveValidationErrorFor(x => x.RoleId);
}

public sealed class ChangeOrgMemberRoleRequestValidatorTests
{
    private readonly ChangeOrgMemberRoleRequestValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void PositiveRoleId_ReturnsNoErrors(int roleId) =>
        _sut.TestValidate(new ChangeOrgMemberRoleRequest(roleId))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveRoleId_FailsValidation(int roleId) =>
        _sut.TestValidate(new ChangeOrgMemberRoleRequest(roleId))
            .ShouldHaveValidationErrorFor(x => x.RoleId);
}

public sealed class AddWorkspaceMemberRequestValidatorTests
{
    private readonly AddWorkspaceMemberRequestValidator _sut = new();

    [Fact]
    public void BothPositive_ReturnsNoErrors() =>
        _sut.TestValidate(new AddWorkspaceMemberRequest(UserId: 5, RoleId: 3))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveUserId_FailsValidation(int userId) =>
        _sut.TestValidate(new AddWorkspaceMemberRequest(userId, 1))
            .ShouldHaveValidationErrorFor(x => x.UserId);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveRoleId_FailsValidation(int roleId) =>
        _sut.TestValidate(new AddWorkspaceMemberRequest(1, roleId))
            .ShouldHaveValidationErrorFor(x => x.RoleId);

    [Fact]
    public void BothInvalid_ReturnsBothErrors()
    {
        var result = _sut.TestValidate(new AddWorkspaceMemberRequest(0, 0));
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
    }
}

public sealed class ReviewJoinRequestRequestValidatorTests
{
    private readonly ReviewJoinRequestRequestValidator _sut = new();

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void ValidDecision_ReturnsNoErrors(string decision) =>
        _sut.TestValidate(new ReviewJoinRequestRequest(decision))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceDecision_FailsValidation(string decision) =>
        _sut.TestValidate(new ReviewJoinRequestRequest(decision))
            .ShouldHaveValidationErrorFor(x => x.Decision);

    [Theory]
    [InlineData("approved")]
    [InlineData("rejected")]
    [InlineData("APPROVED")]
    [InlineData("REJECTED")]
    [InlineData("Pending")]
    [InlineData("pending")]
    [InlineData("accept")]
    [InlineData("deny")]
    [InlineData("yes")]
    [InlineData("no")]
    public void InvalidOrWrongCaseDecision_FailsValidation(string decision) =>
        _sut.TestValidate(new ReviewJoinRequestRequest(decision))
            .ShouldHaveValidationErrorFor(x => x.Decision);
}

public sealed class CreateEntityRequestValidatorTests
{
    private readonly CreateEntityRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateEntityRequest(1, [new PropertyValueInput(1, "value")]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyProperties_ReturnsNoErrors() =>
        _sut.TestValidate(new CreateEntityRequest(1, []))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveEntityTypeId_FailsValidation(int entityTypeId) =>
        _sut.TestValidate(new CreateEntityRequest(entityTypeId, []))
            .ShouldHaveValidationErrorFor(x => x.EntityTypeId);

    [Fact]
    public void NullProperties_FailsValidation() =>
        _sut.TestValidate(new CreateEntityRequest(1, null!))
            .ShouldHaveValidationErrorFor(x => x.Properties);

    [Fact]
    public void PropertyWithZeroId_FailsValidation() =>
        _sut.TestValidate(new CreateEntityRequest(1, [new PropertyValueInput(0, "x")]))
            .ShouldHaveValidationErrorFor("Properties[0].PropertyId");

    [Fact]
    public void PropertyWithNegativeId_FailsValidation() =>
        _sut.TestValidate(new CreateEntityRequest(1, [new PropertyValueInput(-5, "x")]))
            .ShouldHaveValidationErrorFor("Properties[0].PropertyId");

    [Fact]
    public void MixedValidAndInvalidProperties_FailsOnInvalidOnly()
    {
        var result = _sut.TestValidate(new CreateEntityRequest(1,
        [
            new PropertyValueInput(1, "ok"),
            new PropertyValueInput(0, "bad")
        ]));
        result.ShouldHaveValidationErrorFor("Properties[1].PropertyId");
        result.ShouldNotHaveValidationErrorFor("Properties[0].PropertyId");
    }
}

public sealed class UpdateEntityRequestValidatorTests
{
    private readonly UpdateEntityRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateEntityRequest([new PropertyValueInput(2, "updated")]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyProperties_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateEntityRequest([]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void NullProperties_FailsValidation() =>
        _sut.TestValidate(new UpdateEntityRequest(null!))
            .ShouldHaveValidationErrorFor(x => x.Properties);

    [Fact]
    public void PropertyWithZeroId_FailsValidation() =>
        _sut.TestValidate(new UpdateEntityRequest([new PropertyValueInput(0, "x")]))
            .ShouldHaveValidationErrorFor("Properties[0].PropertyId");

    [Fact]
    public void PropertyWithNegativeId_FailsValidation() =>
        _sut.TestValidate(new UpdateEntityRequest([new PropertyValueInput(-1, null)]))
            .ShouldHaveValidationErrorFor("Properties[0].PropertyId");
}

public sealed class InviteToOrgRequestValidatorTests
{
    private readonly InviteToOrgRequestValidator _sut = new();

    [Fact]
    public void ValidEmailNoRole_ReturnsNoErrors() =>
        _sut.TestValidate(new InviteToOrgRequest("user@example.com"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ValidEmailWithPositiveRoleId_ReturnsNoErrors() =>
        _sut.TestValidate(new InviteToOrgRequest("user@example.com", 7))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroOrNegativeRoleIdWhenProvided_FailsValidation(int orgRoleId) =>
        _sut.TestValidate(new InviteToOrgRequest("user@example.com", orgRoleId))
            .ShouldHaveValidationErrorFor(x => x.OrgRoleId);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEmail_FailsValidation(string email) =>
        _sut.TestValidate(new InviteToOrgRequest(email))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("double@@domain.com")]
    [InlineData("nodot")]
    public void InvalidEmailFormat_FailsValidation(string email) =>
        _sut.TestValidate(new InviteToOrgRequest(email))
            .ShouldHaveValidationErrorFor(x => x.Email);
}

public sealed class UpdateOrgUserProfileRequestValidatorTests
{
    private readonly UpdateOrgUserProfileRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest("Jane", "Doe"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceFirstName_FailsValidation(string firstName) =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest(firstName, "Doe"))
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceLastName_FailsValidation(string lastName) =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest("Jane", lastName))
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Fact]
    public void FirstNameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest(new string('A', 100), "Doe"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void FirstNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest(new string('A', 101), "Doe"))
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void LastNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateOrgUserProfileRequest("Jane", new string('Z', 101)))
            .ShouldHaveValidationErrorFor(x => x.LastName);
}

public sealed class CreateOrgUserRequestValidatorTests
{
    private readonly CreateOrgUserRequestValidator _sut = new();

    private static CreateOrgUserRequest Valid() =>
        new("Alice", "Smith", "alice@company.com", "Secure123", null);

    [Fact]
    public void Valid_NoRoleId_ReturnsNoErrors() =>
        _sut.TestValidate(Valid())
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Valid_WithPositiveRoleId_ReturnsNoErrors() =>
        _sut.TestValidate(Valid() with { OrgRoleId = 5 })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceFirstName_FailsValidation(string firstName) =>
        _sut.TestValidate(Valid() with { FirstName = firstName })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceLastName_FailsValidation(string lastName) =>
        _sut.TestValidate(Valid() with { LastName = lastName })
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEmail_FailsValidation(string email) =>
        _sut.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public void InvalidEmailFormat_FailsValidation(string email) =>
        _sut.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePassword_FailsValidation(string password) =>
        _sut.TestValidate(Valid() with { Password = password })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void PasswordTooShort_FailsValidation() =>
        _sut.TestValidate(Valid() with { Password = "Short1" })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void PasswordAtMinLength_ReturnsNoErrors() =>
        _sut.TestValidate(Valid() with { Password = "Exactly8" })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroOrNegativeRoleIdWhenProvided_FailsValidation(int orgRoleId) =>
        _sut.TestValidate(Valid() with { OrgRoleId = orgRoleId })
            .ShouldHaveValidationErrorFor(x => x.OrgRoleId);

    [Fact]
    public void FirstNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(Valid() with { FirstName = new string('A', 101) })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void LastNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(Valid() with { LastName = new string('B', 101) })
            .ShouldHaveValidationErrorFor(x => x.LastName);
}
