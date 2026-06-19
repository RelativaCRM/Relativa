import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import {
  orgApi,
  type CreateOrgUserRequest,
  type OrganizationDto,
  type OrgMemberDto,
  type OrgRoleDto,
  type OrgInvitationDto,
  type OrganizationSettingsDto,
  type UpdateOrganizationSettingsRequest,
} from '@/api/organizations';
import { loadNumber, saveNumber } from '@/api/persistence';
import { getAccountOrg, setAccountOrg } from '@/utils/accountOrgs';
import { useAuthStore } from '@/stores/auth';

const ORG_KEY = 'relativa_org_id';

export const useOrganizationStore = defineStore('organization', () => {
  const organizations = ref<OrganizationDto[]>([]);
  const currentOrgId = ref<number | null>(loadNumber(ORG_KEY));
  const members = ref<OrgMemberDto[]>([]);
  const roles = ref<OrgRoleDto[]>([]);
  const invitations = ref<OrgInvitationDto[]>([]);
  const orgSettings = ref<OrganizationSettingsDto | null>(null);

  const currentOrg = computed(() =>
    organizations.value.find((o) => o.id === currentOrgId.value) ?? null,
  );
  const hasOrganization = computed(() => organizations.value.length > 0);

  function setCurrentOrg(id: number) {
    currentOrgId.value = id;
    saveNumber(ORG_KEY, id);
    setAccountOrg(useAuthStore().user?.email, id);
  }

  async function fetchOrganizations() {
    organizations.value = await orgApi.list();

    const ids = new Set(organizations.value.map((o) => o.id));

    if (currentOrgId.value !== null && !ids.has(currentOrgId.value)) {
      currentOrgId.value = null;
      saveNumber(ORG_KEY, null);
    }

    if (!currentOrgId.value) {
      const remembered = getAccountOrg(useAuthStore().user?.email);
      if (remembered !== null && ids.has(remembered)) {
        setCurrentOrg(remembered);
      } else if (organizations.value.length === 1) {
        setCurrentOrg(organizations.value[0]!.id);
      }
    }

    return organizations.value;
  }

  async function createOrganization(name: string) {
    const org = await orgApi.create(name);
    organizations.value.push(org);
    setCurrentOrg(org.id);
    return org;
  }

  async function updateOrganization(id: number, name: string) {
    await orgApi.update(id, name);
    const idx = organizations.value.findIndex((o) => o.id === id);
    if (idx !== -1) {
      organizations.value[idx] = { ...organizations.value[idx]!, name };
    }
  }

  async function fetchMembers() {
    if (!currentOrgId.value) return;
    members.value = await orgApi.listMembers(currentOrgId.value);
  }

  async function fetchRoles() {
    if (!currentOrgId.value) return;
    roles.value = await orgApi.listRoles(currentOrgId.value);
  }

  async function fetchInvitations() {
    if (!currentOrgId.value) return;
    try {
      invitations.value = await orgApi.listInvitations(currentOrgId.value);
    } catch {
      invitations.value = [];
    }
  }

  async function inviteMember(email: string, orgRoleId?: number) {
    if (!currentOrgId.value) return;
    const inv = await orgApi.invite(currentOrgId.value, email, orgRoleId);
    invitations.value.push(inv);
    return inv;
  }

  async function cancelInvitation(invId: number) {
    if (!currentOrgId.value) return;
    await orgApi.cancelInvitation(currentOrgId.value, invId);
    invitations.value = invitations.value.filter((i) => i.id !== invId);
  }

  async function resendInvitation(invId: number) {
    if (!currentOrgId.value) return;
    const refreshed = await orgApi.resendInvitation(currentOrgId.value, invId);
    const idx = invitations.value.findIndex((i) => i.id === invId);
    if (idx >= 0) invitations.value[idx] = refreshed;
    return refreshed;
  }

  async function changeMemberRole(userId: number, roleId: number) {
    if (!currentOrgId.value) return;
    await orgApi.changeMemberRole(currentOrgId.value, userId, roleId);
    await fetchMembers();
  }

  async function removeMember(userId: number) {
    if (!currentOrgId.value) return;
    await orgApi.removeMember(currentOrgId.value, userId);
    members.value = members.value.filter((m) => m.userId !== userId);
  }

  async function createOrgUser(payload: CreateOrgUserRequest) {
    if (!currentOrgId.value) return;
    const created = await orgApi.createOrgUser(currentOrgId.value, payload);
    await fetchMembers();
    return created;
  }

  async function deleteOrgUser(userId: number) {
    if (!currentOrgId.value) return;
    await orgApi.deleteOrgUser(currentOrgId.value, userId);
    members.value = members.value.filter((m) => m.userId !== userId);
  }

  async function fetchSettings(orgId?: number) {
    const id = orgId ?? currentOrgId.value;
    if (!id) return;
    orgSettings.value = await orgApi.getSettings(id);
    return orgSettings.value;
  }

  async function updateSettings(data: UpdateOrganizationSettingsRequest, orgId?: number) {
    const id = orgId ?? currentOrgId.value;
    if (!id) return;
    await orgApi.updateSettings(id, data);
    if (orgSettings.value) {
      orgSettings.value = { ...orgSettings.value, ...data };
    }
  }

  function clear() {
    organizations.value = [];
    currentOrgId.value = null;
    members.value = [];
    roles.value = [];
    invitations.value = [];
    orgSettings.value = null;
    saveNumber(ORG_KEY, null);
  }

  return {
    organizations,
    currentOrgId,
    currentOrg,
    hasOrganization,
    members,
    roles,
    invitations,
    orgSettings,
    setCurrentOrg,
    fetchOrganizations,
    createOrganization,
    updateOrganization,
    fetchMembers,
    fetchRoles,
    fetchInvitations,
    inviteMember,
    cancelInvitation,
    resendInvitation,
    changeMemberRole,
    removeMember,
    createOrgUser,
    deleteOrgUser,
    fetchSettings,
    updateSettings,
    clear,
  };
});
