import { api } from '@/api/http';

export type GraphNodeType = 'user_self' | 'user' | 'workspace' | 'entity';
export type GraphEdgeType = 'user_workspace' | 'workspace_entity' | 'entity_entity' | 'user_user';
export type GraphResourceType = 'user' | 'workspace' | 'entity';
export type GraphHighlightTag = 'best_deal' | 'worst_deal' | 'best_client' | 'worst_client';

export interface GraphNodeDto {
  id: string;
  type: GraphNodeType;
  label: string;
  subtitle?: string;
  entityTypeName?: string;
  resourceId: number;
  resourceType: GraphResourceType;
  workspaceId?: number;
  permissions: string[];
  highlightTag?: GraphHighlightTag;
}

export interface GraphEdgeDto {
  id: string;
  from: string;
  to: string;
  type: GraphEdgeType;
  label?: string;
}

export interface GraphResponseDto {
  nodes: GraphNodeDto[];
  edges: GraphEdgeDto[];
}

export const graphApi = {
  getGraph(organizationId: number): Promise<GraphResponseDto> {
    return api.get<GraphResponseDto>(`/graph/api/v1/graph?organizationId=${organizationId}`);
  },
};
