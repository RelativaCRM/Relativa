<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
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
} from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';

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

function humanize(name: string): string {
  return name.replace(/_/g, ' ').replace(/^./, (c) => c.toUpperCase());
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

function gotoList() {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(workspaceId.value) },
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
    await entityStore.fetchTypes();
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
  submitting.value = true;
  errorMessage.value = null;
  fieldErrors.value = {};
  try {
    const payload = {
      entityTypeId: selectedTypeId.value,
      properties: properties.value.map((p) => ({
        propertyId: p.propertyId,
        value: serializeValue(p, values.value[p.propertyId] ?? null),
      })),
    };
    await entityStore.create(workspaceId.value, payload);
    const typeLabel = selectedType.value?.name ?? 'Entity';
    toast.add({
      severity: 'success',
      summary: `${typeLabel} created`,
      life: 3000,
    });
    gotoList();
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
