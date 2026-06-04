<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import InputText from 'primevue/inputtext';
import FloatLabel from 'primevue/floatlabel';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Popover from 'primevue/popover';
import AppHeader from '@/components/layout/AppHeader.vue';
import FormError from '@/components/feedback/FormError.vue';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';
import { currentLocale } from '@/i18n';
import { useLocaleSwitch } from '@/i18n/useLocale';
import type { AppLocale } from '@/i18n';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  orgApi,
  type OrganizationDto,
  type OrgSearchResultDto,
  type OrgInvitationDto,
  type JoinRequestDto,
} from '@/api/organizations';
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
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(currentLocale());
}

const langPopover = ref<InstanceType<typeof Popover>>();
const languageOptions = computed(() =>
  locales.map((code) => ({ value: code, label: t(`language.${code}`) })),
);
async function selectLanguage(next: AppLocale) {
  langPopover.value?.hide();
  if (next === current.value) return;
  try {
    await changeLocale(next);
  } catch (err) {
    notifyGlobal(err, { fallback: normalizeError(err).message });
  }
}

const inboxPopover = ref<InstanceType<typeof Popover>>();
const orgInvitations = ref<OrgInvitationDto[]>([]);
const pendingJoinRequests = ref<JoinRequestDto[]>([]);
const inboxLoading = ref(true);
const inboxError = ref<string | null>(null);
const inboxFilter = ref('');
const busyToken = ref<string | null>(null);
const busyRequestId = ref<number | null>(null);

const inboxCount = computed(
  () => orgInvitations.value.length + pendingJoinRequests.value.length,
);
const hasInbox = computed(() => inboxCount.value > 0);

const filteredInvitations = computed(() => {
  const q = inboxFilter.value.trim().toLowerCase();
  if (!q) return orgInvitations.value;
  return orgInvitations.value.filter((i) => i.organizationName.toLowerCase().includes(q));
});
const filteredRequests = computed(() => {
  const q = inboxFilter.value.trim().toLowerCase();
  if (!q) return pendingJoinRequests.value;
  return pendingJoinRequests.value.filter((r) => r.organizationName.toLowerCase().includes(q));
});

async function loadInbox() {
  inboxLoading.value = true;
  try {
    const [invitations, joinReqs] = await Promise.all([
      orgApi.myOrganizationInvitations(),
      orgApi.myJoinRequests(),
    ]);
    orgInvitations.value = invitations;
    pendingJoinRequests.value = joinReqs.filter((r) => r.status === 'Pending');
  } catch {
    orgInvitations.value = [];
    pendingJoinRequests.value = [];
  } finally {
    inboxLoading.value = false;
  }
}

async function acceptInvite(invitation: OrgInvitationDto) {
  busyToken.value = invitation.token;
  inboxError.value = null;
  try {
    await orgApi.acceptOrgInvitation(invitation.token);
    await orgStore.fetchOrganizations();
    router.push({ name: 'home' });
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.acceptFailed')).message;
  } finally {
    busyToken.value = null;
  }
}

async function declineInvite(invitation: OrgInvitationDto) {
  busyToken.value = invitation.token;
  inboxError.value = null;
  try {
    await orgApi.declineOrgInvitation(invitation.token);
    orgInvitations.value = orgInvitations.value.filter((i) => i.id !== invitation.id);
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.declineFailed')).message;
  } finally {
    busyToken.value = null;
  }
}

async function cancelRequest(request: JoinRequestDto) {
  busyRequestId.value = request.id;
  inboxError.value = null;
  try {
    await orgApi.cancelMyJoinRequest(request.id);
    pendingJoinRequests.value = pendingJoinRequests.value.filter((r) => r.id !== request.id);
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.cancelFailed')).message;
  } finally {
    busyRequestId.value = null;
  }
}

