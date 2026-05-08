namespace Relativa.Persistence.Contracts;

/// <summary>
/// RabbitMQ routing for Graph-orchestrated entity graph creates (Core executes persistence).
/// </summary>
public static class EntityGraphRouting
{
    public const string CommandQueueName = "core.entity_graph.create";
    public const string ExchangeName = "relativa.entity_graph";
    public const string CommandRoutingKey = "entity_graph.create";
}

/// <summary>
/// Wire payload published by Graph; Core deserializes <see cref="CreateEntityJson"/> as CreateEntityRequest JSON.
/// </summary>
public sealed record EntityGraphCreateRpcV1(
    int WorkspaceId,
    int UserId,
    string CreateEntityJson);

public sealed record EntityGraphCreateRpcReplyV1(
    bool Success,
    string? ErrorMessage,
    string? EntityDetailJson);
