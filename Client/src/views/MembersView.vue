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
import { roleBadgeFullClass, roleLabel } from '@/utils/roleBadge';
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

const memberSearch = ref('');
const filteredMembers = computed(() => {
  const q = memberSearch.value.trim().toLowerCase();
  const list = orgStore.members;
  if (!q) return list;
  return list.filter(
    (m) =>
      `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
      m.email.toLowerCase().includes(q),
  );
});

function memberInitials(first: string, last: string, email: string): string {
  const fl = `${first?.[0] ?? ''}${last?.[0] ?? ''}`.toUpperCase();
  return fl || (email?.[0] ?? '?').toUpperCase();
}

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
      label: roleLabel(r.name, r.displayName),
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
  <section class="mx-auto max-w-5xl pb-16">
    <header class="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-ink-900">{{ t('members.title') }}</h1>
        <p class="mt-1.5 text-sm text-ink-500">{{ t('members.rowHint') }}</p>
        <p class="mt-1 text-sm text-ink-500">
          {{ t('members.organizationLabel') }}:
          <span class="font-semibold text-brand-600">
            {{ orgStore.currentOrg?.name ?? t('members.orgFallback') }}
          </span>
        </p>
      </div>
      <div class="flex shrink-0 gap-2">
        <button v-if="canCreateOrgUsers" class="btn btn-outline btn-sm" @click="openCreateUserDialog">
          <i class="pi pi-id-card" />
          {{ t('members.createUser') }}
        </button>
        <button class="btn btn-primary btn-sm" @click="openInviteDialog">
          <i class="pi pi-user-plus" />
          {{ t('members.inviteMember') }}
        </button>
      </div>
    </header>

    <LoadingSkeleton v-if="loading" variant="table" :rows="6" :label="t('members.title')" />

    <template v-else>
      <div class="border border-line bg-white">
        <div class="flex items-center gap-2 border-b border-line px-4 py-2.5">
          <i class="pi pi-search text-sm text-ink-400" />
          <input
            v-model="memberSearch"
            :placeholder="t('members.searchPlaceholder')"
            class="w-full bg-transparent text-sm text-ink-900 outline-none placeholder:text-ink-400"
          />
          <span class="shrink-0 text-xs text-ink-400">{{ filteredMembers.length }}</span>
        </div>

        <table v-if="filteredMembers.length" class="w-full text-sm">
          <thead>
            <tr class="border-b border-line bg-surface text-left text-xs font-medium uppercase tracking-wider text-ink-500">
              <th class="px-5 py-3">{{ t('members.colName') }}</th>
              <th class="px-5 py-3">{{ t('members.colEmail') }}</th>
              <th class="px-5 py-3">{{ t('members.colRole') }}</th>
              <th class="px-5 py-3">{{ t('members.colJoined') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="member in filteredMembers"
              :key="member.userId"
              class="cursor-pointer border-b border-line transition-colors last:border-0 hover:bg-surface/70"
              @click="openMember(member.userId)"
            >
              <td class="px-5 py-3">
                <div class="flex items-center gap-3">
                  <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-600 text-xs font-semibold text-white">
                    {{ memberInitials(member.firstName, member.lastName, member.email) }}
                  </span>
                  <span class="font-medium text-ink-900">{{ member.firstName }} {{ member.lastName }}</span>
                </div>
              </td>
              <td class="px-5 py-3 text-ink-600">{{ member.email }}</td>
              <td class="px-5 py-3">
                <span :class="roleBadgeFullClass(member.roleName)">
                  {{ roleLabel(member.roleName, member.roleDisplayName) }}
                </span>
              </td>
              <td class="px-5 py-3 text-ink-500">
                {{ new Date(member.joinedAt).toLocaleDateString() }}
              </td>
            </tr>
          </tbody>
        </table>

        <p v-else class="px-5 py-10 text-center text-sm text-ink-500">
          {{ t('members.noneFound') }}
        </p>

        <div v-if="orgStore.invitations.length" class="border-t border-line">
          <div class="bg-surface px-5 py-3 text-xs font-medium uppercase tracking-wider text-ink-500">
            {{ t('members.pendingInvitations') }}
          </div>
          <div
            v-for="inv in orgStore.invitations"
            :key="inv.id"
            class="flex items-center justify-between border-b border-line px-5 py-3 last:border-0"
          >
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <p class="truncate text-sm text-ink-700">{{ inv.email }}</p>
                <span v-if="inv.roleName" :class="roleBadgeFullClass(inv.roleName)">
                  {{ roleLabel(inv.roleName, inv.roleDisplayName) }}
                </span>
              </div>
              <p class="text-xs text-ink-400">
                {{ t('members.expires') }} {{ new Date(inv.expiresAt).toLocaleDateString() }}
              </p>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <button
                class="btn btn-outline btn-sm !px-2.5"
                :title="t('members.resendTitle')"
                :disabled="resendingInvId === inv.id"
                @click="handleResendInvitation(inv.id)"
              >
                <i :class="resendingInvId === inv.id ? 'pi pi-spin pi-spinner' : 'pi pi-refresh'" />
              </button>
              <button
                class="btn btn-danger btn-sm !px-2.5"
                :title="t('members.cancelInvitationTitle')"
                @click="handleCancelInvitation(inv.id)"
              >
                <i class="pi pi-times" />
              </button>
            </div>
          </div>
        </div>

        <div
          v-if="canManageJoinRequests && pendingJoinRequests.length"
          class="border-t border-line"
        >
          <div class="bg-surface px-5 py-3 text-xs font-medium uppercase tracking-wider text-ink-500">
            {{ t('members.joinRequests') }}
          </div>
          <div
            v-for="req in pendingJoinRequests"
            :key="req.id"
            class="flex items-start justify-between gap-4 border-b border-line px-5 py-3 last:border-0"
          >
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-ink-900">{{ req.userName }}</p>
              <p class="text-xs text-ink-500">{{ req.userEmail }}</p>
              <p v-if="req.message" class="mt-1 text-xs italic text-ink-600">
                &ldquo;{{ req.message }}&rdquo;
              </p>
              <p class="mt-1 text-xs text-ink-400">
                {{ t('members.requested') }} {{ new Date(req.createdAt).toLocaleDateString() }}
              </p>
            </div>
            <div class="flex shrink-0 gap-2">
              <button
                class="btn btn-primary btn-sm"
                :disabled="reviewingId === req.id"
                @click="handleReviewRequest(req.id, 'Approved')"
              >
                <i :class="reviewingId === req.id ? 'pi pi-spin pi-spinner' : 'pi pi-check'" />
                {{ t('members.approve') }}
              </button>
              <button
                class="btn btn-outline btn-sm"
                :disabled="reviewingId === req.id"
                @click="handleReviewRequest(req.id, 'Rejected')"
              >
                <i class="pi pi-times" />
                {{ t('members.reject') }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </template>

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
      <form class="flex flex-col gap-4" novalidate @submit.prevent="handleInvite">
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
          <p class="text-xs text-ink-400">{{ t('members.roleHint') }}</p>
        </div>

        <Message v-if="inviteSuccess" severity="success" :closable="false" class="!my-0">
          {{ inviteSuccess }}
        </Message>
        <Message v-if="inviteError" severity="error" :closable="false" class="!my-0">
          {{ inviteError }}
        </Message>

        <div class="flex justify-end gap-2">
          <Button type="button" :label="t('common.cancel')" severity="secondary" text @click="closeInviteDialog" />
          <Button type="submit" :label="t('members.sendInvitation')" :disabled="!canInvite" :loading="inviteSending" />
        </div>
      </form>
    </Dialog>
  </section>
</template>
