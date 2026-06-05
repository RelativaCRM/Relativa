<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter, RouterLink, RouterView, type RouteLocationRaw } from 'vue-router';
import BrandMark from '@/components/layout/BrandMark.vue';
import SwitchAccountDialog from '@/components/layout/SwitchAccountDialog.vue';
import InvitationsInbox from '@/components/layout/InvitationsInbox.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { useInvitationsInbox } from '@/composables/useInvitationsInbox';
import { useLocaleSwitch } from '@/i18n/useLocale';
import type { AppLocale } from '@/i18n';

const { t } = useI18n();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const route = useRoute();
const router = useRouter();

const showProfilePanel = ref(false);
const showSwitchAccount = ref(false);
const showNotifications = ref(false);

const { inboxCount, hasInbox, ensureLoaded } = useInvitationsInbox();

const userInitials = computed(() => {
  if (!auth.user) return '';
  return `${auth.user.firstName?.[0] ?? ''}${auth.user.lastName?.[0] ?? ''}`.toUpperCase();
});

const inWorkspaceShell = computed(() => /\/w\/\d+/.test(route.path));

const isOrgMembersActive = computed(() =>
  ['members', 'member'].includes(String(route.name)),
);

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

const canManageOrgSettings = computed(() =>
  orgStore.currentOrg?.myPermissions?.includes('manage_org_settings') ?? false,
);

function handleLogout() {
  auth.logout();
  orgStore.clear();
  wsStore.clear();
  entityStore.clear();
  router.push({ name: 'login' });
}

function switchAccount() {
  showProfilePanel.value = false;
  showSwitchAccount.value = true;
}

function openAccount() {
  showProfilePanel.value = false;
  router.push('/account');
}

const { current: currentLocale, changeLocale, locales } = useLocaleSwitch();

const openSubmenu = ref<'lang' | 'appearance' | null>(null);
function toggleSubmenu(which: 'lang' | 'appearance') {
  openSubmenu.value = openSubmenu.value === which ? null : which;
}

const collapsed = ref(localStorage.getItem('sidebarCollapsed') === '1');
function toggleCollapsed() {
  collapsed.value = !collapsed.value;
  localStorage.setItem('sidebarCollapsed', collapsed.value ? '1' : '0');
}

type Appearance = 'light' | 'dark' | 'system';
const appearance = ref<Appearance>(
  (localStorage.getItem('appearance') as Appearance | null) ?? 'system',
);
const appearanceOptions = computed(() => [
  { value: 'light' as const, label: t('prefs.themeLight') },
  { value: 'dark' as const, label: t('prefs.themeDark') },
  { value: 'system' as const, label: t('prefs.themeSystem') },
]);
const appearanceLabel = computed(
  () => appearanceOptions.value.find((o) => o.value === appearance.value)?.label ?? '',
);
const localeLabel = computed(() => t(`language.${currentLocale.value}`));

async function selectLanguage(next: AppLocale) {
  openSubmenu.value = null;
  if (next !== currentLocale.value) {
    await changeLocale(next);
  }
}

function selectAppearance(next: Appearance) {
  appearance.value = next;
  localStorage.setItem('appearance', next);
  openSubmenu.value = null;
}

function sectionLabel(name: string): string | null {
  switch (name) {
    case 'members':
    case 'workspace-members':
      return t('nav.members');
    case 'workspaces':
      return t('nav.workspaces');
    case 'audit-log':
      return t('nav.auditLog');
    case 'graph':
      return t('nav.graph');
    case 'account':
      return t('nav.account');
    case 'org-settings':
    case 'workspace-settings':
      return t('nav.settings');
    case 'workspace-dashboard':
      return t('nav.dashboard');
    case 'workspace-entities': {
      const typeName = route.query.entityType;
      if (typeof typeName === 'string') {
        return entityStore.standaloneTypes.find((x) => x.name === typeName)?.displayName ?? typeName;
      }
      return t('nav.dashboard');
    }
    default:
      return null;
  }
}

