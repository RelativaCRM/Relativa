-- ============================================================
-- Script: 36_workspace_entities_summary.sql
-- Purpose: Count of active entities per type in a workspace.
--          Provides a quick snapshot of what data a workspace holds.
-- Tables:  entity_workspace, entity, entity_type
-- Usage:   Replace :workspace_id with the target workspace UUID.
--          In psql: \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Archived entities are excluded by default.
--          To include them, remove the entity.is_archived filter.
--          An entity can belong to multiple workspaces — this count
--          reflects entities *scoped* to this workspace, not global counts.
-- ============================================================

SELECT
    et.name          AS entity_type,
    et.display_name  AS entity_type_display,
    et.is_standalone,
    COUNT(ew.entity_id) AS entity_count
FROM entity_workspace ew
JOIN entity      e  ON e.id  = ew.entity_id
JOIN entity_type et ON et.id = e.entity_type_id
WHERE ew.workspace_id  = :'workspace_id'
  AND e.is_archived    = FALSE
GROUP BY et.name, et.display_name, et.is_standalone
ORDER BY entity_count DESC;
