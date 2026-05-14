using Microsoft.AspNetCore.SignalR;

namespace Relativa.Graph.Hubs;

public sealed class GraphHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}
