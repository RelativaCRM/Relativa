<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { ApiError } from '@/api/http';

const router = useRouter();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();

const loading = ref(true);
const showCreate = ref(false);
const newName = ref('');
const creating = ref(false);
const createError = ref<string | null>(null);

const canCreate = computed(
  () => newName.value.trim().length > 0 && !creating.value,
);

async function handleCreate() {
  if (!canCreate.value || !orgStore.currentOrgId) return;
  creating.value = true;
  createError.value = null;
  try {
    const ws = await wsStore.createWorkspace(
      newName.value.trim(),
      orgStore.currentOrgId,
    );
    closeDialog();
    router.push({ name: 'workspace-members', params: { id: ws.id } });
  } catch (err) {
    createError.value =
      err instanceof ApiError ? err.message : 'Failed to create workspace.';
  } finally {
    creating.value = false;
  }
}

function closeDialog() {
  showCreate.value = false;
  newName.value = '';
  createError.value = null;
}

function openWorkspace(id: number) {
  wsStore.setCurrentWorkspace(id);
  router.push({ name: 'workspace-members', params: { id } });
}

function displayRole(roleName: string | null): string {
  if (!roleName) return 'Member';
  if (roleName === 'ws_admin') return 'Admin';
  if (roleName === 'ws_member') return 'Member';
  if (roleName === 'ws_viewer') return 'Viewer';
  return roleName;
}

onMounted(async () => {
  try {
    await wsStore.fetchWorkspaces();
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <section class="max-w-4xl">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">Workspaces</h1>
        <p class="mt-1 text-sm text-ink-500">
          Workspaces in
          <span class="font-medium text-ink-700">{{
            orgStore.currentOrg?.name ?? 'your organization'
          }}</span>
        </p>
      </div>
      <Button
        icon="pi pi-plus"
        label="New workspace"
        @click="showCreate = true"
      />
    </div>

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <div
      v-else-if="!wsStore.workspaces.length"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-folder-open text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No workspaces yet. Create one to get started.
      </p>
      <Button
        class="mt-4"
        icon="pi pi-plus"
        label="Create workspace"
        @click="showCreate = true"
      />
    </div>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <button
        v-for="ws in wsStore.workspaces"
        :key="ws.id"
        class="text-left rounded-xl border border-line bg-white p-5 hover:border-brand-300 hover:shadow-sm transition"
        @click="openWorkspace(ws.id)"
      >
        <div class="flex items-start justify-between">
          <div class="min-w-0">
            <p class="text-base font-semibold text-ink-900 truncate">
              {{ ws.name }}
            </p>
            <p class="text-xs text-ink-500 mt-1">
              {{ ws.memberCount }}
              {{ ws.memberCount === 1 ? 'member' : 'members' }}
            </p>
          </div>
          <Tag
            :value="displayRole(ws.userRole)"
            severity="secondary"
            class="shrink-0"
          />
        </div>
        <div
          class="mt-4 flex items-center text-xs text-brand-600 font-medium"
        >
          Manage members <i class="pi pi-arrow-right ml-1 text-[10px]" />
        </div>
      </button>
    </div>

    <Dialog
      v-model:visible="showCreate"
      header="Create workspace"
      modal
      :style="{ width: '420px' }"
      @hide="closeDialog"
    >
      <form
        class="flex flex-col gap-4"
        novalidate
        @submit.prevent="handleCreate"
      >
        <div class="flex flex-col gap-1.5">
          <label for="wsName" class="text-xs font-medium text-ink-600">
            Workspace name <span class="text-danger">*</span>
          </label>
          <InputText
            id="wsName"
            v-model="newName"
            placeholder="e.g. Sales team"
            class="!h-10"
          />
        </div>

        <Message
          v-if="createError"
          severity="error"
          :closable="false"
          class="!my-0"
        >
          {{ createError }}
        </Message>

        <div class="flex justify-end gap-2">
          <Button
            type="button"
            label="Cancel"
            severity="secondary"
            text
            @click="closeDialog"
          />
          <Button
            type="submit"
            label="Create"
            :disabled="!canCreate"
            :loading="creating"
          />
        </div>
      </form>
    </Dialog>
  </section>
</template>
