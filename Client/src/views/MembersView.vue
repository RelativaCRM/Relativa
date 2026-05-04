<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Message from 'primevue/message';
import Select from 'primevue/select';
import Tag from 'primevue/tag';
import { normalizeError } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';
import { orgApi, type JoinRequestDto } from '@/api/organizations';
import { useOrganizationStore } from '@/stores/organization';

const router = useRouter();
const toast = useToast();
const orgStore = useOrganizationStore();
const { notify } = useApiErrorHandler();

const loading = ref(true);
const joinRequests = ref<JoinRequestDto[]>([]);
const reviewingId = ref<number | null>(null);
const resendingInvId = ref<number | null>(null);

const MANAGE_JOIN_REQUESTS_PERM = 'manage_join_requests';
const ASSIGN_ORG_ROLES_PERM = 'assign_org_roles';
const CREATE_ORG_USERS_PERM = 'create_org_users';

function hasPermission(permission: string): boolean {
  const roleName = orgStore.currentOrg?.userRole;
  if (!roleName) return false;
  const role = orgStore.roles.find((r) => r.name === roleName);
  return role?.permissions.some((p) => p.name === permission) ?? false;
}

const canManageJoinRequests = computed(() =>
  hasPermission(MANAGE_JOIN_REQUESTS_PERM),
);
const canAssignOrgRoles = computed(() =>
  hasPermission(ASSIGN_ORG_ROLES_PERM),
);
const canCreateOrgUsers = computed(() =>
  hasPermission(CREATE_ORG_USERS_PERM),
);

const pendingJoinRequests = computed(() =>
  joinRequests.value.filter((r) => r.status === 'Pending'),
);

function displayRole(roleName: string): string {
  if (roleName === 'org_owner') return 'Owner';
  if (roleName === 'org_admin') return 'Admin';
  return 'Member';
}

function roleSeverity(roleName: string): string {
  if (roleName === 'org_owner') return 'warn';
  if (roleName === 'org_admin') return 'info';
  return 'secondary';
}

/* ── Create user dialog ─────────────────────────────── */
const showCreateUser = ref(false);
const createSubmitting = ref(false);
const createError = ref<string | null>(null);
const createForm = ref({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  orgRoleId: null as number | null,
});

const orgRoleOptions = computed(() =>
  orgStore.roles
    .filter((r) => r.name === 'org_member' || r.name === 'org_admin')
    .map((r) => ({
      label: r.name === 'org_admin' ? 'Admin' : 'Member',
      value: r.id,
      name: r.name,
    })),
);
const inviteRoleOptions = orgRoleOptions;

const defaultMemberRoleId = computed(
  () => orgRoleOptions.value.find((r) => r.name === 'org_member')?.value ?? null,
);

function openCreateUserDialog() {
  createForm.value = {
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    orgRoleId: defaultMemberRoleId.value,
  };
  createError.value = null;
  showCreateUser.value = true;
}

function closeCreateUserDialog() {
  showCreateUser.value = false;
}

const canSubmitCreate = computed(() => {
  const f = createForm.value;
  return (
    f.firstName.trim().length > 0 &&
    f.lastName.trim().length > 0 &&
    /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim()) &&
    f.password.length >= 8 &&
    !createSubmitting.value
  );
});

async function handleCreateUser() {
  if (!canSubmitCreate.value) return;
  createSubmitting.value = true;
  createError.value = null;
  try {
    const payload = {
      firstName: createForm.value.firstName.trim(),
      lastName: createForm.value.lastName.trim(),
      email: createForm.value.email.trim(),
      password: createForm.value.password,
      orgRoleId: canAssignOrgRoles.value
        ? (createForm.value.orgRoleId ?? undefined)
        : (defaultMemberRoleId.value ?? undefined),
    };
    await orgStore.createOrgUser(payload);
    toast.add({
      severity: 'success',
      summary: 'User created',
      detail: 'The new organization account has been created.',
      life: 4000,
    });
    showCreateUser.value = false;
  } catch (err) {
    createError.value = normalizeError(err, 'Failed to create user.').message;
  } finally {
    createSubmitting.value = false;
  }
}

