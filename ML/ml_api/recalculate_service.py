import json
import logging
import os
import time
import uuid
from datetime import date, datetime, timezone

logger = logging.getLogger(__name__)

import pika
from django.conf import settings
from django.db import connection, transaction

MAX_BATCH_SIZE = int(os.environ.get("ML_SCORE_BATCH_MAX_SIZE", "200"))
BATCH_TIMEOUT_SECONDS = 5.0

DEAL_TYPE_NAME = "deal"
DEAL_ANALYSIS_TYPE_NAME = "deal_analysis"
CONTRACT_TYPE_NAME = "contract"
CLIENT_TYPE_NAME = "client"

REL_DEAL_ANALYSIS = "deal_analysis"
REL_DEAL_CONTRACT = "deal_contract"
REL_DEAL_CLIENT = "deal_client"

DEAL_PROP_CREATED_AT = "created_at"
DEAL_PROP_STATUS = "status"
DEAL_PROP_EXPECTED_CLOSE = "expected_close"

ANALYSIS_PROP_DAYS_SINCE_CREATED = "days_since_created"
ANALYSIS_PROP_STAGE_ENCODED = "stage_encoded"
ANALYSIS_PROP_NUM_INTERACTIONS = "num_interactions"
ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT = "days_since_last_contact"
ANALYSIS_PROP_NUM_OPEN_DEALS = "num_open_deals"
ANALYSIS_PROP_AVG_DEAL_VALUE = "avg_deal_value"
ANALYSIS_PROP_SOURCE_UPDATED_AT = "source_updated_at"
ANALYSIS_PROP_CALCULATED_AT = "calculated_at"
ANALYSIS_PROP_DAYS_UNTIL_CLOSE = "days_until_expected_close"
ANALYSIS_PROP_HIST_CLOSE_RATE = "historical_close_rate"

CONTRACT_PROP_AMOUNT = "amount"
CONTRACT_PROP_STATUS = "contract_status"
CONTRACT_PROP_END_DATE = "end_date"
CONTRACT_PROP_SIGNED_AT = "signed_at"

CLIENT_PROP_LIFETIME_VALUE = "client_lifetime_value"
CLIENT_PROP_TENURE_DAYS = "client_tenure_days"

DEAL_PROP_CLOSURE_SCORE = "closure_score"
DEAL_PROP_CHURN_SCORE = "churn_score"

REQUIRED_FEATURE_KEYS = (
    ANALYSIS_PROP_DAYS_SINCE_CREATED,
    ANALYSIS_PROP_STAGE_ENCODED,
    ANALYSIS_PROP_NUM_INTERACTIONS,
    ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
    ANALYSIS_PROP_NUM_OPEN_DEALS,
    ANALYSIS_PROP_AVG_DEAL_VALUE,
)

FEATURE_KEYS = REQUIRED_FEATURE_KEYS + (
    ANALYSIS_PROP_DAYS_UNTIL_CLOSE,
    ANALYSIS_PROP_HIST_CLOSE_RATE,
)

DAYS_UNTIL_CLOSE_MEDIAN = 30
HIST_CLOSE_RATE_MEDIAN = 50.0

DEAL_STATUS_TO_STAGE = {
    "opened": 1,
    "pending": 2,
    "closed": 3,
    "revoked": 4,
}

ACTIVE_CONTRACT_STATUSES = {"active"}

DOMAIN_EXCHANGE = "relativa.domain"
RECALC_ROUTING_KEY = "ml.recalculate.enqueued"
RECALC_QUEUE = "domain.events.ml.recalculate.v1"
RECALC_DLX = "relativa.consumer.ml.recalculate.v1.dlx"
RECALC_DLQ = "domain.events.ml.recalculate.v1.failed"
RECALC_CONSUMER_GROUP = "ml.domain.recalculate.v1"

RECALC_PAYLOAD_TYPE = "relativa.domain.ml.recalculate_enqueued.v1"
RECALC_PROGRESS_PAYLOAD_TYPE = "relativa.domain.ml.recalculate_progress.v1"
RECALC_COMPLETED_PAYLOAD_TYPE = "relativa.domain.ml.recalculate_completed.v1"


