<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import { useRoute, useRouter, RouterLink, RouterView } from 'vue-router';
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
      class="h-16 border-b border-line bg-white/95 backdrop-blur-sm flex items-center justify-between px-6 gap-4 sticky top-0 z-30"
    >
      <div class="flex items-center gap-4 min-w-0 flex-1">
        <RouterLink :to="{ name: 'home' }" class="flex items-center" aria-label="Relativa home">
          <BrandMark size="sm" />
        </RouterLink>
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
      </div>
    </header>

    <div class="flex-1 flex">
      <aside class="w-60 border-r border-line bg-white py-6 px-3 hidden md:flex md:flex-col">
        <nav class="nav flex flex-col gap-5 text-sm text-ink-700 flex-1">
          <div>
            <p
              class="px-3 mb-2 text-[11px] font-semibold text-ink-400 uppercase tracking-wider"
            >
              Organization
            </p>
            <div class="flex flex-col gap-0.5">
              <RouterLink
                to="/"
                class="nav-link"
                active-class=""
                exact-active-class="nav-link--active"
              >
                <i class="pi pi-home" />Home
              </RouterLink>
              <RouterLink
                to="/members"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-users" />Members
              </RouterLink>
              <RouterLink
                to="/workspaces"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-folder" />Workspaces
              </RouterLink>
              <RouterLink
                v-if="canViewAuditLog"
                to="/audit-log"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-history" />Audit log
              </RouterLink>
            </div>
          </div>

          <div v-if="inWorkspaceShell && workspaceIdStr">
            <p
              class="px-3 mb-2 text-[11px] font-semibold text-ink-400 uppercase tracking-wider"
            >
              Workspace
            </p>
            <div class="flex flex-col gap-0.5">
              <RouterLink
                :to="{
                  name: 'workspace-entities',
                  params: { workspaceId: workspaceIdStr },
                }"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-database" />Entities
              </RouterLink>
              <RouterLink
                :to="{
                  name: 'graph',
                  params: { workspaceId: workspaceIdStr },
                }"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-share-alt" />Graph
              </RouterLink>
              <RouterLink
                :to="{
                  name: 'workspace-members',
                  params: { workspaceId: workspaceIdStr },
                }"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-user-edit" />Workspace members
              </RouterLink>
            </div>
          </div>

          <div class="pt-3 border-t border-line">
            <RouterLink
              to="/account"
              class="nav-link"
              active-class="nav-link--active"
            >
              <i class="pi pi-user" />Account
            </RouterLink>
          </div>
        </nav>

        <div class="pt-4 mt-4 border-t border-line">
          <button
            type="button"
            class="nav-link nav-link--logout w-full text-left"
            @click="handleLogout"
          >
            <i class="pi pi-sign-out" />Sign out
          </button>
        </div>
      </aside>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.nav-link {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.5rem 0.75rem;
  border-radius: 0.5rem;
  color: rgb(51 65 85);
  position: relative;
  transition: background-color 0.15s ease, color 0.15s ease;
}
.nav-link i {
  font-size: 0.875rem;
  color: rgb(100 116 139);
  width: 1rem;
  display: inline-flex;
  justify-content: center;
}
.nav-link:hover {
  background-color: rgb(248 250 252);
  color: rgb(15 23 42);
}
.nav-link--active {
  background-color: rgb(239 246 255);
  color: rgb(29 78 216);
  font-weight: 500;
}
.nav-link--active i {
  color: rgb(37 99 235);
}
.nav-link--active::before {
  content: '';
  position: absolute;
  left: -0.75rem;
  top: 0.5rem;
  bottom: 0.5rem;
  width: 3px;
  border-radius: 0 3px 3px 0;
  background-color: rgb(37 99 235);
}

.nav-link--logout {
  color: rgb(71 85 105);
  cursor: pointer;
  background: none;
  border: 0;
  font: inherit;
}
.nav-link--logout:hover {
  background-color: rgb(254 242 242);
  color: rgb(185 28 28);
}
.nav-link--logout:hover i {
  color: rgb(220 38 38);
}
</style>
