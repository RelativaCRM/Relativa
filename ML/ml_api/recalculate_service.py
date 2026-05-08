import json
import os
import time
import uuid
from datetime import date, datetime, timezone

import pika
from django.conf import settings
from django.db import connection, transaction

MAX_BATCH_SIZE = int(os.environ.get("ML_SCORE_BATCH_MAX_SIZE", "200"))
BATCH_TIMEOUT_SECONDS = 5.0

DEAL_TYPE_NAME = "deal"
DEAL_ANALYSIS_TYPE_NAME = "deal_analysis"
CONTRACT_TYPE_NAME = "contract"

REL_DEAL_ANALYSIS = "deal_analysis"
REL_DEAL_CONTRACT = "deal_contract"

DEAL_PROP_CREATED_AT = "created_at"
DEAL_PROP_STATUS = "status"

ANALYSIS_PROP_DAYS_SINCE_CREATED = "days_since_created"
ANALYSIS_PROP_STAGE_ENCODED = "stage_encoded"
ANALYSIS_PROP_NUM_INTERACTIONS = "num_interactions"
ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT = "days_since_last_contact"
ANALYSIS_PROP_NUM_OPEN_DEALS = "num_open_deals"
ANALYSIS_PROP_AVG_DEAL_VALUE = "avg_deal_value"
ANALYSIS_PROP_SOURCE_UPDATED_AT = "source_updated_at"
ANALYSIS_PROP_CALCULATED_AT = "calculated_at"

CONTRACT_PROP_AMOUNT = "amount"
CONTRACT_PROP_STATUS = "contract_status"
CONTRACT_PROP_END_DATE = "end_date"
CONTRACT_PROP_SIGNED_AT = "signed_at"

FEATURE_KEYS = (
    ANALYSIS_PROP_DAYS_SINCE_CREATED,
    ANALYSIS_PROP_STAGE_ENCODED,
    ANALYSIS_PROP_NUM_INTERACTIONS,
    ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
    ANALYSIS_PROP_NUM_OPEN_DEALS,
    ANALYSIS_PROP_AVG_DEAL_VALUE,
)

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


