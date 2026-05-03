<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import {
  orgApi,
  type JoinRequestDto,
  type OrgMemberDto,
} from '@/api/organizations';
import { normalizeError } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const toast = useToast();
const { notify } = useApiErrorHandler();

const loading = ref(true);
const joinRequests = ref<JoinRequestDto[]>([]);
const reviewingId = ref<number | null>(null);

const EDIT_OTHER_PROFILE_PERM = 'edit_other_org_users_profile';

const isOrgAdmin = computed(() => {
  const role = orgStore.currentOrg?.userRole;
  return role === 'org_owner' || role === 'org_admin';
});

const canEditOtherProfiles = computed(() => {
  const roleName = orgStore.currentOrg?.userRole;
  if (!roleName) return false;
  const role = orgStore.roles.find((r) => r.name === roleName);
  return (
    role?.permissions.some((p) => p.name === EDIT_OTHER_PROFILE_PERM) ?? false
  );
});

const pendingJoinRequests = computed(() =>
  joinRequests.value.filter((r) => r.status === 'Pending'),
);

async function fetchJoinRequests() {
  if (!orgStore.currentOrgId || !isOrgAdmin.value) return;
  try {
    joinRequests.value = await orgApi.listJoinRequests(orgStore.currentOrgId);
  } catch (err) {
    joinRequests.value = [];
    notify(err, { fallback: 'Failed to load join requests.' });
  }
}

