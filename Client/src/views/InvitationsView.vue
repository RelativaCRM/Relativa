<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import {
  orgApi,
  type OrgInvitationDto,
  type JoinRequestDto,
} from '@/api/organizations';
import { useOrganizationStore } from '@/stores/organization';
import { normalizeError } from '@/api/errors';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const router = useRouter();
const orgStore = useOrganizationStore();

const loading = ref(true);
const orgInvitations = ref<OrgInvitationDto[]>([]);
const orgJoinRequests = ref<JoinRequestDto[]>([]);

const loadErrors = ref({
  orgInv: null as string | null,
  orgJoin: null as string | null,
});

const processingToken = ref<string | null>(null);
const actionError = ref<string | null>(null);
const success = ref<string | null>(null);

const orgScopedOrgInvitations = computed(() => {
  const oid = orgStore.currentOrgId;
  if (!oid) return orgInvitations.value;
  return orgInvitations.value.filter((i) => i.organizationId === oid);
});

const pendingOrgJoinRequests = computed(() =>
  orgJoinRequests.value.filter((r) => r.status === 'Pending'),
);

const hasOrgActivity = computed(
  () =>
    orgScopedOrgInvitations.value.length > 0 ||
    pendingOrgJoinRequests.value.length > 0,
);

const hasLoadErrors = computed(
  () => !!(loadErrors.value.orgInv || loadErrors.value.orgJoin),
);

const hasAnything = computed(
  () => hasOrgActivity.value || hasLoadErrors.value,
);

function captureLoadError(
  key: keyof typeof loadErrors.value,
  err: unknown,
  fallback: string,
) {
  loadErrors.value[key] = normalizeError(err, fallback).message;
}

async function loadInbox() {
  loading.value = true;
  actionError.value = null;
  loadErrors.value = {
    orgInv: null,
    orgJoin: null,
  };

  const tasks: Promise<unknown>[] = [];

  tasks.push(
    orgApi
      .myOrganizationInvitations()
      .then((r) => {
        orgInvitations.value = r;
      })
      .catch((err) => {
        orgInvitations.value = [];
        captureLoadError('orgInv', err, 'Could not load organization invitations.');
      }),
  );

  tasks.push(
    orgApi
      .myJoinRequests()
      .then((r) => {
        orgJoinRequests.value = r;
      })
      .catch((err) => {
        orgJoinRequests.value = [];
        captureLoadError('orgJoin', err, 'Could not load organization join requests.');
      }),
  );

  await Promise.all(tasks);
  loading.value = false;
}

async function acceptOrg(token: string) {
  processingToken.value = token;
  actionError.value = null;
  success.value = null;
  try {
    await orgApi.acceptOrgInvitation(token);
    success.value = 'Organization invitation accepted.';
    await Promise.all([orgStore.fetchOrganizations(), loadInbox()]);
    if (orgStore.hasOrganization) {
      setTimeout(() => router.push({ name: 'home' }), 600);
    }
  } catch (err) {
    actionError.value = normalizeError(err, 'Failed to accept invitation.').message;
  } finally {
    processingToken.value = null;
  }
}

watch(
  () => orgStore.currentOrgId,
  () => {
    void loadInbox();
  },
  { immediate: true },
);

defineExpose({ loadInbox });
</script>

<template>
  <section class="max-w-3xl">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-ink-900">Invitations and join requests</h1>
      <p class="mt-3 text-sm text-ink-500">
        Organization invitations you can accept, and join requests you have submitted.
      </p>
    </div>

    <Message
      v-if="actionError"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ actionError }}
    </Message>
    <Message
      v-if="success"
      severity="success"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ success }}
    </Message>

    <LoadingSkeleton v-if="loading" variant="list" :rows="3" label="Loading invitations" />

    <div
      v-else-if="!hasAnything"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-inbox text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No pending organization invitations or join requests.
      </p>
    </div>

    <div v-else class="flex flex-col gap-6">
      <div
        v-if="
          orgScopedOrgInvitations.length > 0 ||
          pendingOrgJoinRequests.length > 0 ||
          loadErrors.orgInv ||
          loadErrors.orgJoin
        "
        class="rounded-xl border border-line bg-white overflow-hidden"
      >
        <div
          class="px-5 py-3 bg-surface border-b border-line text-xs font-medium text-ink-500 uppercase tracking-wider flex items-center gap-2"
        >
          <i class="pi pi-building" />
          Organization
        </div>

        <Message
          v-if="loadErrors.orgInv"
          severity="error"
          :closable="false"
          class="!my-0 !rounded-none border-0 border-b border-line"
        >
          {{ loadErrors.orgInv }}
        </Message>
        <Message
          v-if="loadErrors.orgJoin"
          severity="error"
          :closable="false"
          class="!my-0 !rounded-none border-0 border-b border-line"
        >
          {{ loadErrors.orgJoin }}
        </Message>

        <div
          v-if="orgScopedOrgInvitations.length"
          class="px-5 py-2 text-[11px] font-medium text-ink-400 uppercase tracking-wide border-b border-line bg-white"
        >
          Invitations
        </div>
        <div
          v-for="inv in orgScopedOrgInvitations"
          :key="'o-' + inv.id"
          class="flex items-center justify-between px-5 py-4 border-b border-line last:border-0"
        >
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <p class="text-sm font-medium text-ink-900">
                {{ inv.organizationName }}
              </p>
              <Tag value="Invite" severity="info" />
            </div>
            <p class="text-xs text-ink-400 mt-1">
              Invited to {{ inv.email }} · Expires
              {{ new Date(inv.expiresAt).toLocaleDateString() }}
            </p>
          </div>
          <Button
            label="Accept"
            icon="pi pi-check"
            :loading="processingToken === inv.token"
            @click="acceptOrg(inv.token)"
          />
        </div>

        <div
          v-if="pendingOrgJoinRequests.length"
          class="px-5 py-2 text-[11px] font-medium text-ink-400 uppercase tracking-wide border-b border-line border-t border-line bg-surface/50"
        >
          Your join requests (organization)
        </div>
        <div
          v-for="jr in pendingOrgJoinRequests"
          :key="'oj-' + jr.id"
          class="px-5 py-3 border-b border-line last:border-0 text-sm text-ink-700"
        >
          <p class="font-medium text-ink-900">Request #{{ jr.id }}</p>
          <p class="text-xs text-ink-500 mt-1">
            {{ jr.status }} · Submitted
            {{ new Date(jr.createdAt).toLocaleDateString() }}
          </p>
          <p v-if="jr.message" class="text-xs text-ink-400 mt-1 line-clamp-2">
            {{ jr.message }}
          </p>
        </div>
      </div>
    </div>
  </section>
</template>
