import { defineStore } from 'pinia';
import { ref } from 'vue';
import {
  workspaceDashboardApi,
  type WorkspaceSummaryDto,
  type MemberActivityDto,
} from '@/api/workspaceDashboard';
import type { PipelineDto, RiskDistributionDto, TrendsDto, TopEntitiesDto } from '@/api/dashboard';
import { normalizeError } from '@/api/errors';

export const useWorkspaceDashboardStore = defineStore('workspaceDashboard', () => {
  const summary          = ref<WorkspaceSummaryDto | null>(null);
  const pipeline         = ref<PipelineDto | null>(null);
  const riskDistribution = ref<RiskDistributionDto | null>(null);
  const trends           = ref<TrendsDto | null>(null);
  const topEntities      = ref<TopEntitiesDto | null>(null);
  const memberActivity   = ref<MemberActivityDto[] | null>(null);

  const isLoading = ref(false);
  const error     = ref<string | null>(null);

  async function fetchAll(workspaceId: number): Promise<void> {
    isLoading.value = true;
    error.value     = null;

    try {
      // Always fetch summary first to determine access level
      summary.value = await workspaceDashboardApi.getSummary(workspaceId);

      if (summary.value.accessLevel === 'full') {
        // Fetch analytics in parallel — ignore 403 on member-activity gracefully
        const [p, r, t, te, ma] = await Promise.allSettled([
          workspaceDashboardApi.getPipeline(workspaceId),
          workspaceDashboardApi.getRiskDistribution(workspaceId),
          workspaceDashboardApi.getTrends(workspaceId),
          workspaceDashboardApi.getTopEntities(workspaceId),
          workspaceDashboardApi.getMemberActivity(workspaceId),
        ]);
        pipeline.value         = p.status         === 'fulfilled' ? p.value         : null;
        riskDistribution.value = r.status         === 'fulfilled' ? r.value         : null;
        trends.value           = t.status         === 'fulfilled' ? t.value         : null;
        topEntities.value      = te.status        === 'fulfilled' ? te.value        : null;
        memberActivity.value   = ma.status        === 'fulfilled' ? ma.value        : null;
      }
    } catch (err) {
      error.value = normalizeError(err, 'Failed to load workspace dashboard.').message;
    } finally {
      isLoading.value = false;
    }
  }

  function clear(): void {
    summary.value          = null;
    pipeline.value         = null;
    riskDistribution.value = null;
    trends.value           = null;
    topEntities.value      = null;
    memberActivity.value   = null;
    error.value            = null;
  }

  return {
    summary, pipeline, riskDistribution, trends, topEntities, memberActivity,
    isLoading, error,
    fetchAll, clear,
  };
});
