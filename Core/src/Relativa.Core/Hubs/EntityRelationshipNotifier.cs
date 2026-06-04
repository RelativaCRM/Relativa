using Microsoft.AspNetCore.SignalR;
using Relativa.Core.Application.Interfaces;

namespace Relativa.Core.Hubs;

public sealed class EntityRelationshipNotifier(IHubContext<CoreHub> hubContext) : IEntityRelationshipNotifier
{
    public Task NotifyChangedAsync(int workspaceId, CancellationToken ct, params int[] entityIds)
    {
        var tasks = entityIds.Distinct().Select(id =>
            hubContext.Clients
                .Group(CoreHub.GroupName(workspaceId, id))
                .SendAsync(CoreSignalREvents.EntityRelationshipsChanged,
                    new { WorkspaceId = workspaceId, EntityId = id },
                    ct));
        return Task.WhenAll(tasks);
    }
}
