<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import InputText from 'primevue/inputtext';
import Tag from 'primevue/tag';
import Popover from 'primevue/popover';
import AppHeader from '@/components/layout/AppHeader.vue';
import FormError from '@/components/feedback/FormError.vue';
import SwitchAccountDialog from '@/components/layout/SwitchAccountDialog.vue';
import InvitationsInbox from '@/components/layout/InvitationsInbox.vue';
import { roleBadgeFullClass, roleLabel } from '@/utils/roleBadge';
import { useLocaleSwitch } from '@/i18n/useLocale';
import type { AppLocale } from '@/i18n';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { useInvitationsInbox } from '@/composables/useInvitationsInbox';
import { orgApi, type OrgSearchResultDto } from '@/api/organizations';
import { normalizeError } from '@/api/errors';
import { notifyGlobal, useApiErrorHandler } from '@/api/errorToast';

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const { notify } = useApiErrorHandler();
const { current, changeLocale, locales } = useLocaleSwitch();

const year = new Date().getFullYear();

const profilePopover = ref<InstanceType<typeof Popover>>();
const openLangFlyout = ref(false);
const showSwitchAccount = ref(false);
const languageOptions = computed(() =>
  locales.map((code) => ({ value: code, label: t(`language.${code}`) })),
);
const localeLabel = computed(() => t(`language.${current.value}`));

async function selectLanguage(next: AppLocale) {
  if (next !== current.value) {
    try {
      await changeLocale(next);
    } catch (err) {
      notifyGlobal(err, { fallback: normalizeError(err).message });
    }
  }
  openLangFlyout.value = false;
  profilePopover.value?.hide();
}

function openSwitchAccount() {
  profilePopover.value?.hide();
  showSwitchAccount.value = true;
}

const userInitials = computed(() => {
  const u = auth.user;
  if (!u) return '';
  const fl = `${u.firstName?.[0] ?? ''}${u.lastName?.[0] ?? ''}`.toUpperCase();
  return fl || (u.email?.[0] ?? '?').toUpperCase();
});
const fullName = computed(
  () => [auth.user?.firstName, auth.user?.lastName].filter(Boolean).join(' ') || auth.user?.email || '',
);

const inboxPopover = ref<InstanceType<typeof Popover>>();
const { inboxCount, hasInbox, ensureLoaded, loadInbox, pendingJoinRequests } =
  useInvitationsInbox();

const myOrgs = computed(() => orgStore.organizations);
const myOrgIds = computed(() => new Set(myOrgs.value.map((o) => o.id)));

function enterOrg(orgId: number) {
  orgStore.setCurrentOrg(orgId);
  router.push({ name: 'home' });
}

const newOrgName = ref('');
const creating = ref(false);
const createError = ref<string | null>(null);

async function handleCreate() {
  if (!newOrgName.value.trim()) return;
  creating.value = true;
  createError.value = null;
  try {
    const org = await orgStore.createOrganization(newOrgName.value.trim());
    newOrgName.value = '';
    if (org) enterOrg(org.id);
  } catch (err) {
    createError.value = normalizeError(err, t('onboarding.createFailed')).message;
  } finally {
    creating.value = false;
  }
}

const searchQuery = ref('');
const searchResults = ref<OrgSearchResultDto[]>([]);
const searching = ref(false);
const joinSending = ref<number | null>(null);
const joinError = ref<string | null>(null);
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

const requestedOrgIds = computed(
  () => new Set(pendingJoinRequests.value.map((r) => r.organizationId)),
);

const visibleSearchResults = computed(() =>
  searchResults.value.filter((o) => !myOrgIds.value.has(o.id)),
);

watch(searchQuery, () => {
  if (debounceTimer) clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => runSearch(), 300);
});

async function runSearch() {
  searching.value = true;
  joinError.value = null;
  try {
    searchResults.value = await orgApi.search(searchQuery.value);
  } catch (err) {
    searchResults.value = [];
    notify(err, { fallback: t('onboarding.joinFailed') });
  } finally {
    searching.value = false;
  }
}

