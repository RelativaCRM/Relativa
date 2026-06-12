<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Message from 'primevue/message';
import InputText from 'primevue/inputtext';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { normalizeError } from '@/api/errors';
import type { EntityListItemDto } from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';
import { hasWorkspacePermission } from '@/utils/workspacePermissions';
import EntityReadView from '@/views/EntityReadView.vue';
import EntityCreateForm from '@/views/EntityCreateForm.vue';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.workspaceId));
const entities = computed(() => entityStore.entitiesFor(workspaceId.value));

const filterType = computed(() => {
  const raw = route.query.entityType;
  if (typeof raw !== 'string' || !raw.trim()) return null;
  return raw.trim().toLowerCase();
});

const searchInput = ref('');
const searchDebounced = ref('');
let searchTimer: ReturnType<typeof setTimeout> | null = null;

watch(searchInput, (v) => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    searchDebounced.value = typeof v === 'string' ? v.trim() : '';
  }, 350);
});

const filteredEntities = computed(() => {
  if (!filterType.value) return entities.value;
  return entities.value.filter(
    (e) => (e.entityTypeName ?? '').toLowerCase() === filterType.value,
  );
});

const filterTypeSchema = computed(() => {
  if (!filterType.value) return null;
  return (
    entityStore.types.find(
      (t) => t.name.toLowerCase() === filterType.value,
    ) ?? null
  );
});

const filterTypeUiLocked = computed(() =>
  filterTypeSchema.value ? isEntityTypeUiLocked(filterTypeSchema.value) : false,
);

const canCreateEntities = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'create_entities'),
);

const detailEntityId = computed(() => {
  const raw = route.query.id;
  if (typeof raw !== 'string' || !raw.trim()) return null;
  const n = Number.parseInt(raw, 10);
  return Number.isFinite(n) && n > 0 ? n : null;
});

const showCreate = computed(() => route.query.action === 'create');

function rowPreview(ent: EntityListItemDto): string {
  const parts = ent.propertyValues.slice(0, 3).map((p) => {
    const val =
      p.value === null || p.value === undefined ? '—' : String(p.value);
    return `${p.displayName}: ${val}`;
  });
  return parts.length ? parts.join(' · ') : '—';
}

const headingTitle = computed(() => {
  const ws = wsStore.currentWorkspace?.name ?? t('entities.workspaceFallback');
  if (filterType.value) {
    return t('entities.titleType', {
      ws,
      type: filterTypeSchema.value?.displayName ?? filterType.value,
    });
  }
  return t('entities.titleAll', { ws });
});

const headingSubtitle = computed(() =>
  filterType.value
    ? t('entities.subtitleType', {
        type: filterTypeSchema.value?.displayName ?? filterType.value,
      })
    : t('entities.subtitleAll'),
);

const loading = ref(true);
const errorMessage = ref<string | null>(null);

async function load() {
  if (!workspaceId.value) return;
  loading.value = true;
  errorMessage.value = null;
  try {
    if (!wsStore.workspaces.length) {
      await wsStore.fetchWorkspaces(orgStore.currentOrgId ?? undefined);
    }
    const belongs = wsStore.workspaces.some((w) => w.id === workspaceId.value);
    if (!belongs) {
      errorMessage.value = t('entities.noAccess');
      entityStore.clearWorkspace(workspaceId.value);
      return;
    }
    wsStore.setCurrentWorkspace(workspaceId.value);
    if (detailEntityId.value || showCreate.value) {
      await entityStore.fetchTypes();
    } else {
      const listQuery = {
        entityTypeId: filterTypeSchema.value?.id,
        q: searchDebounced.value || undefined,
        take: 500,
      };
      await entityStore.fetchList(workspaceId.value, listQuery);
      await entityStore.fetchTypes();
    }
  } catch (err) {
    errorMessage.value = normalizeError(err, t('entities.loadError')).message;
  } finally {
    loading.value = false;
  }
}

function goCreate() {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(workspaceId.value) },
    query: { ...route.query, action: 'create' },
  });
}

function goBack() {
  router.push({ name: 'workspaces' });
}

function openRow(ent: { id: number; entityTypeName: string }) {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(workspaceId.value) },
    query: {
      ...route.query,
      entityType: ent.entityTypeName,
      id: String(ent.id),
    },
  });
}

