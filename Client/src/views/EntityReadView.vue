<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import DatePicker from 'primevue/datepicker';
import ToggleSwitch from 'primevue/toggleswitch';
import ConfirmDialog from 'primevue/confirmdialog';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { hasWorkspacePermission } from '@/utils/workspacePermissions';
import type {
  EntityDetailDto,
  EntityPropertyValueDto,
} from '@/api/entities';
import { mlApi, type DealScoreDto } from '@/api/ml';

const props = defineProps<{
  workspaceId: number;
  entityId: number;
}>();

const emit = defineEmits<{
  close: [];
  updated: [];
}>();

const route = useRoute();
const router = useRouter();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const confirm = useConfirm();
const toast = useToast();

const loading = ref(true);
const saving = ref(false);
const errorMessage = ref<string | null>(null);
const detail = ref<EntityDetailDto | null>(null);
const editMode = ref(false);
const fieldErrors = ref<FieldErrors>({});

const score = ref<DealScoreDto | null>(null);
const scoreLoading = ref(false);
const scoreError = ref<string | null>(null);

type FieldValue = string | number | boolean | Date | null;
const editValues = ref<Record<number, FieldValue>>({});

const canEdit = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'edit_entities'),
);
const canDelete = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'delete_entities'),
);

const writableProps = computed(() => {
  const d = detail.value;
  if (!d) return [];
  return d.propertyValues.filter((p) => !p.isReadonly);
});

/** `overview`, or `out-{relationshipTypeId}` / `in-{relationshipTypeId}` (direction distinguishes same id). */
const activeTab = ref<string>('overview');

const typeSchema = computed(() => {
  const d = detail.value;
  if (!d) return null;
  return entityStore.types.find((t) => t.id === d.entityTypeId) ?? null;
});

type EdgeRelTab = {
  direction: 'out' | 'in';
  relationshipTypeId: number;
  name: string;
  /** Outbound: target type name. Inbound: source type name (records pointing at this entity). */
  otherEntityTypeName: string;
};

function relTabKey(tab: Pick<EdgeRelTab, 'direction' | 'relationshipTypeId'>): string {
  return tab.direction === 'out'
    ? `out-${tab.relationshipTypeId}`
    : `in-${tab.relationshipTypeId}`;
}

