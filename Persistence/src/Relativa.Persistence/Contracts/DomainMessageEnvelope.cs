namespace Relativa.Persistence.Contracts;

public sealed record DomainMessageEnvelope(
    int SchemaVersion,
    Guid MessageId,
    Guid CorrelationId,
    Guid? SagaInstanceId,
    Guid? CausationId,
    DateTimeOffset OccurredAtUtc,
    string SourceService,
    string PayloadTypeName,
    string PayloadJson);
