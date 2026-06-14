-- ============================================================
-- Script: 53_entities_by_type.sql
-- Purpose: List entities of a given type with key identifying
--          property values pivoted into columns, plus workspace
--          membership info.
-- Tables:  entity, entity_type, entity_property_value, property,
--          entity_workspace, workspaces
-- Usage:   Replace :entity_type_name with the desired type.
--          In psql: \set entity_type_name 'client'
--          Valid values: client, deal, contact, task, note,
--                        contract, deal_analysis
-- Notes:   This query pivots only a subset of commonly useful
--          properties. For the complete set of property values for
--          a single entity use script 54_entity_by_id_full.sql.
--          Archived entities are excluded by default.
-- ============================================================

SELECT
    e.id                                                        AS entity_id,
    et.name                                                     AS entity_type,
    e.is_archived,
    -- Pivoted key properties (value depends on entity type):
    MAX(CASE WHEN p.name = 'title'        THEN epv.value_string END) AS title,
    MAX(CASE WHEN p.name = 'company_name' THEN epv.value_string END) AS company_name,
    MAX(CASE WHEN p.name = 'first_name'   THEN epv.value_string END) AS first_name,
    MAX(CASE WHEN p.name = 'last_name'    THEN epv.value_string END) AS last_name,
    MAX(CASE WHEN p.name = 'email'        THEN epv.value_string END) AS email,
    MAX(CASE WHEN p.name = 'status'       THEN epv.value_string END) AS status,
    MAX(CASE WHEN p.name = 'deal_stage'   THEN epv.value_string END) AS deal_stage,
    MAX(CASE WHEN p.name = 'task_status'  THEN epv.value_string END) AS task_status,
    MAX(CASE WHEN p.name = 'task_title'   THEN epv.value_string END) AS task_title,
    MAX(CASE WHEN p.name = 'deal_value'   THEN epv.value_decimal::text END) AS deal_value,
    -- Workspaces this entity belongs to:
    STRING_AGG(DISTINCT w.name, ', ' ORDER BY w.name)          AS workspaces,
    u.email                                                     AS created_by_email
FROM entity e
JOIN entity_type et ON et.id = e.entity_type_id
LEFT JOIN entity_property_value epv ON epv.entity_id   = e.id
LEFT JOIN property              p   ON p.id            = epv.property_id
LEFT JOIN entity_workspace      ew  ON ew.entity_id    = e.id
LEFT JOIN workspaces            w   ON w.id            = ew.workspace_id
LEFT JOIN users                 u   ON u.id            = e.created_by_user_id
WHERE et.name     = :'entity_type_name'
  AND e.is_archived = FALSE
GROUP BY e.id, et.name, e.is_archived, u.email
ORDER BY e.id;
