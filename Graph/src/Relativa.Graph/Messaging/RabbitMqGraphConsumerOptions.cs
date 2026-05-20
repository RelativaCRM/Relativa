using Relativa.Persistence.Contracts;

namespace Relativa.Graph.Messaging;

public sealed class RabbitMqGraphConsumerOptions
{
    public const string SectionKey = "RabbitMqGraph";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    public string DomainExchange { get; set; } = DomainRouting.ExchangeName;

    public string WorkspaceBindingRoutingKeyPattern { get; set; } = "core.workspace.*";

    /// <summary>Primary consumer queue (+ DLX-backed dead-letter).</summary>
    public string WorkspaceQueueName { get; set; } = "domain.events.graph.workspace.v1";

    public string DeadLetterFanoutExchange { get; set; } = "relativa.consumer.graph.workspace.v1.dlx";

    public string DeadLetterQueueName { get; set; } = "domain.events.graph.workspace.v1.failed";

    /// <summary>ML recalculation consumer queue (+ DLX-backed dead-letter).</summary>
    public string MlQueueName { get; set; } = "domain.events.graph.ml.v1";

    public string MlBindingRoutingKeyPattern { get; set; } = "ml.recalculate.*";

    public string MlDeadLetterExchange { get; set; } = "relativa.consumer.graph.ml.v1.dlx";

    public string MlDeadLetterQueueName { get; set; } = "domain.events.graph.ml.v1.failed";
}
