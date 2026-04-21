import { api } from '@/api/http';

export interface WorkspaceDto {
  id: number;
  name: string;
  memberCount: number;
  userRole: string | null;
}

const CORE_PREFIX = '/core/api/v1/workspaces';

export const workspacesApi = {
  list(): Promise<WorkspaceDto[]> {
    return api.get<WorkspaceDto[]>(CORE_PREFIX);
  },
};
