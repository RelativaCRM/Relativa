<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Message from 'primevue/message';
import { useWorkspaceStore } from '@/stores/workspace';
import { entityApi, type EntityListItemDto } from '@/api/entities';
import { ApiError } from '@/api/http';

const route = useRoute();
const router = useRouter();
const wsStore = useWorkspaceStore();

const workspaceId = computed(() => Number(route.params.id));
const entities = ref<EntityListItemDto[]>([]);
const loading = ref(true);
const errorMessage = ref<string | null>(null);

async function load() {
  if (!workspaceId.value) return;
  loading.value = true;
  errorMessage.value = null;
  try {
    if (!wsStore.workspaces.length) {
      await wsStore.fetchWorkspaces();
    }
    const belongs = wsStore.workspaces.some((w) => w.id === workspaceId.value);
    if (!belongs) {
      errorMessage.value =
        'You do not have access to this workspace.';
      entities.value = [];
      return;
    }
    wsStore.setCurrentWorkspace(workspaceId.value);
    entities.value = await entityApi.list(workspaceId.value);
  } catch (err) {
    errorMessage.value =
      err instanceof ApiError ? err.message : 'Failed to load entities.';
  } finally {
    loading.value = false;
  }
}

function goCreate() {
  router.push({
    name: 'workspace-entity-create',
    params: { id: String(workspaceId.value) },
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
          {{ wsStore.currentWorkspace?.name ?? 'Workspace' }} — entities
        </h1>
        <p class="mt-1 text-sm text-ink-500">
          Records (clients, deals, …) in this workspace.
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

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <div
      v-else-if="!entities.length && !errorMessage"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-inbox text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No entities yet. Create one to get started.
      </p>
      <Button
        class="mt-4"
        icon="pi pi-plus"
        label="Create entity"
        @click="goCreate"
      />
    </div>

    <div
      v-else-if="entities.length"
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
            v-for="ent in entities"
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
