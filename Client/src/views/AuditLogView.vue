<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import DataTable, { type DataTablePageEvent } from 'primevue/datatable';
import Column from 'primevue/column';
import DatePicker from 'primevue/datepicker';
import Select from 'primevue/select';
import InputText from 'primevue/inputtext';
import FloatLabel from 'primevue/floatlabel';
import Message from 'primevue/message';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

import { useWorkspaceStore } from '@/stores/workspace';
import { useOrganizationStore } from '@/stores/organization';
import { useAuditStore } from '@/stores/audit';
import {
  type AuditLogEntryDto,
  type AuditLogQuery,
  type AuditScope,
} from '@/api/audit';
import { ApiError } from '@/api/http';
import { scopeBadgeFullClass } from '@/utils/auditBadge';

const { t } = useI18n();
const router = useRouter();
const wsStore = useWorkspaceStore();
const orgStore = useOrganizationStore();
const auditStore = useAuditStore();

const initializing = ref(true);

const availableWorkspaces = computed(() =>
  wsStore.workspaces.filter((w) => w.myPermissions?.includes('view_analytics')),
);
const workspaceOptions = computed(() =>
  availableWorkspaces.value.map((w) => ({ label: w.name, value: w.id })),
);

const canViewWorkspaceScope = computed(() => availableWorkspaces.value.length > 0);
const canViewOrgScope = computed(
  () => (orgStore.currentOrg?.myPermissions ?? []).includes('manage_org_settings'),
);
const canViewPage = computed(() => canViewWorkspaceScope.value || canViewOrgScope.value);

type ScopeOption = { label: string; value: AuditScope };

const scopeOptions = computed<ScopeOption[]>(() => {
  const opts: ScopeOption[] = [];
  if (canViewWorkspaceScope.value) {
    opts.push({ label: t('audit.scopeEntities'), value: 'entity' });
    opts.push({ label: t('audit.scopeWorkspace'), value: 'workspace' });
  }
  if (canViewOrgScope.value) {
    opts.push({ label: t('audit.scopeOrganization'), value: 'organization' });
  }
  if (canViewWorkspaceScope.value || canViewOrgScope.value) {
    opts.push({ label: t('audit.scopeUsers'), value: 'user' });
  }
  return opts;
});

const scope = ref<AuditScope>('entity');
const selectedWorkspaceId = ref<number | null>(null);
const dateRange = ref<Date[] | null>(null);
const actionFilter = ref<string>('');

const showWorkspacePicker = computed(
  () => scope.value === 'entity' || scope.value === 'workspace',
);

const pageSizeOptions = [10, 20, 50, 100];
const pageSize = ref(20);
const pageIndex = ref(1);

watch(
  scopeOptions,
  (opts) => {
    if (!opts.some((o) => o.value === scope.value) && opts[0]) {
      scope.value = opts[0].value;
    }
  },
  { immediate: true },
);

const errorMessage = ref<string | null>(null);

function buildQuery(): AuditLogQuery | null {
  const orgId = orgStore.currentOrgId;

  const q: AuditLogQuery = {
    entityType: scope.value,
    index: pageIndex.value,
    pageSize: pageSize.value,
  };

  if (scope.value === 'entity' || scope.value === 'workspace') {
    if (!selectedWorkspaceId.value) return null;
    q.workspaceId = selectedWorkspaceId.value;
  } else if (scope.value === 'organization') {
    if (!orgId) return null;
    q.organizationId = orgId;
  } else if (scope.value === 'user') {
    if (selectedWorkspaceId.value) q.workspaceId = selectedWorkspaceId.value;
    else if (orgId) q.organizationId = orgId;
  }

  if (dateRange.value?.[0]) q.dateFrom = dateRange.value[0].toISOString();
  if (dateRange.value?.[1]) q.dateTo = dateRange.value[1].toISOString();
  if (actionFilter.value.trim()) q.action = actionFilter.value.trim();

  return q;
}

async function load() {
  const q = buildQuery();
  if (!q) {
    errorMessage.value = t('audit.wsContextRequired');
    return;
  }
  errorMessage.value = null;
  try {
    await auditStore.fetchRows(q);
  } catch (err) {
    errorMessage.value = err instanceof ApiError ? err.message : t('audit.loadError');
  }
}

