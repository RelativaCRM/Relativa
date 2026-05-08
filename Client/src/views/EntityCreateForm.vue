<script setup lang="ts">
import { ref, computed, onMounted, watch, reactive, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import Select from 'primevue/select';
import DatePicker from 'primevue/datepicker';
import ToggleSwitch from 'primevue/toggleswitch';
import Message from 'primevue/message';
import {
  normalizeError,
  firstFieldError,
  type FieldErrors,
} from '@/api/errors';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  type EntityTypeDto,
  type EntityTypePropertyDto,
  type EntityListItemDto,
  entityApi,
} from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';
import { hasWorkspacePermission } from '@/utils/workspacePermissions';

type FieldValue = string | number | boolean | Date | null;

const route = useRoute();
const router = useRouter();
const toast = useToast();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.workspaceId));

const types = computed<EntityTypeDto[]>(() => entityStore.types);
/** Types the user is allowed to create manually (excludes e.g. deal_analysis with all-readonly fields). */
const creatableTypes = computed(() =>
  types.value.filter((t) => !isEntityTypeUiLocked(t)),
);
const selectedTypeId = ref<number | null>(null);
const values = ref<Record<number, FieldValue>>({});
const loadingTypes = ref(true);
const submitting = ref(false);
const errorMessage = ref<string | null>(null);
const fieldErrors = ref<FieldErrors>({});
const accessDenied = ref(false);
const submitAttempted = ref(false);

function isPropertyEmpty(prop: EntityTypePropertyDto): boolean {
  if (prop.dataType === 'Bool') return false;
  return isEmpty(values.value[prop.propertyId] ?? null);
}

function isPropertyRequired(prop: EntityTypePropertyDto): boolean {
  return prop.isRequired || prop.dataType !== 'Bool';
}

function propertyFieldError(prop: EntityTypePropertyDto): string | null {
  if (submitAttempted.value && isPropertyEmpty(prop)) {
    return 'This field is required.';
  }
  return (
    firstFieldError(fieldErrors.value, prop.name) ??
    firstFieldError(fieldErrors.value, `properties[${prop.propertyId}]`) ??
    firstFieldError(fieldErrors.value, `properties.${prop.propertyId}`)
  );
}

function clearPropertyFieldError(prop: EntityTypePropertyDto) {
  if (
    !fieldErrors.value[prop.name] &&
    !fieldErrors.value[`properties[${prop.propertyId}]`] &&
    !fieldErrors.value[`properties.${prop.propertyId}`]
  ) {
    return;
  }
  const next = { ...fieldErrors.value };
  delete next[prop.name];
  delete next[`properties[${prop.propertyId}]`];
  delete next[`properties.${prop.propertyId}`];
  fieldErrors.value = next;
}

const selectedType = computed(
  () =>
    creatableTypes.value.find((t) => t.id === selectedTypeId.value) ?? null,
);

const properties = computed<EntityTypePropertyDto[]>(
  () => selectedType.value?.properties ?? [],
);

const requiredOutgoing = computed(
  () => selectedType.value?.outgoingRelationships.filter((r) => r.isRequired) ?? [],
);

const linkPick = reactive<Record<number, number | null>>({});
const candidatesByRel = ref<Record<number, EntityListItemDto[]>>({});
const orchestrateViaGraph = ref(false);

function entityOptionLabel(e: EntityListItemDto): string {
  const bits = e.propertyValues
    .slice(0, 2)
    .map((p) => `${p.propertyName}=${p.value ?? '—'}`);
  return `#${e.id}${bits.length ? ` · ${bits.join(', ')}` : ''}`;
}

function linkSelectOptions(relationshipTypeId: number) {
  return (candidatesByRel.value[relationshipTypeId] ?? []).map((e) => ({
    label: entityOptionLabel(e),
    value: e.id,
  }));
}

watch(selectedTypeId, async (typeId) => {
  for (const k of Object.keys(linkPick)) {
    delete linkPick[Number(k)];
  }
  candidatesByRel.value = {};
  if (typeId == null || !workspaceId.value) return;
  const type = types.value.find((t) => t.id === typeId);
  if (!type) return;
  for (const rel of type.outgoingRelationships.filter((r) => r.isRequired)) {
    linkPick[rel.relationshipTypeId] = null;
    try {
      const items = await entityApi.list(workspaceId.value, {
        entityTypeId: rel.targetEntityTypeId,
        take: 400,
      });
      candidatesByRel.value = {
        ...candidatesByRel.value,
        [rel.relationshipTypeId]: items,
      };
    } catch {
      candidatesByRel.value = {
        ...candidatesByRel.value,
        [rel.relationshipTypeId]: [],
      };
    }
  }
});

function humanize(name: string): string {
  return name.replace(/_/g, ' ').replace(/^./, (c) => c.toUpperCase());
}

