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
import { ApiError } from '@/api/http';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  type EntityTypeDto,
  type EntityTypePropertyDto,
} from '@/api/entities';

type FieldValue = string | number | boolean | Date | null;

const route = useRoute();
const router = useRouter();
const toast = useToast();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.id));

const types = computed<EntityTypeDto[]>(() => entityStore.types);
const selectedTypeId = ref<number | null>(null);
const values = ref<Record<number, FieldValue>>({});
const loadingTypes = ref(true);
const submitting = ref(false);
const errorMessage = ref<string | null>(null);
const accessDenied = ref(false);

const selectedType = computed(
  () => types.value.find((t) => t.id === selectedTypeId.value) ?? null,
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
  return properties.value.every((p) => {
    if (!p.isRequired) return true;
    return !isEmpty(values.value[p.propertyId] ?? null);
  });
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
}

function gotoList() {
  router.push({
    name: 'workspace-entities',
    params: { id: String(workspaceId.value) },
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
    await wsStore.fetchWorkspaces();
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
    errorMessage.value =
      err instanceof ApiError ? err.message : 'Failed to load entity types.';
  } finally {
    loadingTypes.value = false;
  }
}

async function handleSubmit() {
  if (!isFormValid.value || selectedTypeId.value === null) return;
  submitting.value = true;
  errorMessage.value = null;
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
    errorMessage.value =
      err instanceof ApiError ? err.message : 'Failed to create entity.';
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
          :options="types"
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
            <span v-if="prop.isRequired" class="text-danger">*</span>
          </label>

          <InputText
            v-if="prop.dataType === 'String'"
            :id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as string"
            class="!h-10"
          />

          <InputNumber
            v-else-if="prop.dataType === 'Int'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as number"
            :min="0"
            :max-fraction-digits="0"
          />

          <InputNumber
            v-else-if="prop.dataType === 'Decimal'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as number"
            :min="0"
            :min-fraction-digits="0"
            :max-fraction-digits="2"
          />

          <DatePicker
            v-else-if="prop.dataType === 'Date'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as Date | null"
            date-format="yy-mm-dd"
            show-icon
          />

          <ToggleSwitch
            v-else-if="prop.dataType === 'Bool'"
            :input-id="`p-${prop.propertyId}`"
            v-model="values[prop.propertyId] as boolean"
          />
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
          :disabled="!isFormValid || submitting"
          :loading="submitting"
        />
      </div>
    </form>
  </section>
</template>
