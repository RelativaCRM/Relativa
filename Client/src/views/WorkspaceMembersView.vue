<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import Select from 'primevue/select';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { ApiError } from '@/api/http';
import { useApiErrorHandler } from '@/api/errorToast';
import { roleBadgeFullClass, roleLabel } from '@/utils/roleBadge';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const { notify } = useApiErrorHandler();

const ROLE_ORDER = ['ws_admin', 'ws_manager', 'ws_analyst', 'ws_member'];
const FULL_WS_AUTHORITY = [
  'manage_ws_settings',
  'add_ws_members',
  'remove_ws_members',
  'assign_ws_roles',
  'manage_ws_roles',
  'create_entities',
  'edit_entities',
  'delete_entities',
  'view_entities',
  'view_analytics',
  'delete_workspace',
];

const workspaceId = computed(() => Number(route.params.workspaceId));
const loading = ref(true);

const ADD_WS_MEMBERS = 'add_ws_members';
const REMOVE_WS_MEMBERS = 'remove_ws_members';
const ASSIGN_WS_ROLES = 'assign_ws_roles';
const MANAGE_ORG_WS_MEMBERS = 'manage_org_workspace_members';

const hasWsPermission = (perm: string) => {
  return (wsStore.currentWorkspace?.myPermissions ?? []).includes(perm);
};

const hasOrgPermission = (perm: string) => {
  return (orgStore.currentOrg?.myPermissions ?? []).includes(perm);
};

const canAddMember = computed(
  () =>
    hasWsPermission(ADD_WS_MEMBERS) || hasOrgPermission(MANAGE_ORG_WS_MEMBERS),
);

const canRemoveOther = computed(
  () =>
    hasWsPermission(REMOVE_WS_MEMBERS) ||
    hasOrgPermission(MANAGE_ORG_WS_MEMBERS),
);

const canAssignRoles = computed(() => hasWsPermission(ASSIGN_WS_ROLES));

const showAddMember = ref(false);
const addMemberUserId = ref<number | null>(null);
const addMemberRoleId = ref<number | null>(null);
const addMemberSending = ref(false);
const addMemberSuccess = ref<string | null>(null);

const wsMemberUserIds = computed(
  () => new Set(wsStore.members.map((m) => m.userId)),
);

const eligibleOrgMembers = computed(() =>
  orgStore.members.filter((m) => !wsMemberUserIds.value.has(m.userId)),
);

const orgMemberOptions = computed(() =>
  eligibleOrgMembers.value.map((m) => ({
    label: `${m.firstName} ${m.lastName} (${m.email})`,
    value: m.userId,
  })),
);

const roleOptions = computed(() =>
  ROLE_ORDER.map((name) => wsStore.roles.find((r) => r.name === name))
    .filter((r): r is NonNullable<typeof r> => !!r)
    .map((r) => ({ label: roleLabel(r.name, r.displayName), value: r.id })),
);

