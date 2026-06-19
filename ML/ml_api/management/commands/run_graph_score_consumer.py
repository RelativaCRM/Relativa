import json
import logging
import time

import pika
from django.conf import settings
from django.core.management.base import BaseCommand

from ml_api.apps import MlApiConfig
from ml_api.recalculate_service import (
    BATCH_TIMEOUT_SECONDS,
    normalize_entity_ids,
    recompute_deal_analysis,
    _check_deadline,
    _ensure_deal_analysis_entities,
    _load_analysis_state,
    _load_client_inputs,
    _load_contract_inputs,
    _load_deal_inputs,
    _load_schema_config,
)
from ml_api.views import _needs_analysis_refresh, _persist_scores, _score_or_diagnose

logger = logging.getLogger(__name__)

EXCHANGE_NAME = "relativa.graph_ml"
EXCHANGE_TYPE = "topic"
QUEUE_NAME = "ml.graph.score_request.v1"
ROUTING_KEY = "graph.score.request"
DLX_NAME = "relativa.graph_ml.dlx"
DLQ_NAME = "ml.graph.score_request.v1.failed"


class Command(BaseCommand):
    help = "Run RabbitMQ RPC consumer for Graph→ML scoring requests."

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

        channel.exchange_declare(exchange=EXCHANGE_NAME, exchange_type=EXCHANGE_TYPE, durable=True)
        channel.exchange_declare(exchange=DLX_NAME, exchange_type="fanout", durable=True)
        channel.queue_declare(queue=DLQ_NAME, durable=True)
        channel.queue_bind(exchange=DLX_NAME, queue=DLQ_NAME, routing_key="")
        channel.queue_declare(
            queue=QUEUE_NAME,
            durable=True,
            arguments={"x-dead-letter-exchange": DLX_NAME},
        )
        channel.queue_bind(exchange=EXCHANGE_NAME, queue=QUEUE_NAME, routing_key=ROUTING_KEY)
        channel.basic_qos(prefetch_count=4)

        def on_message(ch, method_frame, properties, body):
            reply_to = properties.reply_to
            correlation_id = properties.correlation_id

            def reply(payload: dict):
                if not reply_to:
                    return
                ch.basic_publish(
                    exchange="",
                    routing_key=reply_to,
                    body=json.dumps(payload).encode("utf-8"),
                    properties=pika.BasicProperties(correlation_id=correlation_id),
                )

            try:
                data = json.loads(body.decode("utf-8"))
            except json.JSONDecodeError:
                logger.exception("Malformed graph score RPC request")
                reply({"scores": [], "errorMessage": "Malformed JSON request."})
                ch.basic_ack(delivery_tag=method_frame.delivery_tag)
                return

            # C# sends camelCase; support both
            raw_ids = data.get("entityIds") or data.get("entity_ids")
            try:
                entity_ids = normalize_entity_ids(raw_ids)
            except ValueError as exc:
                logger.warning("Invalid entity_ids in graph score RPC: %s", exc)
                reply({"scores": [], "errorMessage": str(exc)})
                ch.basic_ack(delivery_tag=method_frame.delivery_tag)
                return

            try:
                scores = _run_scoring(entity_ids)
                reply({"scores": scores, "errorMessage": None})
            except Exception:
                logger.exception("Graph score RPC scoring failed for entity_ids=%s", entity_ids)
                reply({"scores": [], "errorMessage": "Internal scoring error."})

            ch.basic_ack(delivery_tag=method_frame.delivery_tag)

        channel.basic_consume(queue=QUEUE_NAME, on_message_callback=on_message, auto_ack=False)
        self.stdout.write(self.style.SUCCESS(f"ML graph score RPC consumer listening on {QUEUE_NAME}."))
        channel.start_consuming()


def _run_scoring(entity_ids: list[int]) -> list[dict]:
    """Run the same batch scoring pipeline as the score_batch HTTP view."""
    if not entity_ids:
        return []

    if (MlApiConfig.churn_model is None) or (MlApiConfig.closure_model is None):
        return [
            {"entityId": eid, "closureScore": None, "churnScore": None, "unavailableReason": "ML models not loaded."}
            for eid in entity_ids
        ]

    started = time.perf_counter()
    deadline = started + BATCH_TIMEOUT_SECONDS

    config = _load_schema_config()
    _check_deadline(deadline)
    _ensure_deal_analysis_entities(entity_ids, config, deadline)
    _check_deadline(deadline)

    analysis_rows = _load_analysis_state(entity_ids, config)
    deal_rows = _load_deal_inputs(entity_ids, config)
    contract_rows = _load_contract_inputs(entity_ids, config)
    contracts_by_deal: dict[int, list] = {}
    for row in contract_rows:
        contracts_by_deal.setdefault(row["deal_id"], []).append(row)
    _check_deadline(deadline)
    client_rows = _load_client_inputs(entity_ids, config)
    _check_deadline(deadline)

    from ml_api.recalculate_service import ANALYSIS_PROP_SOURCE_UPDATED_AT, ANALYSIS_PROP_CALCULATED_AT
    results_by_id: dict[int, dict] = {}
    stale_analysis_ids: list[int] = []

    for deal_id in entity_ids:
        analysis = analysis_rows.get(deal_id)
        if analysis is None:
            results_by_id[deal_id] = _score_or_diagnose(
                None,
                deal_rows.get(deal_id, {}),
                contracts_by_deal.get(deal_id, []),
                client_rows.get(deal_id, {}),
            )
            continue

        source_updated_at = analysis.get(ANALYSIS_PROP_SOURCE_UPDATED_AT)
        calculated_at = analysis.get(ANALYSIS_PROP_CALCULATED_AT)
        if calculated_at is None or source_updated_at is None or calculated_at < source_updated_at:
            stale_analysis_ids.append(deal_id)
            continue

        if _needs_analysis_refresh(
            analysis,
            deal_rows.get(deal_id, {}),
            contracts_by_deal.get(deal_id, []),
        ):
            stale_analysis_ids.append(deal_id)
            continue

        results_by_id[deal_id] = _score_or_diagnose(
            analysis,
            deal_rows.get(deal_id, {}),
            contracts_by_deal.get(deal_id, []),
            client_rows.get(deal_id, {}),
        )

    if stale_analysis_ids:
        recompute_deal_analysis(stale_analysis_ids, deadline=deadline)
        refreshed = _load_analysis_state(stale_analysis_ids, config)
        refreshed_clients = _load_client_inputs(stale_analysis_ids, config)
        for deal_id in stale_analysis_ids:
            results_by_id[deal_id] = _score_or_diagnose(
                refreshed.get(deal_id),
                deal_rows.get(deal_id, {}),
                contracts_by_deal.get(deal_id, []),
                refreshed_clients.get(deal_id, {}),
            )

    _check_deadline(deadline)

    response_payload = [
        {
            "entity_id": eid,
            "closure_score": results_by_id.get(eid, {}).get("closure_score"),
            "churn_score": results_by_id.get(eid, {}).get("churn_score"),
            "unavailable_reason": results_by_id.get(eid, {}).get("unavailable_reason"),
        }
        for eid in entity_ids
    ]
    _persist_scores(response_payload, config)

    # Serialize to camelCase to match C# MlScoreRpcItemV1
    return [
        {
            "entityId": item["entity_id"],
            "closureScore": item["closure_score"],
            "churnScore": item["churn_score"],
            "unavailableReason": item["unavailable_reason"],
        }
        for item in response_payload
    ]
