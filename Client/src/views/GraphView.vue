<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ConfirmDialog from 'primevue/confirmdialog';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';
import { Network, type Options } from 'vis-network/standalone';
import { useGraphStore } from '@/stores/graph';
import { useEntityStore } from '@/stores/entity';
import { useOrganizationStore } from '@/stores/organization';
import type { GraphNodeDto } from '@/api/graph';

const router = useRouter();
const graphStore = useGraphStore();
const entityStore = useEntityStore();
const orgStore = useOrganizationStore();
const confirm = useConfirm();
const toast = useToast();

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const selectedNode = ref<GraphNodeDto | null>(null);

const orgId = computed(() => orgStore.currentOrgId);

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
const legendItems = computed(() => {
  const items: { label: string; color: string }[] = [
    { label: 'You', color: TYPE_COLORS.user_self },
    { label: 'User', color: TYPE_COLORS.user },
    { label: 'Workspace', color: TYPE_COLORS.workspace },
  ];
  const typeMap = buildEntityTypeColorMap(graphStore.nodes);
  for (const [name, color] of typeMap) {
    items.push({ label: formatTypeName(name), color });
  }
  return items;
});

function formatTypeName(name: string): string {
  return name.split('_').filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

// ── Render ───────────────────────────────────────────────────────────────────
async function render() {
  await nextTick();
  if (!container.value) return;

  const typeColorMap = buildEntityTypeColorMap(graphStore.nodes);

  const visNodes = graphStore.nodes.map(n => ({
    id: n.id,
    label: n.label,
    title: n.subtitle ? `${n.label}\n${n.subtitle}` : n.label,
    color: nodeColor(n, typeColorMap),
    size: n.type === 'user_self' ? 24 : 18,
    borderWidth: n.type === 'user_self' ? 3 : 2,
    font: { size: 13, face: 'Inter, sans-serif', color: '#0f172a', strokeWidth: 0, vadjust: 4 },
    shadow: { enabled: true, color: 'rgba(0,0,0,0.10)', size: 6, x: 0, y: 2 },
  }));

  const visEdges = graphStore.edges.map(e => ({
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
async function load() {
  if (!orgId.value) return;
  selectedNode.value = null;
  await graphStore.fetchGraph(orgId.value);
  if (!graphStore.error) await render();
}

watch(orgId, load);

onMounted(() => { load(); });

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

    <!-- No org -->
    <Message v-if="!orgId" severity="info" :closable="false" class="!my-0">
      Select an organization to see your graph.
    </Message>

    <!-- Error -->
    <Message v-else-if="graphStore.error" severity="error" :closable="false" class="!my-0">
      {{ graphStore.error }}
    </Message>

    <!-- Loading -->
    <div v-else-if="graphStore.isLoading && !hasGraph" class="flex-1 flex items-center justify-center text-ink-400">
      <i class="pi pi-spin pi-spinner text-2xl mr-3" />Loading graph…
    </div>

    <!-- Empty -->
    <div
      v-else-if="!graphStore.isLoading && !hasGraph"
      class="flex-1 flex flex-col items-center justify-center rounded-xl border border-line bg-white"
    >
      <i class="pi pi-share-alt text-4xl text-ink-300" />
      <p class="mt-3 text-sm text-ink-500">No data available yet. Add workspaces and entities to populate the graph.</p>
    </div>

    <!-- Graph + panel -->
    <div v-else class="flex gap-3 flex-1 min-h-0">

      <!-- Canvas -->
      <div class="flex-1 flex flex-col min-w-0 gap-2">
        <!-- Legend -->
        <div class="flex flex-wrap gap-3 shrink-0">
          <div
            v-for="item in legendItems"
            :key="item.label"
            class="flex items-center gap-1.5 text-xs text-ink-500"
          >
            <span
              class="w-3 h-3 rounded-full shrink-0"
              :style="{ backgroundColor: item.color }"
            />
            {{ item.label }}
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