async function handleJoinRequest(org: OrgSearchResultDto) {
  joinSending.value = org.id;
  joinError.value = null;
  try {
    await orgApi.submitJoinRequest(org.id, 'I would like to join your organization.');
    await loadInbox();
  } catch (err) {
    joinError.value = normalizeError(err, t('onboarding.joinFailed')).message;
  } finally {
    joinSending.value = null;
  }
}

function handleLogout() {
  auth.logout();
  orgStore.clear();
  wsStore.clear();
  entityStore.clear();
  router.push({ name: 'login' });
}

onMounted(() => {
  ensureLoaded();
  runSearch();
  orgStore.fetchOrganizations().catch(() => undefined);
});
</script>

<template>
  <div class="onboarding-shell flex min-h-screen flex-col">
    <div class="onboarding-shell__bg" aria-hidden="true" />

    <AppHeader>
      <template #actions>
        <button
          type="button"
          class="hdr-icon"
          :aria-label="t('onboarding.invitations')"
          @click="inboxPopover?.toggle($event)"
        >
          <i class="pi pi-bell text-[17px]" />
          <span v-if="hasInbox" class="hdr-badge">{{ inboxCount }}</span>
        </button>

        <button
          v-if="auth.user"
          type="button"
          class="hdr-avatar rounded-full"
          :aria-label="fullName"
          @click="profilePopover?.toggle($event)"
        >
          {{ userInitials }}
        </button>
      </template>
    </AppHeader>

    <Popover ref="profilePopover" @hide="openLangFlyout = false">
      <div class="w-[252px]">
        <div class="profile-head">
          <span class="profile-avatar rounded-full">{{ userInitials }}</span>
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold text-ink-900">{{ fullName }}</p>
            <p class="truncate text-xs text-ink-400">{{ auth.user?.email }}</p>
          </div>
        </div>

        <div class="py-1">
          <div class="relative">
            <button type="button" class="profile-item" @click="openLangFlyout = !openLangFlyout">
              <i class="pi pi-globe" /><span class="flex-1">{{ t('nav.language') }}</span>
              <span class="text-xs text-ink-400">{{ localeLabel }}</span>
              <i
                :class="['pi text-[10px] text-ink-400', openLangFlyout ? 'pi-chevron-down' : 'pi-chevron-left']"
              />
            </button>
            <div v-if="openLangFlyout" class="profile-flyout">
              <button
                v-for="opt in languageOptions"
                :key="opt.value"
                type="button"
                class="profile-item"
                @click="selectLanguage(opt.value)"
              >
                <span class="flex-1 truncate">{{ opt.label }}</span>
                <i v-if="opt.value === current" class="pi pi-check text-xs text-brand-600" />
              </button>
            </div>
          </div>
        </div>

        <div class="border-t border-line py-1">
          <button type="button" class="profile-item" @click="openSwitchAccount">
            <i class="pi pi-sync" /><span class="flex-1">{{ t('nav.switchAccount') }}</span>
          </button>
          <button type="button" class="profile-item" @click="handleLogout">
            <i class="pi pi-sign-out" /><span class="flex-1">{{ t('nav.signOut') }}</span>
          </button>
        </div>
      </div>
    </Popover>

    <Popover ref="inboxPopover">
      <InvitationsInbox class="w-[320px]" @accepted="router.push({ name: 'home' })" />
    </Popover>

    <main class="relative flex flex-1 items-start justify-center px-4 py-10">
      <div class="w-full max-w-2xl space-y-6">
        <header class="text-center">
          <h1 class="text-2xl font-bold leading-tight text-ink-900">{{ t('onboarding.title') }}</h1>
          <p class="mt-1.5 text-sm text-ink-500">{{ t('onboarding.subtitle') }}</p>
        </header>

        <div v-if="myOrgs.length" class="border border-line bg-white">
          <div class="flex items-center gap-2 border-b border-line px-6 py-4">
            <i class="pi pi-building text-brand-600" />
            <h2 class="text-sm font-semibold text-ink-900">{{ t('onboarding.myOrganizations') }}</h2>
          </div>
          <ul class="divide-y divide-line">
            <li v-for="org in myOrgs" :key="org.id">
              <button
                type="button"
                class="flex w-full items-center justify-between gap-3 px-6 py-3.5 text-left transition-colors hover:bg-brand-50"
                @click="enterOrg(org.id)"
              >
                <span class="min-w-0">
                  <span class="block truncate text-sm font-medium text-ink-900">{{ org.name }}</span>
                  <span class="text-xs text-ink-500">
                    {{ t('onboarding.members', { n: org.memberCount }, org.memberCount) }}
                  </span>
                </span>
                <span class="flex shrink-0 items-center gap-3">
                  <span v-if="org.userRole" :class="roleBadgeFullClass(org.userRole.toLowerCase())">
                    {{ roleLabel(org.userRole, org.userRoleDisplayName) }}
                  </span>
                  <i class="pi pi-angle-right text-ink-300" />
                </span>
              </button>
            </li>
          </ul>
        </div>

        <div class="border border-line bg-white">
          <div class="flex items-center gap-2 border-b border-line px-6 py-4">
            <i class="pi pi-plus-circle text-brand-600" />
            <h2 class="text-sm font-semibold text-ink-900">{{ t('onboarding.create') }}</h2>
          </div>
          <form class="p-6" novalidate @submit.prevent="handleCreate">
            <p class="mb-4 text-[13px] text-ink-500">{{ t('onboarding.createHint') }}</p>
            <div class="flex flex-col gap-1.5">
              <label for="orgName" class="text-xs font-medium text-ink-600">
                {{ t('onboarding.orgNameLabel') }}
              </label>
              <InputText id="orgName" v-model="newOrgName" maxlength="120" class="!h-10" />
            </div>
            <FormError v-if="createError" :message="createError" class="mt-2" />
            <div class="mt-4 flex justify-end">
              <button type="submit" class="btn btn-primary" :disabled="!newOrgName.trim() || creating">
                <i :class="creating ? 'pi pi-spin pi-spinner' : 'pi pi-plus'" />
                {{ t('onboarding.createButton') }}
              </button>
            </div>
          </form>
        </div>

        <div class="border border-line bg-white">
          <div class="flex items-center gap-2 border-b border-line px-6 py-4">
            <i class="pi pi-compass text-brand-600" />
            <h2 class="text-sm font-semibold text-ink-900">{{ t('onboarding.join') }}</h2>
          </div>
          <div class="p-6">
            <p class="mb-4 text-[13px] text-ink-500">{{ t('onboarding.joinHint') }}</p>
            <div class="flex flex-col gap-1.5">
              <label for="searchOrg" class="text-xs font-medium text-ink-600">
                {{ t('onboarding.explorePlaceholder') }}
              </label>
              <InputText id="searchOrg" v-model="searchQuery" class="!h-10" />
            </div>

            <p v-if="searching" class="py-6 text-center text-sm text-ink-500">
              {{ searchQuery.trim() ? t('onboarding.searching') : t('onboarding.browsing') }}
            </p>

            <ul
              v-else-if="visibleSearchResults.length"
              class="nav-scroll mt-4 flex max-h-[300px] flex-col gap-2 overflow-y-auto pr-0.5"
            >
              <li
                v-for="org in visibleSearchResults"
                :key="org.id"
                class="flex items-center justify-between gap-3 border border-line px-4 py-3"
              >
                <div class="min-w-0 flex-1">
                  <p class="truncate text-sm font-medium text-ink-900">{{ org.name }}</p>
                  <div class="mt-0.5 flex items-center gap-2">
                    <span class="text-xs text-ink-500">
                      {{ t('onboarding.members', { n: org.memberCount }, org.memberCount) }}
                    </span>
                    <Tag
                      :value="org.joinPolicy === 'open' ? t('onboarding.open') : t('onboarding.inviteOnly')"
                      :severity="org.joinPolicy === 'open' ? 'success' : 'secondary'"
                      class="!text-[10px]"
                    />
                  </div>
                </div>
                <span
                  v-if="requestedOrgIds.has(org.id)"
                  class="inline-flex shrink-0 items-center px-2 py-0.5 text-[11px] font-semibold bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200"
                >
                  {{ t('onboarding.requestSent') }}
                </span>
                <button
                  v-else
                  type="button"
                  class="btn btn-outline btn-sm shrink-0"
                  :disabled="org.joinPolicy !== 'open' || joinSending === org.id"
                  @click="handleJoinRequest(org)"
                >
                  <i v-if="joinSending === org.id" class="pi pi-spin pi-spinner" />
                  {{ t('onboarding.requestToJoin') }}
                </button>
              </li>
            </ul>

            <p v-else class="py-6 text-center text-sm text-ink-500">
              {{ t('onboarding.noOrgsFound') }}
            </p>

            <FormError v-if="joinError" :message="joinError" class="mt-2" />
          </div>
        </div>
      </div>
    </main>

    <footer
      class="relative z-10 flex items-center justify-center gap-2 border-t border-line bg-white/70 px-6 py-3 text-[11px] text-ink-400 backdrop-blur-sm"
    >
      <span>{{ t('footer.copyright', { year }) }}</span>
      <span aria-hidden="true">·</span>
      <span>{{ t('footer.rights') }}</span>
    </footer>

    <SwitchAccountDialog v-model:visible="showSwitchAccount" />
  </div>
