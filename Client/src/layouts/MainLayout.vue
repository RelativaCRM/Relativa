<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import { useRoute, useRouter, RouterLink, RouterView } from 'vue-router';
import Button from 'primevue/button';
import Select from 'primevue/select';
import BrandMark from '@/components/layout/BrandMark.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const route = useRoute();
const router = useRouter();

const inWorkspaceShell = computed(() => /\/w\/\d+/.test(route.path));

const workspaceIdStr = computed(() =>
  wsStore.currentWorkspaceId != null
    ? String(wsStore.currentWorkspaceId)
    : null,
);

const canViewAuditLog = computed(() => {
  const wsRole = wsStore.currentWorkspace?.userRole;
  if (wsRole === 'ws_admin' || wsRole === 'ws_analyst') return true;
  const orgRole = orgStore.currentOrg?.userRole;
  return orgRole === 'org_owner' || orgRole === 'org_admin';
});

async function handleOrgChange(orgId: number | null) {
  if (orgId == null) return;
  orgStore.setCurrentOrg(orgId);
  try {
    await wsStore.fetchWorkspaces(orgId);
  } catch {
    return;
  }
  const paramWs = Number(route.params.workspaceId);
  if (
    Number.isFinite(paramWs) &&
    !wsStore.workspaces.some((w) => w.id === paramWs)
  ) {
    await router.replace({ name: 'workspaces' });
  }
}

const workspaceRouteNames = new Set([
  'workspace-entities',
  'workspace-entity-create',
  'workspace-members',
  'graph',
]);

