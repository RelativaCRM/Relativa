<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import Message from 'primevue/message';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { orgApi, type OrganizationDto } from '@/api/organizations';
import { ApiError } from '@/api/http';

const router = useRouter();
const auth = useAuthStore();
const orgStore = useOrganizationStore();

type Tab = 'create' | 'join';
const activeTab = ref<Tab>('create');

/* ── Create org ────────────────────────────────────────── */
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
    createError.value =
      err instanceof ApiError ? err.message : 'Failed to create organization.';
  } finally {
    creating.value = false;
  }
}

/* ── Search & join ─────────────────────────────────────── */
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
  } catch {
    searchResults.value = [];
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
    joinError.value =
      err instanceof ApiError ? err.message : 'Failed to send join request.';
  } finally {
    joinSending.value = null;
  }
}

/* ── Logout ────────────────────────────────────────────── */
function handleLogout() {
  auth.logout();
  orgStore.clear();
  router.push({ name: 'login' });
}
</script>

<template>
  <AuthLayout>
    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
      Get started
    </h1>
    <p class="mt-1 text-[13px] text-ink-500">
      Create a new organization or join an existing one.
    </p>

    <!-- Tabs -->
    <div class="mt-5 flex border-b border-line">
      <button
        v-for="tab in (['create', 'join'] as Tab[])"
        :key="tab"
        :class="[
          'flex-1 pb-2.5 text-sm font-medium transition-colors',
          activeTab === tab
            ? 'border-b-2 border-brand-600 text-brand-600'
            : 'text-ink-500 hover:text-ink-700',
        ]"
        @click="activeTab = tab"
      >
        {{ tab === 'create' ? 'Create organization' : 'Join organization' }}
      </button>
    </div>

    <!-- Create tab -->
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
        :disabled="!newOrgName.trim() || creating"
        :loading="creating"
        class="!h-11 !rounded-[10px] !font-semibold"
      >
        Create organization
      </Button>
    </form>

    <!-- Join tab -->
    <div v-else class="mt-5 flex flex-col gap-4">
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
