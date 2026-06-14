-- ============================================================
-- Script: 76_failed_outbox_events.sql
-- Purpose: Show outbox events that have been attempted but failed —
--          i.e. have a last_error recorded. Use for debugging
--          RabbitMQ delivery failures.
-- Tables:  audit_outbox
-- Usage:   Run as-is. No parameters required.
-- Notes:   published_at_utc IS NULL = still not delivered.
--          published_at_utc IS NOT NULL = eventually delivered despite
--          earlier errors (the last_error is a past failure).
--          payload_json contains the full event payload for inspection.
-- ============================================================

SELECT
    id,
    event_id,
    routing_key,
    occurred_at_utc,
    publish_attempts,
    last_error,
    published_at_utc,
    payload_json
FROM audit_outbox
WHERE last_error IS NOT NULL
ORDER BY
    -- Unresolved failures first
    (published_at_utc IS NULL) DESC,
    occurred_at_utc ASC;