async function clearAll() {
  inboxError.value = null;
  const tokens = orgInvitations.value.map((i) => i.token);
  const ids = pendingJoinRequests.value.map((r) => r.id);
  try {
    await Promise.all([
      ...tokens.map((tk) => orgApi.declineOrgInvitation(tk)),
      ...ids.map((id) => orgApi.cancelMyJoinRequest(id)),
    ]);
    orgInvitations.value = [];
    pendingJoinRequests.value = [];
    inboxPopover.value?.hide();
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.declineFailed')).message;
    await loadInbox();
  }
}

const myOrgs = computed(() => orgStore.organizations);
const selectedOrgId = ref<number | null>(null);
const selectedOrg = computed(
  () => myOrgs.value.find((o) => o.id === selectedOrgId.value) ?? null,
);

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
    if (org) selectedOrgId.value = org.id;
  } catch (err) {
    createError.value = normalizeError(err, t('onboarding.createFailed')).message;
  } finally {
    creating.value = false;
  }
}

function openOrg() {
  if (!selectedOrg.value) return;
  orgStore.setCurrentOrg(selectedOrg.value.id);
  router.push({ name: 'home' });
}

function openSettings() {
  if (!selectedOrg.value) return;
  orgStore.setCurrentOrg(selectedOrg.value.id);
  router.push({ name: 'org-settings' });
}

const invitePopover = ref<InstanceType<typeof Popover>>();
const inviteEmail = ref('');
const inviteSending = ref(false);
const inviteError = ref<string | null>(null);
const inviteSuccess = ref<string | null>(null);

function toggleInvite(event: Event) {
  inviteError.value = null;
  inviteSuccess.value = null;
  invitePopover.value?.toggle(event);
}

