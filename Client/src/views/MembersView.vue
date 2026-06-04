<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Message from 'primevue/message';
import Select from 'primevue/select';
import { normalizeError } from '@/api/errors';
import { roleBadgeFullClass } from '@/utils/roleBadge';
import { useApiErrorHandler } from '@/api/errorToast';
import { orgApi, type JoinRequestDto } from '@/api/organizations';
import { useOrganizationStore } from '@/stores/organization';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const { t } = useI18n();
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
    .slice()
    .sort((a, b) => a.priority - b.priority)
    .map((r) => ({
      label: r.displayName,
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
      summary: t('members.userCreatedSummary'),
      detail: t('members.userCreatedDetail'),
      life: 4000,
    });
    showCreateUser.value = false;
  } catch (err) {
    createError.value = normalizeError(err, t('members.createUserError')).message;
  } finally {
    createSubmitting.value = false;
  }
}

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
    inviteSuccess.value = t('members.inviteSent', { email: inviteEmail.value.trim() });
    inviteEmail.value = '';
  } catch (err) {
    inviteError.value = normalizeError(err, t('members.inviteError')).message;
  } finally {
    inviteSending.value = false;
  }
}

async function fetchJoinRequests() {
  if (!orgStore.currentOrgId || !canManageJoinRequests.value) return;
  try {
    joinRequests.value = await orgApi.listJoinRequests(orgStore.currentOrgId);
  } catch (err) {
    joinRequests.value = [];
    notify(err, { fallback: t('members.loadJoinRequestsError') });
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
    notify(err, { fallback: t('members.reviewError') });
  } finally {
    reviewingId.value = null;
  }
}

async function handleCancelInvitation(invId: number) {
  try {
    await orgStore.cancelInvitation(invId);
  } catch (err) {
    notify(err, { fallback: t('members.cancelInvitationError') });
  }
}