def normalize_entity_ids(entity_ids):
    if not isinstance(entity_ids, list) or not entity_ids:
        raise ValueError("entity_ids must be a non-empty array of integers.")
    if len(entity_ids) > MAX_BATCH_SIZE:
        raise ValueError(f"entity_ids exceeds max batch size ({MAX_BATCH_SIZE}).")
    normalized = []
    seen = set()
    for raw in entity_ids:
        if not isinstance(raw, int) or raw <= 0:
            raise ValueError("entity_ids must contain positive integers only.")
        if raw in seen:
            continue
        seen.add(raw)
        normalized.append(raw)
    return normalized


def enqueue_recalculation_job(entity_ids, workspace_id, requested_by_user_id, reason):
    job_id = uuid.uuid4()
    now = datetime.now(timezone.utc)
    envelope = {
        "SchemaVersion": 1,
        "MessageId": str(uuid.uuid4()),
        "CorrelationId": str(job_id),
        "SagaInstanceId": str(job_id),
        "CausationId": None,
        "OccurredAtUtc": now.isoformat(),
        "SourceService": "ml",
        "PayloadTypeName": RECALC_PAYLOAD_TYPE,
        "PayloadJson": json.dumps(
            {
                "JobId": str(job_id),
                "WorkspaceId": workspace_id,
                "RequestedByUserId": requested_by_user_id,
                "RequestedAtUtc": now.isoformat(),
                "Scope": "workspace" if workspace_id is not None and not entity_ids else "entity_ids",
                "EntityIds": entity_ids,
                "Reason": reason,
            }
        ),
    }
    publish_domain_event(RECALC_ROUTING_KEY, envelope)
    return job_id


def publish_domain_event(routing_key, envelope):
    credentials = pika.PlainCredentials(settings.RABBITMQ_USER, settings.RABBITMQ_PASSWORD)
    parameters = pika.ConnectionParameters(
        host=settings.RABBITMQ_HOST,
        port=settings.RABBITMQ_PORT,
        credentials=credentials,
        heartbeat=60,
    )
    conn = pika.BlockingConnection(parameters)
    try:
        ch = conn.channel()
        ch.exchange_declare(exchange=DOMAIN_EXCHANGE, exchange_type="topic", durable=True)
        ch.basic_publish(
            exchange=DOMAIN_EXCHANGE,
            routing_key=routing_key,
            body=json.dumps(envelope).encode("utf-8"),
            properties=pika.BasicProperties(content_type="application/json", delivery_mode=2),
        )
    finally:
        conn.close()


PROGRESS_CHUNK_SIZE = 10


def process_recalc_payload(payload):
    entity_ids = payload.get("EntityIds") or payload.get("entityIds") or []
    workspace_id = payload.get("WorkspaceId") or payload.get("workspaceId")
    job_id_raw = payload.get("JobId") or payload.get("jobId")
    job_id = str(job_id_raw) if job_id_raw else str(uuid.uuid4())
    requested_by_user_id = int(payload.get("RequestedByUserId") or payload.get("requestedByUserId") or 0)
    started_at = datetime.now(timezone.utc)
    today = date.today()

    if workspace_id is not None and not entity_ids:
        entity_ids = _load_workspace_deal_ids(int(workspace_id))
    entity_ids = normalize_entity_ids(entity_ids) if entity_ids else []

    total_count = len(entity_ids)
    _emit_progress(job_id, workspace_id, 0, total_count, "running", "job started")
    if not entity_ids:
        _emit_completed(job_id, workspace_id, "completed", 0, 0, 0, started_at, None)
        return

    config = _load_schema_config()
    # Scale deadline: 3 s per deal, at least 120 s, capped at 600 s.
    deadline = time.perf_counter() + min(max(120.0, total_count * 3.0), 600.0)

    processed = 0
    try:
        for chunk_start in range(0, total_count, PROGRESS_CHUNK_SIZE):
            chunk = entity_ids[chunk_start:chunk_start + PROGRESS_CHUNK_SIZE]
            _ensure_deal_analysis_entities(chunk, config, deadline, created_by_user_id=requested_by_user_id)
            analysis_rows = _load_analysis_state(chunk, config)
            deal_rows = _load_deal_inputs(chunk, config)
            contracts = _load_contract_inputs(chunk, config)
            contracts_by_deal = {}
            for row in contracts:
                contracts_by_deal.setdefault(row["deal_id"], []).append(row)
            _recompute_analysis(chunk, analysis_rows, deal_rows, contracts_by_deal, config, deadline, today)
            processed = chunk_start + len(chunk)
            _emit_progress(job_id, workspace_id, processed, total_count, "running", "")

        _recompute_client_properties(entity_ids, config, deadline, today)
        _emit_completed(job_id, workspace_id, "completed", total_count, total_count, 0, started_at, None)

    except TimeoutError:
        failed = total_count - processed
        _emit_completed(job_id, workspace_id, "timeout", processed, processed, failed, started_at, "deadline exceeded")
        raise
    except Exception:
        failed = total_count - processed
        _emit_completed(job_id, workspace_id, "failed", processed, processed, failed, started_at, "unexpected error")
        raise


