import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import {
  workspaceApi,
  type WorkspaceDto,
  type WorkspaceMemberDto,
  type WorkspaceRoleDto,
} from '@/api/workspaces';
import { loadNumber, saveNumber } from '@/api/persistence';

const WS_KEY = 'relativa_ws_id';

export const useWorkspaceStore = defineStore('workspace', () => {
  const workspaces = ref<WorkspaceDto[]>([]);
  const currentWorkspaceId = ref<number | null>(loadNumber(WS_KEY));
  const members = ref<WorkspaceMemberDto[]>([]);
  const roles = ref<WorkspaceRoleDto[]>([]);

  const currentWorkspace = computed(
    () =>
      workspaces.value.find((w) => w.id === currentWorkspaceId.value) ?? null,
  );

  function setCurrentWorkspace(id: number | null) {
    currentWorkspaceId.value = id;
    saveNumber(WS_KEY, id);
  }

  async function fetchWorkspaces(organizationId?: number) {
    workspaces.value = await workspaceApi.list(organizationId);
    if (
      currentWorkspaceId.value &&
      !workspaces.value.some((w) => w.id === currentWorkspaceId.value)
    ) {
      setCurrentWorkspace(null);
    }
    return workspaces.value;
  }

  async function createWorkspace(name: string, organizationId: number) {
    const ws = await workspaceApi.create(name, organizationId);
    workspaces.value.push(ws);
    setCurrentWorkspace(ws.id);
    return ws;
  }

  async function updateWorkspace(id: number, name: string) {
    await workspaceApi.update(id, name);
    const existing = workspaces.value.find((w) => w.id === id);
    if (existing) existing.name = name;
  }

  async function archiveWorkspace(id: number) {
    await workspaceApi.archive(id);
    workspaces.value = workspaces.value.filter((w) => w.id !== id);
    if (currentWorkspaceId.value === id) {
      setCurrentWorkspace(null);
    }
  }

  async function fetchMembers(wsId: number) {
    members.value = await workspaceApi.listMembers(wsId);
  }

  async function fetchRoles(wsId: number) {
    roles.value = await workspaceApi.listRoles(wsId);
  }

  async function addMember(wsId: number, userId: number, roleId: number) {
    const added = await workspaceApi.addMember(wsId, userId, roleId);
    members.value.push(added);
    return added;
  }

  async function changeMemberRole(
    wsId: number,
    userId: number,
    roleId: number,
  ) {
    await workspaceApi.changeMemberRole(wsId, userId, roleId);
    await fetchMembers(wsId);
  }

  async function removeMember(wsId: number, userId: number) {
    await workspaceApi.removeMember(wsId, userId);
    members.value = members.value.filter((m) => m.userId !== userId);
  }

  function clear() {
    workspaces.value = [];
    currentWorkspaceId.value = null;
    members.value = [];
    roles.value = [];
    saveNumber(WS_KEY, null);
  }

  return {
    workspaces,
    currentWorkspaceId,
    currentWorkspace,
    members,
    roles,
    setCurrentWorkspace,
    fetchWorkspaces,
    createWorkspace,
    updateWorkspace,
    archiveWorkspace,
    fetchMembers,
    fetchRoles,
    addMember,
    changeMemberRole,
    removeMember,
    clear,
  };
});
