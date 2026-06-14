-- ============================================================
-- Script: 62_search_deal_by_title.sql
-- Purpose: Search for deal entities by title using a case-insensitive
--          partial match. Also shows deal status and stage.
-- Tables:  entity, entity_type, entity_property_value, property,
--          entity_workspace, workspaces
-- Usage:   Replace :search_term with a partial deal title.
--          In psql: \set search_term 'enterprise'
-- Notes:   Returns both active and archived deals.
-- ============================================================

SELECT
    e.id AS entity_id,
    MAX(CASE WHEN p.name = 'title'      THEN epv.value_string END)  AS title,
    MAX(CASE WHEN p.name = 'status'     THEN epv.value_string END)  AS status,
    MAX(CASE WHEN p.name = 'deal_stage' THEN epv.value_string END)  AS deal_stage,
    MAX(CASE WHEN p.name = 'deal_value' THEN epv.value_decimal::text END) AS deal_value,
    e.is_archived,
    STRING_AGG(DISTINCT w.name, ', ' ORDER BY w.name)               AS workspaces
FROM entity e
JOIN entity_type           et  ON et.id = e.entity_type_id AND et.name = 'deal'
JOIN entity_property_value epv ON epv.entity_id  = e.id
JOIN property              p   ON p.id           = epv.property_id
LEFT JOIN entity_workspace ew  ON ew.entity_id   = e.id
LEFT JOIN workspaces       w   ON w.id           = ew.workspace_id
WHERE e.id IN (
    -- Subquery finds entity IDs where title matches the search term
    SELECT epv2.entity_id
    FROM entity_property_value epv2
    JOIN property p2 ON p2.id = epv2.property_id AND p2.name = 'title'
    WHERE epv2.value_string ILIKE '%' || :'search_term' || '%'
)
GROUP BY e.id, e.is_archived
ORDER BY title;
