-- ============================================================
-- Script: 61_search_client_by_name.sql
-- Purpose: Search for client entities by company name using a
--          case-insensitive partial match.
-- Tables:  entity, entity_type, entity_property_value, property,
--          entity_workspace, workspaces
-- Usage:   Replace :search_term with a partial company name.
--          In psql: \set search_term 'acme'
-- Notes:   Matches any company_name containing the search term.
--          Both active and archived clients are returned; check
--          is_archived to distinguish them.
--          For exact match replace ILIKE with = LOWER(:search_term).
-- ============================================================

SELECT
    e.id                 AS entity_id,
    epv.value_string     AS company_name,
    e.is_archived,
    STRING_AGG(DISTINCT w.name, ', ' ORDER BY w.name) AS workspaces
FROM entity e
JOIN entity_type            et  ON et.id  = e.entity_type_id  AND et.name = 'client'
JOIN entity_property_value  epv ON epv.entity_id  = e.id
JOIN property               p   ON p.id           = epv.property_id AND p.name = 'company_name'
LEFT JOIN entity_workspace  ew  ON ew.entity_id   = e.id
LEFT JOIN workspaces        w   ON w.id            = ew.workspace_id
WHERE epv.value_string ILIKE '%' || :'search_term' || '%'
GROUP BY e.id, epv.value_string, e.is_archived
ORDER BY epv.value_string;
