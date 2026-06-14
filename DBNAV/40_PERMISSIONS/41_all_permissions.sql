-- ============================================================
-- Script: 41_all_permissions.sql
-- Purpose: List every permission in the system as a reference card.
-- Tables:  permissions
-- Usage:   Run as-is. No parameters required.
-- Notes:   Permissions with names starting with manage_org_ / invite_to_org /
--          remove_org_ / assign_org_ / create_workspaces are organization-
--          scoped. Those starting with manage_ws_ / add_ws_ / remove_ws_ /
--          assign_ws_ / view_ / create_entities / edit_entities /
--          delete_entities / delete_workspace are workspace-scoped.
-- ============================================================

SELECT
    id,
    name,
    display_name,
    is_archived
FROM permissions
ORDER BY name;
