import json
import logging
import uuid
from datetime import datetime, timezone

import pika
from django.conf import settings
from django.core.management.base import BaseCommand
from django.db import connection

logger = logging.getLogger(__name__)

DOMAIN_EXCHANGE = 'relativa.domain'
QUEUE_NAME = 'domain.events.ml.workspace.v1'
DLX_FANOUT = 'relativa.consumer.ml.workspace.v1.dlx'
DLQ_NAME = 'domain.events.ml.workspace.v1.failed'
WORKSPACE_BINDING_PATTERN = 'core.workspace.*'
ENTITY_BINDING_PATTERN = 'core.entity.*'
CONSUMER_GROUP = 'ml.domain.workspace.v1'


def _get_guid(env: dict, *keys):
    """Accept either PascalCase (System.Text.Json default) or camelCase keys."""
    for k in keys:
        if k in env and env[k] is not None:
            return env[k]
    return None


class Command(BaseCommand):
    """Consume workspace choreography events on ``relativa.domain`` with idempotent receipts."""

    help = 'Run RabbitMQ choreography consumer for workspace domain events (stub).'

    def handle(self, *args, **options):
        logging.basicConfig(level=logging.INFO)
        credentials = pika.PlainCredentials(settings.RABBITMQ_USER, settings.RABBITMQ_PASSWORD)
        parameters = pika.ConnectionParameters(
            host=settings.RABBITMQ_HOST,
            port=settings.RABBITMQ_PORT,
            credentials=credentials,
            heartbeat=600,
        )

        rmq_conn = pika.BlockingConnection(parameters)
        channel = rmq_conn.channel()

        channel.exchange_declare(
            exchange=DOMAIN_EXCHANGE, exchange_type='topic', durable=True)
        channel.exchange_declare(exchange=DLX_FANOUT, exchange_type='fanout', durable=True)

        channel.queue_declare(queue=DLQ_NAME, durable=True)
        channel.queue_bind(exchange=DLX_FANOUT, queue=DLQ_NAME, routing_key='')

        channel.queue_declare(
            queue=QUEUE_NAME,
            durable=True,
            arguments={
                'x-dead-letter-exchange': DLX_FANOUT,
            })

        channel.queue_bind(
            exchange=DOMAIN_EXCHANGE, queue=QUEUE_NAME,
            routing_key=WORKSPACE_BINDING_PATTERN)
        channel.queue_bind(
            exchange=DOMAIN_EXCHANGE, queue=QUEUE_NAME,
            routing_key=ENTITY_BINDING_PATTERN)

        channel.basic_qos(prefetch_count=32)

        def on_message(channel_, method_frame, _props, body):
            extra_base = {'MessageId': None, 'CorrelationId': None, 'SagaInstanceId': None}

            try:
                envelope = json.loads(body.decode('utf-8'))
            except json.JSONDecodeError:
                logger.exception('Malformed choreography JSON; rejecting without requeue', extra=extra_base)
                channel_.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                return

            corr = _get_guid(envelope, 'CorrelationId', 'correlationId')
            mid_raw = _get_guid(envelope, 'MessageId', 'messageId')
            saga_raw = _get_guid(envelope, 'SagaInstanceId', 'sagaInstanceId')
            extra = {
                'MessageId': str(mid_raw) if mid_raw is not None else None,
                'CorrelationId': str(corr) if corr is not None else None,
                'SagaInstanceId': str(saga_raw) if saga_raw is not None else None,
            }

            try:
                message_id = uuid.UUID(str(mid_raw))
            except (ValueError, TypeError):
                logger.exception('Envelope missing valid MessageId; rejecting.', extra=extra)
                channel_.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                return

            if not try_mark_processed_once(message_id):
                logger.debug('Duplicate choreography delivery skipped', extra=extra)
                channel_.basic_ack(delivery_tag=method_frame.delivery_tag)
                return

            ptype = envelope.get('PayloadTypeName') or envelope.get('payloadTypeName')
            if ptype == 'relativa.domain.workspace.lifecycle.v1':
                payload_js = envelope.get('PayloadJson') or envelope.get('payloadJson')
                logger.info(
                    'ML choreography workspace.lifecycle received payload=%s',
                    payload_js if isinstance(payload_js, str) and len(payload_js) < 280 else '[payload]',
                    extra=extra)
            elif ptype == 'relativa.domain.entity.analysis_refresh.v1':
                payload_js = envelope.get('PayloadJson') or envelope.get('payloadJson')
                _touch_deal_analysis_source_updated_at(payload_js, extra)
            else:
                logger.info('ML choreography PayloadTypeName=%s', ptype, extra=extra)

            channel_.basic_ack(delivery_tag=method_frame.delivery_tag)

        channel.basic_consume(queue=QUEUE_NAME, on_message_callback=on_message, auto_ack=False)

        self.stdout.write(self.style.SUCCESS('ML choreography consumer consuming %s.' % QUEUE_NAME))

        channel.start_consuming()


