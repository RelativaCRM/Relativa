<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue';
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

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const selectedNode = ref<GraphNodeDto | null>(null);
const dealScores = ref<Map<number, DealScoreDto>>(new Map());
const riskFilter = ref<GraphRiskLevel | null>(null);

// ── Combined filter state ────────────────────────────────────────────────────
// `risk` re-issues the GET /graph request (server-side filter); the rest are
// applied client-side over the returned node/edge set so we don't need new
// query params. Reset granularity is per-filter via FilterPanel.
const filters = ref<FilterPanelState>({
  risk: null,
  managerUserId: null,
  workspaceId: null,
  entityTypeNames: [],
});

const orgId = computed(() => orgStore.currentOrgId);

// ── Highlight palette ────────────────────────────────────────────────────────
const HIGHLIGHT: Record<GraphHighlightTag, { border: string; shadow: string; label: string }> = {
  best_deal:    { border: '#16a34a', shadow: 'rgba(22,163,74,0.55)',  label: 'Best deal (top 20%)' },
  worst_deal:   { border: '#dc2626', shadow: 'rgba(220,38,38,0.55)',  label: 'Worst deal (bottom 20%)' },
  best_client:  { border: '#16a34a', shadow: 'rgba(22,163,74,0.55)',  label: 'Best client (top 20%)' },
  worst_client: { border: '#dc2626', shadow: 'rgba(220,38,38,0.55)',  label: 'Worst client (bottom 20%)' },
};

// ── Color palette ────────────────────────────────────────────────────────────
const TYPE_COLORS: Record<string, string> = {
  user_self: '#1d4ed8',  // brand-700
  user:      '#93c5fd',  // brand-300
  workspace: '#0d9488',  // teal-600
};

const ENTITY_PALETTE = [
  '#7c3aed', // violet-600
  '#d97706', // amber-600
  '#16a34a', // green-600
  '#dc2626', // red-600
  '#0891b2', // cyan-600
  '#9333ea', // purple-600
  '#ea580c', // orange-600
  '#0284c7', // sky-600
];

const TYPE_BORDER_DARKEN: Record<string, string> = {
  user_self: '#1e3a8a',
  user:      '#3b82f6',
  workspace: '#0f766e',
};

// ── Risk palette (deal nodes) ────────────────────────────────────────────────
// Same hues as the dashboard risk-distribution doughnut (red-500/amber-500/emerald-500)
// plus a slate-400 "stale" tone for nodes whose score is unavailable.
type RiskLevel = 'high' | 'medium' | 'low' | 'stale';

const RISK_COLORS: Record<RiskLevel, { fill: string; border: string; label: string }> = {
  high:   { fill: '#ef4444', border: '#b91c1c', label: 'High risk' },     // red-500 / red-700
  medium: { fill: '#f59e0b', border: '#b45309', label: 'Medium risk' },   // amber-500 / amber-700
  low:    { fill: '#10b981', border: '#047857', label: 'Low risk' },      // emerald-500 / emerald-700
  stale:  { fill: '#94a3b8', border: '#475569', label: 'Score unavailable' }, // slate-400 / slate-600
};

// Closure-score thresholds expressed on the 0–100 scale used by the ML service.
// Task spec: red>0.7, yellow 0.4–0.7, green<0.4 → mapped to 70 and 40.
function classifyRisk(score: DealScoreDto | undefined): RiskLevel | null {
  if (!score) return null;
  if (score.unavailable_reason !== null) return 'stale';
  if (score.closure_score === null) return null;
  if (score.closure_score > 70) return 'high';
  if (score.closure_score >= 40) return 'medium';
  return 'low';
}

// Built at render time — no color is tied to any entity type name in source
function buildEntityTypeColorMap(nodes: GraphNodeDto[]): Map<string, string> {
  const map = new Map<string, string>();
  let idx = 0;
  for (const n of nodes) {
    if (n.type === 'entity' && n.entityTypeName && !map.has(n.entityTypeName)) {
      map.set(n.entityTypeName, ENTITY_PALETTE[idx % ENTITY_PALETTE.length]);
      idx++;
    }
  }
  return map;
}

