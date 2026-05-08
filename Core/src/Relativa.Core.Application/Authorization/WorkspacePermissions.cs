namespace Relativa.Core.Application.Authorization;

public static class WorkspacePermissions
{
    public const string ManageWsSettings = "manage_ws_settings";
    public const string AddWsMembers = "add_ws_members";
    public const string RemoveWsMembers = "remove_ws_members";
    public const string AssignWsRoles = "assign_ws_roles";
    public const string ManageWsRoles = "manage_ws_roles";
    public const string CreateEntities = "create_entities";
    public const string EditEntities = "edit_entities";
    public const string DeleteEntities = "delete_entities";
    public const string ViewEntities = "view_entities";
    public const string ViewAnalytics = "view_analytics";
    public const string DeleteWorkspace = "delete_workspace";

    public static readonly IReadOnlyList<string> FullWorkspaceAuthority =
    [
        ManageWsSettings,
        AddWsMembers,
        RemoveWsMembers,
        AssignWsRoles,
        ManageWsRoles,
        CreateEntities,
        EditEntities,
        DeleteEntities,
        ViewEntities,
        ViewAnalytics,
        DeleteWorkspace
    ];
}