def try_mark_processed_once(message_id: uuid.UUID) -> bool:
    insert_sql = (
        '''
        INSERT INTO rabbitmq_processed_delivery (message_id, consumer_group, processed_at_utc)
        VALUES (%s, %s, NOW())
        ON CONFLICT DO NOTHING
        '''
    )
    with connection.cursor() as cursor:
        cursor.execute(insert_sql, [str(message_id), CONSUMER_GROUP])
        return cursor.rowcount == 1


def _touch_deal_analysis_source_updated_at(payload_js: str | None, extra: dict) -> None:
    if not payload_js:
        logger.warning('analysis_refresh payload is empty', extra=extra)
        return

    try:
        payload = json.loads(payload_js)
    except json.JSONDecodeError:
        logger.warning('analysis_refresh payload json malformed', extra=extra)
        return

    entity_id = payload.get('EntityId') or payload.get('entityId')
    entity_type_id = payload.get('EntityTypeId') or payload.get('entityTypeId')
    source_updated_at_utc = payload.get('SourceUpdatedAtUtc') or payload.get('sourceUpdatedAtUtc')
    if entity_id is None:
        return

    try:
        entity_id = int(entity_id)
    except (TypeError, ValueError):
        logger.warning('analysis_refresh entity_id invalid: %s', entity_id, extra=extra)
        return

    if source_updated_at_utc:
        try:
            dt = datetime.fromisoformat(source_updated_at_utc.replace('Z', '+00:00'))
            source_date = dt.date()
        except ValueError:
            source_date = datetime.now(timezone.utc).date()
    else:
        source_date = datetime.now(timezone.utc).date()

    upsert_sql = (
        '''
        WITH rel AS (
            SELECT er.target_entity_id AS analysis_entity_id
            FROM entity_relationship er
            JOIN entity_relationship_type ert ON ert.id = er.relationship_type_id
            WHERE ert.name = 'deal_analysis'
              AND er.source_entity_id = %s
            LIMIT 1
        ),
                prop_source AS (
                        SELECT id AS property_id
                        FROM property
                        WHERE name = 'source_updated_at'
                            AND organization_id IS NULL
                        LIMIT 1
                ),
                prop_calc AS (
                        SELECT id AS property_id
                        FROM property
                        WHERE name = 'calculated_at'
                            AND organization_id IS NULL
                        LIMIT 1
                )
        INSERT INTO entity_property_value (
            entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date
        )
                SELECT rel.analysis_entity_id, prop_source.property_id, NULL, NULL, NULL, NULL, %s
                FROM rel, prop_source
                UNION ALL
                SELECT rel.analysis_entity_id, prop_calc.property_id, NULL, NULL, NULL, NULL, NULL
                FROM rel, prop_calc
        ON CONFLICT (entity_id, property_id)
        DO UPDATE SET value_date = EXCLUDED.value_date
        '''
    )

    # EntityTypeId may be used in future for fan-out (client/contract -> impacted deals).
    _ = entity_type_id

    with connection.cursor() as cursor:
        cursor.execute(upsert_sql, [entity_id, source_date])