def _emit_progress(job_id, workspace_id, processed_count, total_count, status, message):
    now = datetime.now(timezone.utc)
    envelope = {
        "SchemaVersion": 1,
        "MessageId": str(uuid.uuid4()),
        "CorrelationId": str(job_id),
        "SagaInstanceId": str(job_id),
        "CausationId": None,
        "OccurredAtUtc": now.isoformat(),
        "SourceService": "ml",
        "PayloadTypeName": RECALC_PROGRESS_PAYLOAD_TYPE,
        "PayloadJson": json.dumps(
            {
                "JobId": job_id,
                "WorkspaceId": workspace_id,
                "Status": status,
                "ProcessedCount": processed_count,
                "TotalCount": total_count,
                "UpdatedAtUtc": now.isoformat(),
                "Message": message,
            }
        ),
    }
    publish_domain_event("ml.recalculate.progress", envelope)


def _emit_completed(job_id, workspace_id, status, processed_count, succeeded_count, failed_count, started_at, error):
    completed_at = datetime.now(timezone.utc)
    envelope = {
        "SchemaVersion": 1,
        "MessageId": str(uuid.uuid4()),
        "CorrelationId": str(job_id),
        "SagaInstanceId": str(job_id),
        "CausationId": None,
        "OccurredAtUtc": completed_at.isoformat(),
        "SourceService": "ml",
        "PayloadTypeName": RECALC_COMPLETED_PAYLOAD_TYPE,
        "PayloadJson": json.dumps(
            {
                "JobId": job_id,
                "WorkspaceId": workspace_id,
                "Status": status,
                "ProcessedCount": processed_count,
                "SucceededCount": succeeded_count,
                "FailedCount": failed_count,
                "StartedAtUtc": started_at.isoformat(),
                "CompletedAtUtc": completed_at.isoformat(),
                "Error": error,
            }
        ),
    }
    publish_domain_event("ml.recalculate.completed", envelope)