function formatTypeName(name: string): string {
  return name
    .split('_')
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

async function handleWorkspaceChange(wsId: number | null) {
  if (wsId == null) return;
  wsStore.setCurrentWorkspace(wsId);
  const name = route.name;
  if (name && workspaceRouteNames.has(String(name))) {
    await router.push({
      name,
      params: { ...route.params, workspaceId: String(wsId) },
    });
  } else {
    await router.push({
      name: 'workspace-entities',
      params: { workspaceId: String(wsId) },
    });
  }
}

function handleLogout() {
  auth.logout();
  orgStore.clear();
  wsStore.clear();
  entityStore.clear();
  router.push({ name: 'login' });
}

watch(
  () => orgStore.currentOrgId,
  async (id) => {
    if (!id) return;
    try {
      await wsStore.fetchWorkspaces(id);
    } catch {
      /* list refresh is optional when org changes from elsewhere */
    }
  },
);

watch(
  inWorkspaceShell,
  async (active) => {
    if (!active || entityStore.typesLoaded) return;
    try {
      await entityStore.fetchTypes();
    } catch {
      /* sidebar can render without per-type subitems if this fails */
    }
  },
  { immediate: true },
);

onMounted(async () => {
  if (orgStore.currentOrgId) {
    try {
      await wsStore.fetchWorkspaces(orgStore.currentOrgId);
    } catch {
      /* non-fatal */
    }
  }
});
</script>

<template>
  <div class="min-h-screen flex flex-col bg-surface">
    <header
      class="h-16 border-b border-line bg-white flex items-center justify-between px-6 gap-4"
    >
      <div class="flex items-center gap-4 min-w-0 flex-1">
        <BrandMark size="sm" />
        <div
          v-if="orgStore.currentOrg"
          class="hidden md:flex items-center gap-3 min-w-0 border-l border-line pl-4"
        >
          <Select
            v-if="orgStore.organizations.length > 1"
            :model-value="orgStore.currentOrgId"
            :options="orgStore.organizations"
            option-label="name"
            option-value="id"
            placeholder="Organization"
            class="!h-9 min-w-[10rem] max-w-[14rem]"
            @update:model-value="handleOrgChange"
          />
          <span
            v-else
            class="text-sm text-ink-500 truncate max-w-[14rem]"
          >
            {{ orgStore.currentOrg.name }}
          </span>
          <Select
            v-if="orgStore.currentOrgId && wsStore.workspaces.length > 0"
            :model-value="wsStore.currentWorkspaceId"
            :options="wsStore.workspaces"
            option-label="name"
            option-value="id"
            placeholder="Workspace"
            class="!h-9 min-w-[10rem] max-w-[14rem]"
            @update:model-value="handleWorkspaceChange"
          />
        </div>
      </div>
      <div class="flex items-center gap-3 shrink-0">
        <RouterLink
          v-if="auth.user"
          to="/account"
          class="text-sm text-ink-600 hidden sm:inline hover:text-brand-700 hover:underline"
        >
          {{ auth.user.firstName }} {{ auth.user.lastName }}
        </RouterLink>
        <Button
          label="Sign out"
          severity="secondary"
          text
          icon="pi pi-sign-out"
          @click="handleLogout"
        />
      </div>
    </header>

    <div class="flex-1 flex">
      <aside class="w-60 border-r border-line bg-white py-6 px-4 hidden md:block">
        <nav class="flex flex-col gap-4 text-sm text-ink-700">
          <div>
            <p
              class="px-3 mb-2 text-[11px] font-semibold text-ink-400 uppercase tracking-wider"
            >
              Organization
            </p>
            <div class="flex flex-col gap-1">
              <RouterLink
                to="/"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class=""
                exact-active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-home mr-2" />Home
              </RouterLink>
              <RouterLink
                to="/members"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-users mr-2" />Members
              </RouterLink>
              <RouterLink
                to="/workspaces"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-folder mr-2" />Workspaces
              </RouterLink>
              <RouterLink
                v-if="canViewAuditLog"
                to="/audit-log"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-history mr-2" />Audit log
              </RouterLink>
            </div>
          </div>

          <div v-if="inWorkspaceShell && workspaceIdStr">
            <p
              class="px-3 mb-2 text-[11px] font-semibold text-ink-400 uppercase tracking-wider"
            >
              Workspace
            </p>
            <div class="flex flex-col gap-1">
              <div class="flex flex-col">
                <RouterLink
                  :to="{
                    name: 'workspace-entities',
                    params: { workspaceId: workspaceIdStr },
                  }"
                  class="px-3 py-2 rounded-lg hover:bg-surface"
                  exact-active-class="bg-brand-50 text-brand-700 font-medium"
                >
                  <i class="pi pi-database mr-2" />Entities
                </RouterLink>
                <div
                  v-if="entityStore.types.length"
                  class="ml-6 mt-1 flex flex-col gap-1 border-l border-line pl-2"
                >
                  <RouterLink
                    v-for="type in entityStore.types"
                    :key="type.id"
                    :to="{
                      name: 'workspace-entities',
                      params: { workspaceId: workspaceIdStr },
                      query: { type: type.name },
                    }"
                    class="px-3 py-1.5 rounded-lg text-sm hover:bg-surface"
                    exact-active-class="bg-brand-50 text-brand-700 font-medium"
                  >
                    {{ formatTypeName(type.name) }}
                  </RouterLink>
                </div>
              </div>
              <RouterLink
                :to="{
                  name: 'graph',
                  params: { workspaceId: workspaceIdStr },
                }"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-share-alt mr-2" />Graph
              </RouterLink>
              <RouterLink
                :to="{
                  name: 'workspace-members',
                  params: { workspaceId: workspaceIdStr },
                }"
                class="px-3 py-2 rounded-lg hover:bg-surface"
                active-class="bg-brand-50 text-brand-700 font-medium"
              >
                <i class="pi pi-user-edit mr-2" />Workspace members
              </RouterLink>
            </div>
          </div>

          <div class="pt-2 border-t border-line">
            <RouterLink
              to="/account"
              class="px-3 py-2 rounded-lg hover:bg-surface"
              active-class="bg-brand-50 text-brand-700 font-medium"
            >
              <i class="pi pi-user mr-2" />Account
            </RouterLink>
          </div>
        </nav>
      </aside>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
