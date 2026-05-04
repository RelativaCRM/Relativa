using Xunit;

namespace Relativa.Messaging.Tests;

public sealed class RabbitMqExchangeRouterTests
{
    [Fact]
    public void Routes_audit_prefixed_keys_to_audit_exchange()
    {
        var opts = new RabbitMqPublishingOptions
        {
            AuditExchange = "audit.events",
            DomainExchange = "relativa.domain"
        };

        var ex = RabbitMqExchangeRouter.ResolvePublishExchange("audit.workspace", opts);
        Assert.Equal("audit.events", ex);
    }

    [Fact]
    public void Routes_core_domain_keys_to_domain_exchange()
    {
        var opts = new RabbitMqPublishingOptions
        {
            AuditExchange = "audit.events",
            DomainExchange = "relativa.domain"
        };

        var ex = RabbitMqExchangeRouter.ResolvePublishExchange("core.workspace.created", opts);
        Assert.Equal("relativa.domain", ex);
    }

    [Theory]
    [InlineData("AUDIT.user")]
    [InlineData("audit.entity")]
    public void Routing_is_case_insensitive_for_audit_prefix(string key)
    {
        var opts = new RabbitMqPublishingOptions { AuditExchange = "a-ex", DomainExchange = "d-ex" };
        var ex = RabbitMqExchangeRouter.ResolvePublishExchange(key, opts);
        Assert.Equal("a-ex", ex);
    }
}
