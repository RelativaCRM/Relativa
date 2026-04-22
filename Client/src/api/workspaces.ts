import { api } from '@/api/http';
import type { MyWorkspaceInvitationDto } from '@/api/organizations';

/* ── DTOs ───────────────────────────────────────────────── */

export interface WorkspaceDto {
  id: number;
  name: string;
  memberCount: number;
  userRole: string | null;
}

export interface WorkspaceMemberDto {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  roleName: string;
  joinedAt: string;
}

export interface WorkspacePermissionDto {
  id: number;
  name: string;
}

export interface WorkspaceRoleDto {
  id: number;
  name: string;
  isSystem: boolean;
  permissions: WorkspacePermissionDto[];
}

export type WorkspaceInvitationDto = MyWorkspaceInvitationDto;

/* ── API ────────────────────────────────────────────────── */

const CORE = '/core/api/v1';

export const workspaceApi = {
  /* Workspaces */
  list(): Promise<WorkspaceDto[]> {
    return api.get<WorkspaceDto[]>(`${CORE}/workspaces`);
  },
  create(name: string, organizationId: number): Promise<WorkspaceDto> {
    return api.post<WorkspaceDto>(`${CORE}/workspaces`, {
      name,
      organizationId,
    });
  },
  getById(id: number): Promise<WorkspaceDto> {
    return api.get<WorkspaceDto>(`${CORE}/workspaces/${id}`);
  },
  update(id: number, name: string): Promise<void> {
    return api.put(`${CORE}/workspaces/${id}`, { name });
  },
  archive(id: number): Promise<void> {
    return api.del(`${CORE}/workspaces/${id}`);
  },

  /* Members */
  listMembers(wsId: number): Promise<WorkspaceMemberDto[]> {
    return api.get<WorkspaceMemberDto[]>(
      `${CORE}/workspaces/${wsId}/members`,
    );
  },
  addMember(
    wsId: number,
    userId: number,
    roleId: number,
  ): Promise<WorkspaceMemberDto> {
    return api.post<WorkspaceMemberDto>(
      `${CORE}/workspaces/${wsId}/members`,
      { userId, roleId },
    );
  },
  changeMemberRole(
    wsId: number,
    userId: number,
    roleId: number,
  ): Promise<void> {
    return api.put(`${CORE}/workspaces/${wsId}/members/${userId}/role`, {
      roleId,
    });
  },
  removeMember(wsId: number, userId: number): Promise<void> {
    return api.del(`${CORE}/workspaces/${wsId}/members/${userId}`);
  },

  /* Roles */
  listRoles(wsId: number): Promise<WorkspaceRoleDto[]> {
    return api.get<WorkspaceRoleDto[]>(`${CORE}/workspaces/${wsId}/roles`);
  },

  /* Invitations */
  listInvitations(wsId: number): Promise<WorkspaceInvitationDto[]> {
    return api.get<WorkspaceInvitationDto[]>(
      `${CORE}/workspaces/${wsId}/invitations`,
    );
  },
  invite(
    wsId: number,
    email: string,
    roleId: number,
  ): Promise<WorkspaceInvitationDto> {
    return api.post<WorkspaceInvitationDto>(
      `${CORE}/workspaces/${wsId}/invitations`,
      { email, roleId },
    );
  },
  cancelInvitation(wsId: number, invId: number): Promise<void> {
    return api.del(`${CORE}/workspaces/${wsId}/invitations/${invId}`);
  },
};
