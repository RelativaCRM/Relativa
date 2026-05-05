import os
import time
from datetime import date

from django.db import connection, transaction
from rest_framework.decorators import api_view
from rest_framework.response import Response

from .apps import MlApiConfig

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


@api_view(['GET'])
def health(request):
    # Перевіряємо, чи моделі успішно завантажились у пам'ять
    model_loaded = (MlApiConfig.churn_model is not None) and (MlApiConfig.closure_model is not None)
    return Response({'status': 'ok', 'model_loaded': model_loaded})


@api_view(['POST'])
def recalculate(request):
    return Response({'status': 'accepted', 'detail': 'stub'})


@api_view(["POST"])
def score_batch(request):
    if (MlApiConfig.churn_model is None) or (MlApiConfig.closure_model is None):
        return Response(
            {"detail": "ML models are not loaded."},
            status=503,
        )

    entity_ids = request.data.get("entity_ids")
    if not isinstance(entity_ids, list) or not entity_ids:
        return Response({"detail": "entity_ids must be a non-empty array of integers."}, status=400)

    if len(entity_ids) > MAX_BATCH_SIZE:
        return Response(
            {"detail": f"entity_ids exceeds max batch size ({MAX_BATCH_SIZE})."},
            status=400,
        )

    normalized = []
    seen = set()
    for raw in entity_ids:
        if not isinstance(raw, int) or raw <= 0:
            return Response({"detail": "entity_ids must contain positive integers only."}, status=400)
        if raw in seen:
            continue
        seen.add(raw)
        normalized.append(raw)

    started = time.perf_counter()
    deadline = started + BATCH_TIMEOUT_SECONDS

    try:
        config = _load_schema_config()
        _check_deadline(deadline)
        _ensure_deal_analysis_entities(normalized, config, deadline)
        _check_deadline(deadline)

        analysis_rows = _load_analysis_state(normalized, config)
        deal_rows = _load_deal_inputs(normalized, config)
        contract_rows = _load_contract_inputs(normalized, config)
        _check_deadline(deadline)

        by_deal = {deal_id: [] for deal_id in normalized}
        for row in contract_rows:
            by_deal.setdefault(row["deal_id"], []).append(row)

        results_by_id = {}
        today = date.today()
        stale_analysis_ids = []
        for deal_id in normalized:
            analysis = analysis_rows.get(deal_id)
            if analysis is None:
                results_by_id[deal_id] = {"closure_score": None, "churn_score": None}
                continue

            source_updated_at = analysis.get(ANALYSIS_PROP_SOURCE_UPDATED_AT)
            calculated_at = analysis.get(ANALYSIS_PROP_CALCULATED_AT)
            if calculated_at is None or source_updated_at is None or calculated_at < source_updated_at:
                stale_analysis_ids.append(deal_id)
                continue

            scores = _score_from_analysis(analysis)
            results_by_id[deal_id] = scores

        if stale_analysis_ids:
            _recompute_analysis(stale_analysis_ids, analysis_rows, deal_rows, by_deal, config, deadline, today)
            refreshed = _load_analysis_state(stale_analysis_ids, config)
            for deal_id in stale_analysis_ids:
                scores = _score_from_analysis(refreshed.get(deal_id, {}))
                results_by_id[deal_id] = scores

        _check_deadline(deadline)
        response_payload = [
            {
                "entity_id": entity_id,
                "closure_score": results_by_id.get(entity_id, {}).get("closure_score"),
                "churn_score": results_by_id.get(entity_id, {}).get("churn_score"),
            }
            for entity_id in entity_ids
        ]
        return Response(response_payload, status=200)
    except TimeoutError:
        return Response(
            {"detail": "Batch scoring timeout exceeded (5 seconds)."},
            status=504,
        )
    except Exception as exc:
        return Response({"detail": f"Failed to score batch: {exc}"}, status=500)


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

    required_type_names = [DEAL_TYPE_NAME, DEAL_ANALYSIS_TYPE_NAME]
    required_rel_names = [REL_DEAL_ANALYSIS]
    required_props = [
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
    ]

    missing = []
    missing.extend([f"entity_type:{name}" for name in required_type_names if name not in type_ids])
    missing.extend([f"relationship_type:{name}" for name in required_rel_names if name not in rel_ids])
    missing.extend([f"property:{name}" for name in required_props if name not in prop_ids])
    if missing:
        raise RuntimeError(f"Missing schema seeds: {', '.join(missing)}")

    return {
        "type_ids": type_ids,
        "rel_ids": rel_ids,
        "prop_ids": prop_ids,
    }


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
                    """
                    INSERT INTO entity (entity_type_id, is_archived)
                    VALUES (%s, FALSE)
                    RETURNING id
                    """,
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


