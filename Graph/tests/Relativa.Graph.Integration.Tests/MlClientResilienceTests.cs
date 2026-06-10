using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Relativa.Graph.ML;
using Relativa.Graph.Messaging;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class MlClientResilienceTests
{
    private static IOptions<RabbitMqGraphConsumerOptions> DeadBroker() =>
        Options.Create(new RabbitMqGraphConsumerOptions { Host = "localhost", Port = 5699, Username = "guest", Password = "guest" });

    private static CancellationToken Bounded() => new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    [Fact]
    public async Task ScoringClient_EmptyInput_ReturnsEmptyWithoutContactingBroker()
    {
        var client = new RabbitMqMlScoringClient(DeadBroker(), Substitute.For<ILogger<RabbitMqMlScoringClient>>());

        var result = await client.ScoreBatchAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoringClient_BrokerUnavailable_DegradesToEmptyScores()
    {
        var client = new RabbitMqMlScoringClient(DeadBroker(), Substitute.For<ILogger<RabbitMqMlScoringClient>>());

        var result = await client.ScoreBatchAsync([1, 2, 3], Bounded());

        result.Should().BeEmpty("when the ML broker is unreachable the graph must still render, just without ML highlights");
    }

    [Fact]
    public async Task RecalculationClient_EmptyInput_IsNoOp()
    {
        var client = new RabbitMqMlRecalculationClient(DeadBroker(), Substitute.For<ILogger<RabbitMqMlRecalculationClient>>());

        var act = () => client.EnqueueAsync([], requestedByUserId: 1);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecalculationClient_BrokerUnavailable_SwallowsFailure()
    {
        var client = new RabbitMqMlRecalculationClient(DeadBroker(), Substitute.For<ILogger<RabbitMqMlRecalculationClient>>());

        var act = () => client.EnqueueAsync([10, 20], requestedByUserId: 1, workspaceId: 5, Bounded());

        await act.Should().NotThrowAsync("a failed recalculation enqueue is best-effort and must never break the caller");
    }
}