</template>

<style scoped>
.onboarding-shell {
  position: relative;
  background-color: #f8fafc;
  isolation: isolate;
  overflow: hidden;
}

.onboarding-shell__bg {
  position: absolute;
  inset: 0;
  z-index: -1;
  background-image:
    radial-gradient(circle at 12% 18%, rgba(37, 99, 235, 0.1) 0%, rgba(37, 99, 235, 0) 42%),
    radial-gradient(circle at 88% 82%, rgba(59, 130, 246, 0.08) 0%, rgba(59, 130, 246, 0) 45%),
    linear-gradient(180deg, #ffffff 0%, #f1f5fb 100%);
}

.onboarding-shell::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -1;
  background-image:
    linear-gradient(to right, rgba(37, 99, 235, 0.06) 1px, transparent 1px),
    linear-gradient(to bottom, rgba(37, 99, 235, 0.06) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.6), transparent 70%);
  -webkit-mask-image: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.6), transparent 70%);
}

.hdr-icon {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.5rem;
  height: 2.5rem;
  border: 1px solid #e2e8f0;
  background: #fff;
  color: rgb(71 85 105);
  transition: color 0.15s ease, border-color 0.15s ease, background-color 0.15s ease;
}
.hdr-icon:hover {
  color: rgb(37 99 235);
  border-color: rgb(147 197 253);
  background: rgb(239 246 255);
}

.hdr-badge {
  position: absolute;
  top: -0.375rem;
  right: -0.375rem;
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 1rem;
  height: 1rem;
  padding: 0 0.25rem;
  border-radius: 9999px;
  background: rgb(37 99 235);
  color: #fff;
  font-size: 10px;
  font-weight: 600;
  line-height: 1;
}

.hdr-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.5rem;
  height: 2.5rem;
  border-radius: 9999px;
  background: rgb(37 99 235);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 600;
}

.profile-head {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 0.25rem 0.5rem 0.75rem;
  margin-bottom: 0.25rem;
  border-bottom: 1px solid #e2e8f0;
}
.profile-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  flex-shrink: 0;
  border-radius: 9999px;
  background: rgb(37 99 235);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 600;
}

.profile-item {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  width: 100%;
  padding: 0.5rem 0.75rem;
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

.profile-flyout {
  position: absolute;
  right: 100%;
  top: 0;
  margin-right: 0.25rem;
  z-index: 10;
  width: 11rem;
  background: #fff;
  border: 1px solid #e2e8f0;
  box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.15), 0 8px 10px -6px rgba(15, 23, 42, 0.1);
  padding: 0.25rem 0;
}
</style>