function formatTypeName(name: string): string {
  return name
    .split('_')
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

function isEmpty(v: FieldValue): boolean {
  if (v === null || v === undefined) return true;
  if (typeof v === 'string' && v.trim() === '') return true;
  return false;
}

const isFormValid = computed(() => {
  if (!selectedType.value) return false;
  return properties.value.every((p) => !isPropertyEmpty(p));
});

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function formatDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function serializeValue(
  prop: EntityTypePropertyDto,
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

function resetTypeFields() {
  const next: Record<number, FieldValue> = {};
  for (const p of properties.value) {
    next[p.propertyId] = p.dataType === 'Bool' ? false : null;
  }
  values.value = next;
  errorMessage.value = null;
  fieldErrors.value = {};
  submitAttempted.value = false;
}

function listQuery(): Record<string, string> {
  const q: Record<string, string> = {};
  const et = route.query.entityType;
  if (typeof et === 'string' && et.trim()) q.entityType = et.trim();
  return q;
}

function gotoList() {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(workspaceId.value) },
    query: listQuery(),
  });
}

function gotoWorkspaces() {
  router.push({ name: 'workspaces' });
}

async function ensureWorkspaceAccess(): Promise<boolean> {
  if (!workspaceId.value) {
    accessDenied.value = true;
    errorMessage.value = 'Workspace id is missing.';
    return false;
  }
  if (!wsStore.workspaces.length) {
    await wsStore.fetchWorkspaces(orgStore.currentOrgId ?? undefined);
  }
  const belongs = wsStore.workspaces.some((w) => w.id === workspaceId.value);
  if (!belongs) {
    accessDenied.value = true;
    errorMessage.value = 'You do not have access to this workspace.';
    return false;
  }
  wsStore.setCurrentWorkspace(workspaceId.value);
  return true;
}

async function loadTypes() {
  loadingTypes.value = true;
  try {
    const ok = await ensureWorkspaceAccess();
    if (!ok) return;
    if (!hasWorkspacePermission(wsStore.currentWorkspace, 'create_entities')) {
      accessDenied.value = true;
      errorMessage.value = 'You do not have permission to create entities in this workspace.';
      return;
    }
    await entityStore.fetchTypes();
    const wanted = route.query.entityType;
    if (typeof wanted === 'string' && wanted.trim()) {
      const match = creatableTypes.value.find(
        (t) => t.name.toLowerCase() === wanted.trim().toLowerCase(),
      );
      if (match) {
        selectedTypeId.value = match.id;
        await nextTick();
        resetTypeFields();
      }
    }
  } catch (err) {
    const normalized = normalizeError(err, 'Failed to load entity types.');
    errorMessage.value = normalized.message;
  } finally {
    loadingTypes.value = false;
  }
}

async function handleSubmit() {
  submitAttempted.value = true;
  const picked = types.value.find((t) => t.id === selectedTypeId.value);
  if (picked && isEntityTypeUiLocked(picked)) {
    errorMessage.value =
      'This entity type cannot be created from the UI.';
    return;
  }
  if (!isFormValid.value || selectedTypeId.value === null) {
    errorMessage.value = 'Please fill in all fields before submitting.';
    return;
  }
  for (const rel of requiredOutgoing.value) {
    if (linkPick[rel.relationshipTypeId] == null) {
      errorMessage.value = `Select a linked ${rel.targetEntityTypeName.replace(/_/g, ' ')} for "${humanize(rel.name)}".`;
      return;
    }
  }
  submitting.value = true;
  errorMessage.value = null;
  fieldErrors.value = {};
  try {
    const base = {
      entityTypeId: selectedTypeId.value,
      properties: properties.value.map((p) => ({
        propertyId: p.propertyId,
        value: serializeValue(p, values.value[p.propertyId] ?? null),
      })),
    };
    const req = requiredOutgoing.value;
    const body =
      req.length > 0
        ? {
            ...base,
            links: req.map((rel) => ({
              relationshipTypeId: rel.relationshipTypeId,
              targetEntityId: linkPick[rel.relationshipTypeId]!,
            })),
          }
        : base;

    const detail = orchestrateViaGraph.value
      ? await entityStore.createViaGraph(workspaceId.value, body)
      : await entityStore.create(workspaceId.value, body);
    const typeLabel = selectedType.value?.name ?? 'Entity';
    toast.add({
      severity: 'success',
      summary: `${typeLabel} created`,
      life: 3000,
    });
    router.push({
      name: 'workspace-entities',
      params: { workspaceId: String(workspaceId.value) },
      query: {
        ...listQuery(),
        id: String(detail.id),
        entityType: detail.entityTypeName,
      },
    });
  } catch (err) {
    const normalized = normalizeError(err, 'Failed to create entity.');
    fieldErrors.value = normalized.fieldErrors;
    errorMessage.value = normalized.message;
  } finally {
    submitting.value = false;
  }
}

function handleCancel() {
  gotoList();
}

onMounted(loadTypes);

watch(
  () => route.query.entityType,
  () => {
    if (loadingTypes.value || !creatableTypes.value.length) return;
    const wanted = route.query.entityType;
    if (typeof wanted !== 'string' || !wanted.trim()) return;
    const match = creatableTypes.value.find(
      (t) => t.name.toLowerCase() === wanted.trim().toLowerCase(),
    );
    if (match && match.id !== selectedTypeId.value) {
      selectedTypeId.value = match.id;
      resetTypeFields();
    }
  },
);
</script>

