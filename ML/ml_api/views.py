import time
from rest_framework.decorators import api_view
from rest_framework.response import Response

from .apps import MlApiConfig
from .recalculate_service import (
    ANALYSIS_PROP_AVG_DEAL_VALUE,
    ANALYSIS_PROP_CALCULATED_AT,
    ANALYSIS_PROP_DAYS_SINCE_CREATED,
    ANALYSIS_PROP_DAYS_SINCE_LAST_CONTACT,
    ANALYSIS_PROP_NUM_INTERACTIONS,
    ANALYSIS_PROP_NUM_OPEN_DEALS,
    ANALYSIS_PROP_SOURCE_UPDATED_AT,
    ANALYSIS_PROP_STAGE_ENCODED,
    BATCH_TIMEOUT_SECONDS,
    FEATURE_KEYS,
    enqueue_recalculation_job,
    normalize_entity_ids,
    recompute_deal_analysis,
    _check_deadline,
    _ensure_deal_analysis_entities,
    _load_analysis_state,
    _load_contract_inputs,
    _load_deal_inputs,
    _load_schema_config,
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
        _check_deadline(deadline)

        results_by_id = {}
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
            recompute_deal_analysis(stale_analysis_ids, deadline=deadline)
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


def _extract_user_id(request):
    header_value = request.headers.get("X-User-Id")
    if header_value is None:
        return 0
    try:
        return int(header_value)
    except (TypeError, ValueError):
        return 0
