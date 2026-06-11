using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Relativa.Graph.ML;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class HttpMlScoringClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpMlScoringClient Build(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ml.test") },
            Substitute.For<ILogger<HttpMlScoringClient>>());

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ScoreBatchAsync_EmptyInput_ReturnsEmptyAndSkipsHttp()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));

        var result = await Build(handler).ScoreBatchAsync([]);

        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0, "no entities means there is nothing to score, so no HTTP request should be made");
    }

    [Fact]
    public async Task ScoreBatchAsync_SuccessfulResponse_MapsScoresByEntityId()
    {
        const string body = """
        [
          {"entity_id":1,"closure_score":0.8,"churn_score":0.2,"unavailable_reason":null},
          {"entity_id":2,"closure_score":null,"churn_score":null,"unavailable_reason":"no_features"}
        ]
        """;
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));

        var result = await Build(handler).ScoreBatchAsync([1, 2]);

        result.Should().HaveCount(2);
        result[1].ClosureScore.Should().Be(0.8);
        result[1].ChurnScore.Should().Be(0.2);
        result[2].ClosureScore.Should().BeNull();
        result[2].UnavailableReason.Should().Be("no_features");
    }

    [Fact]
    public async Task ScoreBatchAsync_NonSuccessStatus_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));

        var result = await Build(handler).ScoreBatchAsync([1]);

        result.Should().BeEmpty("a failed ML call must degrade gracefully to no highlights, not throw");
    }

    [Fact]
    public async Task ScoreBatchAsync_NetworkException_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await Build(handler).ScoreBatchAsync([1]);

        result.Should().BeEmpty("ML API being unavailable must be swallowed so the graph still renders");
    }

    [Fact]
    public async Task ScoreBatchAsync_NullJsonBody_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "null"));

        var result = await Build(handler).ScoreBatchAsync([1]);

        result.Should().BeEmpty();
    }
}
