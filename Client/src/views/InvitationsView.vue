<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import { orgApi, type MyInvitationsDto } from '@/api/organizations';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { normalizeError } from '@/api/errors';

const router = useRouter();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();

const loading = ref(true);
const inbox = ref<MyInvitationsDto>({
  organizationInvitations: [],
  workspaceInvitations: [],
});
const processingToken = ref<string | null>(null);
const error = ref<string | null>(null);
const success = ref<string | null>(null);

const hasInvitations = computed(
  () =>
    inbox.value.organizationInvitations.length > 0 ||
    inbox.value.workspaceInvitations.length > 0,
);

async function loadInbox() {
  loading.value = true;
  error.value = null;
  try {
    inbox.value = await orgApi.myInvitations();
  } catch (err) {
    error.value = normalizeError(err, 'Failed to load invitations.').message;
  } finally {
    loading.value = false;
  }
}

async function acceptOrg(token: string) {
  processingToken.value = token;
  error.value = null;
  success.value = null;
  try {
    await orgApi.acceptOrgInvitation(token);
    success.value = 'Organization invitation accepted.';
    await Promise.all([orgStore.fetchOrganizations(), loadInbox()]);
    if (orgStore.hasOrganization) {
      setTimeout(() => router.push({ name: 'home' }), 600);
    }
  } catch (err) {
    error.value = normalizeError(err, 'Failed to accept invitation.').message;
  } finally {
    processingToken.value = null;
  }
}

async function acceptWorkspace(token: string) {
  processingToken.value = token;
  error.value = null;
  success.value = null;
  try {
    await orgApi.acceptWorkspaceInvitation(token);
    success.value = 'Workspace invitation accepted.';
    await Promise.all([wsStore.fetchWorkspaces(), loadInbox()]);
  } catch (err) {
    error.value = normalizeError(err, 'Failed to accept invitation.').message;
  } finally {
    processingToken.value = null;
  }
}

onMounted(loadInbox);

defineExpose({ loadInbox });
</script>

<template>
  <section class="max-w-3xl">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-ink-900">Invitations</h1>
      <p class="mt-1 text-sm text-ink-500">
        Organizations and workspaces that invited you to join.
      </p>
    </div>

    <Message
      v-if="error"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ error }}
    </Message>
    <Message
      v-if="success"
      severity="success"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ success }}
    </Message>

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <div
      v-else-if="!hasInvitations"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-inbox text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">You have no pending invitations.</p>
    </div>

    <div v-else class="flex flex-col gap-6">
      <!-- Organization invitations -->
      <div
        v-if="inbox.organizationInvitations.length"
        class="rounded-xl border border-line bg-white overflow-hidden"
      >
        <div
          class="px-5 py-3 bg-surface border-b border-line text-xs font-medium text-ink-500 uppercase tracking-wider flex items-center gap-2"
        >
          <i class="pi pi-building" />
          Organization invitations
        </div>
        <div
          v-for="inv in inbox.organizationInvitations"
          :key="inv.id"
          class="flex items-center justify-between px-5 py-4 border-b border-line last:border-0"
        >
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <p class="text-sm font-medium text-ink-900">
                {{ inv.organizationName }}
              </p>
              <Tag value="Organization" severity="info" />
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
      </div>

      <!-- Workspace invitations -->
      <div
        v-if="inbox.workspaceInvitations.length"
        class="rounded-xl border border-line bg-white overflow-hidden"
      >
        <div
          class="px-5 py-3 bg-surface border-b border-line text-xs font-medium text-ink-500 uppercase tracking-wider flex items-center gap-2"
        >
          <i class="pi pi-folder" />
          Workspace invitations
        </div>
        <div
          v-for="inv in inbox.workspaceInvitations"
          :key="inv.id"
          class="flex items-center justify-between px-5 py-4 border-b border-line last:border-0"
        >
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <p class="text-sm font-medium text-ink-900">
                {{ inv.workspaceName || 'Workspace' }}
              </p>
              <Tag :value="inv.roleName" severity="secondary" />
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
            @click="acceptWorkspace(inv.token)"
          />
        </div>
      </div>
    </div>
  </section>
</template>
