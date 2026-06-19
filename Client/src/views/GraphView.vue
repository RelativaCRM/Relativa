<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ConfirmDialog from 'primevue/confirmdialog';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';
import { Network, type Options } from 'vis-network/standalone';
import ProgressBar from 'primevue/progressbar';
import { useGraphStore } from '@/stores/graph';
import { useEntityStore } from '@/stores/entity';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import type { GraphNodeDto, GraphEdgeDto, GraphHighlightTag, GraphRiskLevel } from '@/api/graph';
import { mlApi, type DealScoreDto } from '@/api/ml';
import GraphSkeleton from '@/components/feedback/GraphSkeleton.vue';
import FilterPanel, {
  type FilterPanelOption,
  type FilterPanelState,
} from '@/components/graph/FilterPanel.vue';

const router = useRouter();
const graphStore = useGraphStore();
const entityStore = useEntityStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const confirm = useConfirm();
const toast = useToast();
const { t } = useI18n();

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const selectedNode = ref<GraphNodeDto | null>(null);
const dealScores = ref<Map<number, DealScoreDto>>(new Map());
const riskFilter = ref<GraphRiskLevel | null>(null);

const filters = ref<FilterPanelState>({
  risk: null,
  managerUserId: null,
  workspaceId: null,
  entityTypeNames: [],
});

const orgId = computed(() => orgStore.currentOrgId);

const HIGHLIGHT: Record<GraphHighlightTag, { border: string; shadow: string }> = {
  best_deal:    { border: '#16a34a', shadow: 'rgba(22,163,74,0.55)' },
  worst_deal:   { border: '#dc2626', shadow: 'rgba(220,38,38,0.55)' },
  best_client:  { border: '#16a34a', shadow: 'rgba(22,163,74,0.55)' },
  worst_client: { border: '#dc2626', shadow: 'rgba(220,38,38,0.55)' },
};

const HIGHLIGHT_KEY: Record<GraphHighlightTag, string> = {
  best_deal: 'bestDeal',
  worst_deal: 'worstDeal',
  best_client: 'bestClient',
  worst_client: 'worstClient',
};

function highlightLabel(tag: GraphHighlightTag): string {
  return t(`graph.highlight.${HIGHLIGHT_KEY[tag]}`);
}

function riskLabel(level: 'high' | 'medium' | 'low'): string {
  return t(`graph.risk.${level}Risk`);
}

const TYPE_COLORS: Record<string, string> = {
  user_self: '#1d4ed8',
  user:      '#93c5fd',
  workspace: '#0d9488',
};

const ENTITY_PALETTE = [
  '#7c3aed',
  '#d97706',
  '#16a34a',
  '#dc2626',
  '#0891b2',
  '#9333ea',
  '#ea580c',
  '#0284c7',
];

const TYPE_BORDER_DARKEN: Record<string, string> = {
  user_self: '#1e3a8a',
  user:      '#3b82f6',
  workspace: '#0f766e',
};

type RiskLevel = 'high' | 'medium' | 'low' | 'stale';

const RISK_COLORS: Record<RiskLevel, { fill: string; border: string }> = {
  high:   { fill: '#ef4444', border: '#b91c1c' },
  medium: { fill: '#f59e0b', border: '#b45309' },
  low:    { fill: '#10b981', border: '#047857' },
  stale:  { fill: '#94a3b8', border: '#475569' },
};

function classifyRisk(score: DealScoreDto | undefined): RiskLevel | null {
  if (!score) return null;
  if (score.unavailable_reason !== null) return 'stale';
  if (score.closure_score === null) return null;
  if (score.closure_score > 70) return 'high';
  if (score.closure_score >= 40) return 'medium';
  return 'low';
}

function buildEntityTypeColorMap(nodes: GraphNodeDto[]): Map<string, string> {
  const map = new Map<string, string>();
  let idx = 0;
  for (const n of nodes) {
    if (n.type === 'entity' && n.entityTypeName && !map.has(n.entityTypeName)) {
      map.set(n.entityTypeName, ENTITY_PALETTE[idx % ENTITY_PALETTE.length]!);
      idx++;
    }
  }
  return map;
}