function nodeColor(node: GraphNodeDto, typeColorMap: Map<string, string>) {
  // Deals get risk-based coloring from ML closure score; everything else uses
  // the type palette. When a deal has no score yet (request pending, never
  // scored, or non-applicable status) we fall back to the entity palette so
  // the canvas isn't dominated by grey before scores arrive.
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

// ── Legend data ──────────────────────────────────────────────────────────────
const typeLegendItems = computed(() => {
  const items: { label: string; color: string; border?: string }[] = [
    { label: 'You', color: TYPE_COLORS.user_self },
    { label: 'User', color: TYPE_COLORS.user },
    { label: 'Workspace', color: TYPE_COLORS.workspace },
  ];
  const typeMap = buildEntityTypeColorMap(graphStore.nodes);
  for (const [name, color] of typeMap) {
    // The deal palette slot is owned by the risk legend; skip it from the type row
    // to avoid showing a misleading purple/violet "deal" swatch.
    if (name === 'deal') continue;
    items.push({ label: formatTypeName(name), color });
  }
  // Append highlight legend entries when any highlighted nodes exist
  const usedTags = new Set(
    graphStore.nodes.map(n => n.highlightTag).filter(Boolean) as string[]
  );
  const addedBorders = new Set<string>();
  for (const tag of ['best_deal', 'worst_deal', 'best_client', 'worst_client'] as const) {
    if (usedTags.has(tag) && !addedBorders.has(HIGHLIGHT[tag].border)) {
      items.push({ label: HIGHLIGHT[tag].label, color: 'transparent', border: HIGHLIGHT[tag].border });
      addedBorders.add(HIGHLIGHT[tag].border);
    }
  }
  return items;
});

// Risk legend is shown only when at least one deal node is present.
const hasDealNodes = computed(() =>
  graphStore.nodes.some(n => n.type === 'entity' && n.entityTypeName === 'deal'),
);

const riskLegendItems = computed(() => {
  if (!hasDealNodes.value) return [];
  const levels: RiskLevel[] = ['high', 'medium', 'low'];
  const items = levels.map(level => ({ label: RISK_COLORS[level].label, color: RISK_COLORS[level].fill }));
  // Surface the stale swatch only when at least one deal actually has an unavailable score
  const anyStale = [...dealScores.value.values()].some(s => s.unavailable_reason !== null);
  if (anyStale) items.push({ label: RISK_COLORS.stale.label, color: RISK_COLORS.stale.fill });
  return items;
});

function formatTypeName(name: string): string {
  return name.split('_').filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

// ── Client-side filter derivation ────────────────────────────────────────────
// Manager / workspace / entity-type filters are applied here rather than
// re-fetching the graph: the backend currently only exposes ?riskLevel=, and
// we don't want a round-trip on every dropdown click. The risk filter goes
// through the store fetch (re-issues GET /graph) — see load() below.

// Adjacency between users and workspaces, derived from `user_workspace` edges.
// Used so the "Manager" dropdown can scope the canvas to a single user's
// workspaces + the entities those workspaces hold.
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

// Visible node id-set after applying manager + workspace + entity-type filters.
// Risk filter is applied server-side, so its trimming is already baked into
// `graphStore.nodes` by the time this runs.
const visibleNodeIds = computed<Set<string>>(() => {
  const f = filters.value;
  const hasAnyClientFilter =
    f.managerUserId !== null ||
    f.workspaceId !== null ||
    f.entityTypeNames.length > 0;
  if (!hasAnyClientFilter) {
    return new Set(graphStore.nodes.map(n => n.id));
  }

  // The selected manager's workspaces define the in-scope workspace set when
  // a manager is picked; otherwise every workspace is in scope.
  const managerScopedWsIds: Set<number> | null =
    f.managerUserId !== null
      ? userToWorkspaceIds.value.get(f.managerUserId) ?? new Set<number>()
      : null;

  const visible = new Set<string>();
  for (const n of graphStore.nodes) {
    if (n.type === 'user_self' || n.type === 'user') {
      // Keep the selected manager's own node (and self) so the canvas isn't
      // just a floating cloud of entities.
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

    // entity
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

// ── Filter panel option lists ────────────────────────────────────────────────
const managerOptions = computed<FilterPanelOption[]>(() => {
  // Source: org members with `ws_manager` role at least somewhere. Falls back
  // to "users present in the graph as nodes" when membership data isn't loaded
  // yet, so the dropdown is never empty on first render.
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
      seen.set(n.entityTypeName, formatTypeName(n.entityTypeName));
    }
  }
  return [...seen.entries()]
    .map(([value, label]) => ({ label, value }))
    .sort((a, b) => a.label.localeCompare(b.label));
});

// Manager dropdown is gated to ws_admin / ws_analyst (matches the parent
// task's spec). Org owners/admins implicitly satisfy this since they hold
// admin-equivalent visibility across workspaces.
const canManagerFilter = computed(() => {
  const orgRole = orgStore.currentOrg?.userRole;
  if (orgRole === 'org_owner' || orgRole === 'org_admin') return true;
  return wsStore.workspaces.some(
    w => w.userRole === 'ws_admin' || w.userRole === 'ws_analyst',
  );
});

// ── Render ───────────────────────────────────────────────────────────────────
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

// Re-render the canvas whenever the client-side filter set changes, but stay
// quiet until the initial graph fetch lands (no canvas → render() exits early
// anyway, but skipping the call keeps Vue's reactivity graph cleaner).
watch(visibleNodeIds, () => {
  if (graphStore.nodes.length === 0) return;
  // Drop selection if the previously-selected node is no longer visible —
  // otherwise the side panel would hover over a filtered-out record.
  if (selectedNode.value && !visibleNodeIds.value.has(selectedNode.value.id)) {
    selectedNode.value = null;
  }
  render();
});

function edgeColor(type: string): string {
  switch (type) {
    case 'user_workspace': return '#93c5fd'; // brand-300
    case 'workspace_entity': return '#cbd5e1'; // slate-300
    case 'entity_entity': return '#94a3b8'; // slate-400
    case 'user_user': return '#bfdbfe'; // brand-200
    default: return '#e2e8f0';
  }
}

// ── Load ─────────────────────────────────────────────────────────────────────
async function loadDealScores() {
  const dealIds = graphStore.nodes
    .filter(n => n.type === 'entity' && n.entityTypeName === 'deal')
    .map(n => n.resourceId);

  if (dealIds.length === 0) {
    dealScores.value = new Map();
    return;
  }

  try {
    const results = await mlApi.scoreBatch(dealIds);
    const map = new Map<number, DealScoreDto>();
    for (const r of results) map.set(r.entity_id, r);
    dealScores.value = map;
  } catch {
    // Soft-fail: graph is still usable without risk colors. The http layer toasts
    // the error already; the canvas keeps the type palette as a safe fallback.
    dealScores.value = new Map();
  }
}

async function load() {
  if (!orgId.value) return;
  selectedNode.value = null;
  dealScores.value = new Map();
  await graphStore.fetchGraph(orgId.value, filters.value.risk);
  if (graphStore.error) return;
  await loadDealScores();
  await render();
}

watch(orgId, () => {
  // Reset every filter when the org switches — keeping a stale predicate would
  // silently apply it to a fresh dataset and could render an empty graph for
  // reasons the user can't see.
  filters.value = {
    risk: null,
    managerUserId: null,
    workspaceId: null,
    entityTypeNames: [],
  };
  load();
});

// Risk re-fetches (server-side filter). The other filters are derived locally
// from `graphStore.nodes` so they don't pay a round-trip.
watch(() => filters.value.risk, () => { load(); });

onMounted(() => {
  load();
  // Background-load org members so the Manager dropdown is populated when an
  // admin/analyst opens the graph for the first time. We don't await — the
  // dropdown falls back to graph user nodes until membership arrives.
  if (orgStore.currentOrgId && orgStore.members.length === 0) {
    orgStore.fetchMembers().catch(() => { /* silently fall back */ });
  }
  // Workspace list is needed to gate `canManagerFilter` correctly outside the
  // workspace-scoped routes (the Graph view is org-level).
  if (orgStore.currentOrgId && wsStore.workspaces.length === 0) {
    wsStore.fetchWorkspaces(orgStore.currentOrgId).catch(() => { /* ignore */ });
  }
});

onUnmounted(() => {
  network.value?.destroy();
  network.value = null;
});

// ── Node actions ─────────────────────────────────────────────────────────────
function typeBadge(node: GraphNodeDto): string {
  if (node.type === 'user_self') return 'You';
  if (node.type === 'user') return 'User';
  if (node.type === 'workspace') return 'Workspace';
  return node.entityTypeName ? formatTypeName(node.entityTypeName) : 'Entity';
}

// ── Selected-node ML data (deal nodes only) ──────────────────────────────────
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

// Tailwind classes for the churn badge. We pick a tone from the same red/amber/emerald
// risk family so the panel reads consistently with the legend.
function churnBadgeClass(score: number): string {
  if (score > 70) return 'bg-red-50 text-red-700 ring-1 ring-inset ring-red-200';
  if (score >= 40) return 'bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200';
  return 'bg-emerald-50 text-emerald-700 ring-1 ring-inset ring-emerald-200';
}

// PrimeVue's ProgressBar value prop colors the fill via the primary theme variable, so we
// recolor it inline to match the closure-score risk tier — keeps the bar legible without
// needing a custom CSS variable per tier.
function closureBarColor(score: number): string {
  if (score > 70) return RISK_COLORS.high.fill;
  if (score >= 40) return RISK_COLORS.medium.fill;
  return RISK_COLORS.low.fill;
}

function viewNode(node: GraphNodeDto) {
  if (node.resourceType === 'entity') {
    router.push({
      name: 'workspace-entities',
      params: { workspaceId: String(node.workspaceId) },
      query: { id: String(node.resourceId) },
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
      query: { id: String(node.resourceId), action: 'edit' },
    });
  } else {
    router.push({ name: 'member', params: { memberUserId: String(node.resourceId) } });
  }
}

function requestDelete(node: GraphNodeDto) {
  confirm.require({
    message: 'Delete this entity? It will be hidden from lists; linked records remain in the workspace.',
    header: 'Delete entity',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try {
        await entityStore.archive(node.workspaceId!, node.resourceId);
        toast.add({ severity: 'success', summary: 'Entity deleted', life: 2500 });
        selectedNode.value = null;
        await load();
      } catch {
        toast.add({ severity: 'error', summary: 'Delete failed', detail: 'Could not delete the entity.', life: 4000 });
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

    <!-- Header -->
    <div class="flex items-start justify-between gap-4 shrink-0">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">Graph</h1>
        <p class="mt-3 text-sm text-ink-500">
          Your full relational context in
          <span class="font-semibold text-brand-600">{{ orgStore.currentOrg?.name ?? 'this organization' }}</span>.
          Click a node to view or manage it.
        </p>
      </div>
      <div class="text-xs text-ink-500 shrink-0 pt-1">
        {{ graphStore.nodeCount }} nodes · {{ graphStore.edgeCount }} edges
      </div>
    </div>

    <!-- Combined filter panel -->
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

    <!-- No org -->
    <Message v-if="!orgId" severity="info" :closable="false" class="!my-0">
      Select an organization to see your graph.
    </Message>

    <!-- Error: graph data could not be loaded. Show retry so the user can recover
         without reloading the whole page. -->
    <div
      v-else-if="graphStore.error"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-exclamation-triangle text-3xl text-ink-300" />
      <p class="mt-3 text-sm font-medium text-ink-700">Graph data is unavailable</p>
      <p class="mt-1 text-xs text-ink-500 max-w-md">
        {{ graphStore.error }}
      </p>
      <Button
        label="Try again"
        icon="pi pi-refresh"
        severity="secondary"
        size="small"
        class="mt-4"
        :loading="graphStore.isLoading"
        @click="load"
      />
    </div>

    <!-- Loading -->
    <GraphSkeleton
      v-else-if="graphStore.isLoading && !hasGraph"
      class="flex-1"
      fill
      label="Loading graph…"
    />

    <!-- Empty -->
    <div
      v-else-if="!graphStore.isLoading && !hasGraph"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-share-alt text-4xl text-ink-300" />
      <template v-if="hasAnyActiveFilter">
        <p class="mt-3 text-sm font-medium text-ink-700">No nodes match the active filters.</p>
        <p class="mt-1 text-xs text-ink-500 max-w-md">
          Clear or relax the filters to see more of the graph.
        </p>
        <Button
          label="Clear all filters"
          icon="pi pi-times"
          severity="secondary"
          size="small"
          class="mt-4"
          @click="resetAllFilters"
        />
      </template>
      <p v-else class="mt-3 text-sm text-ink-500">
        No data available yet. Add workspaces and entities to populate the graph.
      </p>
    </div>

    <!-- Filtered-empty (graph has data, but all client-side filters together
         returned zero nodes — the backend risk filter case is already covered
         above when the server returns zero nodes). -->
    <div
      v-else-if="hasGraph && !hasFilteredGraph"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white p-6 text-center"
    >
      <i class="pi pi-filter-slash text-4xl text-ink-300" />
      <p class="mt-3 text-sm font-medium text-ink-700">No nodes match the active filters.</p>
      <p class="mt-1 text-xs text-ink-500 max-w-md">
        Try removing a filter or widening the selection. The current combination is too narrow.
      </p>
      <Button
        label="Clear all filters"
        icon="pi pi-times"
        severity="secondary"
        size="small"
        class="mt-4"
        @click="resetAllFilters"
      />
    </div>

    <!-- Graph + panel -->
    <div v-else class="flex gap-3 flex-1 min-h-0">

      <!-- Canvas -->
      <div class="flex-1 flex flex-col min-w-0 gap-2">
        <!-- Legend -->
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
              Deal risk
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

      <!-- Node detail panel -->
      <Transition name="panel">
        <div
          v-if="selectedNode"
          class="w-64 shrink-0 rounded-xl border border-line bg-white p-4 flex flex-col gap-3 overflow-y-auto"
        >
          <!-- Type badge -->
          <div class="flex items-center justify-between">
            <span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-brand-50 text-brand-700">
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

          <!-- Label + subtitle -->
          <div>
            <p class="text-sm font-semibold text-ink-900 leading-snug">{{ selectedNode.label }}</p>
            <p v-if="selectedNode.subtitle" class="mt-0.5 text-xs text-ink-400 truncate">
              {{ selectedNode.subtitle }}
            </p>
          </div>

          <!-- ML scores (deals only) -->
          <div
            v-if="selectedIsDeal"
            class="rounded-lg border border-line bg-surface/40 p-3 flex flex-col gap-2.5"
          >
            <div v-if="selectedClosure !== null">
              <div class="flex items-center justify-between mb-1">
                <span class="text-[11px] font-medium text-ink-500 uppercase tracking-wide">
                  Closure score
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
                Churn score
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
              {{ selectedScoreUnavailable ?? 'Scores not available yet.' }}
            </p>
          </div>

          <!-- Actions -->
          <div class="flex flex-col gap-2 mt-auto pt-2 border-t border-slate-100">
            <Button
              label="View"
              icon="pi pi-eye"
              size="small"
              class="w-full"
              @click="viewNode(selectedNode)"
            />
            <Button
              v-if="selectedNode.permissions.includes('edit')"
              label="Edit"
              icon="pi pi-pencil"
              size="small"
              outlined
              class="w-full"
              @click="editNode(selectedNode)"
            />
            <Button
              v-if="selectedNode.permissions.includes('delete') && selectedNode.type === 'entity'"
              label="Delete"
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