async function sendInvite() {
  if (!selectedOrg.value) return;
  const email = inviteEmail.value.trim();
  inviteError.value = null;
  inviteSuccess.value = null;
  if (!emailPattern.test(email)) {
    inviteError.value = t('onboarding.inviteInvalidEmail');
    return;
  }
  inviteSending.value = true;
  try {
    await orgApi.invite(selectedOrg.value.id, email);
    inviteSuccess.value = t('onboarding.inviteSent', { email });
    inviteEmail.value = '';
  } catch (err) {
    inviteError.value = normalizeError(err, t('onboarding.inviteFailed')).message;
  } finally {
    inviteSending.value = false;
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
  loadInbox();
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
          :aria-label="t('language.label')"
          @click="langPopover?.toggle($event)"
        >
          <i class="pi pi-globe text-[17px]" />
        </button>

        <button
          type="button"
          class="hdr-icon"
          :aria-label="t('onboarding.invitations')"
          @click="inboxPopover?.toggle($event)"
        >
          <i class="pi pi-envelope text-[17px]" />
          <span v-if="hasInbox" class="hdr-badge">{{ inboxCount }}</span>
        </button>

        <button
          type="button"
          class="hdr-icon"
          :aria-label="t('onboarding.signOut')"
          @click="handleLogout"
        >
          <i class="pi pi-sign-out text-[17px]" />
        </button>
      </template>
    </AppHeader>

    <Popover ref="langPopover">
      <ul class="nav-scroll min-w-[160px] max-h-60 overflow-y-auto py-1">
        <li v-for="opt in languageOptions" :key="opt.value">
          <button
            type="button"
            :class="[
              'flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm transition-colors hover:bg-brand-50',
              opt.value === current ? 'font-medium text-brand-700' : 'text-ink-700',
            ]"
            @click="selectLanguage(opt.value)"
          >
            <span class="truncate">{{ opt.label }}</span>
            <i v-if="opt.value === current" class="pi pi-check shrink-0 text-xs text-brand-600" />
          </button>
        </li>
      </ul>
    </Popover>

    <Popover ref="inboxPopover">
      <div class="w-[320px]">
        <div class="mb-2 flex items-center justify-between">
          <span class="text-xs font-semibold uppercase tracking-wide text-ink-500">
            {{ t('onboarding.invitations') }}
          </span>
          <button
            v-if="hasInbox"
            type="button"
            class="text-xs font-medium text-brand-600 hover:underline"
            @click="clearAll"
          >
            {{ t('onboarding.clearAll') }}
          </button>
        </div>

        <InputText
          v-if="hasInbox"
          v-model="inboxFilter"
          :placeholder="t('onboarding.filterPlaceholder')"
          class="!h-8 w-full !text-sm"
        />

        <LoadingSkeleton
          v-if="inboxLoading"
          variant="list"
          :rows="2"
          class="mt-3"
          :label="t('onboarding.invitations')"
        />

        <p v-else-if="!hasInbox" class="py-6 text-center text-sm text-ink-500">
          <i class="pi pi-inbox mb-2 block text-2xl text-ink-400" />
          {{ t('onboarding.invitationsEmpty') }}
        </p>

        <div v-else class="nav-scroll mt-2 max-h-72 overflow-y-auto">
          <template v-if="filteredInvitations.length">
            <p class="px-0.5 pb-1 pt-1 text-[11px] font-semibold uppercase text-ink-400">
              {{ t('onboarding.incomingHeading') }}
            </p>
            <div
              v-for="inv in filteredInvitations"
              :key="`inv-${inv.id}`"
              class="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0"
            >
              <div class="min-w-0">
                <p class="truncate text-sm font-medium text-ink-900">{{ inv.organizationName }}</p>
                <p class="text-[11px] text-ink-500">
                  {{ t('onboarding.expires', { date: formatDate(inv.expiresAt) }) }}
                </p>
              </div>
              <div class="flex shrink-0 items-center gap-1">
                <Button
                  size="small"
                  severity="secondary"
                  text
                  :loading="busyToken === inv.token"
                  :aria-label="t('onboarding.revoke')"
                  @click="declineInvite(inv)"
                >
                  <i class="pi pi-times text-xs" />
                </Button>
                <Button
                  size="small"
                  :label="t('onboarding.accept')"
                  :loading="busyToken === inv.token"
                  @click="acceptInvite(inv)"
                />
              </div>
            </div>
          </template>

          <template v-if="filteredRequests.length">
            <p class="px-0.5 pb-1 pt-3 text-[11px] font-semibold uppercase text-ink-400">
              {{ t('onboarding.myRequestsHeading') }}
            </p>
            <div
              v-for="req in filteredRequests"
              :key="`req-${req.id}`"
              class="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0"
            >
              <div class="min-w-0">
                <p class="truncate text-sm font-medium text-ink-900">{{ req.organizationName }}</p>
                <p class="text-[11px] text-ink-500">
                  {{ t('onboarding.requested', { date: formatDate(req.createdAt) }) }}
                </p>
              </div>
              <div class="flex shrink-0 items-center gap-1.5">
                <Tag :value="t('onboarding.pendingReview')" severity="warn" class="!text-[10px]" />
                <Button
                  size="small"
                  severity="secondary"
                  text
                  :loading="busyRequestId === req.id"
                  :aria-label="t('onboarding.revoke')"
                  @click="cancelRequest(req)"
                >
                  <i class="pi pi-times text-xs" />
                </Button>
              </div>
            </div>
          </template>
        </div>

        <FormError v-if="inboxError" :message="inboxError" class="mt-2" />
      </div>
    </Popover>

    <Popover ref="invitePopover">
      <div class="w-[280px]">
        <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-500">
          {{ t('onboarding.invitePeople') }}
        </p>
        <form class="flex flex-col gap-2" novalidate @submit.prevent="sendInvite">
          <FloatLabel variant="on">
            <InputText id="inviteEmail" v-model="inviteEmail" type="email" class="!h-10 w-full" />
            <label for="inviteEmail">{{ t('onboarding.inviteEmailLabel') }}</label>
          </FloatLabel>
          <FormError v-if="inviteError" :message="inviteError" />
          <p v-if="inviteSuccess" class="text-xs text-emerald-600">{{ inviteSuccess }}</p>
          <Button
            type="submit"
            size="small"
            :label="t('onboarding.sendInvite')"
            :loading="inviteSending"
            class="w-full"
          />
        </form>
      </div>
    </Popover>

    <main class="relative flex flex-1 items-center justify-center px-4 py-10">
      <div class="w-full max-w-[940px]">
        <header class="mb-7 text-center">
          <h1 class="text-[26px] font-bold leading-tight text-ink-900">
            {{ t('onboarding.title') }}
          </h1>
          <p class="mt-1.5 text-sm text-ink-500">{{ t('onboarding.subtitle') }}</p>
        </header>

        <div
          class="grid border border-line/80 bg-white/95 shadow-card backdrop-blur-sm md:grid-cols-[1fr_auto_1fr]"
        >
          <section class="flex flex-col p-7">
            <div class="flex items-center gap-2">
              <span class="flex h-8 w-8 items-center justify-center bg-brand-50 text-brand-600">
                <i class="pi pi-building text-sm" />
              </span>
              <h2 class="text-base font-semibold text-ink-900">{{ t('onboarding.create') }}</h2>
            </div>
            <p class="mt-1.5 text-[13px] text-ink-500">{{ t('onboarding.createHint') }}</p>

            <form class="mt-5 flex items-end gap-2" novalidate @submit.prevent="handleCreate">
              <FloatLabel variant="on" class="flex-1">
                <InputText id="orgName" v-model="newOrgName" class="!h-11 w-full" />
                <label for="orgName">{{ t('onboarding.orgNameLabel') }}</label>
              </FloatLabel>
              <Button
                type="submit"
                :loading="creating"
                :disabled="!newOrgName.trim()"
                :aria-label="t('onboarding.createButton')"
                :class="[
                  '!h-11 !w-11 !rounded-none transition-colors',
                  newOrgName.trim()
                    ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
                    : '!bg-slate-200 !border-slate-200 !text-slate-400',
                ]"
              >
                <i class="pi pi-plus" />
              </Button>
            </form>
            <FormError v-if="createError" :message="createError" class="mt-2" />

            <div class="mt-6 flex min-h-0 flex-1 flex-col">
              <span class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">
                {{ t('onboarding.myOrganizations') }}
              </span>

              <div class="mb-2 flex items-center gap-1 border border-line bg-surface px-2 py-1.5">
                <button
                  type="button"
                  class="tb-btn"
                  :disabled="!selectedOrg"
                  :aria-label="t('onboarding.openOrg')"
                  @click="openOrg"
                >
                  <i class="pi pi-sign-in" />
                  <span>{{ t('onboarding.openOrg') }}</span>
                </button>
                <button
                  type="button"
                  class="tb-btn"
                  :disabled="!selectedOrg"
                  :aria-label="t('onboarding.invitePeople')"
                  @click="toggleInvite"
                >
                  <i class="pi pi-user-plus" />
                  <span>{{ t('onboarding.invitePeople') }}</span>
                </button>
                <button
                  type="button"
                  class="tb-btn"
                  :disabled="!selectedOrg"
                  :aria-label="t('onboarding.orgSettingsAction')"
                  @click="openSettings"
                >
                  <i class="pi pi-cog" />
                  <span>{{ t('onboarding.orgSettingsAction') }}</span>
                </button>
              </div>

              <ul
                v-if="myOrgs.length"
                class="nav-scroll flex max-h-[220px] flex-1 flex-col gap-1 overflow-y-auto pr-0.5"
              >
                <li v-for="org in myOrgs" :key="org.id">
                  <button
                    type="button"
                    :class="[
                      'flex w-full items-center justify-between gap-2 border px-3 py-2.5 text-left transition-colors',
                      selectedOrgId === org.id
                        ? 'border-brand-300 bg-brand-50'
                        : 'border-line hover:border-brand-200 hover:bg-surface',
                    ]"
                    @click="selectedOrgId = org.id"
                  >
                    <span class="min-w-0">
                      <span class="block truncate text-sm font-medium text-ink-900">{{ org.name }}</span>
                      <span class="text-xs text-ink-500">
                        {{ t('onboarding.members', { n: org.memberCount }, org.memberCount) }}
                      </span>
                    </span>
                    <span
                      v-if="org.userRole"
                      class="shrink-0 text-[11px] font-medium uppercase text-ink-400"
                    >
                      {{ org.userRole }}
                    </span>
                  </button>
                </li>
              </ul>

              <p v-else class="py-4 text-center text-sm text-ink-500">
                {{ t('onboarding.noOrganizationsYet') }}
              </p>
            </div>
          </section>

          <div class="flex items-center justify-center px-4 py-2 md:flex-col md:px-2 md:py-6">
            <span class="h-px w-full bg-line md:h-full md:w-px" />
            <span class="px-3 text-[11px] font-semibold uppercase tracking-wide text-ink-400 md:py-3">
              {{ t('onboarding.or') }}
            </span>
            <span class="h-px w-full bg-line md:h-full md:w-px" />
          </div>

          <section class="flex flex-col p-7">
            <div class="flex items-center gap-2">
              <span class="flex h-8 w-8 items-center justify-center bg-brand-50 text-brand-600">
                <i class="pi pi-compass text-sm" />
              </span>
              <h2 class="text-base font-semibold text-ink-900">{{ t('onboarding.join') }}</h2>
            </div>
            <p class="mt-1.5 text-[13px] text-ink-500">{{ t('onboarding.joinHint') }}</p>

            <div class="mt-5 flex min-h-0 flex-1 flex-col gap-3">
              <FloatLabel variant="on">
                <InputText id="searchOrg" v-model="searchQuery" class="!h-11 w-full" />
                <label for="searchOrg">{{ t('onboarding.explorePlaceholder') }}</label>
              </FloatLabel>

              <p v-if="searching" class="py-4 text-center text-sm text-ink-500">
                {{ searchQuery.trim() ? t('onboarding.searching') : t('onboarding.browsing') }}
              </p>

              <ul
                v-else-if="searchResults.length"
                class="nav-scroll flex max-h-[268px] flex-1 flex-col gap-2 overflow-y-auto pr-0.5"
              >
                <li
                  v-for="org in searchResults"
                  :key="org.id"
                  class="flex items-center justify-between gap-3 rounded-lg border border-line px-4 py-3"
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
                  <Tag
                    v-if="requestedOrgIds.has(org.id)"
                    :value="t('onboarding.requestSent')"
                    severity="warn"
                    class="shrink-0 !text-[11px]"
                  />
                  <Button
                    v-else
                    size="small"
                    outlined
                    severity="secondary"
                    :label="t('onboarding.requestToJoin')"
                    :disabled="org.joinPolicy !== 'open'"
                    :loading="joinSending === org.id"
                    class="shrink-0"
                    @click="handleJoinRequest(org)"
                  />
                </li>
              </ul>

              <p v-else class="py-4 text-center text-sm text-ink-500">
                {{ t('onboarding.noOrgsFound') }}
              </p>

              <FormError v-if="joinError" :message="joinError" />
            </div>
          </section>
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

.tb-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.3rem 0.6rem;
  border: 1px solid transparent;
  font-size: 0.75rem;
  font-weight: 500;
  color: rgb(71 85 105);
  transition: color 0.15s ease, border-color 0.15s ease, background-color 0.15s ease;
}
.tb-btn:hover:not(:disabled) {
  color: rgb(37 99 235);
  border-color: rgb(147 197 253);
  background: #fff;
}
.tb-btn:disabled {
  color: rgb(203 213 225);
  cursor: not-allowed;
}
</style>
