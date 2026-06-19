-- ============================================================
-- Script: 55_entity_relationships.sql
-- Purpose: Show all relationships of a given entity — both outgoing
--          (this entity is the source) and incoming (this entity is
--          the target).
-- Tables:  entity_relationship, entity_relationship_type, entity,
--          entity_type
-- Usage:   Replace :entity_id with the target entity UUID.
--          In psql: \set entity_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   CASCADE DELETE applies to the source side — deleting the
--          source entity will cascade and remove the relationship row.
--          RESTRICT applies to the target side — you cannot delete
--          a target entity while relationships point to it.
-- ============================================================

-- Outgoing (this entity is the source of the relationship)
SELECT
    'outgoing'           AS direction,
    ert.name             AS relationship_type,
    ert.display_name     AS relationship_display,
    ert.relationship_cardinality,
    er.target_entity_id  AS related_entity_id,
    et_target.name       AS related_entity_type,
    e_target.is_archived AS related_entity_archived
FROM entity_relationship er
JOIN entity_relationship_type ert     ON ert.id = er.relationship_type_id
JOIN entity                   e_target ON e_target.id = er.target_entity_id
JOIN entity_type              et_target ON et_target.id = e_target.entity_type_id
WHERE er.source_entity_id = :'entity_id'

UNION ALL

-- Incoming (this entity is the target of the relationship)
SELECT
    'incoming'           AS direction,
    ert.name             AS relationship_type,
    ert.display_name     AS relationship_display,
    ert.relationship_cardinality,
    er.source_entity_id  AS related_entity_id,
    et_source.name       AS related_entity_type,
    e_source.is_archived AS related_entity_archived
FROM entity_relationship er
JOIN entity_relationship_type ert     ON ert.id = er.relationship_type_id
JOIN entity                   e_source ON e_source.id = er.source_entity_id
JOIN entity_type              et_source ON et_source.id = e_source.entity_type_id
WHERE er.target_entity_id = :'entity_id'

ORDER BY direction, relationship_type;
