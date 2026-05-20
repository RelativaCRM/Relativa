namespace Relativa.Graph.Messaging;

/// <summary>SignalR hub event names forwarded from choreography consumers.</summary>
public static class GraphSignalREvents
{
    /// <summary>Workspace lifecycle payloads (<see cref="Relativa.Persistence.Contracts.WorkspaceLifecyclePayloadV1"/>).</summary>
    public const string WorkspaceLifecycleDomain = "domain.workspace.lifecycle.v1";

    public const string MlRecalculateProgress  = "ml.recalculate.progress.v1";
    public const string MlRecalculateCompleted = "ml.recalculate.completed.v1";
}
