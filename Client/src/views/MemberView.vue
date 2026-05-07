<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import { useToast } from 'primevue/usetoast';
import { roleDisplayName, roleBadgeFullClass } from '@/utils/roleBadge';
import { orgApi } from '@/api/organizations';
import {
  workspaceApi,
  type WorkspaceDto,
  type WorkspaceMemberDto,
  type WorkspaceRoleDto,
} from '@/api/workspaces';
import { useApiErrorHandler } from '@/api/errorToast';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';

const route = useRoute();
const router = useRouter();
const toast = useToast();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const { notify } = useApiErrorHandler();

const loading = ref(true);
const savingProfile = ref(false);
const savingRole = ref(false);
const removing = ref(false);
const archiving = ref(false);
const workspaceLoading = ref(false);
const workspaceActionId = ref<number | null>(null);

const workspaces = ref<WorkspaceDto[]>([]);
const workspaceMembers = ref<Record<number, WorkspaceMemberDto[]>>({});
const workspaceRoles = ref<Record<number, WorkspaceRoleDto[]>>({});
const selectedWorkspaceRole = ref<Record<number, number | null>>({});

const editFirstName = ref('');
const editLastName = ref('');
const selectedOrgRoleId = ref<number | null>(null);

const ASSIGN_ORG_ROLES_PERM = 'assign_org_roles';
const REMOVE_ORG_MEMBERS_PERM = 'remove_org_members';
const EDIT_OTHER_PROFILE_PERM = 'edit_other_org_users_profile';
const DELETE_ORG_USERS_PERM = 'delete_org_users';
const MANAGE_ORG_WORKSPACE_MEMBERS_PERM = 'manage_org_workspace_members';

function hasPermission(permission: string): boolean {
  const roleName = orgStore.currentOrg?.userRole;
  if (!roleName) return false;
  const role = orgStore.roles.find((r) => r.name === roleName);
  return role?.permissions.some((p) => p.name === permission) ?? false;
}

const canEditOtherProfiles = computed(() => hasPermission(EDIT_OTHER_PROFILE_PERM));
const canAssignOrgRoles = computed(() => hasPermission(ASSIGN_ORG_ROLES_PERM));
const canRemoveOrgMembers = computed(() => hasPermission(REMOVE_ORG_MEMBERS_PERM));
const canDeleteOrgUsers = computed(() => hasPermission(DELETE_ORG_USERS_PERM));
const canManageWorkspaceAccess = computed(() =>
  hasPermission(MANAGE_ORG_WORKSPACE_MEMBERS_PERM),
);

const memberUserId = computed(() => Number(route.params.memberUserId));
const member = computed(() =>
  orgStore.members.find((m) => m.userId === memberUserId.value) ?? null,
);
const isSelf = computed(() => member.value?.userId === auth.user?.id);

const orgRoleOptions = computed(() =>
  orgStore.roles
    .filter((r) => r.name === 'org_admin' || r.name === 'org_member')
    .map((r) => ({
      label: r.name === 'org_admin' ? 'Admin' : 'Member',
      value: r.id,
      name: r.name,
    })),
);

function roleIdByName(roleName: string): number | null {
  return orgStore.roles.find((r) => r.name === roleName)?.id ?? null;
}

const displayRole = roleDisplayName;

function emailDomain(email: string | null | undefined): string {
  if (!email) return '';
  const idx = email.lastIndexOf('@');
  if (idx < 0 || idx === email.length - 1) return '';
  return email.slice(idx + 1).trim().toLowerCase();
}

const canArchiveMember = computed(() => {
  if (!member.value || !canDeleteOrgUsers.value || isSelf.value) return false;
  return emailDomain(member.value.email) === emailDomain(auth.user?.email);
});

function workspaceMemberFor(wsId: number): WorkspaceMemberDto | null {
  const members = workspaceMembers.value[wsId] ?? [];
  return members.find((m) => m.userId === memberUserId.value) ?? null;
}

function workspaceRoleOptions(wsId: number) {
  return (workspaceRoles.value[wsId] ?? []).map((r) => ({
    label: r.name,
    value: r.id,
  }));
}

function selectedWorkspaceRoleId(wsId: number): number | null {
  const current = selectedWorkspaceRole.value[wsId];
  if (current) return current;
  const defaultRole = (workspaceRoles.value[wsId] ?? []).find(
    (r) => r.name === 'ws_member',
  );
  return defaultRole?.id ?? null;
}

