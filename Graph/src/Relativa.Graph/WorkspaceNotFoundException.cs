namespace Relativa.Graph;

public sealed class WorkspaceNotFoundException(int workspaceId)
    : KeyNotFoundException($"Workspace {workspaceId} not found or is archived.");
