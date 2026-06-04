<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  orgApi,
  type OrganizationDto,
  type OrgInvitationDto,
  type JoinRequestDto,
} from '@/api/organizations';
import { normalizeError } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const router = useRouter();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const { notify } = useApiErrorHandler();

type Tab = 'my-orgs' | 'invitations' | 'create' | 'join';
const activeTab = ref<Tab>('create');

// --- My organizations ---
const myOrgs = computed(() => orgStore.organizations);
const hasMyOrgs = computed(() => myOrgs.value.length > 0);

function enterOrg(orgId: number) {
  orgStore.setCurrentOrg(orgId);
  router.push({ name: 'home' });
}

// Tabs shown depend on whether user already belongs to orgs
const visibleTabs = computed<Tab[]>(() =>
  hasMyOrgs.value
    ? ['my-orgs', 'invitations', 'create', 'join']
    : ['invitations', 'create', 'join'],
);

// --- Invitations / join requests ---
const orgInvitations = ref<OrgInvitationDto[]>([]);
const pendingJoinRequests = ref<JoinRequestDto[]>([]);
const inboxLoading = ref(true);
const acceptingToken = ref<string | null>(null);
const inboxError = ref<string | null>(null);

const pendingOrgInvitations = computed(() => orgInvitations.value);
const hasInboxItems = computed(
  () => pendingOrgInvitations.value.length > 0 || pendingJoinRequests.value.length > 0,
);

async function loadInbox() {
  inboxLoading.value = true;
  try {
    // Ensure orgs are loaded (may not be if navigated here directly)
    if (!orgStore.organizations.length) {
      await orgStore.fetchOrganizations();
    }

    const [invitations, joinReqs] = await Promise.all([
      orgApi.myOrganizationInvitations(),
      orgApi.myJoinRequests(),
    ]);
    orgInvitations.value = invitations;
    pendingJoinRequests.value = joinReqs.filter((r) => r.status === 'Pending');

    // Default tab priority: my orgs → inbox → create
    if (hasMyOrgs.value) {
      activeTab.value = 'my-orgs';
    } else if (hasInboxItems.value) {
      activeTab.value = 'invitations';
    }
  } catch {
    orgInvitations.value = [];
    pendingJoinRequests.value = [];
  } finally {
    inboxLoading.value = false;
  }
}

async function acceptOrgInvite(token: string) {
  acceptingToken.value = token;
  inboxError.value = null;
  try {
    await orgApi.acceptOrgInvitation(token);
    await orgStore.fetchOrganizations();
    router.push({ name: 'home' });
  } catch (err) {
    inboxError.value = normalizeError(err, 'Failed to accept invitation.').message;
  } finally {
    acceptingToken.value = null;
  }
}

onMounted(loadInbox);

// --- Create org ---
const newOrgName = ref('');
const creating = ref(false);
const createError = ref<string | null>(null);

async function handleCreate() {
  if (!newOrgName.value.trim()) return;
  creating.value = true;
  createError.value = null;
  try {
    await orgStore.createOrganization(newOrgName.value.trim());
    router.push({ name: 'home' });
  } catch (err) {
    createError.value = normalizeError(err, 'Failed to create organization.').message;
  } finally {
    creating.value = false;
  }
}

// --- Join org ---
const searchQuery = ref('');
const searchResults = ref<OrganizationDto[]>([]);
const searching = ref(false);
const joinSending = ref<number | null>(null);
const joinMessage = ref<string | null>(null);
const joinError = ref<string | null>(null);

let debounceTimer: ReturnType<typeof setTimeout> | null = null;

watch(searchQuery, (q) => {
  if (debounceTimer) clearTimeout(debounceTimer);
  if (q.trim().length < 2) {
    searchResults.value = [];
    return;
  }
  debounceTimer = setTimeout(() => searchOrgs(q.trim()), 350);
});

async function searchOrgs(q: string) {
  searching.value = true;
  try {
    searchResults.value = await orgApi.search(q);
  } catch (err) {
    searchResults.value = [];
    notify(err, { fallback: 'Search failed.' });
  } finally {
    searching.value = false;
  }
}