const memberSearch = ref('');
const filteredMembers = computed(() => {
  const q = memberSearch.value.trim().toLowerCase();
  const list = wsStore.members;
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

const canSubmitAdd = computed(
  () =>
    addMemberUserId.value !== null &&
    addMemberRoleId.value !== null &&
    !addMemberSending.value,
);

function roleIdByName(roleName: string): number | undefined {
  return wsStore.roles.find((r) => r.name === roleName)?.id;
}

async function openAddMemberDialog() {
  addMemberUserId.value = null;
  addMemberRoleId.value = null;
  addMemberSuccess.value = null;
  showAddMember.value = true;
  if (orgStore.currentOrgId) {
    try {
      await orgStore.fetchMembers();
    } catch (err) {
      notify(err, { fallback: t('wsMembers.loadOrgMembersError') });
    }
  }
}

function closeAddMemberDialog() {
  showAddMember.value = false;
  addMemberUserId.value = null;
  addMemberRoleId.value = null;
  addMemberSuccess.value = null;
}

async function handleAddMember() {
  if (!canSubmitAdd.value || addMemberUserId.value == null || addMemberRoleId.value == null)
    return;
  addMemberSending.value = true;
  addMemberSuccess.value = null;
  try {
    await wsStore.addMember(
      workspaceId.value,
      addMemberUserId.value,
      addMemberRoleId.value,
    );
    addMemberSuccess.value = t('wsMembers.memberAdded');
    addMemberUserId.value = null;
    addMemberRoleId.value = null;
    await orgStore.fetchMembers();
  } catch (err) {
    notify(err, { fallback: t('wsMembers.addMemberError') });
  } finally {
    addMemberSending.value = false;
  }
}

const changingRole = ref<number | null>(null);
const roleSelectVersion = ref(0);

async function handleRoleChange(userId: number, newRoleId: number) {
  const target = wsStore.members.find((m) => m.userId === userId);
  const roleHasFullAuthority = (roleName?: string) => {
    if (!roleName) return false;
    const role = wsStore.roles.find((r) => r.name === roleName);
    if (!role) return false;
    const granted = new Set(role.permissions.map((p) => p.name));
    return FULL_WS_AUTHORITY.every((p) => granted.has(p));
  };
  const targetHasFullAuthority = roleHasFullAuthority(target?.roleName);
  const newRoleName = wsStore.roles.find((r) => r.id === newRoleId)?.name;
  const newRoleHasFullAuthority = roleHasFullAuthority(newRoleName);
  if (targetHasFullAuthority && !newRoleHasFullAuthority) {
    const adminCount = wsStore.members.filter(
      (m) => roleHasFullAuthority(m.roleName),
    ).length;
    if (adminCount <= 1) {
      toast.add({
        severity: 'error',
        summary: t('wsMembers.conflictSummary'),
        detail: t('wsMembers.lastAdminError'),
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
      summary: t('wsMembers.roleUpdated'),
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: t('wsMembers.roleUpdateError') });
  } finally {
    await wsStore.fetchMembers(workspaceId.value);
    roleSelectVersion.value++;
    changingRole.value = null;
  }
}

const removingId = ref<number | null>(null);

async function handleRemove(userId: number) {
  removingId.value = userId;
  try {
    await wsStore.removeMember(workspaceId.value, userId);
  } catch (err) {
    notify(err, { fallback: t('wsMembers.removeError') });
  } finally {
    removingId.value = null;
  }
}

async function loadAll() {
  loading.value = true;
  try {
    await Promise.all([
      wsStore.fetchMembers(workspaceId.value),
      wsStore.fetchRoles(workspaceId.value),
      wsStore.workspaces.length === 0
        ? wsStore.fetchWorkspaces(orgStore.currentOrgId ?? undefined)
        : Promise.resolve(),
      orgStore.currentOrgId
        ? orgStore.fetchRoles()
        : Promise.resolve(),
    ]);
    wsStore.setCurrentWorkspace(workspaceId.value);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      router.replace({ name: 'workspaces' });
      return;
    }
    notify(err, { fallback: t('wsMembers.loadError') });
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
  <section class="mx-auto max-w-4xl pb-16">
    <button
      class="btn btn-outline btn-sm mb-3"
      @click="router.push({ name: 'workspaces' })"
    >
      <i class="pi pi-arrow-left" />
      {{ t('nav.workspaces') }}
    </button>

    <header class="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold text-ink-900">
          {{ wsStore.currentWorkspace?.name ?? t('wsMembers.fallbackName') }}
        </h1>
        <p class="mt-1.5 text-sm text-ink-500">{{ t('wsMembers.subtitle') }}</p>
      </div>
      <button
        v-if="canAddMember"
        class="btn btn-primary btn-sm shrink-0"
        @click="openAddMemberDialog"
      >
        <i class="pi pi-user-plus" />
        {{ t('wsMembers.addMember') }}
      </button>
    </header>

    <LoadingSkeleton v-if="loading" variant="table" :rows="5" :label="t('wsMembers.fallbackName')" />

    <div v-else class="border border-line bg-white">
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
          <tr
            class="border-b border-line bg-surface text-left text-xs font-medium uppercase tracking-wider text-ink-500"
          >
            <th class="px-5 py-3">{{ t('members.colName') }}</th>
            <th class="px-5 py-3">{{ t('members.colEmail') }}</th>
            <th class="px-5 py-3">{{ t('members.colRole') }}</th>
            <th class="px-5 py-3">{{ t('members.colJoined') }}</th>
            <th class="w-16 px-5 py-3"></th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="member in filteredMembers"
            :key="member.userId"
            class="border-b border-line transition-colors last:border-0 hover:bg-surface/50"
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
              <Select
                v-if="roleOptions.length && canAssignRoles"
                :key="`role-${member.userId}-${member.roleName}-${roleSelectVersion}`"
                :model-value="roleIdByName(member.roleName)"
                :options="roleOptions"
                option-label="label"
                option-value="value"
                :disabled="changingRole === member.userId"
                class="!h-8 !min-w-[120px] !text-xs"
                @update:model-value="handleRoleChange(member.userId, $event)"
              />
              <span v-else :class="roleBadgeFullClass(member.roleName)">
                {{ roleLabel(member.roleName, member.roleDisplayName) }}
              </span>
            </td>
            <td class="px-5 py-3 text-ink-500">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </td>
            <td class="px-5 py-3 text-right">
              <button
                v-if="member.userId !== auth.user?.id && canRemoveOther"
                class="btn btn-danger btn-sm !px-2.5"
                :disabled="removingId === member.userId"
                :title="t('wsMembers.removeTitle')"
                @click="handleRemove(member.userId)"
              >
                <i :class="removingId === member.userId ? 'pi pi-spin pi-spinner' : 'pi pi-trash'" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-else class="px-5 py-10 text-center text-sm text-ink-500">
        {{ wsStore.members.length ? t('members.noneFound') : t('wsMembers.noMembers') }}
      </p>
    </div>

    <Dialog
      v-model:visible="showAddMember"
      :header="t('wsMembers.addDialogTitle')"
      modal
      :style="{ width: '460px' }"
      @hide="closeAddMemberDialog"
    >
      <form
        class="flex flex-col gap-4"
        novalidate
        @submit.prevent="handleAddMember"
      >
        <p class="text-xs text-ink-500">
          {{ t('wsMembers.addHint') }}
        </p>

        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            {{ t('wsMembers.memberLabel') }} <span class="text-danger">*</span>
          </label>
          <Select
            v-model="addMemberUserId"
            :options="orgMemberOptions"
            option-label="label"
            option-value="value"
            :placeholder="t('wsMembers.selectUser')"
            class="!h-10"
            :filter="true"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            {{ t('wsMembers.workspaceRole') }} <span class="text-danger">*</span>
          </label>
          <Select
            v-model="addMemberRoleId"
            :options="roleOptions"
            option-label="label"
            option-value="value"
            :placeholder="t('wsMembers.selectRole')"
            class="!h-10"
          />
        </div>

        <Message
          v-if="addMemberSuccess"
          severity="success"
          :closable="false"
          class="!my-0"
        >
          {{ addMemberSuccess }}
        </Message>

        <div class="flex justify-end gap-2">
          <Button
            type="button"
            :label="t('wsMembers.close')"
            severity="secondary"
            text
            @click="closeAddMemberDialog"
          />
          <Button
            type="submit"
            :label="t('wsMembers.addToWorkspace')"
            :disabled="!canSubmitAdd"
            :loading="addMemberSending"
          />
        </div>
      </form>
    </Dialog>
  </section>
</template>