function nodeColor(node: GraphNodeDto, typeColorMap: Map<string, string>) {
  if (node.type === 'entity' && node.entityTypeName === 'deal') {
    const risk = classifyRisk(dealScores.value.get(node.resourceId));
    if (risk !== null) {
      const palette = RISK_COLORS[risk];
      return {
        background: palette.fill,
        border: palette.border,
        highlight: { background: palette.border, border: '#0f172a' },
        hover: { background: palette.fill, border: '#0f172a' },
      };
    }
  }

  const base = node.type !== 'entity'
    ? TYPE_COLORS[node.type] ?? '#94a3b8'
    : (typeColorMap.get(node.entityTypeName ?? '') ?? '#94a3b8');

  const border = node.type !== 'entity'
    ? (TYPE_BORDER_DARKEN[node.type] ?? '#64748b')
    : base;

  return {
    background: base,
    border,
    highlight: { background: border, border: '#0f172a' },
    hover: { background: base, border: '#0f172a' },
  };
}

const typeLegendItems = computed(() => {
  const items: { label: string; color: string; border?: string }[] = [
    { label: t('graph.nodeType.you'), color: TYPE_COLORS.user_self! },
    { label: t('graph.nodeType.user'), color: TYPE_COLORS.user! },
    { label: t('graph.nodeType.workspace'), color: TYPE_COLORS.workspace! },
  ];
  const typeMap = buildEntityTypeColorMap(graphStore.nodes);
  for (const [name, color] of typeMap) {
    if (name === 'deal') continue;
    items.push({ label: typeDisplayName(name), color });
  }
  const usedTags = new Set(
    graphStore.nodes.map(n => n.highlightTag).filter(Boolean) as string[]
  );
  const addedBorders = new Set<string>();
  for (const tag of ['best_deal', 'worst_deal', 'best_client', 'worst_client'] as const) {
    if (usedTags.has(tag) && !addedBorders.has(HIGHLIGHT[tag].border)) {
      items.push({ label: highlightLabel(tag), color: 'transparent', border: HIGHLIGHT[tag].border });
      addedBorders.add(HIGHLIGHT[tag].border);
    }
  }
  return items;
});

const hasDealNodes = computed(() =>
  graphStore.nodes.some(n => n.type === 'entity' && n.entityTypeName === 'deal'),
);

const riskLegendItems = computed(() => {
  if (!hasDealNodes.value) return [];
  const levels: Array<'high' | 'medium' | 'low'> = ['high', 'medium', 'low'];
  const items = levels.map(level => ({ label: riskLabel(level), color: RISK_COLORS[level].fill }));
  const anyStale = [...dealScores.value.values()].some(s => s.unavailable_reason !== null);
  if (anyStale) items.push({ label: t('graph.risk.scoreUnavailable'), color: RISK_COLORS.stale.fill });
  return items;
});

function typeDisplayName(name: string): string {
  return entityStore.types.find(et => et.name === name)?.displayName
    ?? name.split('_').filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}


const userToWorkspaceIds = computed(() => {
  const map = new Map<number, Set<number>>();
  for (const n of graphStore.nodes) {
    if (n.type === 'user' || n.type === 'user_self') {
      map.set(n.resourceId, new Set<number>());
    }
  }
  const nodeById = new Map(graphStore.nodes.map(n => [n.id, n]));
  for (const e of graphStore.edges) {
    if (e.type !== 'user_workspace') continue;
    const a = nodeById.get(e.from);
    const b = nodeById.get(e.to);
    if (!a || !b) continue;
    const userNode = a.resourceType === 'user' ? a : b.resourceType === 'user' ? b : null;
    const wsNode = a.resourceType === 'workspace' ? a : b.resourceType === 'workspace' ? b : null;
    if (!userNode || !wsNode) continue;
    const set = map.get(userNode.resourceId) ?? new Set<number>();
    set.add(wsNode.resourceId);
    map.set(userNode.resourceId, set);
  }
  return map;
});

