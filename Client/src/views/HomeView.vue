<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import Chart from '@/components/charts/SafeChart.vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';
import ProgressBar from 'primevue/progressbar';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useDashboardStore } from '@/stores/dashboard';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const dashStore = useDashboardStore();

const now = ref(new Date());
let tickHandle: ReturnType<typeof setInterval> | null = null;

const canView = computed(() => {
  const orgPerms = new Set(orgStore.currentOrg?.myPermissions ?? []);
  if (orgPerms.has('manage_org_settings')) return true;
  const wsPerms = wsStore.workspaces.flatMap((w) => w.myPermissions ?? []);
  return wsPerms.includes('view_analytics') || wsPerms.includes('view_basic_stats');
});

const accessLevel = computed(() => dashStore.summary?.accessLevel ?? null);
const hasFullAccess = computed(() => accessLevel.value === 'full_org' || accessLevel.value === 'full');
const isFullOrg = computed(() => accessLevel.value === 'full_org');

onMounted(() => {
  tickHandle = setInterval(() => {
    now.value = new Date();
  }, 60_000);

  if (canView.value && orgStore.currentOrgId) {
    dashStore.fetchAll(orgStore.currentOrgId);
  }
});

onUnmounted(() => {
  if (tickHandle) clearInterval(tickHandle);
});

const greeting = computed(() => {
  const hour = now.value.getHours();
  if (hour >= 5 && hour < 12) return 'Good morning';
  if (hour >= 12 && hour < 18) return 'Good afternoon';
  return 'Good evening';
});

const firstName = computed(() => auth.user?.firstName?.trim() ?? '');

function displayOrgRole(roleName: string | null | undefined): string {
  if (!roleName) return '—';
  if (roleName === 'org_owner') return 'Owner';
  if (roleName === 'org_admin') return 'Admin';
  if (roleName === 'org_member') return 'Member';
  return roleName;
}

const kpis = computed(() => {
  const s = dashStore.summary;
  if (!s) return [];

  if (!hasFullAccess.value) {
    return [
      {
        label: 'Total Deals',
        value: String(s.totalDeals),
        icon: 'pi-briefcase',
        color: 'text-emerald-600',
        bg: 'bg-emerald-50',
      },
      {
        label: 'Active Clients',
        value: `${s.activeClients} / ${s.totalClients}`,
        icon: 'pi-building',
        color: 'text-amber-600',
        bg: 'bg-amber-50',
      },
      {
        label: 'Workspaces',
        value: String(s.totalWorkspaces),
        icon: 'pi-folder',
        color: 'text-sky-600',
        bg: 'bg-sky-50',
      },
    ];
  }

  const base = [
    {
      label: 'Total Pipeline Value',
      value: formatCurrency(s.totalDealValue),
      icon: 'pi-euro',
      color: 'text-blue-600',
      bg: 'bg-blue-50',
    },
    {
      label: 'Open Deals',
      value: String(s.openDeals),
      icon: 'pi-briefcase',
      color: 'text-emerald-600',
      bg: 'bg-emerald-50',
    },
    {
      label: 'Win Rate',
      value: `${(s.winRate * 100).toFixed(1)}%`,
      icon: 'pi-chart-line',
      color: 'text-violet-600',
      bg: 'bg-violet-50',
    },
    {
      label: 'Overdue Tasks',
      value: String(s.tasksOverdue),
      icon: 'pi-exclamation-triangle',
      color: s.tasksOverdue > 0 ? 'text-red-600' : 'text-slate-500',
      bg: s.tasksOverdue > 0 ? 'bg-red-50' : 'bg-slate-50',
    },
    {
      label: 'Active Clients',
      value: `${s.activeClients} / ${s.totalClients}`,
      icon: 'pi-building',
      color: 'text-amber-600',
      bg: 'bg-amber-50',
    },
    {
      label: 'Closing This Month',
      value: String(s.dealsClosingThisMonth),
      icon: 'pi-calendar-clock',
      color: 'text-sky-600',
      bg: 'bg-sky-50',
    },
  ];

  if (isFullOrg.value) {
    base.push({
      label: 'Workspaces',
      value: String(s.totalWorkspaces),
      icon: 'pi-folder',
      color: 'text-indigo-600',
      bg: 'bg-indigo-50',
    });
  }

  return base;
});

const workspaceComparisonChartData = computed(() => {
  const wc = dashStore.workspacesComparison;
  if (!wc?.length) return null;
  return {
    labels: wc.map((w) => w.workspaceName),
    datasets: [
      {
        label: 'Pipeline Value (€k)',
        data: wc.map((w) => Math.round(w.pipelineValue / 1000)),
        backgroundColor: '#3b82f6',
        borderRadius: 4,
      },
      {
        label: 'Deal Count',
        data: wc.map((w) => w.dealCount),
        backgroundColor: '#8b5cf6',
        borderRadius: 4,
        yAxisID: 'y1',
      },
    ],
  };
});