const breadcrumbs = computed(() => {
  const items: Array<{ label: string; to?: RouteLocationRaw }> = [
    { label: t('nav.home'), to: { name: 'home' } },
  ];
  if (orgStore.currentOrg) {
    items.push({ label: orgStore.currentOrg.name, to: { name: 'home' } });
  }
  if (inWorkspaceShell.value) {
    items.push({ label: t('nav.workspaces'), to: { name: 'workspaces' } });
    if (wsStore.currentWorkspace) {
      items.push({
        label: wsStore.currentWorkspace.name,
        to: { name: 'workspace-dashboard', params: { workspaceId: workspaceIdStr.value } },
      });
    }
  }
  const section = sectionLabel(String(route.name ?? ''));
  if (section) items.push({ label: section });
  return items;
});

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
  ensureLoaded();
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
      v-if="showProfilePanel || showNotifications"
      class="fixed inset-0 z-40"
      @click="showProfilePanel = false; showNotifications = false"
    />

    <div
      v-if="showNotifications"
      class="fixed right-4 top-[60px] z-50 w-80 max-w-[calc(100vw-2rem)] bg-white shadow-xl border border-line p-4"
    >
      <InvitationsInbox @accepted="showNotifications = false" />
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
            {{ type.displayName }}
          </RouterLink>
        </div>
      </Transition>

      <button
        type="button"
        class="ml-auto relative w-9 h-9 flex items-center justify-center border border-line text-ink-600 hover:bg-brand-50 hover:text-brand-700 hover:border-brand-200 transition-colors"
        :class="{ 'bg-brand-50 text-brand-700 border-brand-200': showNotifications }"
        :title="t('nav.notifications')"
        :aria-label="t('nav.notifications')"
        @click="showNotifications = !showNotifications"
      >
        <i class="pi pi-bell text-base" />
        <span
          v-if="hasInbox"
          class="absolute -top-1.5 -right-1.5 flex h-4 min-w-4 items-center justify-center bg-brand-600 px-1 text-[10px] font-semibold leading-none text-white"
        >
          {{ inboxCount }}
        </span>
      </button>
    </header>

    <div class="flex-1 flex">
      <aside
        class="sidebar w-14 lg:w-60 shrink-0 border-r border-line bg-white py-4 px-1.5 lg:px-3 flex flex-col sticky top-16 h-[calc(100vh-4rem)] overflow-hidden z-40"
        :class="{ 'sidebar--collapsed': collapsed }"
      >
        <button
          type="button"
          class="sidebar-toggle"
          :class="collapsed ? 'self-center' : 'self-end'"
          :title="collapsed ? t('nav.expand') : t('nav.collapse')"
          :aria-label="collapsed ? t('nav.expand') : t('nav.collapse')"
          @click="toggleCollapsed"
        >
          <i :class="['pi text-sm', collapsed ? 'pi-bars' : 'pi-angle-left']" />
        </button>

        <nav class="nav flex flex-col text-sm text-ink-700 flex-1 overflow-y-auto">

          <RouterLink to="/" class="nav-link" active-class="" exact-active-class="nav-link--active" :title="t('nav.home')">
            <i class="pi pi-home" /><span class="nav-label">{{ t('nav.home') }}</span>
          </RouterLink>
          <hr class="nav-divider border-t border-slate-200 mx-1 my-1" />


          <div class="flex flex-col">
            <RouterLink
              v-if="orgStore.currentOrg"
              :to="{ name: 'organizations' }"
              class="nav-link"
              active-class="nav-link--active"
              :title="orgStore.currentOrg.name"
            >
              <i class="pi pi-building" /><span class="nav-label truncate font-medium">{{ orgStore.currentOrg.name }}</span>
            </RouterLink>

            <div class="nav-sub ml-3 mt-0.5 flex flex-col border-l border-slate-200 pl-2">
              <RouterLink to="/members" class="nav-link" active-class="" :class="{ 'nav-link--active': isOrgMembersActive }" :title="t('nav.members')">
                <i class="pi pi-users" /><span class="nav-label">{{ t('nav.members') }}</span>
              </RouterLink>
              <RouterLink to="/graph" class="nav-link" active-class="nav-link--active" :title="t('nav.graph')">
                <i class="pi pi-share-alt" /><span class="nav-label">{{ t('nav.graph') }}</span>
              </RouterLink>
            </div>

            <RouterLink to="/workspaces" class="nav-link" active-class="nav-link--active" :title="t('nav.workspaces')">
              <i class="pi pi-folder" /><span class="nav-label">{{ t('nav.workspaces') }}</span>
            </RouterLink>

            <div v-if="canViewAuditLog" class="nav-sub ml-3 mt-0.5 flex flex-col border-l border-slate-200 pl-2">
              <RouterLink to="/audit-log" class="nav-link" active-class="nav-link--active" :title="t('nav.auditLog')">
                <i class="pi pi-history" /><span class="nav-label">{{ t('nav.auditLog') }}</span>
              </RouterLink>
            </div>

            <template v-if="canManageOrgSettings">
              <hr class="nav-divider border-t border-slate-200 mx-1 my-1" />
              <RouterLink to="/org-settings" class="nav-link" active-class="nav-link--active" :title="t('nav.orgSettings')">
                <i class="pi pi-cog" /><span class="nav-label">{{ t('nav.settings') }}</span>
              </RouterLink>
            </template>
          </div>


          <div v-if="inWorkspaceShell && workspaceIdStr" class="flex flex-col mt-3 pt-3 border-t border-slate-200">
            <RouterLink
              :to="{ name: 'workspace-dashboard', params: { workspaceId: workspaceIdStr } }"
              class="nav-link"
              active-class=""
              exact-active-class=""
              :title="wsStore.currentWorkspace?.name ?? t('workspace.fallbackName')"
            >
              <i class="pi pi-folder-open" /><span class="nav-label truncate font-medium">{{ wsStore.currentWorkspace?.name ?? t('workspace.fallbackName') }}</span>
            </RouterLink>

            <div class="nav-sub ml-3 mt-0.5 flex flex-col border-l border-slate-200 pl-2">
              <RouterLink
                :to="{ name: 'workspace-dashboard', params: { workspaceId: workspaceIdStr } }"
                class="nav-link"
                active-class=""
                exact-active-class="nav-link--active"
                :title="t('nav.dashboard')"
              >
                <i class="pi pi-th-large" /><span class="nav-label">{{ t('nav.dashboard') }}</span>
              </RouterLink>
              <RouterLink
                :to="{ name: 'workspace-members', params: { workspaceId: workspaceIdStr } }"
                class="nav-link"
                active-class="nav-link--active"
                :title="t('nav.members')"
              >
                <i class="pi pi-users" /><span class="nav-label">{{ t('nav.members') }}</span>
              </RouterLink>
              <RouterLink
                :to="{ name: 'workspace-settings', params: { workspaceId: workspaceIdStr } }"
                class="nav-link"
                active-class="nav-link--active"
                :title="t('nav.workspaceSettings')"
              >
                <i class="pi pi-cog" /><span class="nav-label">{{ t('nav.settings') }}</span>
              </RouterLink>
            </div>
          </div>

        </nav>


        <div class="relative pt-3 mt-3 border-t border-slate-200 shrink-0">
          <div
            v-if="showProfilePanel"
            class="fixed bottom-2 left-2 lg:left-3 z-50 w-60 bg-white shadow-xl border border-line"
          >
            <div class="px-4 py-3 border-b border-line">
              <p class="text-sm font-semibold text-ink-900 truncate leading-tight">
                {{ auth.user?.firstName }} {{ auth.user?.lastName }}
              </p>
              <p class="text-xs text-ink-400 truncate leading-tight">{{ auth.user?.email }}</p>
            </div>

            <div class="py-1">
              <button type="button" class="profile-item" @click="openAccount">
                <i class="pi pi-user" /><span class="flex-1">{{ t('nav.account') }}</span>
              </button>
            </div>

            <div class="py-1 border-t border-line">
              <div class="relative">
                <button type="button" class="profile-item" @click="toggleSubmenu('lang')">
                  <i class="pi pi-globe" /><span class="flex-1">{{ t('nav.language') }}</span>
                  <span class="text-xs text-ink-400">{{ localeLabel }}</span>
                  <i :class="['pi text-[10px] text-ink-400', openSubmenu === 'lang' ? 'pi-chevron-left' : 'pi-chevron-right']" />
                </button>
                <div
                  v-if="openSubmenu === 'lang'"
                  class="absolute left-full top-0 ml-1 z-10 w-44 bg-white shadow-xl border border-line py-1"
                >
                  <button
                    v-for="code in locales"
                    :key="code"
                    type="button"
                    class="profile-item"
                    @click="selectLanguage(code)"
                  >
                    <span class="flex-1">{{ t(`language.${code}`) }}</span>
                    <i v-if="code === currentLocale" class="pi pi-check text-brand-600 text-xs" />
                  </button>
                </div>
              </div>

              <div class="relative">
                <button type="button" class="profile-item" @click="toggleSubmenu('appearance')">
                  <i class="pi pi-palette" /><span class="flex-1">{{ t('nav.appearance') }}</span>
                  <span class="text-xs text-ink-400">{{ appearanceLabel }}</span>
                  <i :class="['pi text-[10px] text-ink-400', openSubmenu === 'appearance' ? 'pi-chevron-left' : 'pi-chevron-right']" />
                </button>
                <div
                  v-if="openSubmenu === 'appearance'"
                  class="absolute left-full top-0 ml-1 z-10 w-44 bg-white shadow-xl border border-line py-1"
                >
                  <button
                    v-for="opt in appearanceOptions"
                    :key="opt.value"
                    type="button"
                    class="profile-item"
                    @click="selectAppearance(opt.value)"
                  >
                    <span class="flex-1">{{ opt.label }}</span>
                    <i v-if="opt.value === appearance" class="pi pi-check text-brand-600 text-xs" />
                  </button>
                </div>
              </div>
            </div>

            <div class="py-1 border-t border-line">
              <button type="button" class="profile-item" @click="switchAccount">
                <i class="pi pi-sync" /><span class="flex-1">{{ t('nav.switchAccount') }}</span>
              </button>
              <button type="button" class="profile-item" @click="handleLogout">
                <i class="pi pi-sign-out" /><span class="flex-1">{{ t('nav.signOut') }}</span>
              </button>
            </div>
          </div>

          <button
            v-if="auth.user"
            type="button"
            class="profile-trigger w-full flex items-center gap-3 px-2 py-2 hover:bg-surface text-left"
            :title="`${auth.user.firstName} ${auth.user.lastName}`"
            @click="showProfilePanel = !showProfilePanel"
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

      <main class="flex-1 min-w-0 flex flex-col">
        <nav
          class="flex items-center gap-1.5 px-6 h-11 shrink-0 border-b border-line bg-white/95 backdrop-blur-sm text-[13px] sticky top-16 z-20 overflow-x-auto"
          aria-label="Breadcrumb"
        >
          <template v-for="(crumb, i) in breadcrumbs" :key="i">
            <RouterLink
              v-if="crumb.to && i < breadcrumbs.length - 1"
              :to="crumb.to"
              class="text-ink-500 hover:text-brand-700 transition-colors truncate max-w-[200px] shrink-0"
            >
              {{ crumb.label }}
            </RouterLink>
            <span v-else class="text-ink-900 font-medium truncate max-w-[220px] shrink-0">
              {{ crumb.label }}
            </span>
            <i
              v-if="i < breadcrumbs.length - 1"
              class="pi pi-angle-right text-[9px] text-ink-300 shrink-0"
            />
          </template>
        </nav>
        <div class="p-6 flex-1 min-w-0">
          <RouterView />
        </div>
      </main>
    </div>


    <SwitchAccountDialog v-model:visible="showSwitchAccount" />
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