def process_recalc_payload(payload):
    entity_ids = payload.get("EntityIds") or payload.get("entityIds") or []
    workspace_id = payload.get("WorkspaceId") or payload.get("workspaceId")
    job_id_raw = payload.get("JobId") or payload.get("jobId")
    job_id = str(job_id_raw) if job_id_raw else str(uuid.uuid4())
    started_at = datetime.now(timezone.utc)

    if workspace_id is not None and not entity_ids:
        entity_ids = _load_workspace_deal_ids(int(workspace_id))
    entity_ids = normalize_entity_ids(entity_ids) if entity_ids else []

    total_count = len(entity_ids)
    _emit_progress(job_id, workspace_id, 0, total_count, "running", "job started")
    if not entity_ids:
        _emit_completed(job_id, workspace_id, "completed", 0, 0, 0, started_at, None)
        return

    config = _load_schema_config()
    deadline = time.perf_counter() + max(BATCH_TIMEOUT_SECONDS * 10, 30.0)
    _ensure_deal_analysis_entities(entity_ids, config, deadline)
    analysis_rows = _load_analysis_state(entity_ids, config)
    deal_rows = _load_deal_inputs(entity_ids, config)
    contracts = _load_contract_inputs(entity_ids, config)
    contracts_by_deal = {}
    for row in contracts:
        contracts_by_deal.setdefault(row["deal_id"], []).append(row)

    _recompute_analysis(entity_ids, analysis_rows, deal_rows, contracts_by_deal, config, deadline, date.today())
    _emit_progress(job_id, workspace_id, total_count, total_count, "running", "recompute done")
    _emit_completed(job_id, workspace_id, "completed", total_count, total_count, 0, started_at, None)


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
            WHERE name IN (%s, %s, %s)
            """,
            [DEAL_TYPE_NAME, DEAL_ANALYSIS_TYPE_NAME, CONTRACT_TYPE_NAME],
        )
        type_ids = {name: type_id for type_id, name in cursor.fetchall()}

        cursor.execute(
            """
            SELECT id, name
            FROM entity_relationship_type
            WHERE name IN (%s, %s)
            """,
            [REL_DEAL_ANALYSIS, REL_DEAL_CONTRACT],
        )
        rel_ids = {name: rel_id for rel_id, name in cursor.fetchall()}

        all_prop_names = [
            DEAL_PROP_CREATED_AT,
            DEAL_PROP_STATUS,
            ANALYSIS_PROP_DAYS_SINCE_CREATED,
            ANALYSIS_PROP_STAGE_ENCODED,
            ANALYSIS_PROP_NUM_INTERACTIONS,
            ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
            ANALYSIS_PROP_NUM_OPEN_DEALS,
            ANALYSIS_PROP_AVG_DEAL_VALUE,
            ANALYSIS_PROP_SOURCE_UPDATED_AT,
            ANALYSIS_PROP_CALCULATED_AT,
            CONTRACT_PROP_AMOUNT,
            CONTRACT_PROP_STATUS,
            CONTRACT_PROP_END_DATE,
            CONTRACT_PROP_SIGNED_AT,
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


def _ensure_deal_analysis_entities(deal_ids, config, deadline):
    _check_deadline(deadline)
    deal_type_id = config["type_ids"][DEAL_TYPE_NAME]
    analysis_type_id = config["type_ids"][DEAL_ANALYSIS_TYPE_NAME]
    rel_type_id = config["rel_ids"][REL_DEAL_ANALYSIS]
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
                    "INSERT INTO entity (entity_type_id, is_archived) VALUES (%s, FALSE) RETURNING id",
                    [analysis_type_id],
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
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT epv.entity_id, epv.property_id, epv.value_string, epv.value_decimal, epv.value_date
            FROM entity_property_value epv
            WHERE epv.entity_id = ANY(%s)
              AND epv.property_id = ANY(%s)
            """,
            [deal_ids, [prop_ids.get(DEAL_PROP_CREATED_AT), prop_ids.get(DEAL_PROP_STATUS), prop_ids.get("deal_value")]],
        )
        for entity_id, property_id, value_string, value_decimal, value_date in cursor.fetchall():
            row = deal_by_id.setdefault(entity_id, {})
            if property_id == prop_ids.get(DEAL_PROP_CREATED_AT):
                row[DEAL_PROP_CREATED_AT] = value_date
            elif property_id == prop_ids.get(DEAL_PROP_STATUS):
                row[DEAL_PROP_STATUS] = value_string
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
    return _load_analysis_state(deal_ids, config)


def _recompute_analysis(deal_ids, analysis_rows, deal_rows, contracts_by_deal, config, deadline, today):
    prop_ids = config["prop_ids"]
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
                updates = [
                    (prop_ids[ANALYSIS_PROP_DAYS_SINCE_CREATED], {"value_int": days_since_created}),
                    (prop_ids[ANALYSIS_PROP_STAGE_ENCODED], {"value_int": stage_encoded}),
                    (prop_ids[ANALYSIS_PROP_NUM_INTERACTIONS], {"value_int": num_interactions}),
                    (prop_ids[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT], {"value_int": days_since_last_contact}),
                    (prop_ids[ANALYSIS_PROP_NUM_OPEN_DEALS], {"value_int": num_open_deals}),
                    (prop_ids[ANALYSIS_PROP_AVG_DEAL_VALUE], {"value_decimal": avg_deal_value}),
                    (prop_ids[ANALYSIS_PROP_SOURCE_UPDATED_AT], {"value_date": today}),
                    (prop_ids[ANALYSIS_PROP_CALCULATED_AT], {"value_date": today}),
                ]
                for property_id, value_map in updates:
                    _upsert_property(cursor, analysis["analysis_entity_id"], property_id, value_map)


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
