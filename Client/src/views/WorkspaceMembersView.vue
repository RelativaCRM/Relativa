<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
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
import { roleDisplayName, roleBadgeFullClass } from '@/utils/roleBadge';

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

/* ── Add member dialog (org members not yet in workspace) ─ */
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
    .map((r) => ({ label: displayRole(r.name), value: r.id })),
);

const canSubmitAdd = computed(
  () =>
    addMemberUserId.value !== null &&
    addMemberRoleId.value !== null &&
    !addMemberSending.value,
);

const displayRole = roleDisplayName;

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
      notify(err, { fallback: 'Could not load organization members.' });
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
    addMemberSuccess.value = 'Member added to workspace.';
    addMemberUserId.value = null;
    addMemberRoleId.value = null;
    await orgStore.fetchMembers();
  } catch (err) {
    notify(err, { fallback: 'Failed to add member.' });
  } finally {
    addMemberSending.value = false;
  }
}

/* ── Role change ───────────────────────────────────────── */
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
        summary: 'Conflict',
        detail: 'Cannot remove the last full-authority workspace member.',
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
        <p class="mt-3 text-sm text-ink-500">Manage workspace members.</p>
      </div>
      <div class="flex items-center gap-2">
        <Button
          icon="pi pi-database"
          label="Entities"
          severity="secondary"
          @click="
            router.push({
              name: 'workspace-entities',
              params: { workspaceId: String(workspaceId) },
            })
          "
        />
        <Button
          v-if="canAddMember"
          icon="pi pi-user-plus"
          label="Add member"
          @click="openAddMemberDialog"
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
                v-if="roleOptions.length && canAssignRoles"
                :key="`role-${member.userId}-${member.roleName}-${roleSelectVersion}`"
                :model-value="roleIdByName(member.roleName)"
                :options="roleOptions"
                option-label="label"
                option-value="value"
                :disabled="changingRole === member.userId"
                class="!h-8 !text-xs !min-w-[120px]"
                @update:model-value="handleRoleChange(member.userId, $event)"
              />
              <span v-else :class="roleBadgeFullClass(member.roleName)">
                {{ displayRole(member.roleName) }}
              </span>
            </td>
            <td class="px-5 py-3 text-ink-500">
              {{ new Date(member.joinedAt).toLocaleDateString() }}
            </td>
            <td class="px-5 py-3">
              <Button
                v-if="member.userId !== auth.user?.id && canRemoveOther"
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
    </div>

    <Dialog
      v-model:visible="showAddMember"
      header="Add organization member to workspace"
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
          Only users who are already in the parent organization can be added.
          Invite them to the organization first if they are not listed.
        </p>

        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            Member <span class="text-danger">*</span>
          </label>
          <Select
            v-model="addMemberUserId"
            :options="orgMemberOptions"
            option-label="label"
            option-value="value"
            placeholder="Select user"
            class="!h-10"
            :filter="true"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            Workspace role <span class="text-danger">*</span>
          </label>
          <Select
            v-model="addMemberRoleId"
            :options="roleOptions"
            option-label="label"
            option-value="value"
            placeholder="Select role"
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
            label="Close"
            severity="secondary"
            text
            @click="closeAddMemberDialog"
          />
          <Button
            type="submit"
            label="Add to workspace"
            :disabled="!canSubmitAdd"
            :loading="addMemberSending"
          />
        </div>
      </form>
    </Dialog>
  </section>
</template>
