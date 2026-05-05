# RabbitMQ choreography, DLQ, and operations

This runbook summarizes how Relativa uses RabbitMQ for **audit fan-out** and **choreographed domain** messaging after the transactional outbox pattern.

## Exchanges

| Exchange | Type | Publishers | Consumers |
|----------|------|------------|-----------|
| `audit.events` (configure via `RabbitMqAudit:Exchange`) | Topic | Core, Authentication (outbox dispatcher) | Audit (`audit.#`) |
| `relativa.domain` (configure via `RabbitMqAudit:DomainExchange` / defaults) | Topic | Core (outbox, domain routing keys such as `core.workspace.*`) | Graph, ML |

Routing rule: **`audit.*`** (case-insensitive `audit.` prefix) goes to the audit exchange; all other routing keys use the domain exchange.

## Dead-letter queues (DLQ)

Graph and ML consumers declare **fanout dead-letter exchanges** and `.failed` queues bound to them. Poison messages (`BasicNack` with `requeue=false`) route to DLQ according to RabbitMQ dead-letter semantics.

Inspect failed traffic:

```bash
docker exec relativa-rabbitmq rabbitmqctl list_queues name messages
```

Purge DLQ once root cause is fixed:

```bash
docker exec relativa-rabbitmq rabbitmqctl purge_queue domain.events.graph.workspace.v1.failed
docker exec relativa-rabbitmq rabbitmqctl purge_queue domain.events.ml.workspace.v1.failed
docker exec relativa-rabbitmq rabbitmqctl purge_queue domain.events.ml.recalculate.v1.failed
```

## Consumer idempotency

Table `rabbitmq_processed_delivery` (composite PK `(message_id, consumer_group)`) backs **exactly-once semantic processing per consumer group**. Replay the same Rabbit payload: second delivery is ACKed without side effects.

## Correlation identifiers

Envelope `DomainMessageEnvelope` carries `MessageId`, `CorrelationId`, and optional `SagaInstanceId`. ML recalculation uses:
- enqueue payload type: `relativa.domain.ml.recalculate_enqueued.v1` (routing key `ml.recalculate.enqueued`)
- progress payload type: `relativa.domain.ml.recalculate_progress.v1` (routing key `ml.recalculate.progress`)
- completed payload type: `relativa.domain.ml.recalculate_completed.v1` (routing key `ml.recalculate.completed`)

## Configuration keys

**Core / Authentication:** `RabbitMqAudit:*` (`Host`, `Port`, `Username`, `Password`, `Exchange`, `DomainExchange`)

**Graph:** `RabbitMqGraph:*`, `ConnectionStrings:Default`

**ML (Django):** `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD`

### ML choreography queues

- `domain.events.ml.workspace.v1` — domain freshness events (`core.workspace.*`, `core.entity.*`)
- `domain.events.ml.recalculate.v1` — async recomputation jobs (`ml.recalculate.enqueued`)
- DLQ: `domain.events.ml.workspace.v1.failed`, `domain.events.ml.recalculate.v1.failed`

## Automated tests

- `Messaging/tests/Relativa.Messaging.Tests` — router unit tests + Testcontainers integration for exchange declare/publish/consume smoke test (requires Docker).