<template>
  <section class="max-w-2xl">
    <div class="mb-6">
      <Button
        text
        icon="pi pi-arrow-left"
        :label="accessDenied ? 'Workspaces' : 'Back to entities'"
        severity="secondary"
        size="small"
        class="!px-1 !mb-1"
        @click="accessDenied ? gotoWorkspaces() : gotoList()"
      />
      <h1 class="text-2xl font-bold text-ink-900">Create entity</h1>
      <p class="mt-1 text-sm text-ink-500">
        Add a new entity to this workspace.
      </p>
    </div>

    <Message
      v-if="accessDenied"
      severity="error"
      :closable="false"
      class="!my-0"
    >
      {{ errorMessage }}
    </Message>

    <div v-else-if="loadingTypes" class="text-center py-12 text-ink-500">
      Loading...
    </div>

    <div
      v-else-if="!types.length"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-info-circle text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No record types available. Contact your administrator.
      </p>
    </div>

    <div
      v-else-if="!creatableTypes.length"
      class="rounded-xl border border-line bg-white p-10 text-center"
    >
      <i class="pi pi-lock text-3xl text-ink-400" />
      <p class="mt-3 text-sm text-ink-500">
        No entity types can be created manually. Derived or system-maintained
        types are hidden here.
      </p>
    </div>

    <form
      v-else
      class="rounded-xl border border-line bg-white p-6 flex flex-col gap-4"
      novalidate
      @submit.prevent="handleSubmit"
    >
      <div class="flex flex-col gap-1.5">
        <label for="entityType" class="text-xs font-medium text-ink-600">
          Entity type <span class="text-danger">*</span>
        </label>
        <Select
          id="entityType"
          v-model="selectedTypeId"
          :options="creatableTypes"
          option-label="name"
          option-value="id"
          placeholder="Select type"
          class="!h-10"
          @update:model-value="resetTypeFields"
        />
      </div>

      <template v-for="rel in requiredOutgoing" :key="rel.relationshipTypeId">
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            {{ humanize(rel.name) }}
            <span class="text-danger">*</span>
            <span class="text-ink-400 font-normal normal-case">
              → {{ formatTypeName(rel.targetEntityTypeName) }}</span>
          </label>
          <Select
            v-model="linkPick[rel.relationshipTypeId]"
            :options="linkSelectOptions(rel.relationshipTypeId)"
            option-label="label"
            option-value="value"
            :placeholder="`Choose ${formatTypeName(rel.targetEntityTypeName)}`"
            class="w-full"
            filter
          />
          <p
            v-if="(candidatesByRel[rel.relationshipTypeId] ?? []).length === 0"
            class="text-xs text-ink-500"
          >
            No matching records in this workspace. Create the linked record first, or widen your filters from the list view.
          </p>
        </div>
      </template>

      <div
        v-if="selectedTypeId"
        class="flex items-start gap-3 rounded-lg border border-line bg-surface/40 px-3 py-2.5"
      >
        <ToggleSwitch
          v-model="orchestrateViaGraph"
          input-id="orch-graph"
          class="mt-0.5"
        />
        <label for="orch-graph" class="text-xs text-ink-600 leading-snug cursor-pointer">
          Submit through Graph orchestration (RabbitMQ → Core). Optional; use when your environment routes creates through the graph service.
        </label>
      </div>

      <template v-for="prop in properties" :key="prop.propertyId">
        <div class="flex flex-col gap-1.5">
          <label
            :for="`p-${prop.propertyId}`"
            class="text-xs font-medium text-ink-600"
          >
            {{ humanize(prop.name) }}
            <span v-if="isPropertyRequired(prop)" class="text-danger">*</span>
          </label>

          <InputText
            v-if="prop.dataType === 'String'"
            :id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as string"
            class="!h-10"
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <InputNumber
            v-else-if="prop.dataType === 'Int'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as number"
            :min="0"
            :max-fraction-digits="0"
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <InputNumber
            v-else-if="prop.dataType === 'Decimal'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as number"
            :min="0"
            :min-fraction-digits="0"
            :max-fraction-digits="2"
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <DatePicker
            v-else-if="prop.dataType === 'Date'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as Date | null"
            date-format="yy-mm-dd"
            show-icon
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <ToggleSwitch
            v-else-if="prop.dataType === 'Bool'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as boolean"
          />

          <small
            v-if="propertyFieldError(prop)"
            class="text-xs text-danger"
          >
            <i class="pi pi-exclamation-circle mr-1" />{{ propertyFieldError(prop) }}
          </small>
        </div>
      </template>

      <Message
        v-if="errorMessage"
        severity="error"
        :closable="false"
        class="!my-0"
      >
        {{ errorMessage }}
      </Message>

      <div class="flex justify-end gap-2 pt-2">
        <Button
          type="button"
          label="Cancel"
          severity="secondary"
          text
          @click="handleCancel"
        />
        <Button
          type="submit"
          label="Create"
          :disabled="submitting"
          :loading="submitting"
        />
      </div>
    </form>
  </section>
</template>