def _load_workspace_deal_ids(workspace_id):
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT e.id
            FROM entity e
            JOIN entity_workspace ew ON ew.entity_id = e.id
            JOIN entity_type et ON et.id = e.entity_type_id
            WHERE ew.workspace_id = %s
              AND e.is_archived = FALSE
              AND et.name = %s
            ORDER BY e.id
            LIMIT %s
            """,
            [workspace_id, DEAL_TYPE_NAME, MAX_BATCH_SIZE],
        )
        return [row[0] for row in cursor.fetchall()]


def _check_deadline(deadline):
    if time.perf_counter() > deadline:
        raise TimeoutError()


def _load_schema_config():
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT id, name
            FROM entity_type
            WHERE name IN (%s, %s, %s, %s)
            """,
            [DEAL_TYPE_NAME, DEAL_ANALYSIS_TYPE_NAME, CONTRACT_TYPE_NAME, CLIENT_TYPE_NAME],
        )
        type_ids = {name: type_id for type_id, name in cursor.fetchall()}

        cursor.execute(
            """
            SELECT id, name
            FROM entity_relationship_type
            WHERE name IN (%s, %s, %s)
            """,
            [REL_DEAL_ANALYSIS, REL_DEAL_CONTRACT, REL_DEAL_CLIENT],
        )
        rel_ids = {name: rel_id for rel_id, name in cursor.fetchall()}

        all_prop_names = [
            DEAL_PROP_CREATED_AT,
            DEAL_PROP_STATUS,
            DEAL_PROP_EXPECTED_CLOSE,
            ANALYSIS_PROP_DAYS_SINCE_CREATED,
            ANALYSIS_PROP_STAGE_ENCODED,
            ANALYSIS_PROP_NUM_INTERACTIONS,
            ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
            ANALYSIS_PROP_NUM_OPEN_DEALS,
            ANALYSIS_PROP_AVG_DEAL_VALUE,
            ANALYSIS_PROP_SOURCE_UPDATED_AT,
            ANALYSIS_PROP_CALCULATED_AT,
            ANALYSIS_PROP_DAYS_UNTIL_CLOSE,
            ANALYSIS_PROP_HIST_CLOSE_RATE,
            CONTRACT_PROP_AMOUNT,
            CONTRACT_PROP_STATUS,
            CONTRACT_PROP_END_DATE,
            CONTRACT_PROP_SIGNED_AT,
            CLIENT_PROP_LIFETIME_VALUE,
            CLIENT_PROP_TENURE_DAYS,
            DEAL_PROP_CLOSURE_SCORE,
            DEAL_PROP_CHURN_SCORE,
            "deal_value",
        ]
        cursor.execute(
            """
            SELECT id, name
            FROM property
            WHERE organization_id IS NULL
              AND name = ANY(%s)
            """,
            [all_prop_names],
        )
        prop_ids = {name: prop_id for prop_id, name in cursor.fetchall()}
    return {"type_ids": type_ids, "rel_ids": rel_ids, "prop_ids": prop_ids}


def _ensure_deal_analysis_entities(deal_ids, config, deadline, created_by_user_id=0):
    _check_deadline(deadline)
    deal_type_id = config["type_ids"].get(DEAL_TYPE_NAME)
    analysis_type_id = config["type_ids"].get(DEAL_ANALYSIS_TYPE_NAME)
    rel_type_id = config["rel_ids"].get(REL_DEAL_ANALYSIS)
    if not deal_type_id or not analysis_type_id or not rel_type_id:
        logger.warning("Schema config missing deal/deal_analysis type or relationship — skipping entity creation")
        return
    with transaction.atomic():
        with connection.cursor() as cursor:
            cursor.execute(
                """
                SELECT e.id
                FROM entity e
                WHERE e.id = ANY(%s)
                  AND e.entity_type_id = %s
                  AND e.is_archived = FALSE
                """,
                [deal_ids, deal_type_id],
            )
            existing_deals = {row[0] for row in cursor.fetchall()}
            if not existing_deals:
                return
            cursor.execute(
                """
                SELECT er.source_entity_id, er.target_entity_id
                FROM entity_relationship er
                WHERE er.relationship_type_id = %s
                  AND er.source_entity_id = ANY(%s)
                """,
                [rel_type_id, list(existing_deals)],
            )
            existing_links = {row[0]: row[1] for row in cursor.fetchall()}
            missing_deals = [deal_id for deal_id in existing_deals if deal_id not in existing_links]
            for deal_id in missing_deals:
                _check_deadline(deadline)
                cursor.execute(
                    "INSERT INTO entity (entity_type_id, created_by_user_id, is_archived) VALUES (%s, %s, FALSE) RETURNING id",
                    [analysis_type_id, created_by_user_id],
                )
                analysis_id = cursor.fetchone()[0]
                cursor.execute(
                    """
                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (%s, %s, %s)
                    """,
                    [deal_id, analysis_id, rel_type_id],
                )
                cursor.execute(
                    """
                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT %s, ew.workspace_id
                    FROM entity_workspace ew
                    WHERE ew.entity_id = %s
                    ON CONFLICT (entity_id, workspace_id) DO NOTHING
                    """,
                    [analysis_id, deal_id],
                )