async function loadWorkspaceAccess() {
  if (!orgStore.currentOrgId || !canManageWorkspaceAccess.value) return;
  workspaceLoading.value = true;
  try {
    workspaces.value = await workspaceApi.list(orgStore.currentOrgId);
    await Promise.all(
      workspaces.value.map(async (ws) => {
        const [members, roles] = await Promise.all([
          workspaceApi.listMembers(ws.id),
          workspaceApi.listRoles(ws.id),
        ]);
        workspaceMembers.value[ws.id] = members;
        workspaceRoles.value[ws.id] = roles;
        const current = members.find((m) => m.userId === memberUserId.value);
        selectedWorkspaceRole.value[ws.id] =
          current
            ? roles.find((r) => r.name === current.roleName)?.id ?? null
            : roles.find((r) => r.name === 'ws_member')?.id ?? null;
      }),
    );
  } catch (err) {
    notify(err, { fallback: 'Failed to load workspace access.' });
  } finally {
    workspaceLoading.value = false;
  }
}

async function handleSaveProfile() {
  if (!member.value || !orgStore.currentOrgId) return;
  savingProfile.value = true;
  try {
    await orgApi.updateOrgUserProfile(orgStore.currentOrgId, member.value.userId, {
      firstName: editFirstName.value.trim(),
      lastName: editLastName.value.trim(),
    });
    await orgStore.fetchMembers();
    toast.add({
      severity: 'success',
      summary: 'Profile updated',
      detail: 'Member details were updated.',
      life: 4000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to update profile.' });
  } finally {
    savingProfile.value = false;
  }
}

async function handleSaveOrgRole() {
  if (!member.value || !selectedOrgRoleId.value || member.value.roleName === 'org_owner') {
    return;
  }
  savingRole.value = true;
  try {
    await orgStore.changeMemberRole(member.value.userId, selectedOrgRoleId.value);
    toast.add({
      severity: 'success',
      summary: 'Role updated',
      detail: 'Organization role has been updated.',
      life: 4000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to update organization role.' });
  } finally {
    savingRole.value = false;
  }
}

async function handleRemoveFromOrg() {
  if (!member.value) return;
  if (!window.confirm('Remove this member from the organization?')) return;
  removing.value = true;
  try {
    await orgStore.removeMember(member.value.userId);
    toast.add({
      severity: 'success',
      summary: 'Member removed',
      detail: 'The user was removed from the organization.',
      life: 4000,
    });
    await router.push({ name: 'members' });
  } catch (err) {
    notify(err, { fallback: 'Failed to remove member.' });
  } finally {
    removing.value = false;
  }
}

async function handleArchiveAccount() {
  if (!member.value || !orgStore.currentOrgId) return;
  if (
    !window.confirm(
      'Archive this account? This disables login and removes the user from active use.',
    )
  ) {
    return;
  }
  archiving.value = true;
  try {
    await orgStore.deleteOrgUser(member.value.userId);
    toast.add({
      severity: 'success',
      summary: 'Account archived',
      detail: 'The user account has been archived.',
      life: 4000,
    });
    await router.push({ name: 'members' });
  } catch (err) {
    notify(err, { fallback: 'Failed to archive account.' });
  } finally {
    archiving.value = false;
  }
}

async function handleGrantWorkspaceAccess(wsId: number) {
  if (!member.value) return;
  const roleId = selectedWorkspaceRoleId(wsId);
  if (!roleId) {
    notify(new Error('Please select a workspace role first.'), { fallback: 'Select a role.' });
    return;
  }
  workspaceActionId.value = wsId;
  try {
    await workspaceApi.addMember(wsId, member.value.userId, roleId);
    workspaceMembers.value[wsId] = await workspaceApi.listMembers(wsId);
    toast.add({
      severity: 'success',
      summary: 'Access granted',
      detail: 'Workspace access has been assigned.',
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to grant workspace access.' });
  } finally {
    workspaceActionId.value = null;
  }
}

async function handleChangeWorkspaceRole(wsId: number) {
  if (!member.value) return;
  const roleId = selectedWorkspaceRoleId(wsId);
  if (!roleId) return;
  workspaceActionId.value = wsId;
  try {
    await workspaceApi.changeMemberRole(wsId, member.value.userId, roleId);
    workspaceMembers.value[wsId] = await workspaceApi.listMembers(wsId);
    toast.add({
      severity: 'success',
      summary: 'Role updated',
      detail: 'Workspace role has been changed.',
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to update workspace role.' });
  } finally {
    workspaceActionId.value = null;
  }
}

async function handleRemoveWorkspaceAccess(wsId: number) {
  if (!member.value) return;
  workspaceActionId.value = wsId;
  try {
    await workspaceApi.removeMember(wsId, member.value.userId);
    workspaceMembers.value[wsId] = await workspaceApi.listMembers(wsId);
    toast.add({
      severity: 'success',
      summary: 'Access removed',
      detail: 'Workspace access has been removed.',
      life: 3000,
    });
  } catch (err) {
    notify(err, { fallback: 'Failed to remove workspace access.' });
  } finally {
    workspaceActionId.value = null;
  }
}

onMounted(async () => {
  try {
    await Promise.all([orgStore.fetchMembers(), orgStore.fetchRoles()]);
    if (!member.value) {
      await router.replace({ name: 'members' });
      return;
    }
    editFirstName.value = member.value.firstName;
    editLastName.value = member.value.lastName;
    selectedOrgRoleId.value = roleIdByName(member.value.roleName);
    await loadWorkspaceAccess();
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <section class="max-w-5xl">
    <div class="flex items-center justify-between mb-6">
      <div>
        <Button
          icon="pi pi-arrow-left"
          label="Back to members"
          text
          class="!px-0 mb-2"
          @click="router.push({ name: 'members' })"
        />
        <h1 class="text-2xl font-bold text-ink-900">Member</h1>
        <p v-if="member" class="mt-3 text-sm text-ink-500">
          <span class="font-semibold text-brand-600">
            {{ member.firstName }} {{ member.lastName }}
          </span>
          · {{ member.email }}
        </p>
      </div>
      <span v-if="member" :class="roleBadgeFullClass(member.roleName)">
        {{ displayRole(member.roleName) }}
      </span>
    </div>

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <div v-else-if="!member" class="rounded-xl border border-line bg-white p-6 text-ink-600">
      Member not found.
    </div>

    <div v-else class="space-y-6">
      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-4">Profile</h2>
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">First name</label>
            <InputText v-model="editFirstName" maxlength="100" :disabled="!canEditOtherProfiles || isSelf" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">Last name</label>
            <InputText v-model="editLastName" maxlength="100" :disabled="!canEditOtherProfiles || isSelf" />
          </div>
        </div>
        <div class="mt-4 flex justify-end">
          <Button
            label="Save profile"
            :loading="savingProfile"
            :disabled="!canEditOtherProfiles || isSelf || !editFirstName.trim() || !editLastName.trim()"
            @click="handleSaveProfile"
          />
        </div>
      </div>

      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-4">Organization access</h2>
        <div class="grid grid-cols-[1fr_auto] items-end gap-3">
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">Role</label>
            <Select
              v-model="selectedOrgRoleId"
              :options="orgRoleOptions"
              option-label="label"
              option-value="value"
              :disabled="!canAssignOrgRoles || member.roleName === 'org_owner' || isSelf"
            />
          </div>
          <Button
            label="Save role"
            :loading="savingRole"
            :disabled="!canAssignOrgRoles || member.roleName === 'org_owner' || isSelf || !selectedOrgRoleId"
            @click="handleSaveOrgRole"
          />
        </div>
      </div>

      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-4">Danger zone</h2>
        <div class="flex flex-wrap gap-2">
          <Button
            label="Remove from organization"
            severity="warning"
            :loading="removing"
            :disabled="!canRemoveOrgMembers || member.roleName === 'org_owner' || isSelf"
            @click="handleRemoveFromOrg"
          />
          <Button
            label="Archive account"
            severity="danger"
            :loading="archiving"
            :disabled="!canArchiveMember"
            @click="handleArchiveAccount"
          />
        </div>
        <p class="mt-2 text-xs text-ink-500">
          Archive account requires <code>delete_org_users</code> and matching email domain.
        </p>
      </div>

      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-lg font-semibold text-ink-900 mb-4">Workspace access</h2>
        <div v-if="!canManageWorkspaceAccess" class="text-sm text-ink-500">
          You need <code>manage_org_workspace_members</code> to manage workspace access.
        </div>
        <div v-else-if="workspaceLoading" class="text-sm text-ink-500">Loading workspaces...</div>
        <div v-else class="space-y-3">
          <div
            v-for="ws in workspaces"
            :key="ws.id"
            class="border border-line rounded-lg p-3 flex items-center gap-3"
          >
            <div class="flex-1 min-w-0">
              <p class="font-medium text-ink-900 truncate">{{ ws.name }}</p>
              <p class="text-xs text-ink-500">
                {{ workspaceMemberFor(ws.id) ? 'Has access' : 'No access' }}
              </p>
            </div>
            <Select
              v-model="selectedWorkspaceRole[ws.id]"
              :options="workspaceRoleOptions(ws.id)"
              option-label="label"
              option-value="value"
              class="!w-48"
            />
            <Button
              v-if="!workspaceMemberFor(ws.id)"
              label="Grant access"
              :loading="workspaceActionId === ws.id"
              @click="handleGrantWorkspaceAccess(ws.id)"
            />
            <Button
              v-else
              label="Save role"
              severity="secondary"
              :loading="workspaceActionId === ws.id"
              @click="handleChangeWorkspaceRole(ws.id)"
            />
            <Button
              v-if="workspaceMemberFor(ws.id)"
              icon="pi pi-times"
              severity="danger"
              text
              rounded
              :loading="workspaceActionId === ws.id"
              @click="handleRemoveWorkspaceAccess(ws.id)"
            />
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
