<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Message from 'primevue/message';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { normalizeError } from '@/api/errors';

const route = useRoute();
const router = useRouter();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.workspaceId));
const entities = computed(() => entityStore.entitiesFor(workspaceId.value));
const filterType = computed(() => {
  const raw = route.query.entityType;
  if (typeof raw !== 'string' || !raw.trim()) return null;
  return raw.trim().toLowerCase();
});
const filteredEntities = computed(() => {
  if (!filterType.value) return entities.value;
  return entities.value.filter(
    (e) => (e.entityTypeName ?? '').toLowerCase() === filterType.value,
  );
});

function formatTypeName(name: string): string {
  return name
    .split('_')
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

const headingTitle = computed(() => {
  const ws = wsStore.currentWorkspace?.name ?? 'Workspace';
  if (filterType.value) {
    return `${ws} — ${formatTypeName(filterType.value)}`;
  }
  return `${ws} — entities`;
});

const headingSubtitle = computed(() =>
  filterType.value
    ? `${formatTypeName(filterType.value)} records in this workspace.`
    : 'Records (clients, deals, …) in this workspace.',
);

const loading = ref(true);
const errorMessage = ref<string | null>(null);

async function load() {
  if (!workspaceId.value) return;
  loading.value = true;
  errorMessage.value = null;
  try {
    if (!wsStore.workspaces.length) {
      await wsStore.fetchWorkspaces(orgStore.currentOrgId ?? undefined);
    }
    const belongs = wsStore.workspaces.some((w) => w.id === workspaceId.value);
    if (!belongs) {
      errorMessage.value =
        'You do not have access to this workspace.';
      entityStore.clearWorkspace(workspaceId.value);
      return;
    }
    wsStore.setCurrentWorkspace(workspaceId.value);
    await entityStore.fetchList(workspaceId.value);
  } catch (err) {
    errorMessage.value = normalizeError(err, 'Failed to load entities.').message;
  } finally {
    loading.value = false;
  }
}

function goCreate() {
  router.push({
    name: 'workspace-entity-create',
    params: { workspaceId: String(workspaceId.value) },
  });
}

function goBack() {
  router.push({ name: 'workspaces' });
}

watch(workspaceId, (id) => {
  if (id) load();
});

onMounted(load);
</script>

<template>
  <section class="max-w-4xl">
    <div class="flex items-center justify-between mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          label="Workspaces"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="goBack"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          {{ headingTitle }}
        </h1>
        <p class="mt-1 text-sm text-ink-500">
          {{ headingSubtitle }}
        </p>
      </div>
      <Button icon="pi pi-plus" label="New entity" @click="goCreate" />
    </div>

    <Message
      v-if="errorMessage"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ errorMessage }}
    </Message>

    <div v-if="loading && !entities.length" class="text-center py-12 text-ink-500">Loading...</div>

    <div
      v-else-if="!filteredEntities.length && !errorMessage"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-inbox text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        <template v-if="filterType">
          No {{ formatTypeName(filterType) }} records yet. Create one to get
          started.
        </template>
        <template v-else>
          No entities yet. Create one to get started.
        </template>
      </p>
      <Button
        class="mt-4"
        icon="pi pi-plus"
        label="Create entity"
        @click="goCreate"
      />
    </div>

    <div
      v-else
      class="rounded-xl border border-line bg-white overflow-hidden"
    >
      <table class="w-full text-sm">
        <thead>
          <tr
            class="border-b border-line bg-surface text-left text-xs font-medium text-ink-500 uppercase tracking-wider"
          >
            <th class="px-5 py-3">ID</th>
            <th class="px-5 py-3">Type</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="ent in filteredEntities"
            :key="ent.id"
            class="border-b border-line last:border-0 hover:bg-surface/50"
          >
            <td class="px-5 py-3 font-mono text-xs text-ink-700">{{ ent.id }}</td>
            <td class="px-5 py-3">
              <Tag :value="ent.entityTypeName" severity="secondary" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