def _load_analysis_state(deal_ids, config):
    rel_type_id = config["rel_ids"][REL_DEAL_ANALYSIS]
    prop_ids = config["prop_ids"]
    analysis_by_deal = {}
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.source_entity_id, er.target_entity_id, epv.property_id, epv.value_int, epv.value_decimal, epv.value_date
            FROM entity_relationship er
            LEFT JOIN entity_property_value epv ON epv.entity_id = er.target_entity_id
            WHERE er.relationship_type_id = %s
              AND er.source_entity_id = ANY(%s)
            """,
            [rel_type_id, deal_ids],
        )
        for deal_id, analysis_id, property_id, value_int, value_decimal, value_date in cursor.fetchall():
            row = analysis_by_deal.setdefault(deal_id, {"analysis_entity_id": analysis_id})
            if property_id == prop_ids.get(ANALYSIS_PROP_DAYS_SINCE_CREATED):
                row[ANALYSIS_PROP_DAYS_SINCE_CREATED] = value_int
            elif property_id == prop_ids.get(ANALYSIS_PROP_STAGE_ENCODED):
                row[ANALYSIS_PROP_STAGE_ENCODED] = value_int
            elif property_id == prop_ids.get(ANALYSIS_PROP_NUM_INTERACTIONS):
                row[ANALYSIS_PROP_NUM_INTERACTIONS] = value_int
            elif property_id == prop_ids.get(ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT):
                row[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT] = value_int
            elif property_id == prop_ids.get(ANALYSIS_PROP_NUM_OPEN_DEALS):
                row[ANALYSIS_PROP_NUM_OPEN_DEALS] = value_int
            elif property_id == prop_ids.get(ANALYSIS_PROP_AVG_DEAL_VALUE):
                row[ANALYSIS_PROP_AVG_DEAL_VALUE] = float(value_decimal) if value_decimal is not None else None
            elif property_id == prop_ids.get(ANALYSIS_PROP_SOURCE_UPDATED_AT):
                row[ANALYSIS_PROP_SOURCE_UPDATED_AT] = value_date
            elif property_id == prop_ids.get(ANALYSIS_PROP_CALCULATED_AT):
                row[ANALYSIS_PROP_CALCULATED_AT] = value_date
    return analysis_by_deal


def _load_deal_inputs(deal_ids, config):
    prop_ids = config["prop_ids"]
    deal_by_id = {}
    wanted = [
        p for p in [
            prop_ids.get(DEAL_PROP_CREATED_AT),
            prop_ids.get(DEAL_PROP_STATUS),
            prop_ids.get(DEAL_PROP_EXPECTED_CLOSE),
            prop_ids.get("deal_value"),
        ] if p is not None
    ]
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT epv.entity_id, epv.property_id, epv.value_string, epv.value_decimal, epv.value_date
            FROM entity_property_value epv
            WHERE epv.entity_id = ANY(%s)
              AND epv.property_id = ANY(%s)
            """,
            [deal_ids, wanted],
        )
        for entity_id, property_id, value_string, value_decimal, value_date in cursor.fetchall():
            row = deal_by_id.setdefault(entity_id, {})
            if property_id == prop_ids.get(DEAL_PROP_CREATED_AT):
                row[DEAL_PROP_CREATED_AT] = value_date
            elif property_id == prop_ids.get(DEAL_PROP_STATUS):
                row[DEAL_PROP_STATUS] = value_string
            elif property_id == prop_ids.get(DEAL_PROP_EXPECTED_CLOSE):
                row[DEAL_PROP_EXPECTED_CLOSE] = value_date
            elif property_id == prop_ids.get("deal_value"):
                row["deal_value"] = float(value_decimal) if value_decimal is not None else None
    return deal_by_id


