namespace Relativa.Core.Application.Authorization;

public static class OrganizationPermissions
{
    public const string ManageOrgSettings = "manage_org_settings";
    public const string InviteToOrg = "invite_to_org";
    public const string ManageJoinRequests = "manage_join_requests";
    public const string RemoveOrgMembers = "remove_org_members";
    public const string AssignOrgRoles = "assign_org_roles";
    public const string ManageOrgRoles = "manage_org_roles";
    public const string CreateWorkspaces = "create_workspaces";
    public const string ManageOrgWorkspaceMembers = "manage_org_workspace_members";

    public const string CreateOrgUsers = "create_org_users";
    public const string EditOtherOrgUsersProfile = "edit_other_org_users_profile";
    public const string DeleteOrgUsers = "delete_org_users";
}
