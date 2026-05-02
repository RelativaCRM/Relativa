<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import { Network } from 'vis-network/standalone';
import { useGraphStore } from '@/stores/graph';
import { useEntityStore } from '@/stores/entity';
import { useWorkspaceStore } from '@/stores/workspace';
import { ApiError } from '@/api/http';
import type { EntityListItemDto } from '@/api/entities';

const router = useRouter();
const graphStore = useGraphStore();
const entityStore = useEntityStore();
const wsStore = useWorkspaceStore();

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const loading = ref(false);
const errorMessage = ref<string | null>(null);

const workspaceId = computed(() => wsStore.currentWorkspaceId);
const entities = computed(() =>
  workspaceId.value ? entityStore.entitiesFor(workspaceId.value) : [],
);

const PRIMARY_FIELDS = ['title', 'name', 'first_name', 'email', 'company'];

function buildLabel(item: EntityListItemDto): string {
  const fallback = `${item.entityTypeName} #${item.id}`;
  const values = item.propertyValues ?? [];
  for (const want of PRIMARY_FIELDS) {
    const match = values.find(
      (p) => p.name.toLowerCase() === want && p.value && p.value.trim() !== '',
    );
    if (match?.value) return match.value;
  }
  const firstNonEmpty = values.find((p) => p.value && p.value.trim() !== '');
  return firstNonEmpty?.value ?? fallback;
}

async function render() {
  await nextTick();
  if (!container.value) return;
  const items = entities.value;
  const nodes = items.map((e) => ({
    id: e.id,
    label: buildLabel(e),
    group: e.entityTypeName,
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
        size: 14,
        font: { size: 13, face: 'Inter, sans-serif' },
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
    errorMessage.value =
      err instanceof ApiError
        ? err.message || `Request failed (${err.status})`
        : err instanceof Error
          ? err.message
          : 'Failed to load graph data.';
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
        <p class="mt-1 text-sm text-ink-500">
          Entities in
          <span class="font-medium text-ink-700">{{
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
            params: { id: String(workspaceId) },
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
}
</style>
