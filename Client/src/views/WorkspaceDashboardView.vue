<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute } from 'vue-router';
import Chart from '@/components/charts/SafeChart.vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';
import ProgressBar from 'primevue/progressbar';
import { useWorkspaceDashboardStore } from '@/stores/workspaceDashboard';
import { useWorkspaceStore } from '@/stores/workspace';

const { t } = useI18n();
const route   = useRoute();
const wsStore = useWorkspaceStore();
const store   = useWorkspaceDashboardStore();

const workspaceId = computed(() => Number(route.params.workspaceId));

onMounted(() => {
  if (workspaceId.value) {
    store.fetchAll(workspaceId.value);
    void store.startMlProgressTracking(workspaceId.value);
  }
});

onUnmounted(() => store.clear());

watch(workspaceId, (id) => {
  store.clear();
  if (id) {
    store.fetchAll(id);
    void store.startMlProgressTracking(id);
  }
});

const isFullAccess  = computed(() => store.summary?.accessLevel === 'full');
const hasMemberData = computed(() => store.memberActivity !== null && store.memberActivity.length > 0);

const kpis = computed(() => {
  const s = store.summary;
  if (!s) return [];

  const cards: Array<{ label: string; value: string; icon: string; color: string; bg: string }> = [
    {
      label: t('home.kpiTotalDeals'),
      value: String(s.totalDeals),
      icon: 'pi-briefcase',
      color: 'text-blue-600',
      bg: 'bg-blue-50',
    },
    {
      label: t('home.kpiOpenDeals'),
      value: String(s.openDeals),
      icon: 'pi-clock',
      color: 'text-emerald-600',
      bg: 'bg-emerald-50',
    },
    {
      label: t('wsDash.won'),
      value: String(s.wonDeals),
      icon: 'pi-check-circle',
      color: 'text-green-600',
      bg: 'bg-green-50',
    },
    {
      label: t('wsDash.clients'),
      value: `${s.activeClients} / ${s.totalClients}`,
      icon: 'pi-building',
      color: 'text-amber-600',
      bg: 'bg-amber-50',
    },
    {
      label: t('home.members'),
      value: String(s.memberCount),
      icon: 'pi-users',
      color: 'text-violet-600',
      bg: 'bg-violet-50',
    },
  ];

  if (s.totalPipelineValue !== null) {
    cards.unshift({
      label: t('wsDash.pipelineValue'),
      value: formatCurrency(s.totalPipelineValue),
      icon: 'pi-euro',
      color: 'text-sky-600',
      bg: 'bg-sky-50',
    });
  }
  if (s.winRate !== null) {
    cards.push({
      label: t('home.kpiWinRate'),
      value: `${(s.winRate * 100).toFixed(1)}%`,
      icon: 'pi-chart-line',
      color: 'text-teal-600',
      bg: 'bg-teal-50',
    });
  }
  if (s.tasksOverdue !== null) {
    cards.push({
      label: t('home.kpiOverdueTasks'),
      value: String(s.tasksOverdue),
      icon: 'pi-exclamation-triangle',
      color: s.tasksOverdue > 0 ? 'text-red-600' : 'text-slate-500',
      bg: s.tasksOverdue > 0 ? 'bg-red-50' : 'bg-slate-50',
    });
  }
  if (s.dealsClosingThisMonth !== null) {
    cards.push({
      label: t('home.kpiClosingThisMonth'),
      value: String(s.dealsClosingThisMonth),
      icon: 'pi-calendar-clock',
      color: 'text-orange-600',
      bg: 'bg-orange-50',
    });
  }

  return cards;
});

