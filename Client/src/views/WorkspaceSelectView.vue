<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { workspacesApi, type WorkspaceDto } from '@/api/workspaces';
import { ApiError } from '@/api/http';

const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const workspaces = ref<WorkspaceDto[]>([]);
const loading = ref(true);
const serverError = ref<string | null>(null);

function redirectTarget(): string {
  const raw = route.query.redirect;
  const target = typeof raw === 'string' ? raw : '/';
  return target === '/select-workspace' ? '/' : target;
}

function select(ws: WorkspaceDto) {
  auth.setWorkspace(String(ws.id));
  router.replace(redirectTarget());
}

onMounted(async () => {
  try {
    workspaces.value = await workspacesApi.list();
    const only = workspaces.value.length === 1 ? workspaces.value[0] : null;
    if (only) {
      select(only);
      return;
    }
  } catch (err) {
    serverError.value =
      err instanceof ApiError
        ? err.message || 'Failed to load workspaces.'
        : 'Network error. Please try again.';
  } finally {
    loading.value = false;
  }
});

function handleLogout() {
  auth.logout();
  router.push({ name: 'login' });
}
</script>

<template>
  <AuthLayout tagline="Choose a workspace to continue">
    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
      Select a workspace
    </h1>
    <p class="mt-1 text-[13px] text-ink-500">
      Pick the workspace you want to work in. You can switch later.
    </p>

    <div v-if="loading" class="mt-8 flex justify-center">
      <ProgressSpinner style="width: 2.5rem; height: 2.5rem" stroke-width="4" />
    </div>

    <Message
      v-else-if="serverError"
      severity="error"
      :closable="false"
      class="mt-6 !my-0"
    >
      {{ serverError }}
    </Message>

    <div
      v-else-if="workspaces.length === 0"
      class="mt-6 rounded-xl border border-dashed border-line bg-surface p-6 text-center"
    >
      <i class="pi pi-inbox text-2xl text-ink-400" />
      <p class="mt-2 text-sm font-medium text-ink-700">No workspaces yet</p>
      <p class="mt-1 text-[13px] text-ink-500">
        Ask an administrator to invite you to a workspace, or create one from an
        organization you own.
      </p>
    </div>

    <ul v-else class="mt-6 flex flex-col gap-3">
      <li v-for="ws in workspaces" :key="ws.id">
        <button
          type="button"
          class="group w-full flex items-center gap-4 rounded-xl border border-line bg-white px-4 py-3 text-left transition-colors hover:border-brand-400 hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-400"
          @click="select(ws)"
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
              <template v-if="ws.userRole"> · {{ ws.userRole }}</template>
            </span>
          </span>
          <i
            class="pi pi-arrow-right text-ink-400 transition-colors group-hover:text-brand-600"
          />
        </button>
      </li>
    </ul>

    <div class="mt-6 flex justify-center">
      <Button
        label="Sign out"
        severity="secondary"
        text
        icon="pi pi-sign-out"
        @click="handleLogout"
      />
    </div>
  </AuthLayout>
</template>
