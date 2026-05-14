import time
from rest_framework.decorators import api_view
from rest_framework.response import Response

from .apps import MlApiConfig
from .recalculate_service import (
    ANALYSIS_PROP_AVG_DEAL_VALUE,
    ANALYSIS_PROP_CALCULATED_AT,
    ANALYSIS_PROP_DAYS_SINCE_CREATED,
    ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
    ANALYSIS_PROP_DAYS_UNTIL_CLOSE,
    ANALYSIS_PROP_HIST_CLOSE_RATE,
    ANALYSIS_PROP_NUM_INTERACTIONS,
    ANALYSIS_PROP_NUM_OPEN_DEALS,
    ANALYSIS_PROP_SOURCE_UPDATED_AT,
    ANALYSIS_PROP_STAGE_ENCODED,
    BATCH_TIMEOUT_SECONDS,
    CONTRACT_PROP_AMOUNT,
    DAYS_UNTIL_CLOSE_MEDIAN,
    DEAL_PROP_CLOSURE_SCORE,
    DEAL_PROP_CHURN_SCORE,
    DEAL_PROP_CREATED_AT,
    DEAL_PROP_STATUS,
    DEAL_STATUS_TO_STAGE,
    HIST_CLOSE_RATE_MEDIAN,
    REQUIRED_FEATURE_KEYS,
    enqueue_recalculation_job,
    normalize_entity_ids,
    recompute_deal_analysis,
    _check_deadline,
    _ensure_deal_analysis_entities,
    _load_analysis_state,
    _load_contract_inputs,
    _load_deal_inputs,
    _load_schema_config,
    _upsert_property,
)


@api_view(['GET'])
def health(request):
    # Перевіряємо, чи моделі успішно завантажились у пам'ять
    model_loaded = (MlApiConfig.churn_model is not None) and (MlApiConfig.closure_model is not None)
    return Response({'status': 'ok', 'model_loaded': model_loaded})


@api_view(['POST'])
def recalculate(request):
    payload = request.data if isinstance(request.data, dict) else {}
    workspace_id = payload.get("workspace_id")
    mode = payload.get("mode")
    entity_ids = payload.get("entity_ids")
    if entity_ids is not None and workspace_id is not None and mode == "workspace":
        return Response({"detail": "Provide either entity_ids or workspace mode, not both."}, status=400)
    if workspace_id is not None and mode == "workspace":
        if not isinstance(workspace_id, int) or workspace_id <= 0:
            return Response({"detail": "workspace_id must be a positive integer."}, status=400)
        normalized = []
    else:
        try:
            normalized = normalize_entity_ids(entity_ids)
        except ValueError as exc:
            return Response({"detail": str(exc)}, status=400)
    requested_by_user_id = _extract_user_id(request)
    reason = payload.get("reason") or "manual"
    try:
        job_id = enqueue_recalculation_job(
            entity_ids=normalized,
            workspace_id=workspace_id if mode == "workspace" else None,
            requested_by_user_id=requested_by_user_id,
            reason=reason,
        )
    except Exception as exc:
        return Response({"detail": f"Failed to enqueue recalculation job: {exc}"}, status=500)
    return Response(
        {
            "status": "accepted",
            "job_id": str(job_id),
            "scope": "workspace" if mode == "workspace" else "entity_ids",
            "entity_count": len(normalized),
            "workspace_id": workspace_id if mode == "workspace" else None,
        },
        status=202,
    )


@api_view(["POST"])
def score_batch(request):
    if (MlApiConfig.churn_model is None) or (MlApiConfig.closure_model is None):
        return Response(
            {"detail": "ML models are not loaded."},
            status=503,
        )

    entity_ids = request.data.get("entity_ids")
    try:
        normalized = normalize_entity_ids(entity_ids)
    except ValueError as exc:
        return Response({"detail": str(exc)}, status=400)

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
        contracts_by_deal = {}
        for row in contract_rows:
            contracts_by_deal.setdefault(row["deal_id"], []).append(row)
        _check_deadline(deadline)

        results_by_id = {}
        stale_analysis_ids = []
        for deal_id in normalized:
            analysis = analysis_rows.get(deal_id)
            if analysis is None:
                results_by_id[deal_id] = _score_or_diagnose(
                    None,
                    deal_rows.get(deal_id, {}),
                    contracts_by_deal.get(deal_id, []),
                )
                continue

            source_updated_at = analysis.get(ANALYSIS_PROP_SOURCE_UPDATED_AT)
            calculated_at = analysis.get(ANALYSIS_PROP_CALCULATED_AT)
            if calculated_at is None or source_updated_at is None or calculated_at < source_updated_at:
                stale_analysis_ids.append(deal_id)
                continue

            results_by_id[deal_id] = _score_or_diagnose(
                analysis,
                deal_rows.get(deal_id, {}),
                contracts_by_deal.get(deal_id, []),
            )

        if stale_analysis_ids:
            recompute_deal_analysis(stale_analysis_ids, deadline=deadline)
            refreshed = _load_analysis_state(stale_analysis_ids, config)
            for deal_id in stale_analysis_ids:
                results_by_id[deal_id] = _score_or_diagnose(
                    refreshed.get(deal_id),
                    deal_rows.get(deal_id, {}),
                    contracts_by_deal.get(deal_id, []),
                )

        _check_deadline(deadline)
        response_payload = [
            {
                "entity_id": entity_id,
                "closure_score": results_by_id.get(entity_id, {}).get("closure_score"),
                "churn_score": results_by_id.get(entity_id, {}).get("churn_score"),
                "unavailable_reason": results_by_id.get(entity_id, {}).get("unavailable_reason"),
            }
            for entity_id in entity_ids
        ]
        _persist_scores(response_payload, config)
        return Response(response_payload, status=200)
    except TimeoutError:
        return Response(
            {"detail": "Batch scoring timeout exceeded (5 seconds)."},
            status=504,
        )
    except Exception as exc:
        return Response({"detail": f"Failed to score batch: {exc}"}, status=500)