function applyFilters() {
  pageIndex.value = 1;
  load();
}

function resetFilters() {
  dateRange.value = null;
  actionFilter.value = '';
  pageIndex.value = 1;
  load();
}

function onPage(event: DataTablePageEvent) {
  pageIndex.value = event.page + 1;
  pageSize.value = event.rows;
  load();
}

watch(scope, () => {
  if (initializing.value) return;
  pageIndex.value = 1;
  load();
});

watch(selectedWorkspaceId, () => {
  if (initializing.value) return;
  if (!showWorkspacePicker.value && scope.value !== 'user') return;
  pageIndex.value = 1;
  load();
});

onMounted(async () => {
  if (orgStore.currentOrgId && !wsStore.workspaces.length) {
    try {
      await wsStore.fetchWorkspaces(orgStore.currentOrgId);
    } catch {
      // permission gate handles empty state
    }
  }
  const current = wsStore.currentWorkspaceId;
  selectedWorkspaceId.value =
    current && availableWorkspaces.value.some((w) => w.id === current)
      ? current
      : (availableWorkspaces.value[0]?.id ?? null);
  initializing.value = false;
  await load();
});

function typeLabel(value: string | null | undefined): string {
  switch (value) {
    case 'organization':
      return t('audit.typeOrganization');
    case 'workspace':
      return t('audit.typeWorkspace');
    case 'entity':
      return t('audit.typeEntity');
    case 'user':
      return t('audit.typeUser');
    default:
      return value ?? '—';
  }
}

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString();
}

function actorEmail(row: AuditLogEntryDto): string {
  return row.actor?.email ?? '—';
}

function humanizeAction(action: string): string {
  if (!action) return '—';
  return action
    .split('_')
    .filter(Boolean)
    .map((w, i) => (i === 0 ? w.charAt(0).toUpperCase() + w.slice(1) : w))
    .join(' ');
}

