<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import { Network } from 'vis-network/standalone';
import { useGraphStore } from '@/stores/graph';
import { useEntityStore } from '@/stores/entity';
import { useWorkspaceStore } from '@/stores/workspace';
import { normalizeError } from '@/api/errors';
import type { EntityListItemDto } from '@/api/entities';

const route = useRoute();
const router = useRouter();
const graphStore = useGraphStore();
const entityStore = useEntityStore();
const wsStore = useWorkspaceStore();

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const loading = ref(false);
const errorMessage = ref<string | null>(null);

const workspaceId = computed(() => {
  const fromRoute = Number(route.params.workspaceId);
  if (Number.isFinite(fromRoute) && fromRoute > 0) return fromRoute;
  return wsStore.currentWorkspaceId;
});
const entities = computed(() =>
  workspaceId.value ? entityStore.entitiesFor(workspaceId.value) : [],
);

const PRIMARY_FIELDS = ['title', 'name', 'first_name', 'email', 'company'];

function valueToString(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (typeof v === 'string') return v.trim();
  return String(v);
}

function buildLabel(item: EntityListItemDto): string {
  const fallback = `${item.entityTypeName} #${item.id}`;
  const values = item.propertyValues ?? [];
  for (const want of PRIMARY_FIELDS) {
    const match = values.find(
      (p) => (p.propertyName ?? '').toLowerCase() === want && valueToString(p.value) !== '',
    );
    if (match) return valueToString(match.value);
  }
  const firstNonEmpty = values.find((p) => valueToString(p.value) !== '');
  return firstNonEmpty ? valueToString(firstNonEmpty.value) : fallback;
}

const NODE_COLOR = {
  background: '#dbeafe', // brand-100
  border: '#2563eb',     // brand-600
  highlight: {
    background: '#1d4ed8', // brand-700
    border: '#1e3a8a',     // brand-900
  },
  hover: {
    background: '#bfdbfe', // brand-200
    border: '#1d4ed8',     // brand-700
  },
};

async function render() {
  await nextTick();
  if (!container.value) return;
  const items = entities.value;
  const nodes = items.map((e) => ({
    id: e.id,
    label: buildLabel(e),
    title: `${e.entityTypeName} · #${e.id}`,
  }));
  const edges: { from: number; to: number }[] = [];

  if (network.value) network.value.destroy();
  network.value = new Network(
    container.value,
    { nodes, edges },
    {
      physics: { enabled: true, stabilization: { iterations: 120 } },
      nodes: {
        shape: 'dot',
        size: 18,
        borderWidth: 2,
        borderWidthSelected: 3,
        color: NODE_COLOR,
        font: {
          size: 13,
          face: 'Inter, sans-serif',
          color: '#0f172a',   // ink-900
          strokeWidth: 0,
          vadjust: 4,
        },
        shadow: {
          enabled: true,
          color: 'rgba(37, 99, 235, 0.12)',
          size: 6,
          x: 0,
          y: 2,
        },
      },
      interaction: { hover: true },
    },
  );
  graphStore.setStats(nodes.length, edges.length);
}

async function load() {
  if (!workspaceId.value) return;
  loading.value = true;
  errorMessage.value = null;
  try {
    await entityStore.fetchList(workspaceId.value);
    await render();
  } catch (err) {
    console.error('GraphView load failed:', err);
    errorMessage.value = normalizeError(err, 'Failed to load graph data.').message;
  } finally {
    loading.value = false;
  }
}

watch(workspaceId, load);

onMounted(() => {
  if (!workspaceId.value) return;
  load();
});

onUnmounted(() => {
  network.value?.destroy();
  network.value = null;
});
</script>

<template>
  <section class="max-w-5xl">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">Graph</h1>
        <p class="mt-3 text-sm text-ink-500">
          Entities in
          <span class="font-semibold text-brand-600">{{
            wsStore.currentWorkspace?.name ?? 'this workspace'
          }}</span>
          rendered as nodes. Relationships will appear once the Graph service
          ships its API.
        </p>
      </div>
      <div class="text-xs text-ink-500">
        {{ graphStore.nodeCount }} nodes · {{ graphStore.edgeCount }} edges
      </div>
    </div>

    <Message
      v-if="!workspaceId"
      severity="info"
      :closable="false"
      class="!my-0 mb-4"
    >
      Select a workspace to see its entity graph.
      <Button
        text
        size="small"
        label="Choose workspace"
        class="!ml-2"
        @click="router.push({ name: 'workspaces' })"
      />
    </Message>

    <Message
      v-else-if="errorMessage"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ errorMessage }}
    </Message>

    <div v-else-if="loading && !entities.length" class="text-center py-12 text-ink-500">
      Loading graph...
    </div>

    <div
      v-else-if="!entities.length"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-share-alt text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No entities in this workspace yet.
      </p>
      <Button
        class="mt-4"
        label="Create entity"
        icon="pi pi-plus"
        @click="
          router.push({
            name: 'workspace-entity-create',
            params: { workspaceId: String(workspaceId) },
          })
        "
      />
    </div>

    <div
      v-show="workspaceId && entities.length"
      ref="container"
      class="graph-host rounded-xl border border-line bg-white"
    />
  </section>
</template>

<style scoped>
.graph-host {
  width: 100%;
  height: 520px;
  background-image: radial-gradient(rgba(37, 99, 235, 0.08) 1px, transparent 1px);
  background-size: 16px 16px;
}
</style>