def _load_contract_inputs(deal_ids, config):
    rel_type_id = config["rel_ids"].get(REL_DEAL_CONTRACT)
    if rel_type_id is None:
        return []
    prop_ids = config["prop_ids"]
    result = []
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.source_entity_id, er.target_entity_id, epv.property_id, epv.value_string, epv.value_decimal, epv.value_date
            FROM entity_relationship er
            INNER JOIN entity contract ON contract.id = er.target_entity_id AND contract.is_archived = FALSE
            LEFT JOIN entity_property_value epv ON epv.entity_id = er.target_entity_id
            WHERE er.relationship_type_id = %s
              AND er.source_entity_id = ANY(%s)
            """,
            [rel_type_id, deal_ids],
        )
        contracts = {}
        for deal_id, contract_id, property_id, value_string, value_decimal, value_date in cursor.fetchall():
            key = (deal_id, contract_id)
            row = contracts.setdefault(key, {"deal_id": deal_id, "contract_id": contract_id})
            if property_id == prop_ids.get(CONTRACT_PROP_AMOUNT):
                row[CONTRACT_PROP_AMOUNT] = float(value_decimal) if value_decimal is not None else None
            elif property_id == prop_ids.get(CONTRACT_PROP_STATUS):
                row[CONTRACT_PROP_STATUS] = value_string
            elif property_id == prop_ids.get(CONTRACT_PROP_END_DATE):
                row[CONTRACT_PROP_END_DATE] = value_date
            elif property_id == prop_ids.get(CONTRACT_PROP_SIGNED_AT):
                row[CONTRACT_PROP_SIGNED_AT] = value_date
        result.extend(contracts.values())
    return result


def recompute_deal_analysis(deal_ids, deadline=None):
    if deadline is None:
        deadline = time.perf_counter() + BATCH_TIMEOUT_SECONDS
    config = _load_schema_config()
    _ensure_deal_analysis_entities(deal_ids, config, deadline)
    analysis_rows = _load_analysis_state(deal_ids, config)
    deal_rows = _load_deal_inputs(deal_ids, config)
    contract_rows = _load_contract_inputs(deal_ids, config)
    by_deal = {}
    for row in contract_rows:
        by_deal.setdefault(row["deal_id"], []).append(row)
    _recompute_analysis(deal_ids, analysis_rows, deal_rows, by_deal, config, deadline, date.today())
    _recompute_client_properties(deal_ids, config, deadline, date.today())
    return _load_analysis_state(deal_ids, config)


def _load_deal_client_map(deal_ids, config):
    rel_id = config["rel_ids"].get(REL_DEAL_CLIENT)
    if not rel_id:
        return {}
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.source_entity_id, er.target_entity_id
            FROM entity_relationship er
            WHERE er.relationship_type_id = %s
              AND er.source_entity_id = ANY(%s)
            """,
            [rel_id, deal_ids],
        )
        return {deal_id: client_id for deal_id, client_id in cursor.fetchall()}


