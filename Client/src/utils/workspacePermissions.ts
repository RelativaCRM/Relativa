import type { WorkspaceDto } from '@/api/workspaces';

/** Workspace list/detail from Core includes `myPermissions` (snake-free camelCase). */
export function workspacePermissions(ws: WorkspaceDto | null | undefined): string[] {
  return ws?.myPermissions ?? [];
}

export function hasWorkspacePermission(
  ws: WorkspaceDto | null | undefined,
  permission: string,
): boolean {
  return workspacePermissions(ws).includes(permission);
}
