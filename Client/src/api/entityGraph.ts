import { api } from '@/api/http';
import type { EntityDetailDto } from '@/api/entities';

const GRAPH = '/graph/api/v1';

/**
 * Composite entity creation via Graph → Rabbit → Core (MVP: body matches Core `CreateEntityRequest`).
 */
export const entityGraphApi = {
  create(workspaceId: number, body: unknown): Promise<EntityDetailDto> {
    return api.post<EntityDetailDto>(
      `${GRAPH}/workspaces/${workspaceId}/entity-graph/create`,
      body as Record<string, unknown>,
    );
  },
};