const visibleNodeIds = computed<Set<string>>(() => {
  const f = filters.value;
  const hasAnyClientFilter =
    f.managerUserId !== null ||
    f.workspaceId !== null ||
    f.entityTypeNames.length > 0;
  if (!hasAnyClientFilter) {
    return new Set(graphStore.nodes.map(n => n.id));
  }

  const managerScopedWsIds: Set<number> | null =
    f.managerUserId !== null
      ? userToWorkspaceIds.value.get(f.managerUserId) ?? new Set<number>()
      : null;

  const visible = new Set<string>();
  for (const n of graphStore.nodes) {
    if (n.type === 'user_self' || n.type === 'user') {
      if (f.managerUserId === null || n.resourceId === f.managerUserId || n.type === 'user_self') {
        visible.add(n.id);
      }
      continue;
    }

    if (n.type === 'workspace') {
      if (f.workspaceId !== null && n.resourceId !== f.workspaceId) continue;
      if (managerScopedWsIds && !managerScopedWsIds.has(n.resourceId)) continue;
      visible.add(n.id);
      continue;
    }

    if (f.workspaceId !== null && n.workspaceId !== f.workspaceId) continue;
    if (managerScopedWsIds && n.workspaceId !== undefined && !managerScopedWsIds.has(n.workspaceId)) continue;
    if (
      f.entityTypeNames.length > 0 &&
      (!n.entityTypeName || !f.entityTypeNames.includes(n.entityTypeName))
    ) {
      continue;
    }
    visible.add(n.id);
  }
  return visible;
});

const filteredNodes = computed<GraphNodeDto[]>(() =>
  graphStore.nodes.filter(n => visibleNodeIds.value.has(n.id)),
);

const filteredEdges = computed<GraphEdgeDto[]>(() =>
  graphStore.edges.filter(
    e => visibleNodeIds.value.has(e.from) && visibleNodeIds.value.has(e.to),
  ),
);

const managerOptions = computed<FilterPanelOption[]>(() => {
  const fromMembers = orgStore.members
    .map(m => ({
      label: `${m.firstName} ${m.lastName}`.trim() || m.email,
      value: m.userId,
    }))
    .sort((a, b) => a.label.localeCompare(b.label));
  if (fromMembers.length > 0) return fromMembers;

  return graphStore.nodes
    .filter(n => n.type === 'user' || n.type === 'user_self')
    .map(n => ({ label: n.label, value: n.resourceId }))
    .sort((a, b) => a.label.localeCompare(b.label));
});

const workspaceOptions = computed<FilterPanelOption[]>(() =>
  graphStore.nodes
    .filter(n => n.type === 'workspace')
    .map(n => ({ label: n.label, value: n.resourceId }))
    .sort((a, b) => a.label.localeCompare(b.label)),
);

const entityTypeOptions = computed<FilterPanelOption[]>(() => {
  const seen = new Map<string, string>();
  for (const n of graphStore.nodes) {
    if (n.type === 'entity' && n.entityTypeName && !seen.has(n.entityTypeName)) {
      seen.set(n.entityTypeName, typeDisplayName(n.entityTypeName));
    }
  }
  return [...seen.entries()]
    .map(([value, label]) => ({ label, value }))
    .sort((a, b) => a.label.localeCompare(b.label));
});

const canManagerFilter = computed(() => {
  const orgRole = orgStore.currentOrg?.userRole;
  if (orgRole === 'org_owner' || orgRole === 'org_admin') return true;
  return wsStore.workspaces.some(
    w => w.userRole === 'ws_admin' || w.userRole === 'ws_analyst',
  );
});

