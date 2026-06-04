<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import DataTable, {
  type DataTablePageEvent,
} from 'primevue/datatable';
import Column from 'primevue/column';
import DatePicker from 'primevue/datepicker';
import Select from 'primevue/select';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import Message from 'primevue/message';

import { useWorkspaceStore } from '@/stores/workspace';
import { useOrganizationStore } from '@/stores/organization';
import { useAuditStore } from '@/stores/audit';
import {
  type AuditLogEntryDto,
  type AuditLogQuery,
  type AuditScope,
} from '@/api/audit';
import { ApiError } from '@/api/http';
import { scopeDisplayName, scopeBadgeFullClass } from '@/utils/auditBadge';

const { t } = useI18n();
const router = useRouter();
const wsStore = useWorkspaceStore();
const orgStore = useOrganizationStore();
const auditStore = useAuditStore();


const canViewWorkspaceScope = computed(
  () => (wsStore.currentWorkspace?.myPermissions ?? []).includes('view_analytics'),
);
const canViewOrgScope = computed(
  () => (orgStore.currentOrg?.myPermissions ?? []).includes('manage_org_settings'),
);
const canViewPage = computed(
  () => canViewWorkspaceScope.value || canViewOrgScope.value,
);


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
const dateRange = ref<Date[] | null>(null);
const actionFilter = ref<string>('');

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
  const wsId = wsStore.currentWorkspaceId;
  const orgId = orgStore.currentOrgId;

  const q: AuditLogQuery = {
    entityType: scope.value,
    index: pageIndex.value,
    pageSize: pageSize.value,
  };

  if (scope.value === 'entity' || scope.value === 'workspace') {
    if (!wsId) return null;
    q.workspaceId = wsId;
  } else if (scope.value === 'organization') {
    if (!orgId) return null;
    q.organizationId = orgId;
  } else if (scope.value === 'user') {
    if (wsId) q.workspaceId = wsId;
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
    errorMessage.value =
      err instanceof ApiError ? err.message : t('audit.loadError');
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
  pageIndex.value = 1;
  load();
});

onMounted(load);


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
    .map((w, i) => i === 0 ? w.charAt(0).toUpperCase() + w.slice(1) : w)
    .join(' ');
}