def _load_analysis_state(deal_ids, config):
    rel_type_id = config["rel_ids"][REL_DEAL_ANALYSIS]
    prop_ids = config["prop_ids"]

    analysis_by_deal = {}
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT er.source_entity_id,
                   er.target_entity_id,
                   epv.property_id,
                   epv.value_int,
                   epv.value_decimal,
                   epv.value_date
            FROM entity_relationship er
            LEFT JOIN entity_property_value epv ON epv.entity_id = er.target_entity_id
            WHERE er.relationship_type_id = %s
              AND er.source_entity_id = ANY(%s)
            """,
            [rel_type_id, deal_ids],
        )
        for deal_id, analysis_id, property_id, value_int, value_decimal, value_date in cursor.fetchall():
            row = analysis_by_deal.setdefault(deal_id, {"analysis_entity_id": analysis_id})
            if property_id == prop_ids[ANALYSIS_PROP_DAYS_SINCE_CREATED]:
                row[ANALYSIS_PROP_DAYS_SINCE_CREATED] = value_int
            elif property_id == prop_ids[ANALYSIS_PROP_STAGE_ENCODED]:
                row[ANALYSIS_PROP_STAGE_ENCODED] = value_int
            elif property_id == prop_ids[ANALYSIS_PROP_NUM_INTERACTIONS]:
                row[ANALYSIS_PROP_NUM_INTERACTIONS] = value_int
            elif property_id == prop_ids[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT]:
                row[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT] = value_int
            elif property_id == prop_ids[ANALYSIS_PROP_NUM_OPEN_DEALS]:
                row[ANALYSIS_PROP_NUM_OPEN_DEALS] = value_int
            elif property_id == prop_ids[ANALYSIS_PROP_AVG_DEAL_VALUE]:
                row[ANALYSIS_PROP_AVG_DEAL_VALUE] = float(value_decimal) if value_decimal is not None else None
            elif property_id == prop_ids[ANALYSIS_PROP_SOURCE_UPDATED_AT]:
                row[ANALYSIS_PROP_SOURCE_UPDATED_AT] = value_date
            elif property_id == prop_ids[ANALYSIS_PROP_CALCULATED_AT]:
                row[ANALYSIS_PROP_CALCULATED_AT] = value_date

    return analysis_by_deal


def _load_deal_inputs(deal_ids, config):
    prop_ids = config["prop_ids"]
    deal_by_id = {}
    with connection.cursor() as cursor:
        cursor.execute(
            """
            SELECT epv.entity_id,
                   epv.property_id,
                   epv.value_string,
                   epv.value_decimal,
                   epv.value_date
            FROM entity_property_value epv
            WHERE epv.entity_id = ANY(%s)
              AND epv.property_id = ANY(%s)
            """,
            [
                deal_ids,
                [
                    prop_ids[DEAL_PROP_CREATED_AT],
                    prop_ids[DEAL_PROP_STATUS],
                    prop_ids.get("deal_value"),
                ],
            ],
        )
        for entity_id, property_id, value_string, value_decimal, value_date in cursor.fetchall():
            row = deal_by_id.setdefault(entity_id, {})
            if property_id == prop_ids[DEAL_PROP_CREATED_AT]:
                row[DEAL_PROP_CREATED_AT] = value_date
            elif property_id == prop_ids[DEAL_PROP_STATUS]:
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
            SELECT er.source_entity_id,
                   er.target_entity_id,
                   epv.property_id,
                   epv.value_string,
                   epv.value_decimal,
                   epv.value_date
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
                if signed_dates:
                    days_since_last_contact = max(0, (today - max(signed_dates)).days)
                else:
                    days_since_last_contact = max(0, min(365, days_since_created // 2))

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
    value_string = value_map.get("value_string")
    value_int = value_map.get("value_int")
    value_decimal = value_map.get("value_decimal")
    value_bool = value_map.get("value_bool")
    value_date = value_map.get("value_date")
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
        [entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date],
    )


def _score_from_analysis(analysis):
    feature_values = [analysis.get(k) for k in FEATURE_KEYS]
    if any(v is None for v in feature_values):
        return {"closure_score": None, "churn_score": None}

    closure_input = [[
        float(analysis[ANALYSIS_PROP_AVG_DEAL_VALUE]),
        int(analysis[ANALYSIS_PROP_DAYS_SINCE_CREATED]),
        int(analysis[ANALYSIS_PROP_STAGE_ENCODED]),
        int(analysis[ANALYSIS_PROP_NUM_INTERACTIONS]),
    ]]
    churn_input = [[
        int(analysis[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT]),
        int(analysis[ANALYSIS_PROP_NUM_OPEN_DEALS]),
        float(analysis[ANALYSIS_PROP_AVG_DEAL_VALUE]),
    ]]
    closure_score = float(MlApiConfig.closure_model.predict_proba(closure_input)[0][1]) * 100.0
    churn_score = float(MlApiConfig.churn_model.predict_proba(churn_input)[0][1]) * 100.0
    return {
        "closure_score": round(closure_score, 4),
        "churn_score": round(churn_score, 4),
    }
