<script setup lang="ts">
import { computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import Chart from 'primevue/chart';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';
import ProgressBar from 'primevue/progressbar';
import { useWorkspaceDashboardStore } from '@/stores/workspaceDashboard';
import { useWorkspaceStore } from '@/stores/workspace';

const route   = useRoute();
const wsStore = useWorkspaceStore();
const store   = useWorkspaceDashboardStore();

const workspaceId = computed(() => Number(route.params.workspaceId));

onMounted(() => {
  if (workspaceId.value) store.fetchAll(workspaceId.value);
});

onUnmounted(() => store.clear());

watch(workspaceId, (id) => {
  if (id) store.fetchAll(id);
});

const isFullAccess  = computed(() => store.summary?.accessLevel === 'full');
const hasMemberData = computed(() => store.memberActivity !== null);

// ── KPI cards ────────────────────────────────────────────────────────────────
const kpis = computed(() => {
  const s = store.summary;
  if (!s) return [];

  const cards: Array<{ label: string; value: string; icon: string; color: string; bg: string }> = [
    {
      label: 'Total Deals',
      value: String(s.totalDeals),
      icon: 'pi-briefcase',
      color: 'text-blue-600',
      bg: 'bg-blue-50',
    },
    {
      label: 'Open Deals',
      value: String(s.openDeals),
      icon: 'pi-clock',
      color: 'text-emerald-600',
      bg: 'bg-emerald-50',
    },
    {
      label: 'Won',
      value: String(s.wonDeals),
      icon: 'pi-check-circle',
      color: 'text-green-600',
      bg: 'bg-green-50',
    },
    {
      label: 'Clients',
      value: `${s.activeClients} / ${s.totalClients}`,
      icon: 'pi-building',
      color: 'text-amber-600',
      bg: 'bg-amber-50',
    },
    {
      label: 'Members',
      value: String(s.memberCount),
      icon: 'pi-users',
      color: 'text-violet-600',
      bg: 'bg-violet-50',
    },
  ];

  if (s.totalPipelineValue !== null) {
    cards.unshift({
      label: 'Pipeline Value',
      value: formatCurrency(s.totalPipelineValue),
      icon: 'pi-euro',
      color: 'text-sky-600',
      bg: 'bg-sky-50',
    });
  }
  if (s.winRate !== null) {
    cards.push({
      label: 'Win Rate',
      value: `${(s.winRate * 100).toFixed(1)}%`,
      icon: 'pi-chart-line',
      color: 'text-teal-600',
      bg: 'bg-teal-50',
    });
  }
  if (s.tasksOverdue !== null) {
    cards.push({
      label: 'Overdue Tasks',
      value: String(s.tasksOverdue),
      icon: 'pi-exclamation-triangle',
      color: s.tasksOverdue > 0 ? 'text-red-600' : 'text-slate-500',
      bg: s.tasksOverdue > 0 ? 'bg-red-50' : 'bg-slate-50',
    });
  }
  if (s.dealsClosingThisMonth !== null) {
    cards.push({
      label: 'Closing This Month',
      value: String(s.dealsClosingThisMonth),
      icon: 'pi-calendar-clock',
      color: 'text-orange-600',
      bg: 'bg-orange-50',
    });
  }

  return cards;
});

// ── Basic status bar chart (for basic access) ─────────────────────────────────
const basicStatusChartData = computed(() => {
  const s = store.summary;
  if (!s) return null;
  return {
    labels: ['Open', 'Won', 'Lost'],
    datasets: [{
      data: [s.openDeals, s.wonDeals, s.lostDeals],
      backgroundColor: ['#3b82f6', '#10b981', '#ef4444'],
      borderRadius: 4,
    }],
  };
});

const basicChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: {
    x: { grid: { display: false }, ticks: { color: '#64748b' } },
    y: { grid: { color: '#f1f5f9' }, ticks: { color: '#64748b', stepSize: 1 } },
  },
};

