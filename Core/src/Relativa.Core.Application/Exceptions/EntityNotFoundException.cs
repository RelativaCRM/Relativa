namespace Relativa.Core.Application.Exceptions;

public sealed class EntityNotFoundException(int entityId, int workspaceId)
    : KeyNotFoundException($"Entity {entityId} not found in workspace {workspaceId}.");
