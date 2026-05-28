<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter, RouterLink, RouterView } from 'vue-router';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import Message from 'primevue/message';
import BrandMark from '@/components/layout/BrandMark.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { normalizeError } from '@/api/errors';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const route = useRoute();
const router = useRouter();

const showOrgPanel = ref(false);
const showWsPanel = ref(false);
const showProfilePanel = ref(false);
const showCreateWs = ref(false);
const orgExpanded = ref(true);
const wsExpanded = ref(true);
const wsListExpanded = ref(false);


const newWsName = ref('');
const creatingWs = ref(false);
const createWsError = ref<string | null>(null);

function openCreateWs() {
  showWsPanel.value = false;
  newWsName.value = '';
  createWsError.value = null;
  showCreateWs.value = true;
}

function closeCreateWs() {
  showCreateWs.value = false;
  newWsName.value = '';
  createWsError.value = null;
}

async function handleCreateWs() {
  if (!newWsName.value.trim() || creatingWs.value || !orgStore.currentOrgId) return;
  creatingWs.value = true;
  createWsError.value = null;
  try {
    const ws = await wsStore.createWorkspace(newWsName.value.trim(), orgStore.currentOrgId);
    closeCreateWs();
    router.push({ name: 'workspace-members', params: { workspaceId: String(ws.id) } });
  } catch (err) {
    createWsError.value = normalizeError(err, 'Failed to create workspace.').message;
  } finally {
    creatingWs.value = false;
  }
}

const userInitials = computed(() => {
  if (!auth.user) return '';
  return `${auth.user.firstName?.[0] ?? ''}${auth.user.lastName?.[0] ?? ''}`.toUpperCase();
});

function selectOrg(orgId: number) {
  handleOrgChange(orgId);
  showOrgPanel.value = false;
}

function selectWorkspace(wsId: number) {
  handleWorkspaceChange(wsId);
  showWsPanel.value = false;
}

const inWorkspaceShell = computed(() => /\/w\/\d+/.test(route.path));

const workspaceIdStr = computed(() =>
  wsStore.currentWorkspaceId != null
    ? String(wsStore.currentWorkspaceId)
    : null,
);

const canViewAuditLog = computed(() => {
  const wsPermissions = new Set(wsStore.currentWorkspace?.myPermissions ?? []);
  if (wsPermissions.has('view_analytics')) return true;
  const orgPermissions = new Set(orgStore.currentOrg?.myPermissions ?? []);
  return orgPermissions.has('manage_org_settings');
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
  await router.push({
    name: 'workspace-dashboard',
    params: { workspaceId: String(wsId) },
  });
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

    }
  },
  { immediate: true },
);

onMounted(async () => {
  if (orgStore.currentOrgId) {
    try {
      await wsStore.fetchWorkspaces(orgStore.currentOrgId);
    } catch {

    }
  }
});
</script>

