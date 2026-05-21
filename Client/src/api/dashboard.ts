import { api } from '@/api/http';

export interface DashboardSummaryDto {
  totalDeals: number;
  openDeals: number;
  totalDealValue: number;
  wonDeals: number;
  lostDeals: number;
  winRate: number;
  avgDealSize: number;
  totalClients: number;
  activeClients: number;
  dealsClosingThisMonth: number;
  tasksOverdue: number;
  totalWorkspaces: number;
  activeWorkspaces: number;
  accessLevel: 'full_org' | 'full' | 'basic';
}

export interface WorkspaceComparisonDto {
  workspaceId: number;
  workspaceName: string;
  dealCount: number;
  pipelineValue: number;
  winRate: number;
  clientCount: number;
  memberCount: number;
  topStage: string;
}

export interface PipelineStageDto {
  name: string;
  count: number;
  value: number;
  percentage: number;
}

export interface PipelineDto {
  stages: PipelineStageDto[];
  statusBreakdown: Record<string, number>;
  conversionRate: number;
  avgDaysToClose: number;
}

export interface RiskBucketDto {
  count: number;
  totalValue: number;
  percentage: number;
}

export interface RiskItemDto {
  entityId: number;
  title: string;
  score: number;
  value: number;
  riskBucket: string;
  clientName?: string;
}

export interface RiskDistributionDto {
  distribution: Record<'high' | 'medium' | 'low', RiskBucketDto>;
  items: RiskItemDto[];
}

export interface TrendsMonthDto {
  label: string;
  newDeals: number;
  closedWon: number;
  closedLost: number;
  wonRevenue: number;
  activeValue: number;
}

export interface TrendsDto {
  months: TrendsMonthDto[];
}

export interface TopDealDto {
  entityId: number;
  title: string;
  value: number;
  stage?: string;
  closureScore?: number;
  clientName?: string;
  priority?: string;
}

export interface TopClientDto {
  entityId: number;
  name: string;
  industry?: string;
  lifetimeValue: number;
  activeDeals: number;
  avgClosureScore?: number;
}

export interface TopEntitiesDto {
  topDeals: TopDealDto[];
  topClients: TopClientDto[];
}

const base = (organizationId: number) => `/graph/api/v1/dashboard?organizationId=${organizationId}`;

export const dashboardApi = {
  getSummary(organizationId: number): Promise<DashboardSummaryDto> {
    return api.get<DashboardSummaryDto>(`/graph/api/v1/dashboard/summary?organizationId=${organizationId}`);
  },
  getPipeline(organizationId: number): Promise<PipelineDto> {
    return api.get<PipelineDto>(`/graph/api/v1/dashboard/pipeline?organizationId=${organizationId}`);
  },
  getRiskDistribution(organizationId: number): Promise<RiskDistributionDto> {
    return api.get<RiskDistributionDto>(`/graph/api/v1/dashboard/risk-distribution?organizationId=${organizationId}`);
  },
  getTrends(organizationId: number): Promise<TrendsDto> {
    return api.get<TrendsDto>(`/graph/api/v1/dashboard/trends?organizationId=${organizationId}`);
  },
  getTopEntities(organizationId: number): Promise<TopEntitiesDto> {
    return api.get<TopEntitiesDto>(`/graph/api/v1/dashboard/top-entities?organizationId=${organizationId}`);
  },
  getWorkspacesComparison(organizationId: number): Promise<WorkspaceComparisonDto[]> {
    return api.get<WorkspaceComparisonDto[]>(`/graph/api/v1/dashboard/workspaces-comparison?organizationId=${organizationId}`);
  },
};