async function render() {
  await nextTick();
  if (!container.value) return;

  const typeColorMap = buildEntityTypeColorMap(graphStore.nodes);

  const visNodes = filteredNodes.value.map(n => {
    const hl = n.highlightTag ? HIGHLIGHT[n.highlightTag] : null;
    const baseColor = nodeColor(n, typeColorMap);
    return {
      id: n.id,
      label: n.label,
      title: n.subtitle ? `${n.label}\n${n.subtitle}` : n.label,
      color: hl
        ? { ...baseColor, border: hl.border, highlight: { ...baseColor.highlight, border: hl.border } }
        : baseColor,
      size: hl ? 22 : (n.type === 'user_self' ? 24 : 18),
      borderWidth: hl ? 4 : (n.type === 'user_self' ? 3 : 2),
      font: { size: 13, face: 'Inter, sans-serif', color: '#0f172a', strokeWidth: 0, vadjust: 4 },
      shadow: hl
        ? { enabled: true, color: hl.shadow, size: 14, x: 0, y: 0 }
        : { enabled: true, color: 'rgba(0,0,0,0.10)', size: 6, x: 0, y: 2 },
    };
  });

  const visEdges = filteredEdges.value.map(e => ({
    id: e.id,
    from: e.from,
    to: e.to,
    label: e.label ?? undefined,
    dashes: e.type === 'entity_entity',
    color: { color: edgeColor(e.type), highlight: '#64748b', hover: '#475569' },
    font: { size: 10, color: '#64748b', strokeWidth: 0 },
    arrows: e.type === 'entity_entity' ? { to: { enabled: true, scaleFactor: 0.5 } } : undefined,
    width: 1.5,
  }));

  if (network.value) network.value.destroy();

  const options: Options = {
    physics: {
      enabled: true,
      stabilization: { iterations: 150, updateInterval: 25 },
      barnesHut: { gravitationalConstant: -8000, centralGravity: 0.3, springLength: 120 },
    },
    nodes: { shape: 'dot', borderWidthSelected: 3 },
    edges: { smooth: { enabled: true, type: 'dynamic', roundness: 0.5 } },
    interaction: { hover: true, tooltipDelay: 300 },
  };

  network.value = new Network(container.value, { nodes: visNodes, edges: visEdges }, options);

  network.value.on('click', (params) => {
    if (params.nodes.length === 1) {
      const nodeId = params.nodes[0] as string;
      selectedNode.value = graphStore.nodes.find(n => n.id === nodeId) ?? null;
    } else {
      selectedNode.value = null;
    }
  });
}

watch(visibleNodeIds, () => {
  if (graphStore.nodes.length === 0) return;
  if (selectedNode.value && !visibleNodeIds.value.has(selectedNode.value.id)) {
    selectedNode.value = null;
  }
  render();
});

function edgeColor(type: string): string {
  switch (type) {
    case 'user_workspace': return '#93c5fd';
    case 'workspace_entity': return '#cbd5e1';
    case 'entity_entity': return '#94a3b8';
    case 'user_user': return '#bfdbfe';
    default: return '#e2e8f0';
  }
}

async function loadDealScores() {
  const allDealIds = graphStore.nodes
    .filter(n => n.type === 'entity' && n.entityTypeName === 'deal')
    .map(n => n.resourceId);

  if (allDealIds.length === 0) {
    dealScores.value = new Map();
    return;
  }

  const missing = allDealIds.filter(id => !dealScores.value.has(id));
  if (missing.length === 0) return;

  try {
    const results = await mlApi.scoreBatch(missing);
    const updated = new Map(dealScores.value);
    for (const r of results) updated.set(r.entity_id, r);
    dealScores.value = updated;
  } catch {
    // keep existing scores on error rather than clearing them
  }
}

async function load() {
  if (!orgId.value) return;
  selectedNode.value = null;
  await graphStore.fetchGraph(orgId.value, filters.value.risk);
  if (graphStore.error) return;
  await loadDealScores();
  await render();
}

let _suppressRiskWatch = false;

watch(orgId, () => {
  _suppressRiskWatch = true;
  dealScores.value = new Map();
  filters.value = {
    risk: null,
    managerUserId: null,
    workspaceId: null,
    entityTypeNames: [],
  };
  load();
});

watch(() => filters.value.risk, () => {
  if (_suppressRiskWatch) { _suppressRiskWatch = false; return; }
  load();
});

onMounted(() => {
  load();
  if (orgStore.currentOrgId && orgStore.members.length === 0) {
    orgStore.fetchMembers().catch(() => {
 });
  }
  if (orgStore.currentOrgId && wsStore.workspaces.length === 0) {
    wsStore.fetchWorkspaces(orgStore.currentOrgId).catch(() => {
 });
  }
});