<template>
  <div class="min-h-screen flex flex-col bg-surface">

    
    <div
      v-if="showOrgPanel || showWsPanel || showProfilePanel"
      class="fixed inset-0 z-40 bg-black/20"
      @click="showOrgPanel = false; showWsPanel = false; showProfilePanel = false"
    />

    
    <div
      v-if="showOrgPanel"
      class="fixed left-[248px] top-24 z-50 w-56 bg-white rounded-xl shadow-xl border border-line overflow-hidden"
    >
      <div class="px-4 py-2.5 border-b border-line flex items-center justify-between">
        <span class="text-[11px] font-semibold text-ink-400 uppercase tracking-wider">Organization</span>
        <button
          type="button"
          class="w-5 h-5 flex items-center justify-center text-brand-600 hover:bg-brand-50"
          @click="showOrgPanel = false; router.push({ name: 'onboarding' })"
        >
          <i class="pi pi-plus text-[10px]" />
        </button>
      </div>
      <div class="overflow-y-auto max-h-72 divide-y divide-slate-100">
        <button
          v-for="org in orgStore.organizations"
          :key="org.id"
          type="button"
          :class="[
            'w-full text-left px-4 py-2.5 text-sm hover:bg-surface flex items-center justify-between gap-2',
            org.id === orgStore.currentOrgId ? 'text-brand-700 font-medium bg-brand-50' : 'text-ink-700',
          ]"
          @click="selectOrg(org.id)"
        >
          <span class="truncate">{{ org.name }}</span>
          <i v-if="org.id === orgStore.currentOrgId" class="pi pi-check text-xs text-brand-600 shrink-0" />
        </button>
      </div>
    </div>

    
    <div
      v-if="showWsPanel"
      class="fixed left-[248px] top-44 z-50 w-56 bg-white rounded-xl shadow-xl border border-line overflow-hidden"
    >
      <div class="px-4 py-2.5 border-b border-line flex items-center justify-between">
        <span class="text-[11px] font-semibold text-ink-400 uppercase tracking-wider">Workspace</span>
        <button
          type="button"
          class="w-5 h-5 flex items-center justify-center text-brand-600 hover:bg-brand-50"
          @click="openCreateWs"
        >
          <i class="pi pi-plus text-[10px]" />
        </button>
      </div>
      <div class="overflow-y-auto max-h-64 divide-y divide-slate-100">
        <button
          v-for="ws in wsStore.workspaces"
          :key="ws.id"
          type="button"
          :class="[
            'w-full text-left px-4 py-2.5 text-sm hover:bg-surface flex items-center justify-between gap-2',
            ws.id === wsStore.currentWorkspaceId ? 'text-brand-700 font-medium bg-brand-50' : 'text-ink-700',
          ]"
          @click="selectWorkspace(ws.id)"
        >
          <span class="truncate">{{ ws.name }}</span>
          <i v-if="ws.id === wsStore.currentWorkspaceId" class="pi pi-check text-xs text-brand-600 shrink-0" />
        </button>
      </div>
    </div>

    
    <div
      v-if="showProfilePanel"
      class="fixed left-[248px] bottom-4 z-50 w-52 bg-white rounded-xl shadow-xl border border-line overflow-hidden"
    >
      <div class="divide-y divide-slate-100">
        <RouterLink
          to="/account"
          class="flex items-center gap-2.5 px-4 py-3 text-sm text-ink-700 hover:bg-surface"
          @click="showProfilePanel = false"
        >
          <i class="pi pi-user text-ink-400 text-xs" />Account
        </RouterLink>
        <button
          type="button"
          class="w-full flex items-center gap-2.5 px-4 py-3 text-sm text-left text-red-600 hover:bg-red-50"
          @click="handleLogout"
        >
          <i class="pi pi-sign-out text-xs" />Sign out
        </button>
      </div>
    </div>

    <header
      class="h-16 border-b border-line bg-white/95 backdrop-blur-sm flex items-center px-6 gap-4 sticky top-0 z-30"
    >
      <RouterLink :to="{ name: 'home' }" class="flex items-center" aria-label="Relativa home">
        <BrandMark size="sm" />
      </RouterLink>

      <Transition name="entity-nav">
        <div v-if="inWorkspaceShell && entityStore.standaloneTypes.length" class="flex items-center gap-2">
          <div class="w-px h-6 bg-line shrink-0" />
          <RouterLink
            v-for="type in entityStore.standaloneTypes"
            :key="type.id"
            :to="{ name: 'workspace-entities', params: { workspaceId: workspaceIdStr }, query: { entityType: type.name } }"
            :class="[
              'flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm transition-colors',
              route.query.entityType === type.name
                ? 'bg-brand-50 text-brand-700 font-medium'
                : 'text-ink-600 hover:bg-brand-50 hover:text-brand-700',
            ]"
          >
            <span :class="['w-1.5 h-1.5 rounded-full shrink-0', route.query.entityType === type.name ? 'bg-brand-500' : 'bg-slate-300']" />
            {{ formatTypeName(type.name) }}
          </RouterLink>
        </div>
      </Transition>
    </header>

    <div class="flex-1 flex">
      <aside class="w-60 border-r border-line bg-white py-4 px-3 hidden md:flex md:flex-col sticky top-16 h-[calc(100vh-4rem)] overflow-hidden">

        <nav class="nav flex flex-col text-sm text-ink-700 flex-1 overflow-y-auto">

          
          <RouterLink to="/" class="nav-link" active-class="" exact-active-class="nav-link--active">
            <i class="pi pi-home" />Home
          </RouterLink>
          <hr class="border-t border-slate-200 mx-1 my-1" />

          
          <div class="flex flex-col">
            <div v-if="orgStore.currentOrg" class="relative flex items-stretch nav-entry" :class="{ 'nav-entry--active': showOrgPanel }">
              <button
                type="button"
                class="group flex flex-1 items-center gap-2 px-2 py-2 text-sm text-left min-w-0 transition-colors text-ink-700 hover:bg-brand-50 hover:text-brand-700"
                @click="orgExpanded = !orgExpanded"
              >
                <span class="relative flex items-center justify-center w-4 h-4 shrink-0">
                  <i class="pi pi-briefcase text-[13px] absolute group-hover:opacity-0 transition-opacity" />
                  <i :class="['pi text-[10px] absolute opacity-0 group-hover:opacity-100 transition-opacity', orgExpanded ? 'pi-chevron-down' : 'pi-chevron-right']" />
                </span>
                <span class="truncate font-medium">{{ orgStore.currentOrg.name }}</span>
              </button>
              <button
                type="button"
                :class="['flex items-center px-2 shrink-0 transition-colors', showOrgPanel ? 'bg-brand-50 text-brand-600' : 'text-ink-300 hover:bg-brand-50 hover:text-brand-600']"
                @click="showOrgPanel = true"
              >
                <i class="pi pi-chevron-right text-[10px]" />
              </button>
            </div>

            <div v-if="orgExpanded" class="ml-3 mt-0.5 flex flex-col border-l border-slate-200 pl-2">
              <RouterLink to="/members" class="nav-link" active-class="nav-link--active">
                <i class="pi pi-users" />Members
              </RouterLink>
              <RouterLink to="/graph" class="nav-link" active-class="nav-link--active">
                <i class="pi pi-share-alt" />Graph
              </RouterLink>
              <hr class="border-t border-slate-200 mx-1 my-1" />

              
              <div class="relative flex items-stretch nav-entry" :class="{ 'nav-entry--active': showWsPanel }">
                <button
                  type="button"
                  class="group flex flex-1 items-center gap-2 px-2 py-2 text-sm text-left min-w-0 transition-colors text-ink-700 hover:bg-brand-50 hover:text-brand-700"
                  @click="wsStore.workspaces.length ? (wsListExpanded = !wsListExpanded) : undefined"
                >
                  <span class="relative flex items-center justify-center w-4 h-4 shrink-0">
                    <i class="pi pi-folder text-[13px] absolute group-hover:opacity-0 transition-opacity" />
                    <i :class="['pi text-[10px] absolute opacity-0 group-hover:opacity-100 transition-opacity', wsListExpanded ? 'pi-chevron-down' : 'pi-chevron-right']" />
                  </span>
                  <span class="truncate">Workspaces</span>
                </button>
                <button
                  type="button"
                  :class="['flex items-center px-2 shrink-0 transition-colors', showWsPanel ? 'bg-brand-50 text-brand-600' : 'text-ink-300 hover:bg-brand-50 hover:text-brand-600']"
                  @click="showWsPanel = true"
                >
                  <i class="pi pi-chevron-right text-[10px]" />
                </button>
              </div>

              <RouterLink
                v-if="!wsStore.workspaces.length"
                to="/workspaces"
                class="flex items-center gap-2 px-3 py-1.5 text-xs text-ink-500 hover:text-brand-600 hover:bg-brand-50"
              >
                <i class="pi pi-plus text-[10px]" />
                Add workspace
              </RouterLink>

              <div
                v-if="wsListExpanded && wsStore.workspaces.length"
                class="nav-scroll ml-4 flex flex-col border-l border-slate-200 pl-2 max-h-48 overflow-y-auto mb-1"
              >
                <button
                  v-for="ws in wsStore.workspaces"
                  :key="ws.id"
                  type="button"
                  :class="[
                    'flex items-center gap-2.5 px-2 py-1.5 text-xs text-left w-full transition-colors hover:bg-brand-50',
                    inWorkspaceShell && ws.id === wsStore.currentWorkspaceId ? 'text-brand-700 font-medium' : 'text-ink-500 hover:text-brand-700',
                  ]"
                  @click="selectWorkspace(ws.id)"
                >
                  <span :class="['w-1.5 h-1.5 rounded-full shrink-0', inWorkspaceShell && ws.id === wsStore.currentWorkspaceId ? 'bg-brand-500' : 'bg-slate-300']" />
                  <span class="truncate">{{ ws.name }}</span>
                </button>
                <RouterLink
                  to="/workspaces"
                  :class="[
                    'flex items-center gap-2.5 px-2 py-1.5 text-xs w-full transition-colors hover:bg-brand-50',
                    route.path === '/workspaces' ? 'text-brand-700 font-medium' : 'text-ink-400 hover:text-brand-600',
                  ]"
                >
                  <span :class="['w-1.5 h-1.5 rounded-full shrink-0', route.path === '/workspaces' ? 'bg-brand-500' : 'bg-slate-200']" />
                  <span>View more...</span>
                </RouterLink>
              </div>

              <template v-if="canViewAuditLog">
                <hr class="border-t border-slate-200 mx-1 my-1" />
                <RouterLink to="/audit-log" class="nav-link" active-class="nav-link--active">
                  <i class="pi pi-history" />Audit log
                </RouterLink>
              </template>
            </div>
          </div>

          
          <div v-if="inWorkspaceShell && workspaceIdStr" class="flex flex-col mt-3 pt-3 border-t border-slate-200">
            <div class="relative flex items-stretch nav-entry">
              <button
                type="button"
                class="group flex flex-1 items-center gap-2 px-2 py-2 text-sm text-left min-w-0 transition-colors text-ink-700 hover:bg-brand-50 hover:text-brand-700"
                @click="wsExpanded = !wsExpanded"
              >
                <span class="relative flex items-center justify-center w-4 h-4 shrink-0">
                  <i class="pi pi-folder-open text-[13px] absolute group-hover:opacity-0 transition-opacity" />
                  <i :class="['pi text-[10px] absolute opacity-0 group-hover:opacity-100 transition-opacity', wsExpanded ? 'pi-chevron-down' : 'pi-chevron-right']" />
                </span>
                <span class="truncate font-medium">{{ wsStore.currentWorkspace?.name ?? 'Workspace' }}</span>
              </button>
            </div>

            <div v-if="wsExpanded" class="ml-3 mt-0.5 flex flex-col border-l border-slate-200 pl-2">

              <RouterLink
                :to="{ name: 'workspace-dashboard', params: { workspaceId: workspaceIdStr } }"
                class="nav-link"
                active-class=""
                exact-active-class="nav-link--active"
              >
                <i class="pi pi-th-large" />Dashboard
              </RouterLink>

              <hr class="border-t border-slate-200 mx-1 my-1" />
              <RouterLink
                :to="{ name: 'workspace-members', params: { workspaceId: workspaceIdStr } }"
                class="nav-link"
                active-class="nav-link--active"
              >
                <i class="pi pi-users" />Members
              </RouterLink>
            </div>
          </div>

        </nav>

        
        <div class="pt-3 mt-3 border-t border-slate-200 shrink-0">
          <button
            v-if="auth.user"
            type="button"
            class="w-full flex items-center gap-3 px-2 py-2 hover:bg-surface text-left"
            @click="showProfilePanel = true"
          >
            <div class="w-8 h-8 rounded-full bg-brand-600 text-white text-xs font-semibold flex items-center justify-center shrink-0">
              {{ userInitials }}
            </div>
            <div class="nav-label min-w-0 flex-1">
              <p class="text-sm font-medium text-ink-900 truncate leading-tight">{{ auth.user.firstName }} {{ auth.user.lastName }}</p>
              <p class="text-xs text-ink-400 truncate leading-tight">{{ auth.user.email }}</p>
            </div>
            <i class="nav-label pi pi-chevron-right text-[10px] text-ink-400 shrink-0" />
          </button>
        </div>
      </aside>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>

    
    <Dialog
      v-model:visible="showCreateWs"
      header="Create workspace"
      modal
      :style="{ width: '420px' }"
      @hide="closeCreateWs"
    >
      <form class="flex flex-col gap-4" novalidate @submit.prevent="handleCreateWs">
        <div class="flex flex-col gap-1.5">
          <label for="sidebarWsName" class="text-xs font-medium text-ink-600">
            Workspace name <span class="text-danger">*</span>
          </label>
          <InputText
            id="sidebarWsName"
            v-model="newWsName"
            placeholder="e.g. Sales team"
            class="!h-10"
          />
        </div>
        <Message v-if="createWsError" severity="error" :closable="false" class="!my-0">
          {{ createWsError }}
        </Message>
        <div class="flex justify-end gap-2">
          <Button type="button" label="Cancel" severity="secondary" text @click="closeCreateWs" />
          <Button
            type="submit"
            label="Create"
            :disabled="!newWsName.trim() || creatingWs"
            :loading="creatingWs"
          />
        </div>
      </form>
    </Dialog>
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
  background-color: rgb(239 246 255);
  color: rgb(29 78 216);
}
.nav-link:hover i {
  color: rgb(37 99 235);
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
  left: -0.25rem;
  top: 0.5rem;
  bottom: 0.5rem;
  width: 3px;
  border-radius: 0 3px 3px 0;
  background-color: rgb(37 99 235);
}

.nav-entry {
  position: relative;
}
.nav-entry--active::before {
  content: '';
  position: absolute;
  left: -0.25rem;
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

.entity-nav-enter-active,
.entity-nav-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.entity-nav-enter-from,
.entity-nav-leave-to {
  opacity: 0;
  transform: translateX(-8px);
}
</style>
