import { defineStore } from 'pinia';
import { ref } from 'vue';
import {
  dashboardApi,
  type DashboardSummaryDto,
  type PipelineDto,
  type RiskDistributionDto,
  type TrendsDto,
  type TopEntitiesDto,
  type WorkspaceComparisonDto,
} from '@/api/dashboard';
import { normalizeError } from '@/api/errors';

export const useDashboardStore = defineStore('dashboard', () => {
  const summary             = ref<DashboardSummaryDto | null>(null);
  const pipeline            = ref<PipelineDto | null>(null);
  const riskDistribution    = ref<RiskDistributionDto | null>(null);
  const trends              = ref<TrendsDto | null>(null);
  const topEntities         = ref<TopEntitiesDto | null>(null);
  const workspacesComparison = ref<WorkspaceComparisonDto[] | null>(null);

  const isLoading = ref(false);
  const error     = ref<string | null>(null);

  async function fetchAll(organizationId: number): Promise<void> {
    isLoading.value = true;
    error.value     = null;
    try {
      summary.value = await dashboardApi.getSummary(organizationId);

      const accessLevel = summary.value.accessLevel;
      if (accessLevel === 'full_org' || accessLevel === 'full') {
        const [p, r, t, te] = await Promise.allSettled([
          dashboardApi.getPipeline(organizationId),
          dashboardApi.getRiskDistribution(organizationId),
          dashboardApi.getTrends(organizationId),
          dashboardApi.getTopEntities(organizationId),
        ]);
        pipeline.value         = p.status  === 'fulfilled' ? p.value  : null;
        riskDistribution.value = r.status  === 'fulfilled' ? r.value  : null;
        trends.value           = t.status  === 'fulfilled' ? t.value  : null;
        topEntities.value      = te.status === 'fulfilled' ? te.value : null;
      }

      if (accessLevel === 'full_org') {
        const wc = await Promise.allSettled([dashboardApi.getWorkspacesComparison(organizationId)]);
        workspacesComparison.value = wc[0].status === 'fulfilled' ? wc[0].value : null;
      }
    } catch (err) {
      error.value = normalizeError(err, 'Failed to load dashboard data.').message;
    } finally {
      isLoading.value = false;
    }
  }

  function clear(): void {
    summary.value              = null;
    pipeline.value             = null;
    riskDistribution.value     = null;
    trends.value               = null;
    topEntities.value          = null;
    workspacesComparison.value = null;
    error.value                = null;
  }

  return {
    summary, pipeline, riskDistribution, trends, topEntities, workspacesComparison,
    isLoading, error,
    fetchAll, clear,
  };
});