const basicStatusChartData = computed(() => {
  const s = store.summary;
  if (!s) return null;
  return {
    labels: [t('wsDash.statusOpen'), t('wsDash.statusWon'), t('wsDash.statusLost')],
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

const pipelineChartData = computed(() => {
  const p = store.pipeline;
  if (!p) return null;
  return {
    labels: p.stages.map((s) => s.name),
    datasets: [{
      label: t('home.dsDeals'),
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

const riskChartData = computed(() => {
  const r = store.riskDistribution;
  if (!r || !r.items.length) return null;
  return {
    labels: [t('home.highRisk'), t('home.mediumRisk'), t('home.lowRisk')],
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

const trendsChartData = computed(() => {
  const tr = store.trends;
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

const sliderRef = ref<HTMLElement | null>(null);
const isDragging = ref(false);
const dragStartX = ref(0);
const dragScrollLeft = ref(0);

function scrollKpi(dir: 'left' | 'right') {
  sliderRef.value?.scrollBy({ left: dir === 'right' ? 240 : -240, behavior: 'smooth' });
}

function onPointerDown(e: PointerEvent) {
  if (!sliderRef.value) return;
  isDragging.value = true;
  dragStartX.value = e.clientX;
  dragScrollLeft.value = sliderRef.value.scrollLeft;
  (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
}

function onPointerMove(e: PointerEvent) {
  if (!isDragging.value || !sliderRef.value) return;
  sliderRef.value.scrollLeft = dragScrollLeft.value - (e.clientX - dragStartX.value);
}

function onPointerUp() {
  isDragging.value = false;
}
</script>

<template>
  <div class="space-y-8">


    <section class="flex items-center justify-between">
      <div>
        <h1 class="text-xl font-semibold text-ink-900">
          {{ store.summary?.workspaceName ?? wsStore.currentWorkspace?.name ?? t('wsMembers.fallbackName') }}
        </h1>
        <p class="text-sm text-ink-400 mt-0.5">{{ t('wsDash.overview') }}</p>
      </div>
      <button
        v-if="isFullAccess && !store.isLoadingSummary"
        type="button"
        class="flex items-center gap-1.5 text-sm text-ink-500 hover:text-brand-600 px-3 py-1.5 rounded-lg hover:bg-brand-50 transition-colors"
        @click="workspaceId && store.fetchAll(workspaceId)"
      >
        <i class="pi pi-refresh text-xs" />
        {{ t('home.refresh') }}
      </button>
    </section>

    <div
      v-if="store.error"
      class="flex items-center gap-3 px-4 py-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700"
    >
      <i class="pi pi-exclamation-circle shrink-0" />
      {{ store.error }}
    </div>

    <div class="flex items-center gap-2">
      <button
        type="button"
        class="shrink-0 w-8 h-8 flex items-center justify-center rounded-lg border border-line bg-white shadow-sm text-ink-500 hover:text-brand-600 hover:border-brand-300 transition-colors"
        :aria-label="t('wsDash.scrollLeft')"
        @click="scrollKpi('left')"
      >
        <i class="pi pi-chevron-left text-xs" />
      </button>

      <div
        ref="sliderRef"
        :class="['kpi-scroll flex-1 min-w-0', isDragging ? 'cursor-grabbing select-none' : 'cursor-grab']"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointerleave="onPointerUp"
      >
        <div v-if="store.isLoadingSummary" class="flex gap-4">
          <div v-for="i in 8" :key="i" class="kpi-card skeleton-shimmer rounded-xl" />
        </div>
        <div v-else-if="kpis.length" class="kpi-row">
          <div
            v-for="kpi in kpis"
            :key="kpi.label"
            class="kpi-card flex flex-col justify-between gap-3 bg-white rounded-xl border border-line px-4 py-4 shadow-sm transition-all duration-150 hover:-translate-y-0.5 hover:border-brand-200 hover:shadow-md"
          >
            <div :class="['w-8 h-8 rounded-lg flex items-center justify-center shrink-0', kpi.bg]">
              <i :class="['pi text-sm', kpi.icon, kpi.color]" />
            </div>
            <div class="min-w-0">
              <p class="text-[11px] text-ink-400 font-medium uppercase tracking-wide leading-tight mb-1 truncate">{{ kpi.label }}</p>
              <p :class="['text-xl font-semibold leading-none truncate', kpi.color]">{{ kpi.value }}</p>
            </div>
          </div>
        </div>
      </div>

      <button
        type="button"
        class="shrink-0 w-8 h-8 flex items-center justify-center rounded-lg border border-line bg-white shadow-sm text-ink-500 hover:text-brand-600 hover:border-brand-300 transition-colors"
        :aria-label="t('wsDash.scrollRight')"
        @click="scrollKpi('right')"
      >
        <i class="pi pi-chevron-right text-xs" />
      </button>
    </div>

    
    <template v-if="store.summary && !isFullAccess">
      <div class="bg-amber-50 border border-amber-200 rounded-xl px-5 py-4 flex items-center gap-3 text-sm text-amber-800">
        <i class="pi pi-lock shrink-0" />
        <span>{{ t('wsDash.limitedAccess') }}</span>
      </div>
      <div v-if="basicStatusChartData" class="bg-white rounded-xl border border-line shadow-sm p-5">
        <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('wsDash.dealStatus') }}</h3>
        <div style="height: 160px">
          <Chart type="bar" :data="basicStatusChartData" :options="basicChartOptions" />
        </div>
      </div>
    </template>

    
    <template v-if="store.summary && isFullAccess">

      
      <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5">
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-sm font-semibold text-ink-700">{{ t('home.dealPipeline') }}</h3>
            <div v-if="store.pipeline" class="flex items-center gap-3 text-xs text-ink-400">
              <span>{{ t('home.winRateShort') }} <strong class="text-emerald-600">{{ (store.pipeline.conversionRate * 100).toFixed(0) }}%</strong></span>
              <span>{{ t('home.avgClose') }} <strong class="text-ink-700">{{ store.pipeline.avgDaysToClose }}d</strong></span>
            </div>
          </div>
          
          <div v-if="store.isLoadingPipeline" class="space-y-2">
            <div class="h-5 skeleton-shimmer rounded w-3/4" />
            <div class="h-5 skeleton-shimmer rounded w-1/2" />
            <div class="h-5 skeleton-shimmer rounded w-2/3" />
            <div class="h-5 skeleton-shimmer rounded w-5/12" />
          </div>
          <template v-else>
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
          </template>
        </div>

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5">
          <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('wsDash.riskDistribution') }}</h3>
          
          <div v-if="store.isLoadingRisk" class="flex items-center gap-6">
            <div class="w-[180px] h-[180px] rounded-full skeleton-shimmer shrink-0" />
            <div class="flex-1 space-y-2">
              <div class="h-4 skeleton-shimmer rounded w-full" />
              <div class="h-4 skeleton-shimmer rounded w-4/5" />
              <div class="h-4 skeleton-shimmer rounded w-full" />
              <div class="h-4 skeleton-shimmer rounded w-3/5" />
            </div>
          </div>
          <template v-else>
            
            <div v-if="riskChartData" :class="['flex items-center gap-6', store.isMlRecalculating && 'opacity-60']">
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
              </div>
            </div>
            
            <div v-if="store.isMlRecalculating" class="mt-3 space-y-2">
              <div class="flex items-center gap-3 px-3 py-2.5 bg-brand-50 border border-brand-100 rounded-lg text-xs text-brand-700">
                <i class="pi pi-spin pi-spinner shrink-0 text-brand-500" />
                <div class="flex-1">
                  <p class="font-medium">{{ t('wsDash.calculatingRisk') }}</p>
                  <p v-if="store.mlRecalcProgress" class="text-brand-500 mt-0.5">
                    {{ t('wsDash.dealsProgress', { processed: store.mlRecalcProgress.processedCount, total: store.mlRecalcProgress.totalCount }) }}
                  </p>
                </div>
                <span
                  v-if="store.mlRecalcProgress && store.mlRecalcProgress.totalCount > 0"
                  class="font-semibold tabular-nums"
                >
                  {{ Math.round((store.mlRecalcProgress.processedCount / store.mlRecalcProgress.totalCount) * 100) }}%
                </span>
              </div>
              <ProgressBar
                v-if="store.mlRecalcProgress && store.mlRecalcProgress.totalCount > 0"
                :value="Math.round((store.mlRecalcProgress.processedCount / store.mlRecalcProgress.totalCount) * 100)"
                :pt="{ value: { class: 'transition-none' } }"
                class="!h-1.5"
                :show-value="false"
              />
            </div>
            
            <p v-else-if="!riskChartData" class="text-xs text-ink-400 italic">{{ t('wsDash.noRiskData') }}</p>
          </template>
        </div>
      </div>

      
      <div class="bg-white rounded-xl border border-line shadow-sm p-5">
        <h3 class="text-sm font-semibold text-ink-700 mb-4">{{ t('home.trends6mo') }}</h3>
        
        <div v-if="store.isLoadingTrends" class="space-y-2">
          <div class="flex items-end gap-1 h-44">
            <div v-for="i in 12" :key="i" :style="`height: ${30 + (i * 13) % 70}%`" class="flex-1 skeleton-shimmer rounded-t" />
          </div>
          <div class="h-3 skeleton-shimmer rounded w-full mt-1" />
        </div>
        <div v-else-if="trendsChartData" style="height: 220px">
          <Chart type="line" :data="trendsChartData" :options="trendsChartOptions" />
        </div>
      </div>

      
      <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
          <h3 class="text-sm font-semibold text-ink-700 mb-3">{{ t('home.topDeals') }}</h3>
          
          <div v-if="store.isLoadingTopEntities" class="space-y-3">
            <div v-for="i in 5" :key="i" class="flex items-center justify-between gap-4">
              <div class="space-y-1.5 flex-1">
                <div class="h-3 skeleton-shimmer rounded w-3/4" />
                <div class="h-2.5 skeleton-shimmer rounded w-1/2" />
              </div>
              <div class="h-4 skeleton-shimmer rounded w-20 shrink-0" />
            </div>
          </div>
          <template v-else>
            <DataTable
              v-if="store.topEntities?.topDeals.length"
              :value="store.topEntities.topDeals"
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
          </template>
        </div>

        
        <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
          <h3 class="text-sm font-semibold text-ink-700 mb-3">{{ t('home.topClients') }}</h3>
          
          <div v-if="store.isLoadingTopEntities" class="space-y-3">
            <div v-for="i in 5" :key="i" class="flex items-center justify-between gap-4">
              <div class="space-y-1.5 flex-1">
                <div class="h-3 skeleton-shimmer rounded w-2/3" />
                <div class="h-2.5 skeleton-shimmer rounded w-1/3" />
              </div>
              <div class="h-4 skeleton-shimmer rounded w-20 shrink-0" />
            </div>
          </div>
          <template v-else>
            <DataTable
              v-if="store.topEntities?.topClients.length"
              :value="store.topEntities.topClients"
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
                  <Tag :value="t('home.dealsCount', { n: data.activeDeals })" severity="secondary" class="!text-[10px] !px-1.5 !py-0" />
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
          </template>
        </div>
      </div>

      
      <div class="bg-white rounded-xl border border-line shadow-sm p-5 overflow-hidden">
        <h3 class="text-sm font-semibold text-ink-700 mb-3">{{ t('wsDash.memberActivity') }}</h3>
        
        <div v-if="store.isLoadingMemberActivity" class="space-y-3">
          <div v-for="i in 3" :key="i" class="flex items-center gap-4">
            <div class="w-8 h-8 rounded-full skeleton-shimmer shrink-0" />
            <div class="flex-1 space-y-1.5">
              <div class="h-3 skeleton-shimmer rounded w-1/3" />
              <div class="h-2.5 skeleton-shimmer rounded w-1/4" />
            </div>
            <div class="h-4 skeleton-shimmer rounded w-12 shrink-0" />
            <div class="h-4 skeleton-shimmer rounded w-12 shrink-0" />
            <div class="h-2 skeleton-shimmer rounded w-20 shrink-0" />
          </div>
        </div>
        <template v-else-if="hasMemberData">
          <DataTable
            :value="store.memberActivity!"
            size="small"
            class="!text-xs"
            :pt="{ thead: { class: '!text-[11px] !uppercase !tracking-wide !text-ink-400' } }"
          >
            <Column field="fullName" :header="t('wsDash.colMember')">
              <template #body="{ data }">
                <div>
                  <p class="font-medium text-ink-800">{{ data.fullName }}</p>
                  <p class="text-ink-400 text-[10px] capitalize">{{ data.roleName.replace('ws_', '') }}</p>
                </div>
              </template>
            </Column>
            <Column field="dealsOwned" :header="t('wsDash.colDealsOwned')">
              <template #body="{ data }">
                <Tag :value="String(data.dealsOwned)" severity="secondary" class="!text-[10px] !px-1.5 !py-0" />
              </template>
            </Column>
            <Column field="tasksOwned" :header="t('wsDash.colTasks')">
              <template #body="{ data }">
                <span class="text-ink-700">{{ data.tasksOwned }}</span>
              </template>
            </Column>
            <Column field="tasksDone" :header="t('wsDash.colDone')">
              <template #body="{ data }">
                <span class="text-emerald-600 font-medium">{{ data.tasksDone }}</span>
              </template>
            </Column>
            <Column :header="t('wsDash.colCompletion')">
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
        </template>
        <p v-else-if="!store.isLoadingMemberActivity" class="text-sm text-ink-400 italic">
          {{ t('wsDash.noMemberData') }}
        </p>
      </div>

    </template>
  </div>
</template>

<style scoped>
.kpi-scroll {
  overflow-x: auto;
  scrollbar-width: none;
  -webkit-overflow-scrolling: touch;
  padding-top: 0.25rem;
  padding-bottom: 0.5rem;
  -webkit-mask-image: linear-gradient(to right, transparent 0%, black 6%, black 94%, transparent 100%);
  mask-image: linear-gradient(to right, transparent 0%, black 6%, black 94%, transparent 100%);
}
.kpi-scroll::-webkit-scrollbar {
  display: none;
}

.kpi-row {
  display: flex;
  gap: 1rem;
  width: max-content;
}

.kpi-card {
  width: 11rem;
  min-height: 6.5rem;
  flex-shrink: 0;
}

.skeleton-shimmer {
  background: linear-gradient(90deg, #f1f5f9 25%, #e2e8f0 50%, #f1f5f9 75%);
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0%   { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
</style>