onUnmounted(() => {
  network.value?.destroy();
  network.value = null;
});

function typeBadge(node: GraphNodeDto): string {
  if (node.type === 'user_self') return t('graph.nodeType.you');
  if (node.type === 'user') return t('graph.nodeType.user');
  if (node.type === 'workspace') return t('graph.nodeType.workspace');
  return node.entityTypeName ? typeDisplayName(node.entityTypeName) : t('graph.nodeType.entity');
}

function selectedScore(): DealScoreDto | undefined {
  const n = selectedNode.value;
  if (!n || n.type !== 'entity' || n.entityTypeName !== 'deal') return undefined;
  return dealScores.value.get(n.resourceId);
}

const selectedClosure = computed(() => {
  const s = selectedScore();
  return s && s.closure_score !== null ? Math.round(s.closure_score) : null;
});

const selectedChurn = computed(() => {
  const s = selectedScore();
  return s && s.churn_score !== null ? Math.round(s.churn_score) : null;
});

const selectedScoreUnavailable = computed(() => {
  const s = selectedScore();
  return s?.unavailable_reason ?? null;
});

const selectedIsDeal = computed(() =>
  selectedNode.value?.type === 'entity' && selectedNode.value.entityTypeName === 'deal',
);

function churnBadgeClass(score: number): string {
  if (score > 70) return 'bg-red-50 text-red-700 ring-1 ring-inset ring-red-200';
  if (score >= 40) return 'bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200';
  return 'bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200';
}

function closureBarColor(score: number): string {
  if (score > 70) return RISK_COLORS.low.fill;
  if (score >= 40) return RISK_COLORS.medium.fill;
  return RISK_COLORS.high.fill;
}

function viewNode(node: GraphNodeDto) {
  if (node.resourceType === 'entity') {
    router.push({
      name: 'workspace-entities',
      params: { workspaceId: String(node.workspaceId) },
      query: { id: String(node.resourceId), entityType: node.entityTypeName },
    });
  } else if (node.resourceType === 'workspace') {
    router.push({ name: 'workspace-members', params: { workspaceId: String(node.resourceId) } });
  } else {
    router.push({ name: 'member', params: { memberUserId: String(node.resourceId) } });
  }
}

function editNode(node: GraphNodeDto) {
  if (node.resourceType === 'entity') {
    router.push({
      name: 'workspace-entities',
      params: { workspaceId: String(node.workspaceId) },
      query: { id: String(node.resourceId), action: 'edit', entityType: node.entityTypeName },
    });
  } else {
    router.push({ name: 'member', params: { memberUserId: String(node.resourceId) } });
  }
}

function requestDelete(node: GraphNodeDto) {
  confirm.require({
    message: t('graph.deleteMessage'),
    header: t('graph.deleteHeader'),
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
    acceptProps: { label: t('common.delete'), severity: 'danger' },
    accept: async () => {
      try {
        await entityStore.archive(node.workspaceId!, node.resourceId);
        toast.add({ severity: 'success', summary: t('graph.deleted'), life: 2500 });
        selectedNode.value = null;
        await load();
      } catch {
        toast.add({ severity: 'error', summary: t('graph.deleteFailed'), detail: t('graph.deleteFailedDetail'), life: 4000 });
      }
    },
  });
}

const hasGraph = computed(() => graphStore.nodes.length > 0);
const hasFilteredGraph = computed(() => filteredNodes.value.length > 0);
const hasAnyActiveFilter = computed(() => {
  const f = filters.value;
  return (
    f.risk !== null ||
    f.managerUserId !== null ||
    f.workspaceId !== null ||
    f.entityTypeNames.length > 0
  );
});

function resetAllFilters() {
  filters.value = {
    risk: null,
    managerUserId: null,
    workspaceId: null,
    entityTypeNames: [],
  };
}
</script>