const workspaceComparisonChartOptions = {
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
      ticks: { color: '#64748b', callback: (v: number) => `€${v}k` },
      title: { display: true, text: 'Pipeline (€k)', color: '#94a3b8' },
    },
    y1: {
      type: 'linear' as const,
      position: 'right' as const,
      grid: { drawOnChartArea: false },
      ticks: { color: '#8b5cf6', stepSize: 1 },
      title: { display: true, text: 'Deals', color: '#8b5cf6' },
    },
    x: { grid: { display: false }, ticks: { color: '#64748b' } },
  },
};

const pipelineChartData = computed(() => {
  const p = dashStore.pipeline;
  if (!p) return null;
  return {
    labels: p.stages.map((s) => s.name),
    datasets: [
      {
        label: 'Deals',
        data: p.stages.map((s) => s.count),
        backgroundColor: ['#3b82f6', '#8b5cf6', '#f59e0b', '#10b981'],
        borderRadius: 4,
      },
    ],
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

const riskChartData = computed(() => {
  const r = dashStore.riskDistribution;
  if (!r) return null;
  return {
    labels: ['High Risk', 'Medium Risk', 'Low Risk'],
    datasets: [
      {
        data: [
          r.distribution.high?.count ?? 0,
          r.distribution.medium?.count ?? 0,
          r.distribution.low?.count ?? 0,
        ],
        backgroundColor: ['#ef4444', '#f59e0b', '#10b981'],
        borderWidth: 0,
      },
    ],
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

const trendsChartData = computed(() => {
  const t = dashStore.trends;
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
    case 'low':    return 'secondary';
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

function scoreBar(score?: number) {
  return score != null ? Math.round(score * 100) : null;
}
</script>

<template>
  <div class="space-y-8">
    
    <section class="max-w-3xl">
      <div class="relative overflow-hidden rounded-2xl border border-line bg-white px-7 py-8 shadow-sm">
        <div
          class="pointer-events-none absolute -top-12 -right-12 h-48 w-48 rounded-full bg-brand-100/60 blur-3xl"
          aria-hidden="true"
        />
        <div
          class="pointer-events-none absolute -bottom-16 -left-10 h-44 w-44 rounded-full bg-brand-50 blur-3xl"
          aria-hidden="true"
        />

        <div class="relative">
          <p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand-600">
            {{ now.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' }) }}
          </p>
          <h1 class="mt-2 text-[28px] font-bold text-ink-900 leading-tight">
            {{ greeting }}<span v-if="firstName">, {{ firstName }}</span>
          </h1>
        </div>
      </div>

      <div class="mt-6 grid gap-4 sm:grid-cols-2">
        <div class="rounded-xl border border-line bg-white p-5">
          <h2 class="text-sm font-semibold text-ink-900">Session</h2>
          <dl class="mt-3 text-sm text-ink-700 grid grid-cols-[auto,1fr] gap-x-6 gap-y-2">
            <dt class="text-ink-500">Email</dt>
            <dd>{{ auth.user?.email ?? '—' }}</dd>
            <dt class="text-ink-500">Token expiry</dt>
            <dd>{{ auth.expiresAt ? new Date(auth.expiresAt).toLocaleString() : '—' }}</dd>
          </dl>
        </div>

        <div class="rounded-xl border border-line bg-white p-5">
          <h2 class="text-sm font-semibold text-ink-900">Organization</h2>
          <dl class="mt-3 text-sm text-ink-700 grid grid-cols-[auto,1fr] gap-x-6 gap-y-2">
            <dt class="text-ink-500">Name</dt>
            <dd>{{ orgStore.currentOrg?.name ?? '—' }}</dd>
            <dt class="text-ink-500">Role</dt>
            <dd>{{ displayOrgRole(orgStore.currentOrg?.userRole) }}</dd>
            <dt class="text-ink-500">Members</dt>
            <dd>{{ orgStore.currentOrg?.memberCount ?? '—' }}</dd>
          </dl>
        </div>
      </div>
    </section>

    
    <template v-if="canView">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-base font-semibold text-ink-900">Analytics</h2>
          <p class="text-sm text-ink-400 mt-0.5">CRM overview for {{ orgStore.currentOrg?.name }}</p>
        </div>
        <button
          v-if="!dashStore.isLoading"
          type="button"
          class="flex items-center gap-1.5 text-sm text-ink-500 hover:text-brand-600 px-3 py-1.5 rounded-lg hover:bg-brand-50 transition-colors"
          @click="orgStore.currentOrgId && dashStore.fetchAll(orgStore.currentOrgId)"
        >
          <i class="pi pi-refresh text-xs" />
          Refresh
        </button>
      </div>

      
      <div v-if="dashStore.isLoading" class="space-y-4">
        <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
          <div v-for="i in 6" :key="i" class="h-24 rounded-xl bg-slate-100 animate-pulse" />
        </div>
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <div class="h-52 rounded-xl bg-slate-100 animate-pulse" />
          <div class="h-52 rounded-xl bg-slate-100 animate-pulse" />
        </div>
        <div class="h-60 rounded-xl bg-slate-100 animate-pulse" />
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <div class="h-64 rounded-xl bg-slate-100 animate-pulse" />
          <div class="h-64 rounded-xl bg-slate-100 animate-pulse" />
        </div>
      </div>

      <template v-else>
        
        <div v-if="dashStore.error" class="flex items-center gap-3 px-4 py-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
          <i class="pi pi-exclamation-circle shrink-0" />
          {{ dashStore.error }}
        </div>

        
        <div v-if="accessLevel === 'basic'" class="flex items-center gap-3 px-4 py-3 bg-amber-50 border border-amber-200 rounded-xl text-sm text-amber-800">
          <i class="pi pi-lock shrink-0" />
          You have limited access. Contact your organization admin to unlock full analytics.
        </div>

        
        <div v-if="dashStore.summary" :class="['grid gap-4', hasFullAccess ? 'grid-cols-2 md:grid-cols-3 xl:grid-cols-6' : 'grid-cols-1 sm:grid-cols-3']">
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

        
        <div v-if="isFullOrg && workspaceComparisonChartData" class="bg-white rounded-xl border border-line shadow-sm p-5">
          <h3 class="text-sm font-semibold text-ink-700 mb-4">Workspace Comparison</h3>
          <div style="height: 220px">
            <Chart type="bar" :data="workspaceComparisonChartData" :options="workspaceComparisonChartOptions" />
          </div>
          <div v-if="dashStore.workspacesComparison?.length" class="mt-4 overflow-x-auto">
            <table class="w-full text-xs text-ink-700">
              <thead>
                <tr class="text-ink-400 text-[10px] uppercase tracking-wide border-b border-line">
                  <th class="text-left pb-2 font-medium">Workspace</th>
                  <th class="text-right pb-2 font-medium">Pipeline</th>
                  <th class="text-right pb-2 font-medium">Deals</th>
                  <th class="text-right pb-2 font-medium">Win Rate</th>
                  <th class="text-right pb-2 font-medium">Clients</th>
                  <th class="text-right pb-2 font-medium">Members</th>
                  <th class="text-right pb-2 font-medium">Top Stage</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="ws in dashStore.workspacesComparison"
                  :key="ws.workspaceId"
                  class="border-b border-slate-50 last:border-0"
                >
                  <td class="py-2 font-medium text-ink-800">{{ ws.workspaceName }}</td>
                  <td class="py-2 text-right text-ink-700">{{ formatCurrency(ws.pipelineValue) }}</td>
                  <td class="py-2 text-right">{{ ws.dealCount }}</td>
                  <td class="py-2 text-right text-emerald-600">{{ (ws.winRate * 100).toFixed(0) }}%</td>
                  <td class="py-2 text-right">{{ ws.clientCount }}</td>
                  <td class="py-2 text-right">{{ ws.memberCount }}</td>
                  <td class="py-2 text-right text-ink-500">{{ ws.topStage || '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        
        <template v-if="hasFullAccess">

        
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

          
          <div class="bg-white rounded-xl border border-line shadow-sm p-5">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-sm font-semibold text-ink-700">Deal Pipeline</h3>
              <div v-if="dashStore.pipeline" class="flex items-center gap-3 text-xs text-ink-400">
                <span>Win rate <strong class="text-emerald-600">{{ (dashStore.pipeline.conversionRate * 100).toFixed(0) }}%</strong></span>
                <span>Avg close <strong class="text-ink-700">{{ dashStore.pipeline.avgDaysToClose }}d</strong></span>
              </div>
            </div>
            <div v-if="pipelineChartData" style="height: 180px">
              <Chart type="bar" :data="pipelineChartData" :options="pipelineChartOptions" />
            </div>
            <div v-if="dashStore.pipeline" class="mt-4 grid grid-cols-4 gap-2 border-t border-line pt-3">
              <div
                v-for="(count, status) in dashStore.pipeline.statusBreakdown"
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
                  v-for="item in dashStore.riskDistribution?.items.slice(0, 6)"
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
                <p v-if="!dashStore.riskDistribution?.items.length" class="text-xs text-ink-400 italic">
                  ML scores unavailable (no active deals or ML service offline).
                </p>
              </div>
            </div>
          </div>
        </div>

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5">
          <h3 class="text-sm font-semibold text-ink-700 mb-4">6-Month Deal Trends</h3>
          <div v-if="trendsChartData" style="height: 220px">
            <Chart type="line" :data="trendsChartData" :options="trendsChartOptions" />
          </div>
        </div>

        
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

          
          <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
            <h3 class="text-sm font-semibold text-ink-700 mb-3">Top Deals by Value</h3>
            <DataTable
              v-if="dashStore.topEntities?.topDeals.length"
              :value="dashStore.topEntities.topDeals"
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
              v-if="dashStore.topEntities?.topClients.length"
              :value="dashStore.topEntities.topClients"
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
                  <Tag
                    :value="`${data.activeDeals} deals`"
                    severity="secondary"
                    class="!text-[10px] !px-1.5 !py-0"
                  />
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

        </template>
      </template>
    </template>
  </div>
</template>
