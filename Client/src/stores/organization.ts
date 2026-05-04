import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import {
  orgApi,
  type OrganizationDto,
  type OrgMemberDto,
  type OrgRoleDto,
  type OrgInvitationDto,
} from '@/api/organizations';
import { loadNumber, saveNumber } from '@/api/persistence';

const ORG_KEY = 'relativa_org_id';

export const useOrganizationStore = defineStore('organization', () => {
  const organizations = ref<OrganizationDto[]>([]);
  const currentOrgId = ref<number | null>(loadNumber(ORG_KEY));
  const members = ref<OrgMemberDto[]>([]);
  const roles = ref<OrgRoleDto[]>([]);
  const invitations = ref<OrgInvitationDto[]>([]);

  const currentOrg = computed(() =>
    organizations.value.find((o) => o.id === currentOrgId.value) ?? null,
  );
  const hasOrganization = computed(() => organizations.value.length > 0);

  function setCurrentOrg(id: number) {
    currentOrgId.value = id;
    saveNumber(ORG_KEY, id);
  }

  async function fetchOrganizations() {
    organizations.value = await orgApi.list();
    const first = organizations.value[0];
    if (first && !currentOrgId.value) {
      setCurrentOrg(first.id);
    }
    return organizations.value;
  }

  async function createOrganization(name: string) {
    const org = await orgApi.create(name);
    organizations.value.push(org);
    setCurrentOrg(org.id);
    return org;
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

  function clear() {
    organizations.value = [];
    currentOrgId.value = null;
    members.value = [];
    roles.value = [];
    invitations.value = [];
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
    setCurrentOrg,
    fetchOrganizations,
    createOrganization,
    fetchMembers,
    fetchRoles,
    fetchInvitations,
    inviteMember,
    cancelInvitation,
    resendInvitation,
    changeMemberRole,
    removeMember,
    clear,
  };
});