<template>
  <ConfirmDialog />
  <section class="flex flex-col gap-4 h-[calc(100vh-7rem)]">

    
    <div class="flex items-start justify-between gap-4 shrink-0">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">{{ t('graph.title') }}</h1>
        <i18n-t keypath="graph.subtitle" tag="p" class="mt-3 text-sm text-ink-500" scope="global">
          <template #org>
            <span class="font-semibold text-brand-600">{{ orgStore.currentOrg?.name ?? t('graph.thisOrg') }}</span>
          </template>
        </i18n-t>
      </div>
      <div class="text-xs text-ink-500 shrink-0 pt-1">
        {{ t('graph.counts', { nodes: graphStore.nodeCount, edges: graphStore.edgeCount }) }}
      </div>
    </div>

    
    <FilterPanel
      v-if="orgId"
      v-model="filters"
      :disabled="graphStore.isLoading"
      :visible-count="filteredNodes.length"
      :total-count="graphStore.nodeCount"
      :manager-options="managerOptions"
      :workspace-options="workspaceOptions"
      :entity-type-options="entityTypeOptions"
      :can-manager-filter="canManagerFilter"
    />

    
    <Message v-if="!orgId" severity="info" :closable="false" class="!my-0">
      {{ t('graph.selectOrg') }}
    </Message>

    
    <div
      v-else-if="graphStore.error"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-exclamation-triangle text-3xl text-ink-300" />
      <p class="mt-3 text-sm font-medium text-ink-700">{{ t('graph.unavailable') }}</p>
      <p class="mt-1 text-xs text-ink-500 max-w-md">
        {{ graphStore.error }}
      </p>
      <Button
        :label="t('common.retry')"
        icon="pi pi-refresh"
        severity="secondary"
        size="small"
        class="mt-4"
        :loading="graphStore.isLoading"
        @click="load"
      />
    </div>

    
    <GraphSkeleton
      v-else-if="graphStore.isLoading && !hasGraph"
      class="flex-1"
      fill
      :label="t('graph.loading')"
    />

    
    <div
      v-else-if="!graphStore.isLoading && !hasGraph"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-share-alt text-4xl text-ink-300" />
      <template v-if="hasAnyActiveFilter">
        <p class="mt-3 text-sm font-medium text-ink-700">{{ t('graph.noMatchFilters') }}</p>
        <p class="mt-1 text-xs text-ink-500 max-w-md">
          {{ t('graph.relaxFilters') }}
        </p>
        <Button
          :label="t('graph.clearAllFilters')"
          icon="pi pi-times"
          severity="secondary"
          size="small"
          class="mt-4"
          @click="resetAllFilters"
        />
      </template>
      <p v-else class="mt-3 text-sm text-ink-500">
        {{ t('graph.noDataYet') }}
      </p>
    </div>

    
    <div
      v-else-if="hasGraph && !hasFilteredGraph"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-filter-slash text-4xl text-ink-300" />
      <p class="mt-3 text-sm font-medium text-ink-700">{{ t('graph.noMatchFilters') }}</p>
      <p class="mt-1 text-xs text-ink-500 max-w-md">
        {{ t('graph.narrowFilters') }}
      </p>
      <Button
        :label="t('graph.clearAllFilters')"
        icon="pi pi-times"
        severity="secondary"
        size="small"
        class="mt-4"
        @click="resetAllFilters"
      />
    </div>

    
    <div v-else class="flex gap-3 flex-1 min-h-0">

      
      <div class="flex-1 flex flex-col min-w-0 gap-2">
        
        <div class="flex flex-wrap items-center gap-x-5 gap-y-2 shrink-0">
          <div class="flex flex-wrap gap-3">
            <div
              v-for="item in typeLegendItems"
              :key="`type-${item.label}`"
              class="flex items-center gap-1.5 text-xs text-ink-500"
            >
              <span
                class="w-3 h-3 rounded-full shrink-0"
                :style="{
                  backgroundColor: item.color,
                  border: item.border ? `2px solid ${item.border}` : undefined,
                  boxShadow: item.border ? `0 0 5px ${item.border}` : undefined,
                }"
              />
              {{ item.label }}
            </div>
          </div>
          <div
            v-if="riskLegendItems.length > 0"
            class="flex items-center gap-3 pl-5 border-l border-line"
          >
            <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
              {{ t('graph.dealRisk') }}
            </span>
            <div
              v-for="item in riskLegendItems"
              :key="`risk-${item.label}`"
              class="flex items-center gap-1.5 text-xs text-ink-500"
            >
              <span
                class="w-3 h-3 rounded-full shrink-0"
                :style="{ backgroundColor: item.color }"
              />
              {{ item.label }}
            </div>
          </div>
        </div>

        <div
          ref="container"
          class="graph-host flex-1 rounded-xl border border-line bg-white"
        />
      </div>

      
      <Transition name="panel">
        <div
          v-if="selectedNode"
          class="w-64 shrink-0 rounded-xl border border-line bg-white p-4 flex flex-col gap-3 overflow-y-auto"
        >
          
          <div class="flex items-center justify-between">
            <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-brand-50 text-brand-700 border border-brand-100">
              {{ typeBadge(selectedNode) }}
            </span>
            <button
              type="button"
              class="text-ink-400 hover:text-ink-700"
              @click="selectedNode = null"
            >
              <i class="pi pi-times text-xs" />
            </button>
          </div>

          
          <div>
            <p class="text-sm font-semibold text-ink-900 leading-snug">{{ selectedNode.label }}</p>
            <p v-if="selectedNode.subtitle" class="mt-0.5 text-xs text-ink-400 truncate">
              {{ selectedNode.subtitle }}
            </p>
          </div>

          
          <div
            v-if="selectedIsDeal"
            class="rounded-lg border border-line bg-surface/40 p-3 flex flex-col gap-2.5"
          >
            <div v-if="selectedClosure !== null">
              <div class="flex items-center justify-between mb-1">
                <span class="text-[11px] font-medium text-ink-500 uppercase tracking-wide">
                  {{ t('graph.closureScore') }}
                </span>
                <span class="text-xs font-semibold text-ink-800">{{ selectedClosure }}%</span>
              </div>
              <ProgressBar
                :value="selectedClosure"
                :show-value="false"
                :pt="{ value: { class: 'transition-none', style: `background-color: ${closureBarColor(selectedClosure)}` } }"
                class="!h-1.5 !bg-slate-100"
              />
            </div>

            <div v-if="selectedChurn !== null" class="flex items-center justify-between">
              <span class="text-[11px] font-medium text-ink-500 uppercase tracking-wide">
                {{ t('graph.churnScore') }}
              </span>
              <span
                class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold"
                :class="churnBadgeClass(selectedChurn)"
              >
                {{ selectedChurn }}%
              </span>
            </div>

            <p
              v-if="selectedClosure === null && selectedChurn === null"
              class="text-xs text-ink-400 italic"
            >
              {{ selectedScoreUnavailable ?? t('graph.scoresUnavailable') }}
            </p>
          </div>

          
          <div class="flex flex-col gap-2 mt-auto pt-2 border-t border-slate-100">
            <Button
              :label="t('graph.view')"
              icon="pi pi-eye"
              size="small"
              class="w-full"
              @click="viewNode(selectedNode)"
            />
            <Button
              v-if="selectedNode.permissions.includes('edit')"
              :label="t('common.edit')"
              icon="pi pi-pencil"
              size="small"
              outlined
              class="w-full"
              @click="editNode(selectedNode)"
            />
            <Button
              v-if="selectedNode.permissions.includes('delete') && selectedNode.type === 'entity'"
              :label="t('common.delete')"
              icon="pi pi-trash"
              size="small"
              severity="danger"
              outlined
              class="w-full"
              @click="requestDelete(selectedNode)"
            />
          </div>
        </div>
      </Transition>
    </div>

  </section>
</template>

<style scoped>
.graph-host {
  background-image: radial-gradient(rgba(37, 99, 235, 0.06) 1px, transparent 1px);
  background-size: 16px 16px;
}

.panel-enter-active,
.panel-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.panel-enter-from,
.panel-leave-to {
  opacity: 0;
  transform: translateX(8px);
}
</style>
