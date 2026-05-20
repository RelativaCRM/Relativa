using Microsoft.AspNetCore.SignalR;

namespace Relativa.Graph.Hubs;

public sealed class GraphHub : Hub
{
    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public Task JoinWorkspace(int workspaceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"workspace-{workspaceId}");

    public Task LeaveWorkspace(int workspaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workspace-{workspaceId}");
}
