import { api } from '@/api/http';
import type { MyWorkspaceInvitationDto } from '@/api/organizations';

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

export interface WsJoinRequestDto {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  workspaceId: number;
  workspaceName: string;
  message: string | null;
  status: string;
  createdAt: string;
  reviewedByName: string | null;
  reviewedAt: string | null;
}

const CORE = '/core/api/v1';

export const workspaceApi = {
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

  listRoles(wsId: number): Promise<WorkspaceRoleDto[]> {
    return api.get<WorkspaceRoleDto[]>(`${CORE}/workspaces/${wsId}/roles`);
  },

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
  resendInvitation(
    wsId: number,
    invId: number,
  ): Promise<WorkspaceInvitationDto> {
    return api.post<WorkspaceInvitationDto>(
      `${CORE}/workspaces/${wsId}/invitations/${invId}/resend`,
    );
  },

  /* Workspace join requests */
  submitJoinRequest(wsId: number, message: string): Promise<WsJoinRequestDto> {
    return api.post<WsJoinRequestDto>(
      `${CORE}/workspaces/${wsId}/join-requests`,
      { message },
    );
  },
  listJoinRequests(wsId: number): Promise<WsJoinRequestDto[]> {
    return api.get<WsJoinRequestDto[]>(
      `${CORE}/workspaces/${wsId}/join-requests`,
    );
  },
  reviewJoinRequest(
    wsId: number,
    reqId: number,
    decision: 'Approved' | 'Rejected',
  ): Promise<void> {
    return api.put(`${CORE}/workspaces/${wsId}/join-requests/${reqId}`, {
      decision,
    });
  },
  myWorkspaceJoinRequests(): Promise<WsJoinRequestDto[]> {
    return api.get<WsJoinRequestDto[]>(`${CORE}/workspace-join-requests/mine`);
  },
};

export const workspacesApi = workspaceApi;
