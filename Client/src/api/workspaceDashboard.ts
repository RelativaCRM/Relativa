import { api } from '@/api/http';
import type {
  PipelineDto,
  RiskDistributionDto,
  TrendsDto,
  TopEntitiesDto,
} from '@/api/dashboard';

export interface WorkspaceSummaryDto {
  workspaceId: number;
  workspaceName: string;
  totalDeals: number;
  openDeals: number;
  wonDeals: number;
  lostDeals: number;
  totalPipelineValue: number | null;
  winRate: number | null;
  avgDealSize: number | null;
  totalClients: number;
  activeClients: number;
  tasksOverdue: number | null;
  dealsClosingThisMonth: number | null;
  memberCount: number;
  accessLevel: 'full' | 'basic';
}

export interface MemberActivityDto {
  userId: number;
  fullName: string;
  roleName: string;
  dealsOwned: number;
  tasksOwned: number;
  tasksDone: number;
}

const base = (workspaceId: number) =>
  `/graph/api/v1/dashboard/workspace/${workspaceId}`;

export const workspaceDashboardApi = {
  getSummary(workspaceId: number): Promise<WorkspaceSummaryDto> {
    return api.get<WorkspaceSummaryDto>(`${base(workspaceId)}/summary`);
  },
  getPipeline(workspaceId: number): Promise<PipelineDto> {
    return api.get<PipelineDto>(`${base(workspaceId)}/pipeline`);
  },
  getRiskDistribution(workspaceId: number): Promise<RiskDistributionDto> {
    return api.get<RiskDistributionDto>(`${base(workspaceId)}/risk-distribution`);
  },
  getTrends(workspaceId: number): Promise<TrendsDto> {
    return api.get<TrendsDto>(`${base(workspaceId)}/trends`);
  },
  getTopEntities(workspaceId: number): Promise<TopEntitiesDto> {
    return api.get<TopEntitiesDto>(`${base(workspaceId)}/top-entities`);
  },
  getMemberActivity(workspaceId: number): Promise<MemberActivityDto[]> {
    return api.get<MemberActivityDto[]>(`${base(workspaceId)}/member-activity`);
  },
};
