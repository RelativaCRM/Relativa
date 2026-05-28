import { api } from '@/api/http';

export interface WorkspaceDto {
  id: number;
  organizationId: number;
  name: string;
  memberCount: number;
  userRole: string | null;
  /** Effective permission names for the current user in this workspace (e.g. `create_entities`). */
  myPermissions?: string[];
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

export interface WorkspaceSettingsDto {
  workspaceId: number;
  description: string | null;
  highRiskThreshold: number;
  mediumRiskThreshold: number;
  riskScoringEnabled: boolean;
}

export interface UpdateWorkspaceSettingsRequest {
  description?: string | null;
  highRiskThreshold: number;
  mediumRiskThreshold: number;
  riskScoringEnabled: boolean;
}

const CORE = '/core/api/v1';

export const workspaceApi = {
  list(organizationId?: number): Promise<WorkspaceDto[]> {
    const q =
      organizationId !== undefined && organizationId !== null
        ? `?organizationId=${encodeURIComponent(String(organizationId))}`
        : '';
    return api.get<WorkspaceDto[]>(`${CORE}/workspaces${q}`);
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

  getSettings(wsId: number): Promise<WorkspaceSettingsDto> {
    return api.get<WorkspaceSettingsDto>(`${CORE}/workspaces/${wsId}/settings`);
  },
  updateSettings(wsId: number, data: UpdateWorkspaceSettingsRequest): Promise<void> {
    return api.put(`${CORE}/workspaces/${wsId}/settings`, data);
  },
};

export const workspacesApi = workspaceApi;
