using Microsoft.AspNetCore.SignalR;

namespace Relativa.Core.Hubs;

public sealed class CoreHub : Hub
{
    public Task JoinEntity(int workspaceId, int entityId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(workspaceId, entityId));

    public Task LeaveEntity(int workspaceId, int entityId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(workspaceId, entityId));

    public static string GroupName(int workspaceId, int entityId) =>
        $"entity-{workspaceId}-{entityId}";
}