async function handleResendInvitation(invId: number) {
  resendingInvId.value = invId;
  try {
    await orgStore.resendInvitation(invId);
    toast.add({
      severity: 'success',
      summary: t('members.resendSummary'),
      detail: t('members.resendDetail'),
      life: 4000,
    });
  } catch (err) {
    notify(err, { fallback: t('members.resendError') });
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
        <h1 class="text-2xl font-bold text-ink-900">{{ t('members.title') }}</h1>
        <p class="mt-3 text-sm text-ink-500">
          {{ t('members.rowHint') }}
        </p>
        <p class="mt-1 text-sm text-ink-500">
          {{ t('members.organizationLabel') }}:
          <span class="font-semibold text-brand-600">{{
            orgStore.currentOrg?.name ?? t('members.orgFallback')
          }}</span>
        </p>
      </div>
      <div class="flex gap-2">
        <Button
          v-if="canCreateOrgUsers"
          icon="pi pi-id-card"
          :label="t('members.createUser')"
          @click="openCreateUserDialog"
        />
        <Button
          icon="pi pi-user-plus"
          :label="t('members.inviteMember')"
          severity="secondary"
          @click="openInviteDialog"
        />
      </div>
    </div>

    
    <LoadingSkeleton v-if="loading" variant="table" :rows="6" label="Loading members" />

    
    <div v-else class="rounded-xl border border-line bg-white overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-line bg-surface text-left text-xs font-medium text-ink-500 uppercase tracking-wider">
            <th class="px-5 py-3">{{ t('members.colName') }}</th>
            <th class="px-5 py-3">{{ t('members.colEmail') }}</th>
            <th class="px-5 py-3">{{ t('members.colRole') }}</th>
            <th class="px-5 py-3">{{ t('members.colJoined') }}</th>
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
              <span :class="roleBadgeFullClass(member.roleName)">
                {{ member.roleDisplayName }}
              </span>
            </td>
            <td class="px-5 py-3 text-ink-500">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </td>
          </tr>
        </tbody>
      </table>

      
      <div v-if="orgStore.invitations.length" class="border-t border-line">
        <div class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider">
          {{ t('members.pendingInvitations') }}
        </div>
        <div
          v-for="inv in orgStore.invitations"
          :key="inv.id"
          class="flex items-center justify-between px-5 py-3 border-b border-line last:border-0"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <p class="text-sm text-ink-700 truncate">{{ inv.email }}</p>
              <span v-if="inv.roleName" :class="roleBadgeFullClass(inv.roleName)">
                {{ inv.roleDisplayName }}
              </span>
            </div>
            <p class="text-xs text-ink-400">
              {{ t('members.expires') }} {{ new Date(inv.expiresAt).toLocaleDateString() }}
            </p>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <Button
              icon="pi pi-refresh"
              severity="secondary"
              text
              rounded
              size="small"
              :title="t('members.resendTitle')"
              :loading="resendingInvId === inv.id"
              @click="handleResendInvitation(inv.id)"
            />
            <Button
              icon="pi pi-times"
              severity="secondary"
              text
              rounded
              size="small"
              :title="t('members.cancelInvitationTitle')"
              @click="handleCancelInvitation(inv.id)"
            />
          </div>
        </div>
      </div>

      
      <div
        v-if="canManageJoinRequests && pendingJoinRequests.length"
        class="border-t border-line"
      >
        <div class="px-5 py-3 bg-surface text-xs font-medium text-ink-500 uppercase tracking-wider">
          {{ t('members.joinRequests') }}
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
              {{ t('members.requested') }} {{ new Date(req.createdAt).toLocaleDateString() }}
            </p>
          </div>
          <div class="flex gap-2 shrink-0">
            <Button
              icon="pi pi-check"
              :label="t('members.approve')"
              size="small"
              :loading="reviewingId === req.id"
              @click="handleReviewRequest(req.id, 'Approved')"
            />
            <Button
              icon="pi pi-times"
              :label="t('members.reject')"
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
      :header="t('members.createUser')"
      modal
      :style="{ width: '460px' }"
      @hide="closeCreateUserDialog"
    >
      <form class="flex flex-col gap-4" @submit.prevent="handleCreateUser">
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1.5">
            <label for="createFirst" class="text-xs font-medium text-ink-600">{{ t('members.firstName') }}</label>
            <InputText id="createFirst" v-model="createForm.firstName" maxlength="100" class="!h-10" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="createLast" class="text-xs font-medium text-ink-600">{{ t('members.lastName') }}</label>
            <InputText id="createLast" v-model="createForm.lastName" maxlength="100" class="!h-10" />
          </div>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createEmail" class="text-xs font-medium text-ink-600">{{ t('members.email') }}</label>
          <InputText id="createEmail" v-model="createForm.email" type="email" class="!h-10" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createPassword" class="text-xs font-medium text-ink-600">{{ t('members.tempPassword') }}</label>
          <InputText id="createPassword" v-model="createForm.password" type="password" class="!h-10" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="createRole" class="text-xs font-medium text-ink-600">{{ t('members.role') }}</label>
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
          <Button type="button" :label="t('common.cancel')" severity="secondary" text @click="closeCreateUserDialog" />
          <Button type="submit" :label="t('members.createUser')" :loading="createSubmitting" :disabled="!canSubmitCreate" />
        </div>
      </form>
    </Dialog>

    
    <Dialog
      v-model:visible="showInvite"
      :header="t('members.inviteMember')"
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
            {{ t('members.emailAddress') }} <span class="text-danger">*</span>
          </label>
          <InputText
            id="inviteEmail"
            v-model="inviteEmail"
            type="email"
            :placeholder="t('members.emailPlaceholder')"
            class="!h-10"
          />
        </div>

        <div v-if="canAssignOrgRoles" class="flex flex-col gap-1.5">
          <label for="inviteRole" class="text-xs font-medium text-ink-600">
            {{ t('members.role') }}
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
            {{ t('members.roleHint') }}
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
            :label="t('common.cancel')"
            severity="secondary"
            text
            @click="closeInviteDialog"
          />
          <Button
            type="submit"
            :label="t('members.sendInvitation')"
            :disabled="!canInvite"
            :loading="inviteSending"
          />
        </div>
      </form>
    </Dialog>
  </section>
</template>