async function handleReviewRequest(
  reqId: number,
  decision: 'Approved' | 'Rejected',
) {
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

/* ── Invite dialog ─────────────────────────────────────── */
const ASSIGN_ORG_ROLES_PERM = 'assign_org_roles';
const showInvite = ref(false);
const inviteEmail = ref('');
const inviteRoleId = ref<number | null>(null);
const inviteSending = ref(false);
const inviteSuccess = ref<string | null>(null);
const inviteError = ref<string | null>(null);

const canAssignOrgRoles = computed(() => {
  const roleName = orgStore.currentOrg?.userRole;
  if (!roleName) return false;
  const role = orgStore.roles.find((r) => r.name === roleName);
  return (
    role?.permissions.some((p) => p.name === ASSIGN_ORG_ROLES_PERM) ?? false
  );
});

const inviteRoleOptions = computed(() =>
  orgStore.roles
    .filter((r) => r.name === 'org_admin' || r.name === 'org_member')
    .map((r) => ({
      label: r.name === 'org_admin' ? 'Admin' : 'Member',
      value: r.id,
      name: r.name,
    })),
);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const canInvite = computed(
  () => emailPattern.test(inviteEmail.value) && !inviteSending.value,
);

async function handleInvite() {
  if (!canInvite.value) return;
  inviteSending.value = true;
  inviteSuccess.value = null;
  inviteError.value = null;
  try {
    await orgStore.inviteMember(
      inviteEmail.value.trim(),
      inviteRoleId.value ?? undefined,
    );
    inviteSuccess.value = `Invitation sent to ${inviteEmail.value}`;
    inviteEmail.value = '';
  } catch (err) {
    inviteError.value = normalizeError(err, 'Failed to send invitation.').message;
  } finally {
    inviteSending.value = false;
  }
}

function openInviteDialog() {
  const memberRoleId =
    inviteRoleOptions.value.find((o) => o.name === 'org_member')?.value ?? null;
  inviteRoleId.value = memberRoleId;
  showInvite.value = true;
}

function closeInviteDialog() {
  showInvite.value = false;
  inviteEmail.value = '';
  inviteRoleId.value = null;
  inviteSuccess.value = null;
  inviteError.value = null;
}

/* ── Role change ───────────────────────────────────────── */
const changingRole = ref<number | null>(null);
const roleSelectVersion = ref(0);

const roleOptions = computed(() =>
  orgStore.roles
    .filter((r) => r.name === 'org_admin' || r.name === 'org_member')
    .map((r) => ({
      label: r.name === 'org_admin' ? 'Admin' : 'Member',
      value: r.id,
    })),
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

function roleIdByName(roleName: string): number | undefined {
  return orgStore.roles.find((r) => r.name === roleName)?.id;
}

async function handleRoleChange(userId: number, newRoleId: number) {
  const target = orgStore.members.find((m) => m.userId === userId);
  const newRoleName = orgStore.roles.find((r) => r.id === newRoleId)?.name;

  if (target && target.roleName === 'org_owner' && newRoleName !== 'org_owner') {
    const ownerCount = orgStore.members.filter(
      (m) => m.roleName === 'org_owner',
    ).length;
    if (ownerCount <= 1) {
      toast.add({
        severity: 'error',
        summary: 'Conflict',
        detail: 'Cannot remove the last organization owner.',
        life: 5000,
      });
      await orgStore.fetchMembers();
      roleSelectVersion.value++;
      return;
    }
  }

  changingRole.value = userId;
  try {
    await orgStore.changeMemberRole(userId, newRoleId);
  } catch (err) {
    notify(err, { fallback: 'Failed to update role.' });
  } finally {
    await orgStore.fetchMembers();
    roleSelectVersion.value++;
    changingRole.value = null;
  }
}

/* ── Remove ────────────────────────────────────────────── */
const removingId = ref<number | null>(null);

async function handleRemove(userId: number) {
  removingId.value = userId;
  try {
    await orgStore.removeMember(userId);
  } catch (err) {
    notify(err, { fallback: 'Failed to remove member.' });
  } finally {
    removingId.value = null;
  }
}

/* ── Edit member profile (org permission) ──────────────── */
const showEditProfile = ref(false);
const editingMember = ref<OrgMemberDto | null>(null);
const editFirstName = ref('');
const editLastName = ref('');
const editProfileSaving = ref(false);

function openEditProfile(member: OrgMemberDto) {
  editingMember.value = member;
  editFirstName.value = member.firstName;
  editLastName.value = member.lastName;
  showEditProfile.value = true;
}

function closeEditProfile() {
  showEditProfile.value = false;
  editingMember.value = null;
  editFirstName.value = '';
  editLastName.value = '';
}

async function handleSaveMemberProfile() {
  if (!orgStore.currentOrgId || !editingMember.value) return;
  editProfileSaving.value = true;
  try {
    await orgApi.updateOrgUserProfile(
      orgStore.currentOrgId,
      editingMember.value.userId,
      {
        firstName: editFirstName.value.trim(),
        lastName: editLastName.value.trim(),
      },
    );
    await orgStore.fetchMembers();
    toast.add({
      severity: 'success',
      summary: 'Profile updated',
      detail: 'Member name has been saved.',
      life: 4000,
    });
    closeEditProfile();
  } catch (err) {
    notify(err, { fallback: 'Failed to update member profile.' });
  } finally {
    editProfileSaving.value = false;
  }
}

/* ── Cancel / resend invitation ────────────────────────── */
const resendingInvId = ref<number | null>(null);

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

/* ── Init ──────────────────────────────────────────────── */
onMounted(async () => {
  await Promise.all([
    orgStore.fetchMembers(),
    orgStore.fetchRoles(),
    orgStore.fetchInvitations(),
    fetchJoinRequests(),
  ]);
  loading.value = false;
});
</script>

<template>
  <section class="max-w-4xl">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">Members</h1>
        <p class="mt-1 text-sm text-ink-500">
          Manage members of
          <span class="font-medium text-ink-700">{{
            orgStore.currentOrg?.name ?? 'organization'
          }}</span>
        </p>
      </div>
      <Button
        icon="pi pi-user-plus"
        label="Invite member"
        @click="openInviteDialog"
      />
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
            <th class="px-5 py-3 w-28 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="member in orgStore.members"
            :key="member.userId"
            class="border-b border-line last:border-0 hover:bg-surface/50"
          >
            <td class="px-5 py-3 font-medium text-ink-900">
              {{ member.firstName }} {{ member.lastName }}
            </td>
            <td class="px-5 py-3 text-ink-600">{{ member.email }}</td>
            <td class="px-5 py-3">
              <!-- Owner is not editable -->
              <Tag
                v-if="member.roleName === 'org_owner'"
                :value="displayRole(member.roleName)"
                :severity="roleSeverity(member.roleName)"
              />
              <!-- Other roles — editable via select -->
              <Select
                v-else-if="member.userId !== auth.user?.id"
                :key="`role-${member.userId}-${member.roleName}-${roleSelectVersion}`"
                :model-value="roleIdByName(member.roleName)"
                :options="roleOptions"
                option-label="label"
                option-value="value"
                :disabled="changingRole === member.userId"
                class="!h-8 !text-xs !min-w-[110px]"
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
            <td class="px-5 py-3 text-right">
              <div class="flex items-center justify-end gap-1">
                <Button
                  v-if="
                    canEditOtherProfiles && member.userId !== auth.user?.id
                  "
                  title="Edit profile"
                  icon="pi pi-pencil"
                  severity="secondary"
                  text
                  rounded
                  @click="openEditProfile(member)"
                />
                <Button
                  v-if="member.roleName !== 'org_owner' && member.userId !== auth.user?.id"
                  icon="pi pi-trash"
                  severity="danger"
                  text
                  rounded
                  :loading="removingId === member.userId"
                  @click="handleRemove(member.userId)"
                />
              </div>
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
        v-if="isOrgAdmin && pendingJoinRequests.length"
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

    <!-- Edit member profile -->
    <Dialog
      v-model:visible="showEditProfile"
      header="Edit member profile"
      modal
      :style="{ width: '420px' }"
      @hide="closeEditProfile"
    >
      <div v-if="editingMember" class="flex flex-col gap-4">
        <p class="text-xs text-ink-500">
          {{ editingMember.email }}
        </p>
        <div class="flex flex-col gap-1.5">
          <label for="editMemberFirst" class="text-xs font-medium text-ink-600">
            First name <span class="text-danger">*</span>
          </label>
          <InputText
            id="editMemberFirst"
            v-model="editFirstName"
            maxlength="100"
            class="!h-10"
          />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="editMemberLast" class="text-xs font-medium text-ink-600">
            Last name <span class="text-danger">*</span>
          </label>
          <InputText
            id="editMemberLast"
            v-model="editLastName"
            maxlength="100"
            class="!h-10"
          />
        </div>
        <div class="flex justify-end gap-2">
          <Button
            label="Cancel"
            severity="secondary"
            text
            :disabled="editProfileSaving"
            @click="closeEditProfile"
          />
          <Button
            label="Save"
            :loading="editProfileSaving"
            :disabled="
              !editFirstName.trim() ||
              !editLastName.trim()
            "
            @click="handleSaveMemberProfile"
          />
        </div>
      </div>
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