def _load_client_deal_statuses(client_ids, config):
    rel_id = config["rel_ids"].get(REL_DEAL_CLIENT)
    status_prop = config["prop_ids"].get(DEAL_PROP_STATUS)
    if not rel_id or not status_prop or not client_ids:
        return {}
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.target_entity_id, epv.value_string
            FROM entity_relationship er
            JOIN entity e ON e.id = er.source_entity_id AND e.is_archived = FALSE
            JOIN entity_property_value epv ON epv.entity_id = er.source_entity_id AND epv.property_id = %s
            WHERE er.relationship_type_id = %s
              AND er.target_entity_id = ANY(%s)
            """,
            [status_prop, rel_id, client_ids],
        )
        result = {}
        for client_id, status in cursor.fetchall():
            result.setdefault(client_id, []).append(status)
        return result


def _recompute_analysis(deal_ids, analysis_rows, deal_rows, contracts_by_deal, config, deadline, today):
    prop_ids = config["prop_ids"]
    deal_client_map = _load_deal_client_map(deal_ids, config)
    client_ids = list(set(deal_client_map.values()))
    client_deal_statuses = _load_client_deal_statuses(client_ids, config) if client_ids else {}

    with transaction.atomic():
        with connection.cursor() as cursor:
            for deal_id in deal_ids:
                _check_deadline(deadline)
                analysis = analysis_rows.get(deal_id)
                deal = deal_rows.get(deal_id, {})
                if analysis is None:
                    continue
                created_at = deal.get(DEAL_PROP_CREATED_AT)
                status = (deal.get(DEAL_PROP_STATUS) or "").lower()
                deal_value = deal.get("deal_value") or 0.0
                contracts = contracts_by_deal.get(deal_id, [])
                if not created_at or status not in DEAL_STATUS_TO_STAGE:
                    continue
                days_since_created = max(0, (today - created_at).days)
                stage_encoded = DEAL_STATUS_TO_STAGE[status]
                num_interactions = max(0, min(100, days_since_created // 7))
                active_contracts = [c for c in contracts if (c.get(CONTRACT_PROP_STATUS) or "").lower() in ACTIVE_CONTRACT_STATUSES]
                chosen_contracts = active_contracts if active_contracts else contracts
                amounts = [float(c.get(CONTRACT_PROP_AMOUNT) or 0.0) for c in chosen_contracts if c.get(CONTRACT_PROP_AMOUNT) is not None]
                avg_deal_value = float(sum(amounts) / len(amounts)) if amounts else float(deal_value)
                num_open_deals = len(active_contracts)
                signed_dates = [c.get(CONTRACT_PROP_SIGNED_AT) for c in chosen_contracts if c.get(CONTRACT_PROP_SIGNED_AT) is not None]
                days_since_last_contact = max(0, (today - max(signed_dates)).days) if signed_dates else max(0, min(365, days_since_created // 2))

                expected_close = deal.get(DEAL_PROP_EXPECTED_CLOSE)
                days_until_close = (expected_close - today).days if expected_close is not None else None

                client_id = deal_client_map.get(deal_id)
                if client_id:
                    client_statuses = client_deal_statuses.get(client_id, [])
                    total = len(client_statuses)
                    closed = sum(1 for s in client_statuses if (s or "").lower() == "closed")
                    hist_close_rate = (closed / total * 100.0) if total > 0 else None
                else:
                    hist_close_rate = None

                required_prop_ids = {
                    ANALYSIS_PROP_DAYS_SINCE_CREATED:      prop_ids.get(ANALYSIS_PROP_DAYS_SINCE_CREATED),
                    ANALYSIS_PROP_STAGE_ENCODED:           prop_ids.get(ANALYSIS_PROP_STAGE_ENCODED),
                    ANALYSIS_PROP_NUM_INTERACTIONS:        prop_ids.get(ANALYSIS_PROP_NUM_INTERACTIONS),
                    ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT: prop_ids.get(ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT),
                    ANALYSIS_PROP_NUM_OPEN_DEALS:          prop_ids.get(ANALYSIS_PROP_NUM_OPEN_DEALS),
                    ANALYSIS_PROP_AVG_DEAL_VALUE:          prop_ids.get(ANALYSIS_PROP_AVG_DEAL_VALUE),
                    ANALYSIS_PROP_SOURCE_UPDATED_AT:       prop_ids.get(ANALYSIS_PROP_SOURCE_UPDATED_AT),
                    ANALYSIS_PROP_CALCULATED_AT:           prop_ids.get(ANALYSIS_PROP_CALCULATED_AT),
                }
                if None in required_prop_ids.values():
                    logger.warning(
                        "Schema config is missing one or more analysis property IDs — skipping deal %s", deal_id
                    )
                    continue
                updates = [
                    (required_prop_ids[ANALYSIS_PROP_DAYS_SINCE_CREATED], {"value_int": days_since_created}),
                    (required_prop_ids[ANALYSIS_PROP_STAGE_ENCODED], {"value_int": stage_encoded}),
                    (required_prop_ids[ANALYSIS_PROP_NUM_INTERACTIONS], {"value_int": num_interactions}),
                    (required_prop_ids[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT], {"value_int": days_since_last_contact}),
                    (required_prop_ids[ANALYSIS_PROP_NUM_OPEN_DEALS], {"value_int": num_open_deals}),
                    (required_prop_ids[ANALYSIS_PROP_AVG_DEAL_VALUE], {"value_decimal": avg_deal_value}),
                    (required_prop_ids[ANALYSIS_PROP_SOURCE_UPDATED_AT], {"value_date": today}),
                    (required_prop_ids[ANALYSIS_PROP_CALCULATED_AT], {"value_date": today}),
                ]
                if prop_ids.get(ANALYSIS_PROP_DAYS_UNTIL_CLOSE):
                    updates.append((prop_ids[ANALYSIS_PROP_DAYS_UNTIL_CLOSE], {"value_int": days_until_close}))
                if prop_ids.get(ANALYSIS_PROP_HIST_CLOSE_RATE):
                    updates.append((prop_ids[ANALYSIS_PROP_HIST_CLOSE_RATE], {"value_decimal": hist_close_rate}))
                for property_id, value_map in updates:
                    _upsert_property(cursor, analysis["analysis_entity_id"], property_id, value_map)


def _recompute_client_properties(deal_ids, config, deadline, today):
    ltv_prop = config["prop_ids"].get(CLIENT_PROP_LIFETIME_VALUE)
    tenure_prop = config["prop_ids"].get(CLIENT_PROP_TENURE_DAYS)
    if not ltv_prop and not tenure_prop:
        return

    rel_id = config["rel_ids"].get(REL_DEAL_CLIENT)
    if not rel_id:
        return

    deal_client_map = _load_deal_client_map(deal_ids, config)
    client_ids = list(set(deal_client_map.values()))
    if not client_ids:
        return

    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.target_entity_id, er.source_entity_id
            FROM entity_relationship er
            JOIN entity e ON e.id = er.source_entity_id AND e.is_archived = FALSE
            WHERE er.relationship_type_id = %s
              AND er.target_entity_id = ANY(%s)
            """,
            [rel_id, client_ids],
        )
        client_to_deals = {}
        for client_id, deal_id in cursor.fetchall():
            client_to_deals.setdefault(client_id, []).append(deal_id)

        all_deal_ids = [d for deals in client_to_deals.values() for d in deals]
        if not all_deal_ids:
            return

        prop_ids = config["prop_ids"]
        deal_value_prop = prop_ids.get("deal_value")
        created_at_prop = prop_ids.get(DEAL_PROP_CREATED_AT)
        wanted = [p for p in [deal_value_prop, created_at_prop] if p is not None]
        cursor.execute(
            """
            SELECT epv.entity_id, epv.property_id, epv.value_decimal, epv.value_date
            FROM entity_property_value epv
            WHERE epv.entity_id = ANY(%s)
              AND epv.property_id = ANY(%s)
            """,
            [all_deal_ids, wanted],
        )
        deal_values = {}
        deal_dates = {}
        for deal_id, prop_id, value_decimal, value_date in cursor.fetchall():
            if prop_id == deal_value_prop and value_decimal is not None:
                deal_values[deal_id] = float(value_decimal)
            elif prop_id == created_at_prop and value_date is not None:
                deal_dates[deal_id] = value_date

    with transaction.atomic():
        with connection.cursor() as cursor:
            for client_id in client_ids:
                _check_deadline(deadline)
                client_deal_ids = client_to_deals.get(client_id, [])
                if not client_deal_ids:
                    continue
                ltv = sum(deal_values.get(d, 0.0) for d in client_deal_ids)
                dates = [deal_dates[d] for d in client_deal_ids if d in deal_dates]
                tenure_days = (today - min(dates)).days if dates else None
                if ltv_prop:
                    _upsert_property(cursor, client_id, ltv_prop, {"value_decimal": ltv})
                if tenure_prop and tenure_days is not None:
                    _upsert_property(cursor, client_id, tenure_prop, {"value_int": tenure_days})


def _upsert_property(cursor, entity_id, property_id, value_map):
    cursor.execute(
        """
        INSERT INTO entity_property_value (
            entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date
        )
        VALUES (%s, %s, %s, %s, %s, %s, %s)
        ON CONFLICT (entity_id, property_id)
        DO UPDATE SET
            value_string = EXCLUDED.value_string,
            value_int = EXCLUDED.value_int,
            value_decimal = EXCLUDED.value_decimal,
            value_bool = EXCLUDED.value_bool,
            value_date = EXCLUDED.value_date
        """,
        [
            entity_id,
            property_id,
            value_map.get("value_string"),
            value_map.get("value_int"),
            value_map.get("value_decimal"),
            value_map.get("value_bool"),
            value_map.get("value_date"),
        ],
    )
