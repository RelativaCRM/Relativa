import json
import logging
import uuid

import pika
from django.conf import settings
from django.core.management.base import BaseCommand
from django.db import connection

from ml_api.recalculate_service import (
    DOMAIN_EXCHANGE,
    RECALC_CONSUMER_GROUP,
    RECALC_DLQ,
    RECALC_DLX,
    RECALC_PAYLOAD_TYPE,
    RECALC_QUEUE,
    RECALC_ROUTING_KEY,
    process_recalc_payload,
)

logger = logging.getLogger(__name__)


class Command(BaseCommand):
    help = "Run RabbitMQ consumer for ML recalculation jobs."

    def handle(self, *args, **options):
        logging.basicConfig(level=logging.INFO)
        credentials = pika.PlainCredentials(settings.RABBITMQ_USER, settings.RABBITMQ_PASSWORD)
        parameters = pika.ConnectionParameters(
            host=settings.RABBITMQ_HOST,
            port=settings.RABBITMQ_PORT,
            credentials=credentials,
            heartbeat=600,
        )
        conn = pika.BlockingConnection(parameters)
        channel = conn.channel()

        channel.exchange_declare(exchange=DOMAIN_EXCHANGE, exchange_type="topic", durable=True)
        channel.exchange_declare(exchange=RECALC_DLX, exchange_type="fanout", durable=True)
        channel.queue_declare(queue=RECALC_DLQ, durable=True)
        channel.queue_bind(exchange=RECALC_DLX, queue=RECALC_DLQ, routing_key="")
        channel.queue_declare(
            queue=RECALC_QUEUE,
            durable=True,
            arguments={"x-dead-letter-exchange": RECALC_DLX},
        )
        channel.queue_bind(exchange=DOMAIN_EXCHANGE, queue=RECALC_QUEUE, routing_key=RECALC_ROUTING_KEY)
        channel.basic_qos(prefetch_count=8)

        def on_message(ch, method_frame, _props, body):
            try:
                envelope = json.loads(body.decode("utf-8"))
            except json.JSONDecodeError:
                logger.exception("Malformed recalculation envelope")
                ch.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                return

            message_id_raw = envelope.get("MessageId") or envelope.get("messageId")
            try:
                message_id = uuid.UUID(str(message_id_raw))
            except (ValueError, TypeError):
                logger.exception("Invalid MessageId in recalculation envelope")
                ch.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                return

            if not try_mark_processed_once(message_id):
                ch.basic_ack(delivery_tag=method_frame.delivery_tag)
                return

            payload_type = envelope.get("PayloadTypeName") or envelope.get("payloadTypeName")
            if payload_type != RECALC_PAYLOAD_TYPE:
                logger.info("Skipping unsupported recalculation payload type: %s", payload_type)
                ch.basic_ack(delivery_tag=method_frame.delivery_tag)
                return

            payload_raw = envelope.get("PayloadJson") or envelope.get("payloadJson")
            try:
                payload = json.loads(payload_raw) if payload_raw else {}
                process_recalc_payload(payload)
            except Exception:
                logger.exception("Recalculation processing failed")
                ch.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
                return

            ch.basic_ack(delivery_tag=method_frame.delivery_tag)

        channel.basic_consume(queue=RECALC_QUEUE, on_message_callback=on_message, auto_ack=False)
        self.stdout.write(self.style.SUCCESS(f"ML recalculation consumer consuming {RECALC_QUEUE}."))
        channel.start_consuming()


def try_mark_processed_once(message_id: uuid.UUID) -> bool:
    insert_sql = (
        """
        INSERT INTO rabbitmq_processed_delivery (message_id, consumer_group, processed_at_utc)
        VALUES (%s, %s, NOW())
        ON CONFLICT DO NOTHING
        """
    )
    with connection.cursor() as cursor:
        cursor.execute(insert_sql, [str(message_id), RECALC_CONSUMER_GROUP])
        return cursor.rowcount == 1
