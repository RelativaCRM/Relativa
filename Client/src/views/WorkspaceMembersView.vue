<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import { useAuthStore } from '@/stores/auth';
import { useWorkspaceStore } from '@/stores/workspace';
import { ApiError } from '@/api/http';
import { normalizeError } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const route = useRoute();
const router = useRouter();
const toast = useToast();
const auth = useAuthStore();
const wsStore = useWorkspaceStore();
const { notify } = useApiErrorHandler();

const ROLE_ORDER = ['ws_admin', 'ws_manager', 'ws_analyst', 'ws_member'];

const workspaceId = computed(() => Number(route.params.id));
const loading = ref(true);

/* ── Invite dialog ─────────────────────────────────────── */
const showInvite = ref(false);
const inviteEmail = ref('');
const inviteRoleId = ref<number | null>(null);
const inviteSending = ref(false);
const inviteSuccess = ref<string | null>(null);
const inviteError = ref<string | null>(null);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const canInvite = computed(
  () =>
    emailPattern.test(inviteEmail.value) &&
    inviteRoleId.value !== null &&
    !inviteSending.value,
);

const roleOptions = computed(() =>
  ROLE_ORDER.map((name) => wsStore.roles.find((r) => r.name === name))
    .filter((r): r is NonNullable<typeof r> => !!r)
    .map((r) => ({ label: displayRole(r.name), value: r.id })),
);

function displayRole(roleName: string): string {
  if (roleName === 'ws_admin') return 'Admin';
  if (roleName === 'ws_manager') return 'Manager';
  if (roleName === 'ws_analyst') return 'Analyst';
  if (roleName === 'ws_member') return 'Member';
  return roleName;
}

function roleSeverity(roleName: string): string {
  if (roleName === 'ws_admin') return 'info';
  if (roleName === 'ws_manager') return 'warn';
  if (roleName === 'ws_analyst') return 'success';
  return 'secondary';
}

function roleIdByName(roleName: string): number | undefined {
  return wsStore.roles.find((r) => r.name === roleName)?.id;
}

async function handleInvite() {
  if (!canInvite.value || inviteRoleId.value === null) return;
  inviteSending.value = true;
  inviteSuccess.value = null;
  inviteError.value = null;
  try {
    await wsStore.inviteMember(
      workspaceId.value,
      inviteEmail.value.trim(),
      inviteRoleId.value,
    );
    inviteSuccess.value = `Invitation sent to ${inviteEmail.value}`;
    inviteEmail.value = '';
  } catch (err) {
    inviteError.value = normalizeError(err, 'Failed to send invitation.').message;
  } finally {
    inviteSending.value = false;
  }
}

function closeInviteDialog() {
  showInvite.value = false;
  inviteEmail.value = '';
  inviteSuccess.value = null;
  inviteError.value = null;
}

/* ── Role change ───────────────────────────────────────── */
const changingRole = ref<number | null>(null);
const roleSelectVersion = ref(0);