function humanizeField(field: string): string {
  if (!field) return '';
  return field
    .split('_')
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

function entityIdOf(row: AuditLogEntryDto): number | null {
  return row.entity?.id ?? null;
}

function targetWorkspaceId(row: AuditLogEntryDto): number | null {
  return row.entity?.id != null ? (row.workspace?.id ?? selectedWorkspaceId.value) : null;
}

function goToEntity(row: AuditLogEntryDto) {
  const wsId = targetWorkspaceId(row);
  if (!wsId) return;
  router.push({ name: 'workspace-entities', params: { workspaceId: String(wsId) } });
}

function isEmpty(value: unknown): boolean {
  if (value == null) return true;
  if (Array.isArray(value)) return value.length === 0;
  if (typeof value === 'object') return Object.keys(value).length === 0;
  if (typeof value === 'string') return value.length === 0;
  return false;
}

function stringifyJson(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string') return value;
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

const expandedJson = ref<Record<string, { old: boolean; next: boolean }>>({});

function isOpen(rowId: string, slot: 'old' | 'next'): boolean {
  return expandedJson.value[rowId]?.[slot] ?? false;
}

function toggle(rowId: string, slot: 'old' | 'next') {
  const cur = expandedJson.value[rowId] ?? { old: false, next: false };
  expandedJson.value = {
    ...expandedJson.value,
    [rowId]: { ...cur, [slot]: !cur[slot] },
  };
}
</script>

<template>
  <section class="w-full">
    <header class="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold text-ink-900">{{ t('audit.title') }}</h1>
        <p class="mt-1.5 text-sm text-ink-500">
          {{ t('audit.changeHistoryFor') }}
          <span v-if="showWorkspacePicker && selectedWorkspaceId" class="font-semibold text-brand-600">
            {{ workspaceOptions.find((w) => w.value === selectedWorkspaceId)?.label }}
          </span>
          <span v-else class="font-semibold text-brand-600">
            {{ orgStore.currentOrg?.name ?? t('audit.thisAccount') }}
          </span>
        </p>
      </div>
      <button
        class="btn btn-outline btn-sm"
        :disabled="auditStore.loading"
        :aria-label="t('audit.refresh')"
        @click="load"
      >
        <i :class="auditStore.loading ? 'pi pi-spin pi-spinner' : 'pi pi-refresh'" />
        {{ t('audit.refresh') }}
      </button>
    </header>

    <template v-if="initializing">
      <LoadingSkeleton variant="table" :rows="6" :label="t('audit.title')" />
    </template>

    <template v-else-if="canViewPage">
      <div class="mb-4 flex flex-wrap items-end gap-3 border border-line bg-white p-4">
        <FloatLabel variant="on">
          <Select
            input-id="auditScope"
            v-model="scope"
            :options="scopeOptions"
            option-label="label"
            option-value="value"
            class="!h-10 min-w-[170px]"
          />
          <label for="auditScope">{{ t('audit.scope') }}</label>
        </FloatLabel>

        <FloatLabel v-if="showWorkspacePicker" variant="on">
          <Select
            input-id="auditWorkspace"
            v-model="selectedWorkspaceId"
            :options="workspaceOptions"
            option-label="label"
            option-value="value"
            filter
            class="!h-10 min-w-[220px]"
          />
          <label for="auditWorkspace">{{ t('audit.workspaceLabel') }}</label>
        </FloatLabel>

        <FloatLabel variant="on">
          <DatePicker
            input-id="auditDate"
            v-model="dateRange"
            selection-mode="range"
            :manual-input="false"
            show-button-bar
            class="!h-10 min-w-[260px]"
          />
          <label for="auditDate">{{ t('audit.dateRange') }}</label>
        </FloatLabel>

        <FloatLabel variant="on">
          <InputText
            id="auditAction"
            v-model="actionFilter"
            class="!h-10 min-w-[200px]"
            @keydown.enter="applyFilters"
          />
          <label for="auditAction">{{ t('audit.action') }}</label>
        </FloatLabel>

        <div class="ml-auto flex gap-2">
          <button class="btn btn-outline btn-sm" :disabled="auditStore.loading" @click="resetFilters">
            {{ t('audit.reset') }}
          </button>
          <button class="btn btn-primary btn-sm" :disabled="auditStore.loading" @click="applyFilters">
            <i :class="auditStore.loading ? 'pi pi-spin pi-spinner' : 'pi pi-filter'" />
            {{ t('audit.apply') }}
          </button>
        </div>
      </div>

      <Message v-if="errorMessage" severity="error" :closable="false" class="!my-0 mb-4">
        {{ errorMessage }}
      </Message>

      <div class="border border-line bg-white">
        <DataTable
          :value="auditStore.rows"
          :loading="auditStore.loading"
          lazy
          paginator
          :rows="pageSize"
          :total-records="auditStore.total"
          :first="(pageIndex - 1) * pageSize"
          :rows-per-page-options="pageSizeOptions"
          data-key="id"
          striped-rows
          responsive-layout="scroll"
          :aria-label="t('audit.title')"
          @page="onPage"
        >
          <template #empty>
            <div class="py-12 text-center text-ink-500">
              <i class="pi pi-inbox mb-2 block text-3xl text-ink-300" />
              {{ t('audit.empty') }}
            </div>
          </template>

          <Column field="changedAt" :header="t('audit.colDate')" style="width: 180px">
            <template #body="{ data }">
              <span class="whitespace-nowrap text-ink-700">{{ formatDate(data.changedAt) }}</span>
            </template>
          </Column>

          <Column field="entity_type" :header="t('audit.colType')" style="width: 150px">
            <template #body="{ data }">
              <span :class="scopeBadgeFullClass(data.entity_type)">
                {{ typeLabel(data.entity_type) }}
              </span>
            </template>
          </Column>

          <Column field="action" :header="t('audit.colAction')" style="width: 220px">
            <template #body="{ data }">
              <span class="text-xs text-ink-700">{{ humanizeAction(data.action) }}</span>
              <div v-if="data.fieldName" class="mt-0.5 text-[11px] text-ink-400">
                {{ humanizeField(data.fieldName) }}
              </div>
            </template>
          </Column>

          <Column field="actor" :header="t('audit.colAuthor')" style="width: 220px">
            <template #body="{ data }">
              <div v-if="data.actor" class="leading-tight">
                <div class="truncate text-sm text-ink-800">{{ actorEmail(data) }}</div>
                <div
                  v-if="data.actor.firstName || data.actor.lastName"
                  class="text-[11px] text-ink-400"
                >
                  {{ data.actor.firstName }} {{ data.actor.lastName }}
                </div>
              </div>
              <span v-else class="text-ink-400">—</span>
            </template>
          </Column>

          <Column :header="t('audit.colTarget')" style="width: 180px">
            <template #body="{ data }">
              <button
                v-if="entityIdOf(data) && targetWorkspaceId(data)"
                class="text-sm text-brand-600 hover:underline"
                @click="goToEntity(data)"
              >
                {{ t('audit.targetEntity', { id: entityIdOf(data) }) }}
                <i class="pi pi-arrow-right ml-1 text-[10px]" />
              </button>
              <span v-else-if="data.workspace?.name" class="text-sm text-ink-700">
                {{ t('audit.targetWorkspace', { name: data.workspace.name }) }}
              </span>
              <span v-else-if="data.organization?.name" class="text-sm text-ink-700">
                {{ t('audit.targetOrganization', { name: data.organization.name }) }}
              </span>
              <span v-else-if="data.targetUser?.email" class="text-sm text-ink-700">
                {{ t('audit.targetUser', { email: data.targetUser.email }) }}
              </span>
              <span v-else class="text-ink-400">—</span>
            </template>
          </Column>

          <Column :header="t('audit.colOldNew')">
            <template #body="{ data }">
              <div class="flex flex-col gap-1.5">
                <div>
                  <button
                    type="button"
                    class="inline-flex items-center gap-1 text-xs text-ink-500 hover:text-ink-800"
                    :disabled="isEmpty(data.oldValue)"
                    :aria-expanded="isOpen(data.id, 'old')"
                    @click="toggle(data.id, 'old')"
                  >
                    <i
                      :class="[
                        'pi text-[10px]',
                        isOpen(data.id, 'old') ? 'pi-chevron-down' : 'pi-chevron-right',
                      ]"
                    />
                    <span class="font-medium">{{ t('audit.old') }}</span>
                    <span v-if="isEmpty(data.oldValue)" class="text-ink-400">—</span>
                  </button>
                  <pre
                    v-if="isOpen(data.id, 'old') && !isEmpty(data.oldValue)"
                    class="mt-1 max-h-60 overflow-auto whitespace-pre-wrap break-all bg-surface p-2 text-[11px] text-ink-700"
                  >{{ stringifyJson(data.oldValue) }}</pre>
                </div>
                <div>
                  <button
                    type="button"
                    class="inline-flex items-center gap-1 text-xs text-ink-500 hover:text-ink-800"
                    :disabled="isEmpty(data.newValue)"
                    :aria-expanded="isOpen(data.id, 'next')"
                    @click="toggle(data.id, 'next')"
                  >
                    <i
                      :class="[
                        'pi text-[10px]',
                        isOpen(data.id, 'next') ? 'pi-chevron-down' : 'pi-chevron-right',
                      ]"
                    />
                    <span class="font-medium">{{ t('audit.new') }}</span>
                    <span v-if="isEmpty(data.newValue)" class="text-ink-400">—</span>
                  </button>
                  <pre
                    v-if="isOpen(data.id, 'next') && !isEmpty(data.newValue)"
                    class="mt-1 max-h-60 overflow-auto whitespace-pre-wrap break-all bg-surface p-2 text-[11px] text-ink-700"
                  >{{ stringifyJson(data.newValue) }}</pre>
                </div>
              </div>
            </template>
          </Column>
        </DataTable>
      </div>
    </template>

    <div v-else class="mx-auto max-w-3xl border border-line bg-white p-10 text-center">
      <i class="pi pi-lock text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">{{ t('audit.noPermission') }}</p>
    </div>
  </section>
</template>