// ── Pipeline chart ────────────────────────────────────────────────────────────
const pipelineChartData = computed(() => {
  const p = store.pipeline;
  if (!p) return null;
  return {
    labels: p.stages.map((s) => s.name),
    datasets: [{
      label: 'Deals',
      data: p.stages.map((s) => s.count),
      backgroundColor: ['#3b82f6', '#8b5cf6', '#f59e0b', '#10b981'],
      borderRadius: 4,
    }],
  };
});

const pipelineChartOptions = {
  indexAxis: 'y' as const,
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: {
    x: { grid: { color: '#f1f5f9' }, ticks: { color: '#64748b' } },
    y: { grid: { display: false }, ticks: { color: '#334155' } },
  },
};

// ── Risk doughnut ─────────────────────────────────────────────────────────────
const riskChartData = computed(() => {
  const r = store.riskDistribution;
  if (!r) return null;
  return {
    labels: ['High Risk', 'Medium Risk', 'Low Risk'],
    datasets: [{
      data: [
        r.distribution.high?.count ?? 0,
        r.distribution.medium?.count ?? 0,
        r.distribution.low?.count ?? 0,
      ],
      backgroundColor: ['#ef4444', '#f59e0b', '#10b981'],
      borderWidth: 0,
    }],
  };
});

const riskChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  cutout: '65%',
  plugins: {
    legend: { position: 'bottom' as const, labels: { color: '#334155', padding: 12 } },
  },
};

// ── Trends line ───────────────────────────────────────────────────────────────
const trendsChartData = computed(() => {
  const t = store.trends;
  if (!t) return null;
  return {
    labels: t.months.map((m) => m.label),
    datasets: [
      {
        label: 'Pipeline Deals',
        data: t.months.map((m) => m.newDeals),
        borderColor: '#3b82f6',
        backgroundColor: 'rgba(59,130,246,0.08)',
        fill: true,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: 'Won',
        data: t.months.map((m) => m.closedWon),
        borderColor: '#10b981',
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: 'Lost',
        data: t.months.map((m) => m.closedLost),
        borderColor: '#ef4444',
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: 'Won Revenue (€)',
        data: t.months.map((m) => m.wonRevenue),
        borderColor: '#8b5cf6',
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.3,
        yAxisID: 'y1',
        borderDash: [4, 4],
      },
    ],
  };
});

const trendsChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: { position: 'top' as const, labels: { color: '#334155', boxWidth: 12 } },
  },
  scales: {
    y: {
      type: 'linear' as const,
      position: 'left' as const,
      grid: { color: '#f1f5f9' },
      ticks: { color: '#64748b', stepSize: 1 },
      title: { display: true, text: 'Deals', color: '#94a3b8' },
    },
    y1: {
      type: 'linear' as const,
      position: 'right' as const,
      grid: { drawOnChartArea: false },
      ticks: {
        color: '#8b5cf6',
        callback: (v: number) => `€${(v / 1000).toFixed(0)}k`,
      },
      title: { display: true, text: 'Revenue', color: '#8b5cf6' },
    },
    x: { grid: { display: false }, ticks: { color: '#64748b' } },
  },
};

// ── Helpers ───────────────────────────────────────────────────────────────────
function formatCurrency(v: number) {
  return new Intl.NumberFormat('de-DE', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 0,
  }).format(v);
}

function priorityClass(priority?: string) {
  switch (priority?.toLowerCase()) {
    case 'high':   return 'danger';
    case 'medium': return 'warn';
    default:       return 'secondary';
  }
}

function riskClass(bucket: string) {
  switch (bucket) {
    case 'high':   return 'danger';
    case 'medium': return 'warn';
    case 'low':    return 'success';
    default:       return 'secondary';
  }
}

function scoreBar(score?: number | null) {
  return score != null ? Math.round(score * 100) : null;
}
</script>