/* ── Invite dialog ───────────────────────────────────── */
const showInvite = ref(false);
const inviteEmail = ref('');
const inviteRoleId = ref<number | null>(null);
const inviteSending = ref(false);
const inviteSuccess = ref<string | null>(null);
const inviteError = ref<string | null>(null);

const canInvite = computed(
  () => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(inviteEmail.value) && !inviteSending.value,
);

function openInviteDialog() {
  inviteRoleId.value = defaultMemberRoleId.value;
  inviteEmail.value = '';
  inviteError.value = null;
  inviteSuccess.value = null;
  showInvite.value = true;
}

function closeInviteDialog() {
  showInvite.value = false;
}

async function handleInvite() {
  if (!canInvite.value) return;
  inviteSending.value = true;
  inviteSuccess.value = null;
  inviteError.value = null;
  try {
    await orgStore.inviteMember(inviteEmail.value.trim(), inviteRoleId.value ?? undefined);
    inviteSuccess.value = `Invitation sent to ${inviteEmail.value.trim()}`;
    inviteEmail.value = '';
  } catch (err) {
    inviteError.value = normalizeError(err, 'Failed to send invitation.').message;
  } finally {
    inviteSending.value = false;
  }
}

/* ── Join requests + invitations ─────────────────────── */
async function fetchJoinRequests() {
  if (!orgStore.currentOrgId || !canManageJoinRequests.value) return;
  try {
    joinRequests.value = await orgApi.listJoinRequests(orgStore.currentOrgId);
  } catch (err) {
    joinRequests.value = [];
    notify(err, { fallback: 'Failed to load join requests.' });
  }
}

async function handleReviewRequest(reqId: number, decision: 'Approved' | 'Rejected') {
  if (!orgStore.currentOrgId) return;
  reviewingId.value = reqId;
  try {
    await orgApi.reviewJoinRequest(orgStore.currentOrgId, reqId, decision);
    joinRequests.value = joinRequests.value.filter((r) => r.id !== reqId);
    if (decision === 'Approved') {
      await orgStore.fetchMembers();
    }
  } catch (err) {
    notify(err, { fallback: 'Failed to review join request.' });
  } finally {
    reviewingId.value = null;
  }
}

async function handleCancelInvitation(invId: number) {
  try {
    await orgStore.cancelInvitation(invId);
  } catch (err) {
    notify(err, { fallback: 'Failed to cancel invitation.' });
  }
}