.profile-item {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  width: 100%;
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  text-align: left;
  color: rgb(51 65 85);
  transition: background-color 0.15s ease, color 0.15s ease;
}
.profile-item > i:first-child {
  width: 1rem;
  display: inline-flex;
  justify-content: center;
  font-size: 0.8125rem;
  color: rgb(148 163 184);
}
.profile-item:hover {
  background-color: rgb(239 246 255);
  color: rgb(29 78 216);
}
.profile-item:hover > i:first-child {
  color: rgb(37 99 235);
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

@media (max-width: 1023.98px) {
  .sidebar .nav-label,
  .sidebar .nav-divider,
  .sidebar .nav-entry,
  .sidebar .nav-scroll {
    display: none !important;
  }
  .sidebar .nav-sub {
    margin-left: 0;
    padding-left: 0;
    border-left: 0;
  }
  .sidebar .nav-link {
    justify-content: center;
    padding-left: 0.25rem;
    padding-right: 0.25rem;
    gap: 0;
  }
  .sidebar .nav-link--active::before {
    left: -0.375rem;
  }
  .sidebar .profile-trigger {
    justify-content: center;
    padding-left: 0;
    padding-right: 0;
  }
}

.sidebar-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  margin-bottom: 0.5rem;
  color: rgb(148 163 184);
  transition: background-color 0.15s ease, color 0.15s ease;
}
.sidebar-toggle:hover {
  background-color: rgb(239 246 255);
  color: rgb(37 99 235);
}

.sidebar--collapsed {
  width: 3.5rem !important;
}
.sidebar--collapsed .nav-label,
.sidebar--collapsed .nav-scroll {
  display: none !important;
}
.sidebar--collapsed .nav-sub {
  margin-left: 0;
  padding-left: 0;
  border-left: 0;
}
.sidebar--collapsed .nav-link,
.sidebar--collapsed .nav-entry-main {
  justify-content: center;
  padding-left: 0.25rem;
  padding-right: 0.25rem;
  gap: 0;
}
.sidebar--collapsed .nav-divider {
  margin-left: 0.625rem;
  margin-right: 0.625rem;
}
.sidebar--collapsed .nav-link--active::before {
  left: -0.375rem;
}
.sidebar--collapsed .profile-trigger {
  justify-content: center;
  padding-left: 0;
  padding-right: 0;
}
</style>