<template>
  <div class="space-y-8">

    <!-- Header -->
    <section>
      <h1 class="text-xl font-semibold text-ink-900">
        {{ store.summary?.workspaceName ?? wsStore.currentWorkspace?.name ?? 'Workspace' }}
      </h1>
      <p class="text-sm text-ink-400 mt-0.5">Dashboard overview</p>
    </section>

    <!-- Loading skeleton -->
    <div v-if="store.isLoading" class="space-y-4">
      <div class="grid grid-cols-2 md:grid-cols-4 xl:grid-cols-6 gap-4">
        <div v-for="i in 6" :key="i" class="h-24 rounded-xl bg-slate-100 animate-pulse" />
      </div>
      <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <div class="h-52 rounded-xl bg-slate-100 animate-pulse" />
        <div class="h-52 rounded-xl bg-slate-100 animate-pulse" />
      </div>
    </div>

    <template v-else>
      <!-- Error banner -->
      <div
        v-if="store.error"
        class="flex items-center gap-3 px-4 py-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700"
      >
        <i class="pi pi-exclamation-circle shrink-0" />
        {{ store.error }}
      </div>

      <template v-if="store.summary">

        <!-- ── KPI row ── -->
        <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
          <div
            v-for="kpi in kpis"
            :key="kpi.label"
            class="flex flex-col gap-2 bg-white rounded-xl border border-line px-4 py-4 shadow-sm"
          >
            <div :class="['w-8 h-8 rounded-lg flex items-center justify-center shrink-0', kpi.bg]">
              <i :class="['pi text-sm', kpi.icon, kpi.color]" />
            </div>
            <div>
              <p class="text-[11px] text-ink-400 font-medium uppercase tracking-wide leading-none mb-1">{{ kpi.label }}</p>
              <p :class="['text-xl font-semibold leading-none', kpi.color]">{{ kpi.value }}</p>
            </div>
          </div>
        </div>

        <!-- ── Basic access notice + status chart ── -->
        <template v-if="!isFullAccess">
          <div class="bg-amber-50 border border-amber-200 rounded-xl px-5 py-4 flex items-center gap-3 text-sm text-amber-800">
            <i class="pi pi-lock shrink-0" />
            <span>You have limited analytics access. Contact your workspace admin to see full analytics.</span>
          </div>

          <div v-if="basicStatusChartData" class="bg-white rounded-xl border border-line shadow-sm p-5">
            <h3 class="text-sm font-semibold text-ink-700 mb-4">Deal Status Distribution</h3>
            <div style="height: 160px">
              <Chart type="bar" :data="basicStatusChartData" :options="basicChartOptions" />
            </div>
          </div>
        </template>

        <!-- ── Full analytics sections ── -->
        <template v-if="isFullAccess">

          <!-- Refresh button -->
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-base font-semibold text-ink-900">Analytics</h2>
              <p class="text-sm text-ink-400 mt-0.5">Workspace-level CRM metrics</p>
            </div>
            <button
              v-if="!store.isLoading"
              type="button"
              class="flex items-center gap-1.5 text-sm text-ink-500 hover:text-brand-600 px-3 py-1.5 rounded-lg hover:bg-brand-50 transition-colors"
              @click="workspaceId && store.fetchAll(workspaceId)"
            >
              <i class="pi pi-refresh text-xs" />
              Refresh
            </button>
          </div>

          <!-- ── Pipeline + Risk ── -->
          <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

            <div class="bg-white rounded-xl border border-line shadow-sm p-5">
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-sm font-semibold text-ink-700">Deal Pipeline</h3>
                <div v-if="store.pipeline" class="flex items-center gap-3 text-xs text-ink-400">
                  <span>Win rate <strong class="text-emerald-600">{{ (store.pipeline.conversionRate * 100).toFixed(0) }}%</strong></span>
                  <span>Avg close <strong class="text-ink-700">{{ store.pipeline.avgDaysToClose }}d</strong></span>
                </div>
              </div>
              <div v-if="pipelineChartData" style="height: 180px">
                <Chart type="bar" :data="pipelineChartData" :options="pipelineChartOptions" />
              </div>
              <div v-if="store.pipeline" class="mt-4 grid grid-cols-4 gap-2 border-t border-line pt-3">
                <div
                  v-for="(count, status) in store.pipeline.statusBreakdown"
                  :key="status"
                  class="text-center"
                >
                  <p class="text-lg font-semibold text-ink-800">{{ count }}</p>
                  <p class="text-[10px] uppercase tracking-wide text-ink-400">{{ status }}</p>
                </div>
              </div>
            </div>

            <div class="bg-white rounded-xl border border-line shadow-sm p-5">
              <h3 class="text-sm font-semibold text-ink-700 mb-4">Risk Distribution (Active Deals)</h3>
              <div v-if="riskChartData" class="flex items-center gap-6">
                <div style="height: 180px; width: 180px; flex-shrink: 0">
                  <Chart type="doughnut" :data="riskChartData" :options="riskChartOptions" />
                </div>
                <div class="flex-1 space-y-2 overflow-auto max-h-48">
                  <div
                    v-for="item in store.riskDistribution?.items.slice(0, 6)"
                    :key="item.entityId"
                    class="flex items-center justify-between text-xs gap-2"
                  >
                    <div class="min-w-0">
                      <p class="truncate text-ink-700 font-medium">{{ item.title }}</p>
                      <p v-if="item.clientName" class="truncate text-ink-400">{{ item.clientName }}</p>
                    </div>
                    <div class="flex items-center gap-2 shrink-0">
                      <Tag :value="item.riskBucket" :severity="riskClass(item.riskBucket)" class="!text-[10px] !px-1.5 !py-0" />
                      <span class="text-ink-500 w-8 text-right">{{ (item.score * 100).toFixed(0) }}%</span>
                    </div>
                  </div>
                  <p v-if="!store.riskDistribution?.items.length" class="text-xs text-ink-400 italic">
                    ML scores unavailable.
                  </p>
                </div>
              </div>
            </div>
          </div>

          <!-- ── Trends ── -->
          <div class="bg-white rounded-xl border border-line shadow-sm p-5">
            <h3 class="text-sm font-semibold text-ink-700 mb-4">6-Month Deal Trends</h3>
            <div v-if="trendsChartData" style="height: 220px">
              <Chart type="line" :data="trendsChartData" :options="trendsChartOptions" />
            </div>
          </div>

          <!-- ── Top entities ── -->
          <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

            <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
              <h3 class="text-sm font-semibold text-ink-700 mb-3">Top Deals by Value</h3>
              <DataTable
                v-if="store.topEntities?.topDeals.length"
                :value="store.topEntities.topDeals"
                size="small"
                class="!text-xs"
                :pt="{ thead: { class: 'hidden' } }"
              >
                <Column field="title" header="Deal">
                  <template #body="{ data }">
                    <div>
                      <p class="font-medium text-ink-800 truncate max-w-[160px]">{{ data.title }}</p>
                      <p v-if="data.clientName" class="text-ink-400 truncate max-w-[160px]">{{ data.clientName }}</p>
                    </div>
                  </template>
                </Column>
                <Column field="value" header="Value">
                  <template #body="{ data }">
                    <span class="font-semibold text-ink-700">{{ formatCurrency(data.value) }}</span>
                  </template>
                </Column>
                <Column field="stage" header="Stage">
                  <template #body="{ data }">
                    <span class="text-ink-500">{{ data.stage ?? '—' }}</span>
                  </template>
                </Column>
                <Column field="priority" header="Priority">
                  <template #body="{ data }">
                    <Tag
                      v-if="data.priority"
                      :value="data.priority"
                      :severity="priorityClass(data.priority)"
                      class="!text-[10px] !px-1.5 !py-0 capitalize"
                    />
                  </template>
                </Column>
                <Column field="closureScore" header="Score">
                  <template #body="{ data }">
                    <div v-if="scoreBar(data.closureScore) != null" class="flex items-center gap-1.5">
                      <ProgressBar
                        :value="scoreBar(data.closureScore)!"
                        :pt="{ value: { class: 'transition-none' } }"
                        class="!h-1.5 !w-16 !bg-slate-100"
                      />
                      <span class="text-ink-500 shrink-0">{{ scoreBar(data.closureScore) }}%</span>
                    </div>
                    <span v-else class="text-ink-300">—</span>
                  </template>
                </Column>
              </DataTable>
              <p v-else class="text-sm text-ink-400 italic">No deal data yet.</p>
            </div>

            <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
              <h3 class="text-sm font-semibold text-ink-700 mb-3">Top Clients by Lifetime Value</h3>
              <DataTable
                v-if="store.topEntities?.topClients.length"
                :value="store.topEntities.topClients"
                size="small"
                class="!text-xs"
                :pt="{ thead: { class: 'hidden' } }"
              >
                <Column field="name" header="Client">
                  <template #body="{ data }">
                    <div>
                      <p class="font-medium text-ink-800 truncate max-w-[160px]">{{ data.name }}</p>
                      <p v-if="data.industry" class="text-ink-400 capitalize">{{ data.industry }}</p>
                    </div>
                  </template>
                </Column>
                <Column field="lifetimeValue" header="LTV">
                  <template #body="{ data }">
                    <span class="font-semibold text-ink-700">{{ formatCurrency(data.lifetimeValue) }}</span>
                  </template>
                </Column>
                <Column field="activeDeals" header="Active">
                  <template #body="{ data }">
                    <Tag :value="`${data.activeDeals} deals`" severity="secondary" class="!text-[10px] !px-1.5 !py-0" />
                  </template>
                </Column>
                <Column field="avgClosureScore" header="Avg Score">
                  <template #body="{ data }">
                    <div v-if="scoreBar(data.avgClosureScore) != null" class="flex items-center gap-1.5">
                      <ProgressBar
                        :value="scoreBar(data.avgClosureScore)!"
                        :pt="{ value: { class: 'transition-none' } }"
                        class="!h-1.5 !w-16 !bg-slate-100"
                      />
                      <span class="text-ink-500 shrink-0">{{ scoreBar(data.avgClosureScore) }}%</span>
                    </div>
                    <span v-else class="text-ink-300">—</span>
                  </template>
                </Column>
              </DataTable>
              <p v-else class="text-sm text-ink-400 italic">No client data yet.</p>
            </div>
          </div>

          <!-- ── Member activity (view_team_analytics) ── -->
          <div v-if="hasMemberData" class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
            <h3 class="text-sm font-semibold text-ink-700 mb-3">Member Activity</h3>
            <DataTable
              :value="store.memberActivity!"
              size="small"
              class="!text-xs"
              :pt="{ thead: { class: '!text-[11px] !uppercase !tracking-wide !text-ink-400' } }"
            >
              <Column field="fullName" header="Member">
                <template #body="{ data }">
                  <div>
                    <p class="font-medium text-ink-800">{{ data.fullName }}</p>
                    <p class="text-ink-400 text-[10px] capitalize">{{ data.roleName.replace('ws_', '') }}</p>
                  </div>
                </template>
              </Column>
              <Column field="dealsOwned" header="Deals Owned">
                <template #body="{ data }">
                  <Tag :value="String(data.dealsOwned)" severity="secondary" class="!text-[10px] !px-1.5 !py-0" />
                </template>
              </Column>
              <Column field="tasksOwned" header="Tasks">
                <template #body="{ data }">
                  <span class="text-ink-700">{{ data.tasksOwned }}</span>
                </template>
              </Column>
              <Column field="tasksDone" header="Done">
                <template #body="{ data }">
                  <span class="text-emerald-600 font-medium">{{ data.tasksDone }}</span>
                </template>
              </Column>
              <Column header="Completion">
                <template #body="{ data }">
                  <div v-if="data.tasksOwned > 0" class="flex items-center gap-1.5">
                    <ProgressBar
                      :value="Math.round((data.tasksDone / data.tasksOwned) * 100)"
                      :pt="{ value: { class: 'transition-none' } }"
                      class="!h-1.5 !w-20 !bg-slate-100"
                    />
                    <span class="text-ink-500 shrink-0">{{ Math.round((data.tasksDone / data.tasksOwned) * 100) }}%</span>
                  </div>
                  <span v-else class="text-ink-300">—</span>
                </template>
              </Column>
            </DataTable>
          </div>

        </template>
      </template>
    </template>
  </div>
</template>
