-- ============================================================
-- Script: 56_entity_relationship_types.sql
-- Purpose: Reference card for all defined relationship types —
--          what entity types they connect, cardinality, and whether
--          the relationship is required on the source side.
-- Tables:  entity_relationship_type, entity_type (x2)
-- Usage:   Run as-is. No parameters required.
-- Notes:   Cardinality values:
--            many_to_one   (0) — many sources point to one target
--            one_to_many   (1) — one source has many targets
--            one_to_one    (2) — exactly one-to-one
--            many_to_many  (3) — junction table semantics
--          is_required=TRUE means the source entity cannot exist
--          without this relationship (e.g. contract must have a deal).
-- ============================================================

SELECT
    ert.id,
    ert.name,
    ert.display_name,
    src.name                    AS source_entity_type,
    tgt.name                    AS target_entity_type,
    ert.relationship_cardinality,
    ert.is_required
FROM entity_relationship_type ert
JOIN entity_type src ON src.id = ert.source_entity_type_id
JOIN entity_type tgt ON tgt.id = ert.target_entity_type_id
ORDER BY src.name, ert.name;