# ---------------------------------------------------------------------------
# Score persistence
# ---------------------------------------------------------------------------

def _persist_scores(scored_items, config):
    from django.db import connection, transaction
    closure_prop = config["prop_ids"].get(DEAL_PROP_CLOSURE_SCORE)
    churn_prop = config["prop_ids"].get(DEAL_PROP_CHURN_SCORE)
    if not closure_prop and not churn_prop:
        return
    with transaction.atomic():
        with connection.cursor() as cursor:
            for item in scored_items:
                entity_id = item.get("entity_id")
                if entity_id is None or item.get("unavailable_reason") is not None:
                    continue
                closure = item.get("closure_score")
                churn = item.get("churn_score")
                if closure_prop and closure is not None:
                    _upsert_property(cursor, entity_id, closure_prop, {"value_decimal": closure})
                if churn_prop and churn is not None:
                    _upsert_property(cursor, entity_id, churn_prop, {"value_decimal": churn})


# ---------------------------------------------------------------------------
# Score / diagnose helpers
# ---------------------------------------------------------------------------

_ALLOWED_STATUSES_HUMAN = "opened, pending, closed, or revoked"


def _score_or_diagnose(analysis, deal_row, contracts):
    """Return the score dict for a single deal, or a structured 'why missing' reason."""
    reason = _diagnose_missing_inputs(analysis, deal_row, contracts)
    if reason is not None:
        return {"closure_score": None, "churn_score": None, "unavailable_reason": reason}

    days_until_close = analysis.get(ANALYSIS_PROP_DAYS_UNTIL_CLOSE)
    days_until_close = float(days_until_close) if days_until_close is not None else float(DAYS_UNTIL_CLOSE_MEDIAN)
    hist_close_rate = analysis.get(ANALYSIS_PROP_HIST_CLOSE_RATE)
    hist_close_rate = float(hist_close_rate) if hist_close_rate is not None else float(HIST_CLOSE_RATE_MEDIAN)

    closure_input = [[
        float(analysis[ANALYSIS_PROP_AVG_DEAL_VALUE]),
        int(analysis[ANALYSIS_PROP_DAYS_SINCE_CREATED]),
        int(analysis[ANALYSIS_PROP_STAGE_ENCODED]),
        int(analysis[ANALYSIS_PROP_NUM_INTERACTIONS]),
        days_until_close,
    ]]
    churn_input = [[
        int(analysis[ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT]),
        int(analysis[ANALYSIS_PROP_NUM_OPEN_DEALS]),
        float(analysis[ANALYSIS_PROP_AVG_DEAL_VALUE]),
        hist_close_rate,
    ]]
    closure_score = float(MlApiConfig.closure_model.predict_proba(closure_input)[0][1]) * 100.0
    churn_score = float(MlApiConfig.churn_model.predict_proba(churn_input)[0][1]) * 100.0
    return {
        "closure_score": round(closure_score, 4),
        "churn_score": round(churn_score, 4),
        "unavailable_reason": None,
    }


def _diagnose_missing_inputs(analysis, deal_row, contracts):
    """Inspect the deal/contracts/analysis inputs and return a user-facing reason
    when scoring is impossible. Returns None when every model input is present.
    Order matters — we report the first blocker encountered, walking from the
    deal upward toward the model features so the user sees the most actionable
    fix first."""
    if analysis is None:
        return "Scores are not available yet — analysis has not been computed for this deal."

    created_at = deal_row.get(DEAL_PROP_CREATED_AT)
    if created_at is None:
        return "Deal is missing a created date, which is required for scoring."

    raw_status = deal_row.get(DEAL_PROP_STATUS)
    status = (raw_status or "").lower()
    if not status:
        return f"Deal is missing a status — set it to {_ALLOWED_STATUSES_HUMAN} to enable scoring."
    if status not in DEAL_STATUS_TO_STAGE:
        return (
            f"Deal status '{raw_status}' is not recognised — "
            f"set it to {_ALLOWED_STATUSES_HUMAN} to enable scoring."
        )

    missing_features = [k for k in REQUIRED_FEATURE_KEYS if analysis.get(k) is None]
    if not missing_features:
        return None

    if ANALYSIS_PROP_AVG_DEAL_VALUE in missing_features:
        if not contracts and (deal_row.get("deal_value") in (None, 0)):
            return (
                "Cannot score: deal has no linked contract and no deal value "
                "to fall back on."
            )
        if contracts and all(c.get(CONTRACT_PROP_AMOUNT) in (None, 0) for c in contracts):
            return "Linked contract is missing an amount."

    return "One or more model inputs are missing; try refreshing the analysis."


def _extract_user_id(request):
    header_value = request.headers.get("X-User-Id")
    if header_value is None:
        return 0
    try:
        return int(header_value)
    except (TypeError, ValueError):
        return 0
