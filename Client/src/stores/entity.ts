import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import {
  entityApi,
  type CreateEntityRequest,
  type EntityDetailDto,
  type EntityListItemDto,
  type EntityTypeDto,
  type ListEntitiesQuery,
  type UpdateEntityRequest,
} from '@/api/entities';
import { entityGraphApi } from '@/api/entityGraph';

export const useEntityStore = defineStore('entity', () => {
  const types = ref<EntityTypeDto[]>([]);
  const entitiesByWorkspace = ref<Record<number, EntityListItemDto[]>>({});
  const detailById = ref<Record<number, EntityDetailDto>>({});
  const selectedId = ref<number | null>(null);

  const typesLoaded = computed(() => types.value.length > 0);

  /** Types shown in the workspace sidebar (top-level / creatable-from-nav only). */
  const standaloneTypes = computed(() =>
    types.value.filter((t) => t.isStandalone),
  );

  function entitiesFor(workspaceId: number): EntityListItemDto[] {
    return entitiesByWorkspace.value[workspaceId] ?? [];
  }

  function select(id: number | null) {
    selectedId.value = id;
  }

  async function fetchTypes(force = false): Promise<EntityTypeDto[]> {
    if (!force && typesLoaded.value) return types.value;
    types.value = await entityApi.listTypes();
    return types.value;
  }

  async function fetchList(
    workspaceId: number,
    query?: ListEntitiesQuery,
  ): Promise<EntityListItemDto[]> {
    const list = await entityApi.list(workspaceId, query);
    entitiesByWorkspace.value = {
      ...entitiesByWorkspace.value,
      [workspaceId]: list,
    };
    return list;
  }

  async function fetchDetail(
    workspaceId: number,
    entityId: number,
  ): Promise<EntityDetailDto> {
    const detail = await entityApi.get(workspaceId, entityId);
    detailById.value = { ...detailById.value, [entityId]: detail };
    return detail;
  }

  async function create(
    workspaceId: number,
    payload: CreateEntityRequest,
  ): Promise<EntityDetailDto> {
    const detail = await entityApi.create(workspaceId, payload);
    mergeAfterCreate(workspaceId, detail);
    return detail;
  }

  async function createViaGraph(
    workspaceId: number,
    payload: CreateEntityRequest,
  ): Promise<EntityDetailDto> {
    const detail = await entityGraphApi.create(workspaceId, payload);
    mergeAfterCreate(workspaceId, detail);
    return detail;
  }

  function mergeAfterCreate(workspaceId: number, detail: EntityDetailDto) {
    detailById.value = { ...detailById.value, [detail.id]: detail };
    const current = entitiesByWorkspace.value[workspaceId] ?? [];
    entitiesByWorkspace.value = {
      ...entitiesByWorkspace.value,
      [workspaceId]: [...current, detail],
    };
  }

  async function update(
    workspaceId: number,
    entityId: number,
    payload: UpdateEntityRequest,
  ): Promise<EntityDetailDto> {
    const detail = await entityApi.update(workspaceId, entityId, payload);
    detailById.value = { ...detailById.value, [entityId]: detail };
    return detail;
  }

  async function archive(workspaceId: number, entityId: number): Promise<void> {
    await entityApi.archive(workspaceId, entityId);
    const list = entitiesByWorkspace.value[workspaceId];
    if (list) {
      entitiesByWorkspace.value = {
        ...entitiesByWorkspace.value,
        [workspaceId]: list.filter((e) => e.id !== entityId),
      };
    }
    if (selectedId.value === entityId) selectedId.value = null;
  }

  function clearWorkspace(workspaceId: number) {
    if (entitiesByWorkspace.value[workspaceId]) {
      const next = { ...entitiesByWorkspace.value };
      delete next[workspaceId];
      entitiesByWorkspace.value = next;
    }
  }

  function clear() {
    types.value = [];
    entitiesByWorkspace.value = {};
    detailById.value = {};
    selectedId.value = null;
  }

  return {
    types,
    standaloneTypes,
    entitiesByWorkspace,
    detailById,
    selectedId,
    typesLoaded,
    entitiesFor,
    select,
    fetchTypes,
    fetchList,
    fetchDetail,
    create,
    createViaGraph,
    update,
    archive,
    clearWorkspace,
    clear,
  };
});
