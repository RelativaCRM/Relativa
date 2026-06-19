using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphQueryEndpointsApiTests : IClassFixture<GraphApiFactory>
{
    private readonly GraphApiFactory _factory;

    public GraphQueryEndpointsApiTests(GraphApiFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> GetGraph(string query, int? userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/graph?{query}");
        if (userId is not null)
            request.Headers.Add("X-User-Id", userId.Value.ToString());
        return await _factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task GetGraph_MissingUserHeader_Returns401()
    {
        var response = await GetGraph($"organizationId={_factory.OrgId}", userId: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetGraph_InvalidRiskLevel_Returns400()
    {
        var response = await GetGraph($"organizationId={_factory.OrgId}&riskLevel=bogus", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "riskLevel is constrained to high/medium/low");
    }

    [Fact]
    public async Task GetGraph_ValidUser_Returns200WithFocalUserNode()
    {
        var response = await GetGraph($"organizationId={_factory.OrgId}", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nodes").GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
            "the graph always contains at least the focal user node");
    }

    [Fact]
    public async Task GetGraph_ValidRiskLevel_Returns200()
    {
        var response = await GetGraph($"organizationId={_factory.OrgId}&riskLevel=high", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EntityGraphCreate_MissingUserHeader_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/v1/workspaces/{_factory.WorkspaceId}/entity-graph/create", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the create endpoint rejects unidentified callers before issuing the RabbitMQ RPC");
    }
}
