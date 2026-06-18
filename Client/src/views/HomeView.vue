<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import Chart from '@/components/charts/SafeChart.vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';
import ProgressBar from 'primevue/progressbar';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useDashboardStore } from '@/stores/dashboard';
import { roleLabel } from '@/utils/roleBadge';

const { t } = useI18n();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const dashStore = useDashboardStore();
const router = useRouter();

const fullName = computed(() => {
  const u = auth.user;
  if (!u) return '';
  return `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
});

const userInitials = computed(() => {
  const u = auth.user;
  if (!u) return '';
  const f = (u.firstName?.[0] ?? '').toUpperCase();
  const l = (u.lastName?.[0] ?? '').toUpperCase();
  return `${f}${l}` || (u.email?.[0] ?? '').toUpperCase();
});

const workspaceCount = computed(() => wsStore.workspaces.length);

const canManageOrg = computed(() =>
  orgStore.currentOrg?.myPermissions?.includes('manage_org_settings') ?? false,
);

function goToOrgSettings() {
  router.push({ name: 'org-settings' });
}

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
  if (hour >= 5 && hour < 12) return t('home.greetingMorning');
  if (hour >= 12 && hour < 18) return t('home.greetingAfternoon');
  return t('home.greetingEvening');
});

const firstName = computed(() => auth.user?.firstName?.trim() ?? '');

const kpis = computed(() => {
  const s = dashStore.summary;
  if (!s) return [];

  if (!hasFullAccess.value) {
    return [
      {
        label: t('home.kpiTotalDeals'),
        value: String(s.totalDeals),
        icon: 'pi-briefcase',
        color: 'text-emerald-600',
        bg: 'bg-emerald-50',
      },
      {
        label: t('home.kpiActiveClients'),
        value: `${s.activeClients} / ${s.totalClients}`,
        icon: 'pi-building',
        color: 'text-amber-600',
        bg: 'bg-amber-50',
      },
      {
        label: t('home.workspaces'),
        value: String(s.totalWorkspaces),
        icon: 'pi-folder',
        color: 'text-sky-600',
        bg: 'bg-sky-50',
      },
    ];
  }

  const base = [
    {
      label: t('home.kpiTotalPipelineValue'),
      value: formatCurrency(s.totalDealValue),
      icon: 'pi-euro',
      color: 'text-blue-600',
      bg: 'bg-blue-50',
    },
    {
      label: t('home.kpiOpenDeals'),
      value: String(s.openDeals),
      icon: 'pi-briefcase',
      color: 'text-emerald-600',
      bg: 'bg-emerald-50',
    },
    {
      label: t('home.kpiWinRate'),
      value: `${(s.winRate * 100).toFixed(1)}%`,
      icon: 'pi-chart-line',
      color: 'text-violet-600',
      bg: 'bg-violet-50',
    },
    {
      label: t('home.kpiOverdueTasks'),
      value: String(s.tasksOverdue),
      icon: 'pi-exclamation-triangle',
      color: s.tasksOverdue > 0 ? 'text-red-600' : 'text-slate-500',
      bg: s.tasksOverdue > 0 ? 'bg-red-50' : 'bg-slate-50',
    },
    {
      label: t('home.kpiActiveClients'),
      value: `${s.activeClients} / ${s.totalClients}`,
      icon: 'pi-building',
      color: 'text-amber-600',
      bg: 'bg-amber-50',
    },
    {
      label: t('home.kpiClosingThisMonth'),
      value: String(s.dealsClosingThisMonth),
      icon: 'pi-calendar-clock',
      color: 'text-sky-600',
      bg: 'bg-sky-50',
    },
  ];

  if (isFullOrg.value) {
    base.push({
      label: t('home.workspaces'),
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
        label: t('home.dsPipelineValueK'),
        data: wc.map((w) => Math.round(w.pipelineValue / 1000)),
        backgroundColor: '#3b82f6',
        borderRadius: 4,
      },
      {
        label: t('home.dsDealCount'),
        data: wc.map((w) => w.dealCount),
        backgroundColor: '#8b5cf6',
        borderRadius: 4,
        yAxisID: 'y1',
      },
    ],
  };
});

const workspaceComparisonChartOptions = computed(() => ({
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
      title: { display: true, text: t('home.axisPipelineK'), color: '#94a3b8' },
    },
    y1: {
      type: 'linear' as const,
      position: 'right' as const,
      grid: { drawOnChartArea: false },
      ticks: { color: '#8b5cf6', stepSize: 1 },
      title: { display: true, text: t('home.axisDeals'), color: '#8b5cf6' },
    },
    x: { grid: { display: false }, ticks: { color: '#64748b' } },
  },
}));

const pipelineChartData = computed(() => {
  const p = dashStore.pipeline;
  if (!p) return null;
  return {
    labels: p.stages.map((s) => s.name),
    datasets: [
      {
        label: t('home.dsDeals'),
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
    labels: [t('home.highRisk'), t('home.mediumRisk'), t('home.lowRisk')],
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
  const tr = dashStore.trends;
  if (!tr) return null;
  return {
    labels: tr.months.map((m) => m.label),
    datasets: [
      {
        label: t('home.dsPipelineDeals'),
        data: tr.months.map((m) => m.newDeals),
        borderColor: '#3b82f6',
        backgroundColor: 'rgba(59,130,246,0.08)',
        fill: true,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: t('home.dsWon'),
        data: tr.months.map((m) => m.closedWon),
        borderColor: '#10b981',
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: t('home.dsLost'),
        data: tr.months.map((m) => m.closedLost),
        borderColor: '#ef4444',
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.3,
        yAxisID: 'y',
      },
      {
        label: t('home.dsWonRevenue'),
        data: tr.months.map((m) => m.wonRevenue),
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

const trendsChartOptions = computed(() => ({
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
      title: { display: true, text: t('home.axisDeals'), color: '#94a3b8' },
    },
    y1: {
      type: 'linear' as const,
      position: 'right' as const,
      grid: { drawOnChartArea: false },
      ticks: {
        color: '#8b5cf6',
        callback: (v: number) => `€${(v / 1000).toFixed(0)}k`,
      },
      title: { display: true, text: t('home.axisRevenue'), color: '#8b5cf6' },
    },
    x: { grid: { display: false }, ticks: { color: '#64748b' } },
  },
}));

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
    
    <div class="flex flex-col xl:flex-row gap-8 items-start">
    <section class="w-full xl:w-auto xl:min-w-[28rem] xl:max-w-[42rem] xl:shrink-0">
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

      <div v-if="auth.user" class="mt-6">
        <article class="session-card rounded border border-line bg-white px-6 py-5">
          <header class="flex items-start justify-between gap-4">
            <div class="flex items-center gap-4 min-w-0">
              <div class="session-card__avatar shrink-0">
                <span>{{ userInitials }}</span>
              </div>
              <div class="min-w-0">
                <div class="flex items-center gap-2 flex-wrap min-w-0">
                  <p class="text-base font-semibold text-ink-900 truncate">
                    {{ fullName || auth.user.email }}
                  </p>
                  <span
                    v-if="orgStore.currentOrg"
                    class="text-ink-300 select-none"
                    aria-hidden="true"
                  >·</span>
                  <p
                    v-if="orgStore.currentOrg"
                    class="text-sm text-ink-700 truncate"
                  >{{ orgStore.currentOrg.name }}</p>
                  <span
                    v-if="orgStore.currentOrg?.userRole"
                    class="session-card__role-badge"
                  >
                    {{ roleLabel(orgStore.currentOrg.userRole, orgStore.currentOrg.userRoleDisplayName) }}
                  </span>
                </div>
                <p class="mt-0.5 text-sm text-ink-500 truncate">{{ auth.user.email }}</p>
              </div>
            </div>

            <span class="session-card__active-pill shrink-0">
              <span class="relative flex h-1.5 w-1.5">
                <span class="absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75 animate-ping" />
                <span class="relative inline-flex h-1.5 w-1.5 rounded-full bg-emerald-500" />
              </span>
              {{ t('home.active') }}
            </span>
          </header>

          <div class="mt-5 flex items-end gap-3 flex-wrap">
            <dl class="grid grid-cols-2 gap-3 flex-1 min-w-[16rem]">
              <div class="session-card__stat">
                <dt>{{ t('home.members') }}</dt>
                <dd>{{ orgStore.currentOrg?.memberCount ?? '—' }}</dd>
              </div>
              <div class="session-card__stat">
                <dt>{{ t('home.workspaces') }}</dt>
                <dd>{{ workspaceCount }}</dd>
              </div>
            </dl>

            <button
              v-if="canManageOrg"
              type="button"
              class="session-card__manage-btn shrink-0"
              @click="goToOrgSettings"
            >
              {{ t('home.manage') }}
            </button>
          </div>
        </article>
      </div>
    </section>

      <div v-if="canView" class="flex-1 min-w-0">
        <div v-if="dashStore.isLoading" class="grid grid-cols-2 lg:grid-cols-3 gap-4">
          <div v-for="i in 6" :key="i" class="h-24 rounded-xl bg-slate-100 animate-pulse" />
        </div>
        <div
          v-else-if="dashStore.summary"
          :class="['grid gap-4', hasFullAccess ? 'grid-cols-2 lg:grid-cols-3' : 'grid-cols-3']"
        >
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
      </div>
    </div>


    <template v-if="canView">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-base font-semibold text-ink-900">{{ t('home.analytics') }}</h2>
          <p class="text-sm text-ink-400 mt-0.5">{{ t('home.analyticsSubtitle', { org: orgStore.currentOrg?.name }) }}</p>
        </div>
        <button
          v-if="!dashStore.isLoading"
          type="button"
          class="flex items-center gap-1.5 text-sm text-ink-500 hover:text-brand-600 px-3 py-1.5 rounded-lg hover:bg-brand-50 transition-colors"
          @click="orgStore.currentOrgId && dashStore.fetchAll(orgStore.currentOrgId)"
        >
          <i class="pi pi-refresh text-xs" />
          {{ t('home.refresh') }}
        </button>
      </div>

      
      <div v-if="dashStore.isLoading" class="space-y-4">
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
          {{ t('home.limitedAccess') }}
        </div>


        <div v-if="isFullOrg && workspaceComparisonChartData" class="bg-white rounded-xl border border-line shadow-sm p-5">
          <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('home.workspaceComparison') }}</h3>
          <div style="height: 220px">
            <Chart type="bar" :data="workspaceComparisonChartData" :options="workspaceComparisonChartOptions" />
          </div>
          <div v-if="dashStore.workspacesComparison?.length" class="mt-4 overflow-x-auto">
            <table class="w-full text-xs text-ink-700">
              <thead>
                <tr class="text-ink-400 text-[10px] uppercase tracking-wide border-b border-line">
                  <th class="text-left pb-2 font-medium">{{ t('home.colWorkspace') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colPipeline') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colDeals') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colWinRate') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colClients') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colMembers') }}</th>
                  <th class="text-right pb-2 font-medium">{{ t('home.colTopStage') }}</th>
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
              <h3 class="text-sm font-semibold text-ink-700">{{ t('home.dealPipeline') }}</h3>
              <div v-if="dashStore.pipeline" class="flex items-center gap-3 text-xs text-ink-400">
                <span>{{ t('home.winRateShort') }} <strong class="text-emerald-600">{{ (dashStore.pipeline.conversionRate * 100).toFixed(0) }}%</strong></span>
                <span>{{ t('home.avgClose') }} <strong class="text-ink-700">{{ dashStore.pipeline.avgDaysToClose }}d</strong></span>
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
            <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('home.riskDistribution') }}</h3>
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
                  {{ t('home.mlUnavailable') }}
                </p>
              </div>
            </div>
          </div>
        </div>

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5">
          <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('home.trends6mo') }}</h3>
          <div v-if="trendsChartData" style="height: 220px">
            <Chart type="line" :data="trendsChartData" :options="trendsChartOptions" />
          </div>
        </div>

        
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

          
          <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
            <h3 class="text-sm font-semibold text-ink-700 mb-3">{{ t('home.topDeals') }}</h3>
            <DataTable
              v-if="dashStore.topEntities?.topDeals.length"
              :value="dashStore.topEntities.topDeals"
              size="small"
              class="!text-xs"
              :pt="{ thead: { class: 'hidden' } }"
            >
              <Column field="title" :header="t('home.colDeal')">
                <template #body="{ data }">
                  <div>
                    <p class="font-medium text-ink-800 truncate max-w-[160px]">{{ data.title }}</p>
                    <p v-if="data.clientName" class="text-ink-400 truncate max-w-[160px]">{{ data.clientName }}</p>
                  </div>
                </template>
              </Column>
              <Column field="value" :header="t('home.colValue')">
                <template #body="{ data }">
                  <span class="font-semibold text-ink-700">{{ formatCurrency(data.value) }}</span>
                </template>
              </Column>
              <Column field="stage" :header="t('home.colStage')">
                <template #body="{ data }">
                  <span class="text-ink-500">{{ data.stage ?? '—' }}</span>
                </template>
              </Column>
              <Column field="priority" :header="t('home.colPriority')">
                <template #body="{ data }">
                  <Tag
                    v-if="data.priority"
                    :value="data.priority"
                    :severity="priorityClass(data.priority)"
                    class="!text-[10px] !px-1.5 !py-0 capitalize"
                  />
                </template>
              </Column>
              <Column field="closureScore" :header="t('home.colScore')">
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
            <p v-else class="text-sm text-ink-400 italic">{{ t('home.noDealData') }}</p>
          </div>

          
          <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
            <h3 class="text-sm font-semibold text-ink-700 mb-3">{{ t('home.topClients') }}</h3>
            <DataTable
              v-if="dashStore.topEntities?.topClients.length"
              :value="dashStore.topEntities.topClients"
              size="small"
              class="!text-xs"
              :pt="{ thead: { class: 'hidden' } }"
            >
              <Column field="name" :header="t('home.colClient')">
                <template #body="{ data }">
                  <div>
                    <p class="font-medium text-ink-800 truncate max-w-[160px]">{{ data.name }}</p>
                    <p v-if="data.industry" class="text-ink-400 capitalize">{{ data.industry }}</p>
                  </div>
                </template>
              </Column>
              <Column field="lifetimeValue" :header="t('home.colLtv')">
                <template #body="{ data }">
                  <div class="flex flex-col leading-tight">
                    <span class="font-semibold text-ink-700">{{ formatCurrency(data.lifetimeValue) }}</span>
                    <span v-if="data.isExpectedLtv" class="text-[10px] text-ink-400">{{ t('home.expectedLtv') }}</span>
                  </div>
                </template>
              </Column>
              <Column field="activeDeals" :header="t('home.colActive')">
                <template #body="{ data }">
                  <Tag
                    :value="t('home.dealsCount', { n: data.activeDeals })"
                    severity="secondary"
                    class="!text-[10px] !px-1.5 !py-0"
                  />
                </template>
              </Column>
              <Column field="avgClosureScore" :header="t('home.colAvgScore')">
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
            <p v-else class="text-sm text-ink-400 italic">{{ t('home.noClientData') }}</p>
          </div>
        </div>

        </template>
      </template>
    </template>
  </div>
</template>

<style scoped>
.session-card {
  transition: box-shadow 0.15s ease, border-color 0.15s ease;
}

.session-card:hover {
  border-color: rgb(203 213 225);
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
}

.session-card__avatar {
  width: 2.75rem;
  height: 2.75rem;
  border-radius: 0.25rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-size: 0.875rem;
  font-weight: 600;
  letter-spacing: 0.03em;
  background-color: rgb(37 99 235);
}

.session-card__role-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.125rem 0.625rem;
  border-radius: 0.25rem;
  background-color: rgb(37 99 235);
  color: #ffffff;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.01em;
}

.session-card__active-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0.625rem;
  border-radius: 0.25rem;
  border: 1px solid rgb(167 243 208);
  background-color: #ffffff;
  color: rgb(4 120 87);
  font-size: 0.6875rem;
  font-weight: 700;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.session-card__stat {
  padding: 0.875rem 1rem;
  border: 1px solid rgb(226 232 240);
  border-radius: 0.25rem;
  background-color: rgb(248 250 252);
}

.session-card__stat dt {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: rgb(100 116 139);
}

.session-card__stat dd {
  margin-top: 0.25rem;
  font-size: 1.5rem;
  font-weight: 600;
  line-height: 1.1;
  color: rgb(15 23 42);
}

.session-card__manage-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 2.25rem;
  padding: 0 1.25rem;
  border-radius: 0.25rem;
  background-color: rgb(37 99 235);
  color: #ffffff;
  font-size: 0.8125rem;
  font-weight: 600;
  letter-spacing: 0.01em;
  border: 1px solid rgb(37 99 235);
  transition: background-color 0.15s ease, border-color 0.15s ease;
  min-width: 6rem;
}

.session-card__manage-btn:hover {
  background-color: rgb(29 78 216);
  border-color: rgb(29 78 216);
}

.session-card__manage-btn:active {
  background-color: rgb(30 64 175);
  border-color: rgb(30 64 175);
}

.session-card__manage-btn:focus-visible {
  outline: 2px solid rgb(37 99 235);
  outline-offset: 2px;
}
</style>