async function handleRoleChange(userId: number, newRoleId: number) {
  const target = wsStore.members.find((m) => m.userId === userId);
  const newRoleName = wsStore.roles.find((r) => r.id === newRoleId)?.name;
  if (target && target.roleName === 'ws_admin' && newRoleName !== 'ws_admin') {
    const adminCount = wsStore.members.filter(
      (m) => m.roleName === 'ws_admin',
    ).length;
    if (adminCount <= 1) {
      toast.add({
        severity: 'error',
        summary: 'Conflict',
        detail: 'Cannot remove the last workspace administrator.',
        life: 5000,
      });
      await wsStore.fetchMembers(workspaceId.value);
      roleSelectVersion.value++;
      return;
    }
  }

  changingRole.value = userId;
  try {
    await wsStore.changeMemberRole(workspaceId.value, userId, newRoleId);
    toast.add({
      severity: 'success',
      summary: 'Role updated',
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to update role.' });
  } finally {
    await wsStore.fetchMembers(workspaceId.value);
    roleSelectVersion.value++;
    changingRole.value = null;
  }
}

/* ── Remove ────────────────────────────────────────────── */
const removingId = ref<number | null>(null);

async function handleRemove(userId: number) {
  removingId.value = userId;
  try {
    await wsStore.removeMember(workspaceId.value, userId);
  } catch (err) {
    notify(err, { fallback: 'Failed to remove member.' });
  } finally {
    removingId.value = null;
  }
}

/* ── Cancel / resend invitation ────────────────────────── */
const resendingInvId = ref<number | null>(null);

async function handleCancelInvitation(invId: number) {
  try {
    await wsStore.cancelInvitation(workspaceId.value, invId);
  } catch (err) {
    notify(err, { fallback: 'Failed to cancel invitation.' });
  }
}

async function handleResendInvitation(invId: number) {
  resendingInvId.value = invId;
  try {
    await wsStore.resendInvitation(workspaceId.value, invId);
    toast.add({
      severity: 'success',
      summary: 'Invitation resent',
      detail: 'Token rotated and expiry extended.',
      life: 4000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to resend invitation.' });
  } finally {
    resendingInvId.value = null;
  }
}

/* ── Workspace join requests ───────────────────────────── */
const MANAGE_WS_JOIN_REQUESTS_PERM = 'manage_ws_join_requests';
const canManageJoinRequests = computed(() => {
  const roleName = wsStore.members.find(
    (m) => m.userId === auth.user?.id,
  )?.roleName;
  if (!roleName) return false;
  const role = wsStore.roles.find((r) => r.name === roleName);
  return (
    role?.permissions.some(
      (p) => p.name === MANAGE_WS_JOIN_REQUESTS_PERM,
    ) ?? false
  );
});
const reviewingJoinReqId = ref<number | null>(null);

async function fetchJoinRequests() {
  if (!canManageJoinRequests.value) return;
  try {
    await wsStore.fetchJoinRequests(workspaceId.value);
  } catch (err) {
    notify(err, { fallback: 'Failed to load join requests.' });
  }
}

async function handleReviewJoinRequest(
  reqId: number,
  decision: 'Approved' | 'Rejected',
) {
  reviewingJoinReqId.value = reqId;
  try {
    await wsStore.reviewJoinRequest(workspaceId.value, reqId, decision);
    if (decision === 'Approved') {
      await wsStore.fetchMembers(workspaceId.value);
    }
  } catch (err) {
    notify(err, { fallback: 'Failed to review join request.' });
  } finally {
    reviewingJoinReqId.value = null;
  }
}

async function loadAll() {
  loading.value = true;
  try {
    await Promise.all([
      wsStore.fetchMembers(workspaceId.value),
      wsStore.fetchRoles(workspaceId.value),
      wsStore.fetchInvitations(workspaceId.value),
      wsStore.workspaces.length === 0
        ? wsStore.fetchWorkspaces()
        : Promise.resolve(),
    ]);
    wsStore.setCurrentWorkspace(workspaceId.value);
    await fetchJoinRequests();
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      router.replace({ name: 'workspaces' });
      return;
    }
    notify(err, { fallback: 'Failed to load workspace.' });
  } finally {
    loading.value = false;
  }
}

watch(workspaceId, (id) => {
  if (id) loadAll();
});

onMounted(loadAll);
</script>

<template>
  <section class="max-w-4xl">
    <div class="flex items-center justify-between mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          label="Workspaces"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="router.push({ name: 'workspaces' })"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          {{ wsStore.currentWorkspace?.name ?? 'Workspace' }}
        </h1>
        <p class="mt-1 text-sm text-ink-500">Manage workspace members.</p>
      </div>
      <div class="flex items-center gap-2">
        <Button
          icon="pi pi-database"
          label="Entities"
          severity="secondary"
          @click="
            router.push({
              name: 'workspace-entities',
              params: { id: workspaceId },
            })
          "
        />
        <Button
          icon="pi pi-user-plus"
          label="Invite member"
          @click="showInvite = true"
        />
      </div>
    </div>

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <div v-else class="rounded-xl border border-line bg-white overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr
            class="border-b border-line bg-surface text-left text-xs font-medium text-ink-500 uppercase tracking-wider"
          >
            <th class="px-5 py-3">Name</th>
            <th class="px-5 py-3">Email</th>
            <th class="px-5 py-3">Role</th>
            <th class="px-5 py-3">Joined</th>
            <th class="px-5 py-3 w-20"></th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="member in wsStore.members"
            :key="member.userId"
            class="border-b border-line last:border-0 hover:bg-surface/50"
          >
            <td class="px-5 py-3 font-medium text-ink-900">
              {{ member.firstName }} {{ member.lastName }}
            </td>
            <td class="px-5 py-3 text-ink-600">{{ member.email }}</td>
            <td class="px-5 py-3">
              <Select
                v-if="roleOptions.length"
                :key="`role-${member.userId}-${member.roleName}-${roleSelectVersion}`"
                :model-value="roleIdByName(member.roleName)"
                :options="roleOptions"
                option-label="label"
                option-value="value"
                :disabled="changingRole === member.userId"
                class="!h-8 !text-xs !min-w-[120px]"
                @update:model-value="handleRoleChange(member.userId, $event)"
              />
              <Tag
                v-else
                :value="displayRole(member.roleName)"
                :severity="roleSeverity(member.roleName)"
              />
            </td>
            <td class="px-5 py-3 text-ink-500">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </td>
            <td class="px-5 py-3">
              <Button
                v-if="member.userId !== auth.user?.id"
                icon="pi pi-trash"
                severity="danger"
                text
                rounded
                :loading="removingId === member.userId"
                @click="handleRemove(member.userId)"
              />
            </td>
          </tr>
        </tbody>
      </table>

      <div
        v-if="!wsStore.members.length"
        class="py-10 text-center text-sm text-ink-500"
      >
        No members yet.
      </div>

      <div v-if="wsStore.invitations.length" class="border-t border-line">
        <div
          class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider"
        >
          Pending invitations
        </div>
        <div
          v-for="inv in wsStore.invitations"
          :key="inv.id"
          class="flex items-center justify-between px-5 py-3 border-b border-line last:border-0"
        >
          <div class="min-w-0 flex-1">
            <p class="text-sm text-ink-700 truncate">
              {{ inv.email }}
              <span class="text-xs text-ink-400 ml-2"
                >as {{ displayRole(inv.roleName) }}</span
              >
            </p>
            <p class="text-xs text-ink-400">
              Expires {{ new Date(inv.expiresAt).toLocaleDateString() }}
            </p>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <Button
              icon="pi pi-refresh"
              severity="secondary"
              text
              rounded
              size="small"
              title="Resend (rotate token and extend expiry)"
              :loading="resendingInvId === inv.id"
              @click="handleResendInvitation(inv.id)"
            />
            <Button
              icon="pi pi-times"
              severity="secondary"
              text
              rounded
              size="small"
              title="Cancel invitation"
              @click="handleCancelInvitation(inv.id)"
            />
          </div>
        </div>
      </div>

      <div
        v-if="canManageJoinRequests && wsStore.joinRequests.length"
        class="border-t border-line"
      >
        <div
          class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider"
        >
          Join requests
        </div>
        <div
          v-for="req in wsStore.joinRequests"
          :key="req.id"
          class="flex items-start justify-between px-5 py-3 border-b border-line last:border-0 gap-4"
        >
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium text-ink-900">
              {{ req.userName }}
            </p>
            <p class="text-xs text-ink-500">{{ req.userEmail }}</p>
            <p v-if="req.message" class="text-xs text-ink-600 mt-1 italic">
              &ldquo;{{ req.message }}&rdquo;
            </p>
            <p class="text-xs text-ink-400 mt-1">
              Requested {{ new Date(req.createdAt).toLocaleDateString() }}
            </p>
          </div>
          <div class="flex gap-2 shrink-0">
            <Button
              icon="pi pi-check"
              label="Approve"
              size="small"
              :loading="reviewingJoinReqId === req.id"
              @click="handleReviewJoinRequest(req.id, 'Approved')"
            />
            <Button
              icon="pi pi-times"
              label="Reject"
              size="small"
              severity="secondary"
              :loading="reviewingJoinReqId === req.id"
              @click="handleReviewJoinRequest(req.id, 'Rejected')"
            />
          </div>
        </div>
      </div>
    </div>

    <Dialog
      v-model:visible="showInvite"
      header="Invite to workspace"
      modal
      :style="{ width: '420px' }"
      @hide="closeInviteDialog"
    >
      <form
        class="flex flex-col gap-4"
        novalidate
        @submit.prevent="handleInvite"
      >
        <div class="flex flex-col gap-1.5">
          <label for="wsInviteEmail" class="text-xs font-medium text-ink-600">
            Email address <span class="text-danger">*</span>
          </label>
          <InputText
            id="wsInviteEmail"
            v-model="inviteEmail"
            type="email"
            placeholder="colleague@example.com"
            class="!h-10"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="wsInviteRole" class="text-xs font-medium text-ink-600">
            Role <span class="text-danger">*</span>
          </label>
          <Select
            id="wsInviteRole"
            v-model="inviteRoleId"
            :options="roleOptions"
            option-label="label"
            option-value="value"
            placeholder="Select role"
            class="!h-10"
          />
        </div>

        <Message
          v-if="inviteSuccess"
          severity="success"
          :closable="false"
          class="!my-0"
        >
          {{ inviteSuccess }}
        </Message>
        <Message
          v-if="inviteError"
          severity="error"
          :closable="false"
          class="!my-0"
        >
          {{ inviteError }}
        </Message>

        <div class="flex justify-end gap-2">
          <Button
            type="button"
            label="Cancel"
            severity="secondary"
            text
            @click="closeInviteDialog"
          />
          <Button
            type="submit"
            label="Send invitation"
            :disabled="!canInvite"
            :loading="inviteSending"
          />
        </div>
      </form>
    </Dialog>
  </section>
</template>