function humanizeField(field: string): string {
  if (!field) return '';
  return field
    .split('_')
    .filter(Boolean)
    .map(w => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

function entityIdOf(row: AuditLogEntryDto): number | null {
  return row.entity?.id ?? null;
}

function targetWorkspaceId(row: AuditLogEntryDto): number | null {
  return row.entity?.id != null
    ? row.workspace?.id ?? wsStore.currentWorkspaceId
    : null;
}

function goToEntity(row: AuditLogEntryDto) {
  const wsId = targetWorkspaceId(row);
  if (!wsId) return;
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(wsId) },
  });
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
  <section v-if="canViewPage" class="max-w-6xl">
    <div class="flex items-center justify-between mb-6 gap-4">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold text-ink-900">{{ t('audit.title') }}</h1>
        <p class="mt-3 text-sm text-ink-500">
          {{ t('audit.changeHistoryFor') }}
          <span v-if="wsStore.currentWorkspace" class="font-semibold text-brand-600">
            {{ wsStore.currentWorkspace.name }}
          </span>
          <span v-else class="font-semibold text-brand-600">
            {{ orgStore.currentOrg?.name ?? t('audit.thisAccount') }}
          </span>
        </p>
      </div>
      <Button
        icon="pi pi-refresh"
        :label="t('audit.refresh')"
        severity="secondary"
        :loading="auditStore.loading"
        @click="load"
      />
    </div>

    
    <div
      class="rounded-xl border border-line bg-white p-4 mb-4 flex flex-wrap items-end gap-3"
    >
      <div class="flex flex-col gap-1">
        <label class="text-xs font-medium text-ink-600">{{ t('audit.scope') }}</label>
        <Select
          v-model="scope"
          :options="scopeOptions"
          option-label="label"
          option-value="value"
          class="!h-10 min-w-[160px]"
        />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-xs font-medium text-ink-600">{{ t('audit.dateRange') }}</label>
        <DatePicker
          v-model="dateRange"
          selection-mode="range"
          :manual-input="false"
          show-button-bar
          :placeholder="t('audit.dateRangePlaceholder')"
          class="!h-10 min-w-[260px]"
        />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-xs font-medium text-ink-600">{{ t('audit.action') }}</label>
        <InputText
          v-model="actionFilter"
          :placeholder="t('audit.actionPlaceholder')"
          class="!h-10 min-w-[200px]"
          @keydown.enter="applyFilters"
        />
      </div>

      <div class="flex gap-2 ml-auto">
        <Button
          :label="t('audit.reset')"
          severity="secondary"
          text
          :disabled="auditStore.loading"
          @click="resetFilters"
        />
        <Button
          :label="t('audit.apply')"
          icon="pi pi-filter"
          :loading="auditStore.loading"
          @click="applyFilters"
        />
      </div>
    </div>

    <Message
      v-if="errorMessage"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ errorMessage }}
    </Message>

    
    <div class="rounded-xl border border-line bg-white overflow-hidden">
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
        @page="onPage"
      >
        <template #empty>
          <div class="text-center py-10 text-ink-500">
            {{ t('audit.empty') }}
          </div>
        </template>

        <Column field="changedAt" :header="t('audit.colDate')" style="width: 180px">
          <template #body="{ data }">
            <span class="text-ink-700 whitespace-nowrap">
              {{ formatDate(data.changedAt) }}
            </span>
          </template>
        </Column>

        <Column field="entity_type" :header="t('audit.colType')" style="width: 150px">
          <template #body="{ data }">
            <span :class="scopeBadgeFullClass(data.entity_type)">
              {{ scopeDisplayName(data.entity_type) }}
            </span>
          </template>
        </Column>

        <Column field="action" :header="t('audit.colAction')" style="width: 220px">
          <template #body="{ data }">
            <span class="text-xs text-ink-700">{{ humanizeAction(data.action) }}</span>
            <div v-if="data.fieldName" class="text-[11px] text-ink-400 mt-0.5">
              {{ humanizeField(data.fieldName) }}
            </div>
          </template>
        </Column>

        <Column field="actor" :header="t('audit.colAuthor')" style="width: 220px">
          <template #body="{ data }">
            <div v-if="data.actor" class="leading-tight">
              <div class="text-sm text-ink-800 truncate">
                {{ actorEmail(data) }}
              </div>
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
              class="text-brand-600 hover:underline text-sm"
              @click="goToEntity(data)"
            >
              {{ t('audit.targetEntity', { id: entityIdOf(data) }) }}
              <i class="pi pi-arrow-right text-[10px] ml-1" />
            </button>
            <span
              v-else-if="data.workspace?.name"
              class="text-sm text-ink-700"
            >
              {{ t('audit.targetWorkspace', { name: data.workspace.name }) }}
            </span>
            <span
              v-else-if="data.organization?.name"
              class="text-sm text-ink-700"
            >
              {{ t('audit.targetOrganization', { name: data.organization.name }) }}
            </span>
            <span
              v-else-if="data.targetUser?.email"
              class="text-sm text-ink-700"
            >
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
                  class="text-xs text-ink-500 hover:text-ink-800 inline-flex items-center gap-1"
                  :disabled="isEmpty(data.oldValue)"
                  @click="toggle(data.id, 'old')"
                >
                  <i
                    :class="[
                      'pi text-[10px]',
                      isOpen(data.id, 'old')
                        ? 'pi-chevron-down'
                        : 'pi-chevron-right',
                    ]"
                  />
                  <span class="font-medium">{{ t('audit.old') }}</span>
                  <span v-if="isEmpty(data.oldValue)" class="text-ink-400">—</span>
                </button>
                <pre
                  v-if="isOpen(data.id, 'old') && !isEmpty(data.oldValue)"
                  class="mt-1 text-[11px] bg-surface text-ink-700 rounded p-2 max-h-60 overflow-auto whitespace-pre-wrap break-all"
                >{{ stringifyJson(data.oldValue) }}</pre>
              </div>
              <div>
                <button
                  type="button"
                  class="text-xs text-ink-500 hover:text-ink-800 inline-flex items-center gap-1"
                  :disabled="isEmpty(data.newValue)"
                  @click="toggle(data.id, 'next')"
                >
                  <i
                    :class="[
                      'pi text-[10px]',
                      isOpen(data.id, 'next')
                        ? 'pi-chevron-down'
                        : 'pi-chevron-right',
                    ]"
                  />
                  <span class="font-medium">{{ t('audit.new') }}</span>
                  <span v-if="isEmpty(data.newValue)" class="text-ink-400">—</span>
                </button>
                <pre
                  v-if="isOpen(data.id, 'next') && !isEmpty(data.newValue)"
                  class="mt-1 text-[11px] bg-surface text-ink-700 rounded p-2 max-h-60 overflow-auto whitespace-pre-wrap break-all"
                >{{ stringifyJson(data.newValue) }}</pre>
              </div>
            </div>
          </template>
        </Column>
      </DataTable>
    </div>
  </section>

  <section v-else class="max-w-3xl">
    <div class="rounded-xl border border-line bg-white p-10 text-center">
      <i class="pi pi-lock text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        {{ t('audit.noPermission') }}
      </p>
    </div>
  </section>
</template>
