using Relativa.Messaging;

namespace Relativa.Messaging.Tests;

public sealed class RabbitMqExchangeRouterTests
{
    private static RabbitMqPublishingOptions Opts(string audit = "audit.events", string domain = "relativa.domain") =>
        new() { AuditExchange = audit, DomainExchange = domain };

    [Fact]
    public void AuditPrefixedKey_RoutesToAuditExchange() =>
        Assert.Equal("audit.events", RabbitMqExchangeRouter.ResolvePublishExchange("audit.workspace", Opts()));

    [Fact]
    public void NonAuditKey_RoutesToDomainExchange() =>
        Assert.Equal("relativa.domain", RabbitMqExchangeRouter.ResolvePublishExchange("core.workspace.created", Opts()));

    [Theory]
    [InlineData("AUDIT.user")]
    [InlineData("audit.entity")]
    [InlineData("Audit.Organization")]
    public void AuditPrefix_IsCaseInsensitive(string key) =>
        Assert.Equal("a-ex", RabbitMqExchangeRouter.ResolvePublishExchange(key, Opts(audit: "a-ex", domain: "d-ex")));

    [Fact]
    public void NullRoutingKey_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            RabbitMqExchangeRouter.ResolvePublishExchange(null!, Opts()));

    [Fact]
    public void NullOptions_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            RabbitMqExchangeRouter.ResolvePublishExchange("audit.x", null!));

    [Fact]
    public void EmptyKey_RoutesToDomainExchange() =>
        Assert.Equal("relativa.domain", RabbitMqExchangeRouter.ResolvePublishExchange("", Opts()));

    [Fact]
    public void AuditWordWithoutDot_RoutesToDomainExchange() =>
        Assert.Equal("relativa.domain", RabbitMqExchangeRouter.ResolvePublishExchange("audit", Opts()));

    [Fact]
    public void KeyContainingAuditInMiddle_RoutesToDomainExchange() =>
        Assert.Equal("relativa.domain", RabbitMqExchangeRouter.ResolvePublishExchange("core.audit.event", Opts()));

    [Fact]
    public void ExactlyAuditDot_RoutesToAuditExchange() =>
        Assert.Equal("audit.events", RabbitMqExchangeRouter.ResolvePublishExchange("audit.", Opts()));
}