const outboundRelTabs = computed((): EdgeRelTab[] => {
  const schemaRels = typeSchema.value?.outgoingRelationships;
  if (schemaRels?.length) {
    return [...schemaRels]
      .map((r) => ({
        direction: 'out' as const,
        relationshipTypeId: r.relationshipTypeId,
        name: r.name,
        otherEntityTypeName: r.targetEntityTypeName,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  const d = detail.value;
  if (!d?.outboundRelationships.length) return [];

  const byId = new Map<number, EdgeRelTab>();
  for (const r of d.outboundRelationships) {
    if (byId.has(r.relationshipTypeId)) continue;
    byId.set(r.relationshipTypeId, {
      direction: 'out',
      relationshipTypeId: r.relationshipTypeId,
      name: r.relationshipName,
      otherEntityTypeName: r.relatedEntityTypeName,
    });
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
});

/**
 * Hide an inbound relationship tab only when the current entity type already has an
 * outgoing relationship type covering the same pair (e.g. on a `deal`, hide the
 * `contract_deal` inbound because `deal_contract` already provides the deal-side
 * outbound view). Solo inbound tabs with no complementary outbound (e.g. `deal_client`
 * viewed from a client) stay visible — without them, "deals from this client" would
 * disappear entirely.
 */
const outboundTargetTypeIds = computed<Set<number>>(() => {
  const schemaOut = typeSchema.value?.outgoingRelationships;
  if (schemaOut?.length) {
    return new Set(schemaOut.map((r) => r.targetEntityTypeId));
  }
  return new Set();
});

const outboundTargetTypeNames = computed<Set<string>>(() => {
  const schemaOut = typeSchema.value?.outgoingRelationships;
  if (schemaOut?.length) {
    return new Set(schemaOut.map((r) => r.targetEntityTypeName));
  }
  const d = detail.value;
  if (!d?.outboundRelationships.length) return new Set();
  return new Set(d.outboundRelationships.map((r) => r.relatedEntityTypeName));
});

const inboundRelTabs = computed((): EdgeRelTab[] => {
  const schemaRels = typeSchema.value?.incomingRelationships;
  if (schemaRels?.length) {
    const coveredTargets = outboundTargetTypeIds.value;
    return [...schemaRels]
      .filter((r) => !coveredTargets.has(r.sourceEntityTypeId))
      .map((r) => ({
        direction: 'in' as const,
        relationshipTypeId: r.relationshipTypeId,
        name: r.name,
        otherEntityTypeName: r.sourceEntityTypeName,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  const d = detail.value;
  if (!d?.inboundRelationships.length) return [];

  const coveredNames = outboundTargetTypeNames.value;
  const byId = new Map<number, EdgeRelTab>();
  for (const r of d.inboundRelationships) {
    if (byId.has(r.relationshipTypeId)) continue;
    if (coveredNames.has(r.relatedEntityTypeName)) continue;
    byId.set(r.relationshipTypeId, {
      direction: 'in',
      relationshipTypeId: r.relationshipTypeId,
      name: r.relationshipName,
      otherEntityTypeName: r.relatedEntityTypeName,
    });
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
});

const hasRelationshipTabs = computed(
  () =>
    outboundRelTabs.value.length > 0 || inboundRelTabs.value.length > 0,
);

function outboundLinksFor(relationshipTypeId: number) {
  return (
    detail.value?.outboundRelationships.filter(
      (r) => r.relationshipTypeId === relationshipTypeId,
    ) ?? []
  );
}

function inboundLinksFor(relationshipTypeId: number) {
  return (
    detail.value?.inboundRelationships.filter(
      (r) => r.relationshipTypeId === relationshipTypeId,
    ) ?? []
  );
}

function humanize(name: string): string {
  return name.replace(/_/g, ' ').replace(/^./, (c) => c.toUpperCase());
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function formatDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function parseDetailToEditValues(d: EntityDetailDto) {
  const next: Record<number, FieldValue> = {};
  for (const p of d.propertyValues) {
    if (p.isReadonly) continue;
    switch (p.dataType) {
      case 'Bool':
        next[p.propertyId] = Boolean(p.value);
        break;
      case 'Int':
      case 'Decimal':
        next[p.propertyId] =
          p.value === null || p.value === undefined
            ? null
            : Number(p.value);
        break;
      case 'Date':
        next[p.propertyId] =
          typeof p.value === 'string' && p.value
            ? new Date(`${p.value}T12:00:00`)
            : null;
        break;
      default:
        next[p.propertyId] =
          p.value === null || p.value === undefined ? '' : String(p.value);
    }
  }
  editValues.value = next;
}

function isEmpty(v: FieldValue): boolean {
  if (v === null || v === undefined) return true;
  if (typeof v === 'string' && v.trim() === '') return true;
  return false;
}

function serializeValue(
  prop: EntityPropertyValueDto,
  raw: FieldValue,
): string | null {
  if (isEmpty(raw)) return null;
  switch (prop.dataType) {
    case 'Date':
      return raw instanceof Date ? formatDate(raw) : String(raw);
    case 'Bool':
      return raw ? 'true' : 'false';
    case 'Decimal':
    case 'Int':
      return Number(raw).toString();
    case 'String':
    default:
      return String(raw).trim();
  }
}

function formatDisplayValue(p: EntityPropertyValueDto): string {
  if (p.value === null || p.value === undefined) return '—';
  if (typeof p.value === 'boolean') return p.value ? 'Yes' : 'No';
  return String(p.value);
}

function previewLabel(pv: EntityPropertyValueDto[]): string {
  const first = pv[0];
  if (!first) return '';
  return `${humanize(first.propertyName)}: ${formatDisplayValue(first)}`;
}

function goToEntity(typeName: string, id: number) {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(props.workspaceId) },
    query: { ...route.query, entityType: typeName, id: String(id) },
  });
}

function exitDetail() {
  const q = { ...route.query } as Record<string, unknown>;
  delete q.id;
  delete q.action;
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(props.workspaceId) },
    query: q as Record<string, string>,
  });
  emit('close');
}

const isDeal = computed(() => detail.value?.entityTypeName === 'deal');

async function loadDetail() {
  loading.value = true;
  errorMessage.value = null;
  activeTab.value = 'overview';
  score.value = null;
  scoreError.value = null;
  scoreLoading.value = false;
  try {
    await entityStore.fetchTypes();
    const d = await entityStore.fetchDetail(props.workspaceId, props.entityId);
    detail.value = d;
    parseDetailToEditValues(d);
    editMode.value = false;
  } catch (err) {
    errorMessage.value = normalizeError(err, 'Failed to load entity.').message;
    detail.value = null;
  } finally {
    loading.value = false;
  }

  // Fire-and-forget score fetch for deals so the page paints first.
  if (detail.value && isDeal.value) {
    void loadScore();
  }
}

async function loadScore() {
  const targetEntityId = props.entityId;
  scoreLoading.value = true;
  scoreError.value = null;
  try {
    const results = await mlApi.scoreBatch([targetEntityId]);
    if (props.entityId !== targetEntityId) return;
    score.value = results[0] ?? null;
  } catch (err) {
    if (props.entityId !== targetEntityId) return;
    score.value = null;
    scoreError.value = normalizeError(err, 'Could not load scores.').message;
  } finally {
    if (props.entityId === targetEntityId) {
      scoreLoading.value = false;
    }
  }
}

async function refreshScore() {
  if (!isDeal.value) return;
  await loadScore();
  if (scoreError.value) {
    toast.add({
      severity: 'error',
      summary: 'Score refresh failed',
      detail: scoreError.value,
      life: 4000,
    });
  } else if (score.value && score.value.unavailable_reason === null) {
    toast.add({ severity: 'success', summary: 'Scores refreshed', life: 2000 });
  }
}

function formatScore(value: number | null): string {
  if (value === null || value === undefined) return '—';
  return `${value.toFixed(1)}%`;
}

function cancelEdit() {
  if (detail.value) parseDetailToEditValues(detail.value);
  editMode.value = false;
  fieldErrors.value = {};
}

async function saveEdit() {
  const d = detail.value;
  if (!d) return;
  saving.value = true;
  errorMessage.value = null;
  fieldErrors.value = {};
  const properties = writableProps.value.map((p) => ({
    propertyId: p.propertyId,
    value: serializeValue(p, editValues.value[p.propertyId] ?? null),
  }));
  try {
    const updated = await entityStore.update(
      props.workspaceId,
      props.entityId,
      { properties },
    );
    detail.value = updated;
    parseDetailToEditValues(updated);
    editMode.value = false;
    toast.add({ severity: 'success', summary: 'Saved', life: 2500 });
    emit('updated');
  } catch (err) {
    const normalized = normalizeError(err, 'Failed to save.');
    fieldErrors.value = normalized.fieldErrors;
    errorMessage.value = normalized.message;
  } finally {
    saving.value = false;
  }
}

function fieldError(prop: EntityPropertyValueDto): string | null {
  return (
    firstFieldError(fieldErrors.value, prop.propertyName) ??
    firstFieldError(fieldErrors.value, `properties[${prop.propertyId}]`) ??
    firstFieldError(fieldErrors.value, `properties.${prop.propertyId}`)
  );
}

function requestArchive() {
  confirm.require({
    message:
      'Archive this entity? It will be hidden from lists but history remains.',
    header: 'Archive entity',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Archive', severity: 'danger' },
    accept: async () => {
      try {
        await entityStore.archive(props.workspaceId, props.entityId);
        toast.add({ severity: 'success', summary: 'Entity archived', life: 2500 });
        exitDetail();
      } catch (err) {
        toast.add({
          severity: 'error',
          summary: 'Archive failed',
          detail: normalizeError(err, 'Could not archive.').message,
          life: 5000,
        });
      }
    },
  });
}

watch(
  () => [props.workspaceId, props.entityId] as const,
  () => loadDetail(),
  { immediate: true },
);
</script>

<template>
  <ConfirmDialog />
  <section class="max-w-3xl">
    <div class="flex items-start justify-between gap-4 mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          label="Back to list"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="exitDetail"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          <template v-if="detail">
            {{ detail.entityTypeName.replace(/_/g, ' ') }} · #{{ detail.id }}
          </template>
          <template v-else>Entity</template>
        </h1>
        <p v-if="detail" class="mt-1 text-sm text-ink-500">
          Workspace record details and relationships.
        </p>
      </div>
      <div v-if="detail && !detail.isArchived" class="flex flex-wrap gap-2 shrink-0">
        <Button
          v-if="canEdit && !editMode"
          label="Edit"
          icon="pi pi-pencil"
          severity="secondary"
          outlined
          @click="editMode = true"
        />
        <template v-if="canEdit && editMode">
          <Button
            label="Cancel"
            severity="secondary"
            text
            :disabled="saving"
            @click="cancelEdit"
          />
          <Button
            label="Save"
            icon="pi pi-check"
            :loading="saving"
            :disabled="saving"
            @click="saveEdit"
          />
        </template>
        <Button
          v-if="canDelete && !editMode"
          label="Archive"
          icon="pi pi-trash"
          severity="danger"
          outlined
          @click="requestArchive"
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

    <Message
      v-else-if="detail?.isArchived"
      severity="warn"
      :closable="false"
      class="!my-0 mb-4"
    >
      This entity is archived (read-only in lists).
    </Message>

    <div v-if="loading" class="text-center py-12 text-ink-500">Loading...</div>

    <template v-else-if="detail">
      <nav
        v-if="hasRelationshipTabs"
        class="flex flex-wrap gap-2 mb-4"
        aria-label="Record sections"
      >
        <button
          type="button"
          class="rounded-full px-4 py-1.5 text-sm font-medium transition-colors border"
          :class="
            activeTab === 'overview'
              ? 'border-brand-600 bg-brand-50 text-brand-800'
              : 'border-line bg-white text-ink-600 hover:bg-surface/80'
          "
          @click="activeTab = 'overview'"
        >
          Overview
        </button>
        <button
          v-for="tab in outboundRelTabs"
          :key="relTabKey(tab)"
          type="button"
          class="rounded-full px-4 py-1.5 text-sm font-medium transition-colors border"
          :class="
            activeTab === relTabKey(tab)
              ? 'border-brand-600 bg-brand-50 text-brand-800'
              : 'border-line bg-white text-ink-600 hover:bg-surface/80'
          "
          @click="activeTab = relTabKey(tab)"
        >
          {{ humanize(tab.name) }}
          <span class="text-ink-400 font-normal">
            → {{ tab.otherEntityTypeName.replace(/_/g, ' ') }}
          </span>
        </button>
        <button
          v-for="tab in inboundRelTabs"
          :key="relTabKey(tab)"
          type="button"
          class="rounded-full px-4 py-1.5 text-sm font-medium transition-colors border"
          :class="
            activeTab === relTabKey(tab)
              ? 'border-brand-600 bg-brand-50 text-brand-800'
              : 'border-line bg-white text-ink-600 hover:bg-surface/80'
          "
          @click="activeTab = relTabKey(tab)"
        >
          {{ humanize(tab.name) }}
          <span class="text-ink-400 font-normal">
            ← {{ tab.otherEntityTypeName.replace(/_/g, ' ') }}
          </span>
        </button>
      </nav>

      <div
        v-if="isDeal"
        v-show="activeTab === 'overview'"
        class="rounded-xl border border-line bg-white p-6 mb-6"
      >
        <div class="flex items-start justify-between gap-3 mb-4">
          <div>
            <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide">
              Scores
            </h2>
            <p class="mt-1 text-xs text-ink-500">
              Closure and churn likelihood, computed by the ML service from this deal's analysis.
            </p>
          </div>
          <Button
            icon="pi pi-refresh"
            label="Refresh data"
            severity="secondary"
            outlined
            size="small"
            :loading="scoreLoading"
            :disabled="scoreLoading"
            @click="refreshScore"
          />
        </div>

        <div v-if="scoreLoading && !score" class="flex items-center gap-2 text-sm text-ink-500">
          <i class="pi pi-spin pi-spinner" />
          <span>Loading scores…</span>
        </div>

        <Message
          v-else-if="scoreError"
          severity="warn"
          :closable="false"
          class="!my-0"
        >
          {{ scoreError }}
        </Message>

        <Message
          v-else-if="score && score.unavailable_reason"
          severity="info"
          :closable="false"
          class="!my-0"
        >
          {{ score.unavailable_reason }}
        </Message>

        <div v-else-if="score" class="grid gap-4 sm:grid-cols-2">
          <div class="rounded-lg border border-line bg-surface/40 p-4">
            <div class="text-xs font-medium text-ink-500 uppercase tracking-wide">
              Closure score
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.closure_score) }}
            </div>
          </div>
          <div class="rounded-lg border border-line bg-surface/40 p-4">
            <div class="text-xs font-medium text-ink-500 uppercase tracking-wide">
              Churn score
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.churn_score) }}
            </div>
          </div>
        </div>

        <p v-else class="text-sm text-ink-500">
          Scores have not been requested yet.
        </p>
      </div>

      <div v-show="activeTab === 'overview'" class="rounded-xl border border-line bg-white p-6 mb-6">
        <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-4">
          Properties
        </h2>
        <dl class="grid gap-4 sm:grid-cols-2">
          <template v-for="p in detail.propertyValues" :key="p.propertyId">
            <div class="sm:col-span-2 border-b border-line pb-4 last:border-0 last:pb-0">
              <dt class="text-xs font-medium text-ink-500 uppercase tracking-wide">
                {{ humanize(p.propertyName) }}
                <span
                  v-if="p.isReadonly"
                  class="ml-2 normal-case text-ink-400 font-normal"
                >(read-only)</span>
              </dt>
              <dd class="mt-1 text-sm text-ink-900">
                <template v-if="editMode && !p.isReadonly">
                  <InputText
                    v-if="p.dataType === 'String'"
                    v-model="editValues[p.propertyId] as string"
                    class="w-full !h-10"
                    :invalid="!!fieldError(p)"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Int'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :min-fraction-digits="0"
                    :max-fraction-digits="0"
                    :invalid="!!fieldError(p)"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Decimal'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :min-fraction-digits="0"
                    :max-fraction-digits="4"
                    :invalid="!!fieldError(p)"
                  />
                  <DatePicker
                    v-else-if="p.dataType === 'Date'"
                    v-model="editValues[p.propertyId] as Date | null"
                    date-format="yy-mm-dd"
                    show-icon
                    class="w-full"
                    :invalid="!!fieldError(p)"
                  />
                  <ToggleSwitch
                    v-else-if="p.dataType === 'Bool'"
                    v-model="editValues[p.propertyId] as boolean"
                  />
                  <small v-if="fieldError(p)" class="text-xs text-danger block mt-1">
                    {{ fieldError(p) }}
                  </small>
                </template>
                <template v-else>
                  {{ formatDisplayValue(p) }}
                </template>
              </dd>
            </div>
          </template>
        </dl>
      </div>

      <template v-for="tab in outboundRelTabs" :key="`panel-${relTabKey(tab)}`">
        <div
          v-show="activeTab === relTabKey(tab)"
          class="rounded-xl border border-line bg-white p-6 mb-6"
        >
          <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-1">
            {{ humanize(tab.name) }}
          </h2>
          <p class="text-xs text-ink-500 mb-4">
            Linked {{ tab.otherEntityTypeName.replace(/_/g, ' ') }} records (outgoing).
          </p>
          <ul v-if="outboundLinksFor(tab.relationshipTypeId).length" class="space-y-2 text-sm">
            <li
              v-for="r in outboundLinksFor(tab.relationshipTypeId)"
              :key="`o-${r.relationshipTypeId}-${r.relatedEntityId}`"
            >
              <button
                type="button"
                class="text-left w-full rounded-lg border border-line px-3 py-2 hover:bg-surface/80 transition-colors"
                @click="goToEntity(r.relatedEntityTypeName, r.relatedEntityId)"
              >
                <span class="text-brand-700">{{ r.relatedEntityTypeName.replace(/_/g, ' ') }}</span>
                <span class="font-mono text-xs text-ink-600"> #{{ r.relatedEntityId }}</span>
                <span v-if="r.previewPropertyValues.length" class="block text-xs text-ink-500 mt-1">
                  {{ previewLabel(r.previewPropertyValues) }}
                </span>
              </button>
            </li>
          </ul>
          <p v-else class="text-sm text-ink-500">No links of this type yet.</p>
        </div>
      </template>

      <template v-for="tab in inboundRelTabs" :key="`panel-${relTabKey(tab)}`">
        <div
          v-show="activeTab === relTabKey(tab)"
          class="rounded-xl border border-line bg-white p-6 mb-6"
        >
          <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-1">
            {{ humanize(tab.name) }}
          </h2>
          <p class="text-xs text-ink-500 mb-4">
            {{ tab.otherEntityTypeName.replace(/_/g, ' ') }}
            records pointing here (incoming).
          </p>
          <ul v-if="inboundLinksFor(tab.relationshipTypeId).length" class="space-y-2 text-sm">
            <li
              v-for="r in inboundLinksFor(tab.relationshipTypeId)"
              :key="`i-${r.relationshipTypeId}-${r.relatedEntityId}`"
            >
              <button
                type="button"
                class="text-left w-full rounded-lg border border-line px-3 py-2 hover:bg-surface/80 transition-colors"
                @click="goToEntity(r.relatedEntityTypeName, r.relatedEntityId)"
              >
                <span class="text-brand-700">{{ r.relatedEntityTypeName.replace(/_/g, ' ') }}</span>
                <span class="font-mono text-xs text-ink-600"> #{{ r.relatedEntityId }}</span>
                <span v-if="r.previewPropertyValues.length" class="block text-xs text-ink-500 mt-1">
                  {{ previewLabel(r.previewPropertyValues) }}
                </span>
              </button>
            </li>
          </ul>
          <p v-else class="text-sm text-ink-500">No links of this type yet.</p>
        </div>
      </template>
    </template>
  </section>
</template>