async function handleJoinRequest(orgId: number) {
  joinSending.value = orgId;
  joinMessage.value = null;
  joinError.value = null;
  try {
    await orgApi.submitJoinRequest(orgId, 'I would like to join your organization.');
    joinMessage.value = 'Join request sent. An administrator will review it.';
  } catch (err) {
    joinError.value = normalizeError(err, 'Failed to send join request.').message;
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

function tabLabel(tab: Tab): string {
  switch (tab) {
    case 'my-orgs': return 'My organizations';
    case 'invitations': return 'Invitations';
    case 'create': return 'Create organization';
    case 'join': return 'Join organization';
  }
}
</script>

<template>
  <AuthLayout>
    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
      {{ hasMyOrgs ? 'Choose organization' : 'Get started' }}
    </h1>
    <p class="mt-1 text-[13px] text-ink-500">
      {{ hasMyOrgs
        ? 'Select the organization you want to work in, or join a new one.'
        : 'Create a new organization or join an existing one.' }}
    </p>

    <!-- Tab bar -->
    <div class="mt-5 flex border-b border-line">
      <button
        v-for="tab in visibleTabs"
        :key="tab"
        :class="[
          'flex-1 pb-2.5 text-sm font-medium transition-colors relative',
          activeTab === tab
            ? 'border-b-2 border-brand-600 text-brand-600'
            : 'text-ink-500 hover:text-ink-700',
        ]"
        @click="activeTab = tab"
      >
        {{ tabLabel(tab) }}
        <Tag
          v-if="tab === 'invitations' && hasInboxItems"
          :value="pendingOrgInvitations.length + pendingJoinRequests.length"
          severity="info"
          class="!ml-1.5 !text-[10px] !py-0 !px-1.5"
        />
      </button>
    </div>

    <!-- My organizations -->
    <div
      v-if="activeTab === 'my-orgs'"
      class="mt-5 flex flex-col gap-3"
    >
      <LoadingSkeleton
        v-if="inboxLoading"
        variant="list"
        :rows="2"
        label="Loading organizations"
      />
      <ul v-else class="flex flex-col gap-2 max-h-72 overflow-y-auto">
        <li
          v-for="org in myOrgs"
          :key="org.id"
          class="flex items-center justify-between rounded-lg border border-line px-4 py-3"
        >
          <div class="min-w-0">
            <p class="text-sm font-medium text-ink-900 truncate">{{ org.name }}</p>
            <p v-if="org.memberCount != null" class="text-xs text-ink-500">
              {{ org.memberCount }} {{ org.memberCount === 1 ? 'member' : 'members' }}
            </p>
          </div>
          <Button
            size="small"
            label="Enter"
            @click="enterOrg(org.id)"
          />
        </li>
      </ul>
    </div>

    <!-- Invitations -->
    <div
      v-if="activeTab === 'invitations'"
      class="mt-5 flex flex-col gap-3"
    >
      <LoadingSkeleton
        v-if="inboxLoading"
        variant="list"
        :rows="2"
        label="Loading invitations"
      />

      <div
        v-else-if="!hasInboxItems"
        class="text-center text-sm text-ink-500 py-6"
      >
        <i class="pi pi-inbox text-2xl text-ink-400 block mb-2" />
        No pending invitations. Switch to
        <button
          class="text-brand-600 hover:underline font-medium"
          @click="activeTab = 'create'"
        >
          create
        </button>
        or
        <button
          class="text-brand-600 hover:underline font-medium"
          @click="activeTab = 'join'"
        >
          join
        </button>
        to continue.
      </div>

      <div v-else class="flex flex-col gap-2">
        <!-- Org invitations -->
        <ul v-if="pendingOrgInvitations.length" class="flex flex-col gap-2">
          <li
            v-for="inv in pendingOrgInvitations"
            :key="inv.id"
            class="flex items-center justify-between rounded-lg border border-line px-4 py-3"
          >
            <div class="min-w-0">
              <p class="text-sm font-medium text-ink-900 truncate">
                {{ inv.organizationName }}
              </p>
              <p class="text-xs text-ink-500">
                Expires {{ new Date(inv.expiresAt).toLocaleDateString() }}
              </p>
            </div>
            <Button
              size="small"
              label="Accept"
              :loading="acceptingToken === inv.token"
              @click="acceptOrgInvite(inv.token)"
            />
          </li>
        </ul>

        <!-- Pending join requests -->
        <ul v-if="pendingJoinRequests.length" class="flex flex-col gap-2">
          <li
            v-for="req in pendingJoinRequests"
            :key="req.id"
            class="flex items-center justify-between rounded-lg border border-line px-4 py-3"
          >
            <div class="min-w-0">
              <p class="text-sm font-medium text-ink-900 truncate">
                {{ req.organizationName }}
              </p>
              <p class="text-xs text-ink-500">
                Requested {{ new Date(req.createdAt).toLocaleDateString() }}
              </p>
            </div>
            <Tag value="Pending review" severity="warn" class="!text-[11px]" />
          </li>
        </ul>
      </div>

      <Message
        v-if="inboxError"
        severity="error"
        :closable="false"
        class="!my-0"
      >
        {{ inboxError }}
      </Message>
    </div>

    <!-- Create org -->
    <form
      v-if="activeTab === 'create'"
      class="mt-5 flex flex-col gap-4"
      novalidate
      @submit.prevent="handleCreate"
    >
      <div class="flex flex-col gap-1.5">
        <label for="orgName" class="text-xs font-medium text-ink-600">
          Organization name <span class="text-danger">*</span>
        </label>
        <InputText
          id="orgName"
          v-model="newOrgName"
          placeholder="My Company"
          class="!h-10"
        />
      </div>

      <Message v-if="createError" severity="error" :closable="false" class="!my-0">
        {{ createError }}
      </Message>

      <Button
        type="submit"
        :loading="creating"
        :class="[
          '!h-11 !rounded-none !font-semibold w-full transition-colors',
          newOrgName.trim()
            ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
            : '!bg-slate-200 !border-slate-200 !text-slate-400',
        ]"
      >
        Create organization
      </Button>
    </form>

    <!-- Join org -->
    <div v-else-if="activeTab === 'join'" class="mt-5 flex flex-col gap-4">
      <div class="flex flex-col gap-1.5">
        <label for="searchOrg" class="text-xs font-medium text-ink-600">
          Search by name
        </label>
        <InputText
          id="searchOrg"
          v-model="searchQuery"
          placeholder="Type at least 2 characters..."
          class="!h-10"
        />
      </div>

      <div
        v-if="searching"
        class="text-center text-sm text-ink-500 py-4"
      >
        Searching...
      </div>

      <ul
        v-else-if="searchResults.length"
        class="flex flex-col gap-2 max-h-64 overflow-y-auto"
      >
        <li
          v-for="org in searchResults"
          :key="org.id"
          class="flex items-center justify-between rounded-lg border border-line px-4 py-3"
        >
          <div>
            <p class="text-sm font-medium text-ink-900">{{ org.name }}</p>
            <p class="text-xs text-ink-500">
              {{ org.memberCount }} {{ org.memberCount === 1 ? 'member' : 'members' }}
            </p>
          </div>
          <Button
            size="small"
            severity="secondary"
            :loading="joinSending === org.id"
            @click="handleJoinRequest(org.id)"
          >
            Request to join
          </Button>
        </li>
      </ul>

      <p
        v-else-if="searchQuery.trim().length >= 2"
        class="text-center text-sm text-ink-500 py-4"
      >
        No organizations found.
      </p>

      <Message v-if="joinMessage" severity="success" :closable="false" class="!my-0">
        {{ joinMessage }}
      </Message>
      <Message v-if="joinError" severity="error" :closable="false" class="!my-0">
        {{ joinError }}
      </Message>
    </div>

    <!-- Sign out -->
    <p class="mt-6 text-center text-[13px] text-ink-500">
      <button class="font-medium text-brand-600 hover:underline" @click="handleLogout">
        Sign out
      </button>
    </p>
  </AuthLayout>
</template>
