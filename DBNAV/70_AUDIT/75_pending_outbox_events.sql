-- ============================================================
-- Script: 75_pending_outbox_events.sql
-- Purpose: Show all audit outbox events that have not yet been
--          published to RabbitMQ. A high count or old timestamps
--          indicate delivery problems.
-- Tables:  audit_outbox
-- Usage:   Run as-is. No parameters required.
-- Notes:   published_at_utc IS NULL = not yet delivered.
--          publish_attempts > 0 with last_error IS NOT NULL means
--          delivery has been attempted but failed — see script
--          76_failed_outbox_events.sql for details.
--          Under normal operation this query should return 0 rows
--          or a very small number of recently created events.
-- ============================================================

SELECT
    id,
    event_id,
    routing_key,
    occurred_at_utc,
    created_at_utc,
    publish_attempts,
    last_error
FROM audit_outbox
WHERE published_at_utc IS NULL
ORDER BY occurred_at_utc ASC;
