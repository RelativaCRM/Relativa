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
import Dialog from 'primevue/dialog';
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
  type OutgoingRelationshipDto,
  entityApi,
} from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';
import { hasWorkspacePermission } from '@/utils/workspacePermissions';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

type FieldValue = string | number | boolean | Date | null;

const route = useRoute();
const router = useRouter();
const toast = useToast();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.workspaceId));

const types = computed<EntityTypeDto[]>(() => entityStore.types);

const creatableTypes = computed(() =>
  types.value.filter((t) => t.isStandalone && !isEntityTypeUiLocked(t)),
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
  return prop.isRequired;
}

function propertyFieldError(prop: EntityTypePropertyDto): string | null {
  if (submitAttempted.value && isPropertyRequired(prop) && isPropertyEmpty(prop)) {
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

const nestedDialogOpen = ref(false);
const nestedForRel = ref<OutgoingRelationshipDto | null>(null);
const nestedValues = ref<Record<number, FieldValue>>({});
const nestedLinkPick = reactive<Record<number, number | null>>({});
const nestedCandidatesByRel = ref<Record<number, EntityListItemDto[]>>({});
const nestedSubmitting = ref(false);
const nestedError = ref<string | null>(null);
const nestedFieldErrors = ref<FieldErrors>({});
const nestedSubmitAttempted = ref(false);

const nestedTargetType = computed(() => {
  const rel = nestedForRel.value;
  if (!rel) return null;
  return types.value.find((t) => t.id === rel.targetEntityTypeId) ?? null;
});

const nestedTargetProperties = computed(
  () => nestedTargetType.value?.properties ?? [],
);

const nestedRequiredOutgoing = computed(
  () =>
    nestedTargetType.value?.outgoingRelationships.filter((r) => r.isRequired) ??
    [],
);

function targetTypeForRequiredLink(rel: OutgoingRelationshipDto): EntityTypeDto | null {
  return types.value.find((t) => t.id === rel.targetEntityTypeId) ?? null;
}

function canCreateLinkedTarget(rel: OutgoingRelationshipDto): boolean {
  const t = targetTypeForRequiredLink(rel);
  return !!t && !isEntityTypeUiLocked(t);
}

function nestedLinkSelectOptions(relationshipTypeId: number) {
  return (nestedCandidatesByRel.value[relationshipTypeId] ?? []).map((e) => ({
    label: entityOptionLabel(e),
    value: e.id,
  }));
}

async function openNestedCreate(rel: OutgoingRelationshipDto) {
  const target = targetTypeForRequiredLink(rel);
  if (!target || isEntityTypeUiLocked(target)) return;

  nestedForRel.value = rel;
  nestedDialogOpen.value = true;
  nestedError.value = null;
  nestedFieldErrors.value = {};
  nestedSubmitAttempted.value = false;

  const nv: Record<number, FieldValue> = {};
  for (const p of target.properties) {
    nv[p.propertyId] = p.dataType === 'Bool' ? false : null;
  }
  nestedValues.value = nv;

  for (const k of Object.keys(nestedLinkPick)) {
    delete nestedLinkPick[Number(k)];
  }
  nestedCandidatesByRel.value = {};

  const wid = workspaceId.value;
  if (!wid) return;

  const innerReq = target.outgoingRelationships.filter((r) => r.isRequired);
  for (const ir of innerReq) {
    nestedLinkPick[ir.relationshipTypeId] = null;
    try {
      const items = await entityApi.list(wid, {
        entityTypeId: ir.targetEntityTypeId,
        take: 400,
      });
      nestedCandidatesByRel.value = {
        ...nestedCandidatesByRel.value,
        [ir.relationshipTypeId]: items,
      };
    } catch {
      nestedCandidatesByRel.value = {
        ...nestedCandidatesByRel.value,
        [ir.relationshipTypeId]: [],
      };
    }
  }
}

function nestedPropertyFieldError(prop: EntityTypePropertyDto): string | null {
  if (nestedSubmitAttempted.value && isPropertyRequired(prop) && isPropertyEmptyFor(prop, nestedValues.value)) {
    return 'This field is required.';
  }
  return (
    firstFieldError(nestedFieldErrors.value, prop.name) ??
    firstFieldError(nestedFieldErrors.value, `properties[${prop.propertyId}]`) ??
    firstFieldError(nestedFieldErrors.value, `properties.${prop.propertyId}`)
  );
}

function isPropertyEmptyFor(
  prop: EntityTypePropertyDto,
  vals: Record<number, FieldValue>,
): boolean {
  if (prop.dataType === 'Bool') return false;
  return isEmpty(vals[prop.propertyId] ?? null);
}

function clearNestedPropertyFieldError(prop: EntityTypePropertyDto) {
  if (
    !nestedFieldErrors.value[prop.name] &&
    !nestedFieldErrors.value[`properties[${prop.propertyId}]`] &&
    !nestedFieldErrors.value[`properties.${prop.propertyId}`]
  ) {
    return;
  }
  const next = { ...nestedFieldErrors.value };
  delete next[prop.name];
  delete next[`properties[${prop.propertyId}]`];
  delete next[`properties.${prop.propertyId}`];
  nestedFieldErrors.value = next;
}

function nestedFormValid(type: EntityTypeDto): boolean {
  if (
    !type.properties
      .filter((p) => !p.isReadonly && p.isRequired)
      .every((p) => !isPropertyEmptyFor(p, nestedValues.value))
  ) {
    return false;
  }
  for (const ir of type.outgoingRelationships.filter((r) => r.isRequired)) {
    if (nestedLinkPick[ir.relationshipTypeId] == null) return false;
  }
  return true;
}

async function submitNestedCreate() {
  const rel = nestedForRel.value;
  const target = nestedTargetType.value;
  const wid = workspaceId.value;
  nestedSubmitAttempted.value = true;
  if (!rel || !target || !wid) return;

  if (!nestedFormValid(target)) {
    nestedError.value =
      target.outgoingRelationships.some(
        (r) => r.isRequired && nestedLinkPick[r.relationshipTypeId] == null,
      )
        ? 'Select all required links for this record.'
        : 'Fill in all required fields.';
    return;
  }

  nestedSubmitting.value = true;
  nestedError.value = null;
  nestedFieldErrors.value = {};
  try {
    const innerReq = target.outgoingRelationships.filter((r) => r.isRequired);
    const base = {
      entityTypeId: target.id,
      properties: target.properties
        .filter((p) => !p.isReadonly)
        .map((p) => ({
          propertyId: p.propertyId,
          value: serializeValue(p, nestedValues.value[p.propertyId] ?? null),
        })),
    };
    const body =
      innerReq.length > 0
        ? {
            ...base,
            links: innerReq.map((ir) => ({
              relationshipTypeId: ir.relationshipTypeId,
              targetEntityId: nestedLinkPick[ir.relationshipTypeId]!,
            })),
          }
        : base;

    const detail = await entityStore.createViaGraph(wid, body);

    const outerRelId = rel.relationshipTypeId;
    const asList: EntityListItemDto = {
      id: detail.id,
      entityTypeId: detail.entityTypeId,
      entityTypeName: detail.entityTypeName,
      propertyValues: detail.propertyValues,
    };
    const existing = candidatesByRel.value[outerRelId] ?? [];
    candidatesByRel.value = {
      ...candidatesByRel.value,
      [outerRelId]: [asList, ...existing],
    };
    linkPick[outerRelId] = detail.id;

    toast.add({
      severity: 'success',
      summary: `${target.displayName} created`,
      detail: `Linked to this ${selectedType.value ? selectedType.value.displayName : 'record'}.`,
      life: 3500,
    });
    nestedDialogOpen.value = false;
  } catch (err) {
    const normalized = normalizeError(err, 'Failed to create linked record.');
    nestedFieldErrors.value = normalized.fieldErrors;
    nestedError.value = normalized.message;
  } finally {
    nestedSubmitting.value = false;
  }
}

function cancelNestedCreate() {
  nestedDialogOpen.value = false;
}

watch(nestedDialogOpen, (open) => {
  if (!open) {
    nestedForRel.value = null;
    nestedError.value = null;
    nestedFieldErrors.value = {};
    nestedSubmitAttempted.value = false;
    nestedSubmitting.value = false;
  }
});

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

function isEmpty(v: FieldValue): boolean {
  if (v === null || v === undefined) return true;
  if (typeof v === 'string' && v.trim() === '') return true;
  return false;
}

const isFormValid = computed(() => {
  if (!selectedType.value) return false;
  return properties.value
    .filter((p) => !p.isReadonly && p.isRequired)
    .every((p) => !isPropertyEmpty(p));
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
      errorMessage.value = `Select a linked ${rel.targetEntityTypeDisplayName} for "${rel.displayName}".`;
      return;
    }
  }
  submitting.value = true;
  errorMessage.value = null;
  fieldErrors.value = {};
  try {
    const base = {
      entityTypeId: selectedTypeId.value,
      properties: properties.value
        .filter((p) => !p.isReadonly)
        .map((p) => ({
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

    const detail = await entityStore.createViaGraph(workspaceId.value, body);
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

    <LoadingSkeleton
      v-else-if="loadingTypes"
      variant="detail"
      :rows="5"
      label="Loading entity types"
    />

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
            {{ rel.displayName }}
            <span class="text-danger">*</span>
            <span class="text-ink-400 font-normal normal-case">
              → {{ rel.targetEntityTypeDisplayName }}</span>
          </label>
          <Select
            v-model="linkPick[rel.relationshipTypeId]"
            :options="linkSelectOptions(rel.relationshipTypeId)"
            option-label="label"
            option-value="value"
            :placeholder="`Choose ${rel.targetEntityTypeDisplayName}`"
            class="w-full !h-10"
            filter
          />
          <Button
            v-if="canCreateLinkedTarget(rel)"
            type="button"
            icon="pi pi-plus"
            :label="`Create new ${rel.targetEntityTypeDisplayName}`"
            severity="secondary"
            outlined
            size="small"
            class="w-fit !h-10"
            @click="openNestedCreate(rel)"
          />
          <p
            v-if="
              !canCreateLinkedTarget(rel) &&
              (candidatesByRel[rel.relationshipTypeId] ?? []).length === 0
            "
            class="text-xs text-ink-500"
          >
            No matching records in this workspace. This linked type cannot be created from this screen — contact an administrator.
          </p>
          <p
            v-else-if="
              canCreateLinkedTarget(rel) &&
              (candidatesByRel[rel.relationshipTypeId] ?? []).length === 0
            "
            class="text-xs text-ink-500"
          >
            No records yet — use “Create new {{ rel.targetEntityTypeDisplayName }}” to add one; it will be selected for this deal automatically.
          </p>
        </div>
      </template>

      <template
        v-for="prop in properties.filter((p) => !p.isReadonly)"
        :key="prop.propertyId"
      >
        <div class="flex flex-col gap-1.5">
          <label
            :for="`p-${prop.propertyId}`"
            class="text-xs font-medium text-ink-600"
          >
            {{ prop.displayName }}
            <span v-if="isPropertyRequired(prop)" class="text-danger">*</span>
          </label>

          <Select
            v-if="prop.dataType === 'String' && prop.allowedValues?.length > 0"
            :id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as string"
            :options="prop.allowedValues"
            option-label="displayName"
            option-value="value"
            placeholder="Select..."
            class="w-full !h-10"
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <InputText
            v-else-if="prop.dataType === 'String'"
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
            :input-class="'!h-10 w-full'"
            class="w-full"
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
            :input-class="'!h-10 w-full'"
            class="w-full"
            :invalid="!!propertyFieldError(prop)"
            @update:model-value="clearPropertyFieldError(prop)"
          />

          <DatePicker
            v-else-if="prop.dataType === 'Date'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as Date | null"
            date-format="yy-mm-dd"
            show-icon
            :input-class="'!h-10 w-full'"
            class="w-full"
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

      <div class="flex justify-end gap-3 pt-2">
        <Button
          type="button"
          label="Cancel"
          outlined
          class="!h-10 !px-4 !bg-white !border !border-brand-600 !text-brand-600 hover:!bg-brand-50"
          @click="handleCancel"
        />
        <Button
          type="submit"
          label="Create"
          :disabled="submitting"
          :loading="submitting"
          class="!h-10 !px-4 !bg-brand-600 !border !border-brand-600 !text-white hover:!bg-brand-700 hover:!border-brand-700"
        />
      </div>
    </form>

    <Dialog
      v-model:visible="nestedDialogOpen"
      :header="
        nestedTargetType
          ? `New ${nestedTargetType.displayName}`
          : 'New record'
      "
      modal
      :draggable="false"
      class="nested-create-dialog max-w-[min(32rem,calc(100vw-2rem))]"
      @keydown.esc="cancelNestedCreate"
    >
      <div v-if="nestedTargetType" class="flex flex-col gap-4">
        <p class="text-xs text-ink-500 leading-snug -mt-1">
          This saves to your workspace and is linked when you finish the parent record below.
        </p>

        <template v-for="ir in nestedRequiredOutgoing" :key="ir.relationshipTypeId">
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">
              {{ ir.displayName }}
              <span class="text-danger">*</span>
              <span class="text-ink-400 font-normal normal-case">
                → {{ ir.targetEntityTypeDisplayName }}</span>
            </label>
            <Select
              v-model="nestedLinkPick[ir.relationshipTypeId]"
              :options="nestedLinkSelectOptions(ir.relationshipTypeId)"
              option-label="label"
              option-value="value"
              :placeholder="`Choose ${ir.targetEntityTypeDisplayName}`"
              class="w-full !h-10"
              filter
            />
          </div>
        </template>

        <template
          v-for="prop in nestedTargetProperties.filter((p) => !p.isReadonly)"
          :key="prop.propertyId"
        >
          <div class="flex flex-col gap-1.5">
            <label
              :for="`np-${prop.propertyId}`"
              class="text-xs font-medium text-ink-600"
            >
              {{ prop.displayName }}
              <span v-if="isPropertyRequired(prop)" class="text-danger">*</span>
            </label>

            <InputText
              v-if="prop.dataType === 'String'"
              :id="`np-${prop.propertyId}`"
              v-model="nestedValues[prop.propertyId] as string"
              class="!h-10 w-full"
              :invalid="!!nestedPropertyFieldError(prop)"
              @update:model-value="clearNestedPropertyFieldError(prop)"
            />

            <InputNumber
              v-else-if="prop.dataType === 'Int'"
              :input-id="`np-${prop.propertyId}`"
              v-model="nestedValues[prop.propertyId] as number"
              class="w-full"
              :input-class="'!h-10 w-full'"
              :min="0"
              :max-fraction-digits="0"
              :invalid="!!nestedPropertyFieldError(prop)"
              @update:model-value="clearNestedPropertyFieldError(prop)"
            />

            <InputNumber
              v-else-if="prop.dataType === 'Decimal'"
              :input-id="`np-${prop.propertyId}`"
              v-model="nestedValues[prop.propertyId] as number"
              class="w-full"
              :input-class="'!h-10 w-full'"
              :min="0"
              :min-fraction-digits="0"
              :max-fraction-digits="2"
              :invalid="!!nestedPropertyFieldError(prop)"
              @update:model-value="clearNestedPropertyFieldError(prop)"
            />

            <DatePicker
              v-else-if="prop.dataType === 'Date'"
              :input-id="`np-${prop.propertyId}`"
              v-model="nestedValues[prop.propertyId] as Date | null"
              date-format="yy-mm-dd"
              show-icon
              class="w-full"
              :input-class="'!h-10 w-full'"
              :invalid="!!nestedPropertyFieldError(prop)"
              @update:model-value="clearNestedPropertyFieldError(prop)"
            />

            <ToggleSwitch
              v-else-if="prop.dataType === 'Bool'"
              :input-id="`np-${prop.propertyId}`"
              v-model="nestedValues[prop.propertyId] as boolean"
            />

            <small v-if="nestedPropertyFieldError(prop)" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{
                nestedPropertyFieldError(prop)
              }}
            </small>
          </div>
        </template>

        <Message
          v-if="nestedError"
          severity="error"
          :closable="false"
          class="!my-0"
        >
          {{ nestedError }}
        </Message>

        <div class="flex justify-end gap-3 pt-2">
          <Button
            label="Cancel"
            outlined
            type="button"
            class="!h-10 !px-4 !bg-white !border !border-brand-600 !text-brand-600 hover:!bg-brand-50"
            @click="cancelNestedCreate"
          />
          <Button
            label="Create & link"
            type="button"
            icon="pi pi-check"
            :loading="nestedSubmitting"
            :disabled="nestedSubmitting"
            class="!h-10 !px-4 !bg-brand-600 !border !border-brand-600 !text-white hover:!bg-brand-700 hover:!border-brand-700"
            @click="submitNestedCreate"
          />
        </div>
      </div>
    </Dialog>
  </section>
</template>
