using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class DashboardEndpointsApiTests : IClassFixture<GraphApiFactory>
{
    private readonly GraphApiFactory _factory;

    public DashboardEndpointsApiTests(GraphApiFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> Get(string path, int? userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (userId is not null)
            request.Headers.Add("X-User-Id", userId.Value.ToString());
        return await _factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task GetSummary_OrgAdmin_Returns200WithFullOrgAccessLevel()
    {
        var response = await Get($"/api/v1/dashboard/summary?organizationId={_factory.OrgId}", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessLevel").GetString().Should().Be("full_org");
    }

    [Fact]
    public async Task GetSummary_MissingUserHeader_Returns401()
    {
        var response = await Get($"/api/v1/dashboard/summary?organizationId={_factory.OrgId}", userId: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSummary_OutsiderWithoutPermissions_Returns403()
    {
        var response = await Get($"/api/v1/dashboard/summary?organizationId={_factory.OrgId}", _factory.OutsiderUserId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPipeline_AnalyticsUser_Returns200WithFourStages()
    {
        var response = await Get($"/api/v1/dashboard/pipeline?organizationId={_factory.OrgId}", _factory.AnalyticsUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("stages").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task GetTrends_OrgAdmin_Returns200WithSixMonths()
    {
        var response = await Get($"/api/v1/dashboard/trends?organizationId={_factory.OrgId}", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("months").GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task GetWorkspacesComparison_OrgAdmin_Returns200()
    {
        var response = await Get($"/api/v1/dashboard/workspaces-comparison?organizationId={_factory.OrgId}", _factory.OrgAdminUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetWorkspaceSummary_AnalyticsMember_Returns200()
    {
        var response = await Get($"/api/v1/dashboard/workspace/{_factory.WorkspaceId}/summary", _factory.AnalyticsUserId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetWorkspaceSummary_Outsider_Returns403()
    {
        var response = await Get($"/api/v1/dashboard/workspace/{_factory.WorkspaceId}/summary", _factory.OutsiderUserId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
