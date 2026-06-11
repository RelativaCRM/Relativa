using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace Relativa.Audit.Integration.Tests;

public sealed class AuditEndpointsApiTests : IClassFixture<AuditApiFactory>
{
    private readonly AuditApiFactory _factory;

    public AuditEndpointsApiTests(AuditApiFactory factory) => _factory = factory;

    private HttpClient Authenticated(int userId = 1)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateTokenFor(userId));
        return client;
    }

    [Fact]
    public async Task GetAuditLog_NoToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/audit-log?entity_type=organization&organization_id=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the /audit-log endpoint requires the AuditReaders policy (authenticated user)");
    }

    [Fact]
    public async Task GetEntityAuditLog_NoToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/entities/5/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLog_AuthenticatedButMissingEntityType_Returns400()
    {
        var response = await Authenticated().GetAsync("/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "BuildQuery requires entity_type (or scope); a valid token passes auth but the request is invalid");
    }

    [Fact]
    public async Task Root_ReturnsServiceIdentity()
    {
        var response = await _factory.CreateClient().GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("relativa-audit");
    }
}
