import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { HubConnection } from '@microsoft/signalr';
import {
  workspaceDashboardApi,
  type WorkspaceSummaryDto,
  type MemberActivityDto,
} from '@/api/workspaceDashboard';
import type { PipelineDto, RiskDistributionDto, TrendsDto, TopEntitiesDto } from '@/api/dashboard';
import { normalizeError } from '@/api/errors';
import { buildGraphHubConnection } from '@/api/graphHub';

export const useWorkspaceDashboardStore = defineStore('workspaceDashboard', () => {
  const summary          = ref<WorkspaceSummaryDto | null>(null);
  const pipeline         = ref<PipelineDto | null>(null);
  const riskDistribution = ref<RiskDistributionDto | null>(null);
  const trends           = ref<TrendsDto | null>(null);
  const topEntities      = ref<TopEntitiesDto | null>(null);
  const memberActivity   = ref<MemberActivityDto[] | null>(null);

  const isLoadingSummary        = ref(false);
  const isLoadingPipeline       = ref(false);
  const isLoadingRisk           = ref(false);
  const isLoadingTrends         = ref(false);
  const isLoadingTopEntities    = ref(false);
  const isLoadingMemberActivity = ref(false);

  const isMlRecalculating = ref(false);
  const mlRecalcProgress  = ref<{ processedCount: number; totalCount: number } | null>(null);

  const error = ref<string | null>(null);

  let _hubConnection: HubConnection | null = null;
  let _mlHalfRefreshed = false;
  let _mlKillTimer: ReturnType<typeof setTimeout> | null = null;

  function _clearMlKillTimer(): void {
    if (_mlKillTimer !== null) {
      clearTimeout(_mlKillTimer);
      _mlKillTimer = null;
    }
  }

  function _armMlKillTimer(workspaceId: number): void {
    _clearMlKillTimer();
    // Safety net: if no completed event arrives within 5 minutes, poll once and
    // clear the spinner — guards against the race where ML finishes before
    // the SignalR connection is fully negotiated.
    _mlKillTimer = setTimeout(() => {
      _mlKillTimer = null;
      if (!isMlRecalculating.value) return;
      void workspaceDashboardApi.getRiskDistribution(workspaceId)
        .then(v => {
          riskDistribution.value = v;
          isMlRecalculating.value = false;
          mlRecalcProgress.value = null;
        })
        .catch(() => {
          isMlRecalculating.value = false;
          mlRecalcProgress.value = null;
        });
    }, 5 * 60 * 1000);
  }

  async function fetchAll(workspaceId: number): Promise<void> {
    error.value = null;

    // All 6 loading flags up front — every section shows a skeleton immediately.
    isLoadingSummary.value        = true;
    isLoadingPipeline.value       = true;
    isLoadingRisk.value           = true;
    isLoadingTrends.value         = true;
    isLoadingTopEntities.value    = true;
    isLoadingMemberActivity.value = true;

    // Fire all requests at T=0 so the fastest ones populate first.
    const summaryPromise = workspaceDashboardApi.getSummary(workspaceId)
      .then(v  => { summary.value = v; })
      .catch(err => {
        error.value = normalizeError(err, 'Failed to load workspace dashboard.').message;
        throw err;
      })
      .finally(() => { isLoadingSummary.value = false; });

    void workspaceDashboardApi.getPipeline(workspaceId)
      .then(v  => { pipeline.value = v; })
      .catch(() => { pipeline.value = null; })
      .finally(() => { isLoadingPipeline.value = false; });

    void workspaceDashboardApi.getRiskDistribution(workspaceId)
      .then(v => {
        riskDistribution.value = v;
        // Empty items means the backend triggered recalculation — show the spinner immediately
        // without waiting for a SignalR progress event to arrive.
        if (!v.items.length) isMlRecalculating.value = true;
      })
      .catch(() => { riskDistribution.value = null; })
      .finally(() => { isLoadingRisk.value = false; });

    void workspaceDashboardApi.getTrends(workspaceId)
      .then(v  => { trends.value = v; })
      .catch(() => { trends.value = null; })
      .finally(() => { isLoadingTrends.value = false; });

    void workspaceDashboardApi.getTopEntities(workspaceId)
      .then(v  => { topEntities.value = v; })
      .catch(() => { topEntities.value = null; })
      .finally(() => { isLoadingTopEntities.value = false; });

    void workspaceDashboardApi.getMemberActivity(workspaceId)
      .then(v  => { memberActivity.value = v; })
      .catch(() => { memberActivity.value = null; })
      .finally(() => { isLoadingMemberActivity.value = false; });

    try {
      await summaryPromise;
    } catch {
      // Summary failed — remove skeletons for sections that won't render.
      isLoadingPipeline.value = isLoadingRisk.value = isLoadingTrends.value =
        isLoadingTopEntities.value = isLoadingMemberActivity.value = false;
      return;
    }

    // If risk returned empty items but the workspace has no deals, nothing is
    // being calculated — clear the spinner so the view shows the empty state.
    if (isMlRecalculating.value && (summary.value?.totalDeals ?? 0) === 0) {
      isMlRecalculating.value = false;
    }

    // Basic-access workspaces don't render the full-analytics sections,
    // so clear their loading indicators immediately.
    if (summary.value?.accessLevel !== 'full') {
      isLoadingPipeline.value = isLoadingRisk.value = isLoadingTrends.value =
        isLoadingTopEntities.value = isLoadingMemberActivity.value = false;
    }
  }

  async function startMlProgressTracking(workspaceId: number): Promise<void> {
    await stopMlProgressTracking();
    _mlHalfRefreshed = false;

    const conn = buildGraphHubConnection();
    _hubConnection = conn;

    conn.on('ml.recalculate.progress.v1', (msg: { processedCount: number; totalCount: number; workspaceId?: number }) => {
      if (msg.workspaceId !== undefined && msg.workspaceId !== workspaceId) return;
      isMlRecalculating.value = true;
      mlRecalcProgress.value = { processedCount: msg.processedCount, totalCount: msg.totalCount };

      if (!_mlHalfRefreshed && msg.totalCount > 0 && msg.processedCount / msg.totalCount > 0.5) {
        _mlHalfRefreshed = true;
        void workspaceDashboardApi.getRiskDistribution(workspaceId)
          .then(v => { riskDistribution.value = v; })
          .catch(() => {});
      }
    });

    conn.on('ml.recalculate.completed.v1', (msg: { workspaceId?: number }) => {
      if (msg.workspaceId !== undefined && msg.workspaceId !== workspaceId) return;
      _clearMlKillTimer();
      isMlRecalculating.value = false;
      mlRecalcProgress.value = null;
      void workspaceDashboardApi.getRiskDistribution(workspaceId)
        .then(v => { riskDistribution.value = v; })
        .catch(() => {});
      void workspaceDashboardApi.getTopEntities(workspaceId)
        .then(v => { topEntities.value = v; })
        .catch(() => {});
    });

    try {
      await conn.start();
      await conn.invoke('JoinWorkspace', workspaceId);
      // Arm the kill timer now that we're connected.
      _armMlKillTimer(workspaceId);
      // Race-condition guard: ML may have completed while we were negotiating the
      // WebSocket. Poll once to check — if data is present now, clear the spinner.
      if (isMlRecalculating.value) {
        void workspaceDashboardApi.getRiskDistribution(workspaceId)
          .then(v => {
            if (v.items.length) {
              _clearMlKillTimer();
              riskDistribution.value = v;
              isMlRecalculating.value = false;
              mlRecalcProgress.value = null;
            }
          })
          .catch(() => {});
      }
    } catch {
      // SignalR is best-effort — tracking failure is non-fatal
    }
  }

  async function stopMlProgressTracking(): Promise<void> {
    _clearMlKillTimer();
    const conn = _hubConnection;
    _hubConnection = null;
    isMlRecalculating.value = false;
    mlRecalcProgress.value = null;
    if (conn) {
      try { await conn.stop(); } catch { /* ignore */ }
    }
  }

  function clear(): void {
    void stopMlProgressTracking();
    summary.value          = null;
    pipeline.value         = null;
    riskDistribution.value = null;
    trends.value           = null;
    topEntities.value      = null;
    memberActivity.value   = null;
    error.value            = null;
    isLoadingSummary.value        = false;
    isLoadingPipeline.value       = false;
    isLoadingRisk.value           = false;
    isLoadingTrends.value         = false;
    isLoadingTopEntities.value    = false;
    isLoadingMemberActivity.value = false;
  }

  return {
    summary, pipeline, riskDistribution, trends, topEntities, memberActivity,
    isLoadingSummary, isLoadingPipeline, isLoadingRisk,
    isLoadingTrends, isLoadingTopEntities, isLoadingMemberActivity,
    isMlRecalculating, mlRecalcProgress,
    error,
    fetchAll, clear, startMlProgressTracking, stopMlProgressTracking,
  };
});