async function onDetailUpdated() {
  const listQuery = {
    entityTypeId: filterTypeSchema.value?.id,
    q: searchDebounced.value || undefined,
    take: 500,
  };
  await entityStore.fetchList(workspaceId.value, listQuery);
}

watch(workspaceId, (id) => {
  if (id) load();
});

watch(
  () => [filterTypeSchema.value?.id, searchDebounced.value, detailEntityId.value, showCreate.value] as const,
  () => {
    if (!workspaceId.value) return;
    if (detailEntityId.value || showCreate.value) return;
    load();
  },
);

onMounted(load);
</script>

<template>
  <EntityCreateForm v-if="showCreate" />
  <EntityReadView
    v-else-if="detailEntityId"
    :workspace-id="workspaceId"
    :entity-id="detailEntityId"
    @close="load"
    @updated="onDetailUpdated"
  />
  <section v-else class="max-w-4xl">
    <div class="flex items-center justify-between mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          :label="t('nav.workspaces')"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="goBack"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          {{ headingTitle }}
        </h1>
        <p class="mt-1 text-sm text-ink-500">
          {{ headingSubtitle }}
        </p>
      </div>
      <Button
        v-if="canCreateEntities && !filterTypeUiLocked"
        icon="pi pi-plus"
        :label="t('entities.newEntity')"
        @click="goCreate"
      />
    </div>

    <Message
      v-if="errorMessage"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ errorMessage }}
    </Message>

    <div class="mb-6 rounded-xl border border-line bg-white p-4 shadow-sm">
      <div class="flex flex-col sm:flex-row sm:items-center gap-3">
        <div class="flex items-center gap-2 shrink-0">
          <span
            class="flex h-9 w-9 items-center justify-center rounded-lg bg-surface text-ink-500"
            aria-hidden="true"
          >
            <i class="pi pi-search text-sm" />
          </span>
          <div class="min-w-0">
            <p class="text-sm font-medium text-ink-800">{{ t('entities.search') }}</p>
            <p class="text-xs text-ink-500 hidden sm:block">
              {{ t('entities.searchHint') }}
            </p>
          </div>
        </div>
        <InputText
          v-model="searchInput"
          :placeholder="
            filterTypeSchema
              ? t('entities.searchTypePlaceholder', { type: filterTypeSchema.displayName })
              : t('entities.searchAllPlaceholder')
          "
          class="w-full flex-1 !h-10"
        />
      </div>
    </div>

    <LoadingSkeleton
      v-if="loading && !entities.length"
      variant="table"
      :rows="6"
      :label="t('common.loading')"
    />

    <div
      v-else-if="!filteredEntities.length && !errorMessage"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-inbox text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        <template v-if="filterType">
          <template v-if="filterTypeUiLocked">
            {{ t('entities.emptyTypeLocked', { type: filterTypeSchema?.displayName ?? filterType }) }}
          </template>
          <template v-else>
            {{ t('entities.emptyType', { type: filterTypeSchema?.displayName ?? filterType }) }}
          </template>
        </template>
        <template v-else>
          {{ t('entities.emptyAll') }}
        </template>
      </p>
      <Button
        v-if="canCreateEntities && (!filterTypeUiLocked || !filterType)"
        class="mt-4"
        icon="pi pi-plus"
        :label="t('entities.createEntity')"
        @click="goCreate"
      />
    </div>

    <div
      v-else
      class="rounded-xl border border-line bg-white overflow-hidden"
    >
      <table class="w-full text-sm">
        <thead>
          <tr
            class="border-b border-line bg-surface text-left text-xs font-medium text-ink-500 uppercase tracking-wider"
          >
            <th class="px-5 py-3">{{ t('entities.colId') }}</th>
            <th class="px-5 py-3">{{ t('entities.colType') }}</th>
            <th class="px-5 py-3 w-full">{{ t('entities.colPreview') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="ent in filteredEntities"
            :key="ent.id"
            role="button"
            tabindex="0"
            class="border-b border-line last:border-0 hover:bg-surface/50 cursor-pointer"
            @click="openRow(ent)"
            @keydown.enter.prevent="openRow(ent)"
          >
            <td class="px-5 py-3 font-mono text-xs text-ink-700">{{ ent.id }}</td>
            <td class="px-5 py-3">
              <Tag :value="ent.entityTypeDisplayName" severity="secondary" />
            </td>
            <td class="px-5 py-3 text-ink-600 text-xs leading-relaxed">
              {{ rowPreview(ent) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
