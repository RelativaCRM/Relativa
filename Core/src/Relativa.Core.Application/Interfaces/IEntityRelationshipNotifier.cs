namespace Relativa.Core.Application.Interfaces;

public interface IEntityRelationshipNotifier
{
    Task NotifyChangedAsync(int workspaceId, CancellationToken ct, params int[] entityIds);
}
