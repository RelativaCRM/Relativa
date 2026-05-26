using FluentAssertions;
using Relativa.Core.Application.Authorization;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class RolePermissionEvaluatorTests
{
    private static WorkspaceRole WsRole(params string?[] permissionNames) =>
        new()
        {
            Name = "test_ws_role",
            RolePermissions = permissionNames
                .Select(n => new WorkspaceRolePermission
                {
                    Permission = n is null ? null! : new Permission { Name = n }
                })
                .ToList()
        };

    private static OrganizationRole OrgRole(params string?[] permissionNames) =>
        new()
        {
            Name = "test_org_role",
            OrganizationId = 1,
            RolePermissions = permissionNames
                .Select(n => new OrganizationRolePermission
                {
                    Permission = n is null ? null! : new Permission { Name = n }
                })
                .ToList()
        };

    [Fact]
    public void HasPermission_WsRole_NullRole_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission((WorkspaceRole?)null, "view_entities").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WsRole_EmptyPermissions_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(WsRole(), "view_entities").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WsRole_MatchingPermission_ReturnsTrue()
    {
        RolePermissionEvaluator.HasPermission(WsRole("view_entities", "manage_ws_settings"), "view_entities").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WsRole_NoMatchingPermission_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(WsRole("manage_ws_settings"), "view_entities").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WsRole_CaseMismatch_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(WsRole("VIEW_ENTITIES"), "view_entities").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WsRole_NullPermissionName_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(WsRole((string?)null), "view_entities").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_OrgRole_NullRole_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission((OrganizationRole?)null, "manage_org_settings").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_OrgRole_MatchingPermission_ReturnsTrue()
    {
        RolePermissionEvaluator.HasPermission(OrgRole("manage_org_settings"), "manage_org_settings").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_OrgRole_NoMatchingPermission_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(OrgRole("view_analytics"), "manage_org_settings").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_OrgRole_CaseMismatch_ReturnsFalse()
    {
        RolePermissionEvaluator.HasPermission(OrgRole("MANAGE_ORG_SETTINGS"), "manage_org_settings").Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_WsRole_NullRole_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions((WorkspaceRole?)null, ["view_entities"]).Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_WsRole_EmptyRequiredList_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(WsRole("view_entities"), []).Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_WsRole_AllPresent_ReturnsTrue()
    {
        RolePermissionEvaluator.HasAllPermissions(
            WsRole("view_entities", "view_analytics", "manage_ws_settings"),
            ["view_entities", "view_analytics"])
            .Should().BeTrue();
    }

    [Fact]
    public void HasAllPermissions_WsRole_PartialMatch_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(
            WsRole("view_entities"),
            ["view_entities", "manage_ws_settings"])
            .Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_WsRole_NullPermissionEntryExcluded_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(
            WsRole((string?)null, "view_entities"),
            ["view_entities", "manage_ws_settings"])
            .Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_WsRole_CaseMismatch_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(
            WsRole("VIEW_ENTITIES"),
            ["view_entities"])
            .Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_OrgRole_NullRole_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions((OrganizationRole?)null, ["manage_org_settings"]).Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_OrgRole_EmptyRequiredList_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(OrgRole("manage_org_settings"), []).Should().BeFalse();
    }

    [Fact]
    public void HasAllPermissions_OrgRole_AllPresent_ReturnsTrue()
    {
        RolePermissionEvaluator.HasAllPermissions(
            OrgRole("manage_org_settings", "view_analytics"),
            ["manage_org_settings", "view_analytics"])
            .Should().BeTrue();
    }

    [Fact]
    public void HasAllPermissions_OrgRole_PartialMatch_ReturnsFalse()
    {
        RolePermissionEvaluator.HasAllPermissions(
            OrgRole("manage_org_settings"),
            ["manage_org_settings", "view_analytics"])
            .Should().BeFalse();
    }
}
