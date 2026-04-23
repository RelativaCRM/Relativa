<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import Tag from 'primevue/tag';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { ApiError } from '@/api/http';
import type { WorkspaceDto } from '@/api/workspaces';

const router = useRouter();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();

const loading = ref(true);
const loadError = ref<string | null>(null);

const newWorkspaceName = ref('');
const creating = ref(false);
const createError = ref<string | null>(null);

const canCreate = computed(
  () =>
    newWorkspaceName.value.trim().length > 0 &&
    !creating.value &&
    !!orgStore.currentOrgId,
);

function displayRole(roleName: string | null): string {
  if (!roleName) return 'Member';
  if (roleName === 'ws_admin') return 'Admin';
  if (roleName === 'ws_member') return 'Member';
  if (roleName === 'ws_viewer') return 'Viewer';
  return roleName;
}

function chooseWorkspace(ws: WorkspaceDto) {
  wsStore.setCurrentWorkspace(ws.id);
  router.push({ name: 'home' });
}

async function handleCreate() {
  if (!canCreate.value || !orgStore.currentOrgId) return;
  creating.value = true;
  createError.value = null;
  try {
    await wsStore.createWorkspace(
      newWorkspaceName.value.trim(),
      orgStore.currentOrgId,
    );
    router.push({ name: 'home' });
  } catch (err) {
    createError.value =
      err instanceof ApiError ? err.message : 'Failed to create workspace.';
  } finally {
    creating.value = false;
  }
}

function handleLogout() {
  auth.logout();
  orgStore.clear();
  wsStore.clear();
  router.push({ name: 'login' });
}

onMounted(async () => {
  try {
    await wsStore.fetchWorkspaces();
    if (wsStore.workspaces.length === 1) {
      const only = wsStore.workspaces[0];
      wsStore.setCurrentWorkspace(only.id);
      router.replace({ name: 'home' });
      return;
    }
  } catch (err) {
    loadError.value =
      err instanceof ApiError
        ? err.message || 'Failed to load workspaces.'
        : 'Network error. Please try again.';
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthLayout tagline="Choose a workspace to continue">
    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
      Select a workspace
    </h1>
    <p class="mt-1 text-[13px] text-ink-500">
      <template v-if="orgStore.currentOrg">
        Pick a workspace in
        <span class="font-medium text-ink-700">{{
          orgStore.currentOrg.name
        }}</span>
        to continue.
      </template>
      <template v-else>
        Pick a workspace to continue.
      </template>
    </p>

    <div v-if="loading" class="mt-8 flex justify-center">
      <ProgressSpinner style="width: 2.5rem; height: 2.5rem" stroke-width="4" />
    </div>

    <Message
      v-else-if="loadError"
      severity="error"
      :closable="false"
      class="mt-6 !my-0"
    >
      {{ loadError }}
    </Message>

    <div
      v-else-if="wsStore.workspaces.length === 0"
      class="mt-6 flex flex-col gap-4"
    >
      <div
        class="rounded-xl border border-dashed border-line bg-surface p-6 text-center"
      >
        <i class="pi pi-folder-open text-2xl text-ink-400" />
        <p class="mt-2 text-sm font-medium text-ink-700">No workspaces yet</p>
        <p class="mt-1 text-[13px] text-ink-500">
          Create your first workspace to get started.
        </p>
      </div>

      <form
        class="flex flex-col gap-3"
        novalidate
        @submit.prevent="handleCreate"
      >
        <div class="flex flex-col gap-1.5">
          <label for="wsName" class="text-xs font-medium text-ink-600">
            Workspace name <span class="text-danger">*</span>
          </label>
          <InputText
            id="wsName"
            v-model="newWorkspaceName"
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

        <Button
          type="submit"
          label="Create workspace"
          icon="pi pi-plus"
          :disabled="!canCreate"
          :loading="creating"
          class="!h-11 !rounded-[10px] !font-semibold"
        />
      </form>
    </div>

    <ul v-else class="mt-6 flex flex-col gap-3">
      <li v-for="ws in wsStore.workspaces" :key="ws.id">
        <button
          type="button"
          class="group w-full flex items-center gap-4 rounded-xl border border-line bg-white px-4 py-3 text-left transition-colors hover:border-brand-400 hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-400"
          @click="chooseWorkspace(ws)"
        >
          <span
            class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-sm font-semibold text-brand-700"
          >
            {{ ws.name.charAt(0).toUpperCase() }}
          </span>
          <span class="flex-1 min-w-0">
            <span class="block truncate text-sm font-semibold text-ink-900">
              {{ ws.name }}
            </span>
            <span class="block text-xs text-ink-500">
              {{ ws.memberCount }}
              {{ ws.memberCount === 1 ? 'member' : 'members' }}
            </span>
          </span>
          <Tag
            :value="displayRole(ws.userRole)"
            severity="secondary"
            class="shrink-0"
          />
          <i
            class="pi pi-arrow-right text-ink-400 transition-colors group-hover:text-brand-600"
          />
        </button>
      </li>
    </ul>

    <p class="mt-6 text-center text-[13px] text-ink-500">
      <button
        class="font-medium text-brand-600 hover:underline"
        @click="handleLogout"
      >
        Sign out
      </button>
    </p>
  </AuthLayout>
</template>