async function handleResendInvitation(invId: number) {
  resendingInvId.value = invId;
  try {
    await orgStore.resendInvitation(invId);
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

function openMember(userId: number) {
  router.push({ name: 'member', params: { memberUserId: userId } });
}

onMounted(async () => {
  try {
    await Promise.all([
      orgStore.fetchMembers(),
      orgStore.fetchRoles(),
      orgStore.fetchInvitations(),
    ]);
    await fetchJoinRequests();
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <section class="max-w-5xl">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">Members</h1>
        <p class="mt-1 text-sm text-ink-500">
          Click a row to open the member details view.
        </p>
        <p class="mt-1 text-sm text-ink-500">
          Organization:
          <span class="font-medium text-ink-700">{{
            orgStore.currentOrg?.name ?? 'organization'
          }}</span>
        </p>
      </div>
      <div class="flex gap-2">
        <Button
          v-if="canCreateOrgUsers"
          icon="pi pi-id-card"
          label="Create user"
          @click="openCreateUserDialog"
        />
        <Button
          icon="pi pi-user-plus"
          label="Invite member"
          severity="secondary"
          @click="openInviteDialog"
        />
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <!-- Members table -->
    <div v-else class="rounded-xl border border-line bg-white overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-line bg-surface text-left text-xs font-medium text-ink-500 uppercase tracking-wider">
            <th class="px-5 py-3">Name</th>
            <th class="px-5 py-3">Email</th>
            <th class="px-5 py-3">Role</th>
            <th class="px-5 py-3">Joined</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="member in orgStore.members"
            :key="member.userId"
            class="border-b border-line last:border-0 hover:bg-surface/70 cursor-pointer"
            @click="openMember(member.userId)"
          >
            <td class="px-5 py-3 font-medium text-ink-900">
              {{ member.firstName }} {{ member.lastName }}
            </td>
            <td class="px-5 py-3 text-ink-600">{{ member.email }}</td>
            <td class="px-5 py-3">
              <Tag
                :value="displayRole(member.roleName)"
                :severity="roleSeverity(member.roleName)"
              />
            </td>
            <td class="px-5 py-3 text-ink-500">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Pending invitations -->
      <div v-if="orgStore.invitations.length" class="border-t border-line">
        <div class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider">
          Pending invitations
        </div>
        <div
          v-for="inv in orgStore.invitations"
          :key="inv.id"
          class="flex items-center justify-between px-5 py-3 border-b border-line last:border-0"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <p class="text-sm text-ink-700 truncate">{{ inv.email }}</p>
              <Tag
                v-if="inv.roleName"
                :value="displayRole(inv.roleName)"
                :severity="roleSeverity(inv.roleName)"
              />
            </div>
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

      <!-- Join requests (admins only) -->
      <div
        v-if="canManageJoinRequests && pendingJoinRequests.length"
        class="border-t border-line"
      >
        <div class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider">
          Join requests
        </div>
        <div
          v-for="req in pendingJoinRequests"
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
              :loading="reviewingId === req.id"
              @click="handleReviewRequest(req.id, 'Approved')"
            />
            <Button
              icon="pi pi-times"
              label="Reject"
              size="small"
              severity="secondary"
              :loading="reviewingId === req.id"
              @click="handleReviewRequest(req.id, 'Rejected')"
            />
          </div>
        </div>
      </div>
    </div>

    <Dialog
      v-model:visible="showCreateUser"
      header="Create user"
      modal
      :style="{ width: '460px' }"
      @hide="closeCreateUserDialog"
    >
      <form class="flex flex-col gap-4" @submit.prevent="handleCreateUser">
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1.5">
            <label for="createFirst" class="text-xs font-medium text-ink-600">First name</label>
            <InputText id="createFirst" v-model="createForm.firstName" maxlength="100" class="!h-10" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="createLast" class="text-xs font-medium text-ink-600">Last name</label>
            <InputText id="createLast" v-model="createForm.lastName" maxlength="100" class="!h-10" />
          </div>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createEmail" class="text-xs font-medium text-ink-600">Email</label>
          <InputText id="createEmail" v-model="createForm.email" type="email" class="!h-10" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createPassword" class="text-xs font-medium text-ink-600">Temporary password</label>
          <InputText id="createPassword" v-model="createForm.password" type="password" class="!h-10" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createRole" class="text-xs font-medium text-ink-600">Role</label>
          <Select
            id="createRole"
            v-model="createForm.orgRoleId"
            :options="orgRoleOptions"
            option-label="label"
            option-value="value"
            :disabled="!canAssignOrgRoles"
            class="!h-10"
          />
        </div>
        <Message v-if="createError" severity="error" :closable="false" class="!my-0">
          {{ createError }}
        </Message>
        <div class="flex justify-end gap-2">
          <Button type="button" label="Cancel" severity="secondary" text @click="closeCreateUserDialog" />
          <Button type="submit" label="Create user" :loading="createSubmitting" :disabled="!canSubmitCreate" />
        </div>
      </form>
    </Dialog>

    <!-- Invite dialog -->
    <Dialog
      v-model:visible="showInvite"
      header="Invite member"
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
          <label for="inviteEmail" class="text-xs font-medium text-ink-600">
            Email address <span class="text-danger">*</span>
          </label>
          <InputText
            id="inviteEmail"
            v-model="inviteEmail"
            type="email"
            placeholder="colleague@example.com"
            class="!h-10"
          />
        </div>

        <div v-if="canAssignOrgRoles" class="flex flex-col gap-1.5">
          <label for="inviteRole" class="text-xs font-medium text-ink-600">
            Role
          </label>
          <Select
            id="inviteRole"
            v-model="inviteRoleId"
            :options="inviteRoleOptions"
            option-label="label"
            option-value="value"
            class="!h-10"
          />
          <p class="text-xs text-ink-400">
            Defaults to Member. Requires the
            <code class="text-ink-600">assign_org_roles</code> permission to
            invite as Admin.
          </p>
        </div>

        <Message v-if="inviteSuccess" severity="success" :closable="false" class="!my-0">
          {{ inviteSuccess }}
        </Message>
        <Message v-if="inviteError" severity="error" :closable="false" class="!my-0">
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
