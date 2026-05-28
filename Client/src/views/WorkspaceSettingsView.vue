<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import Textarea from 'primevue/textarea';
import ToggleSwitch from 'primevue/toggleswitch';
import Message from 'primevue/message';
import Skeleton from 'primevue/skeleton';
import { useWorkspaceStore } from '@/stores/workspace';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const route = useRoute();
const wsStore = useWorkspaceStore();
const toast = useToast();
const { notify } = useApiErrorHandler();

const workspaceId = computed(() => Number(route.params.workspaceId));

const loading = ref(false);
const submitting = ref(false);
const fieldErrors = ref<FieldErrors>({});

const canEdit = computed(() =>
  wsStore.currentWorkspace?.myPermissions?.includes('manage_ws_settings') ?? false,
);

const form = reactive({
  name: '',
  description: '' as string | null,
  highRiskThreshold: 0.7,
  mediumRiskThreshold: 0.4,
  riskScoringEnabled: true,
});

function populateForm() {
  const s = wsStore.wsSettings;
  if (!s) return;
  form.name = s.name;
  form.description = s.description;
  form.highRiskThreshold = s.highRiskThreshold;
  form.mediumRiskThreshold = s.mediumRiskThreshold;
  form.riskScoringEnabled = s.riskScoringEnabled;
}

function clearField(field: string) {
  const next = { ...fieldErrors.value };
  delete next[field];
  fieldErrors.value = next;
}

onMounted(async () => {
  loading.value = true;
  try {
    await wsStore.fetchSettings(workspaceId.value);
    populateForm();
  } catch (err) {
    notify(err, { fallback: 'Could not load workspace settings.' });
  } finally {
    loading.value = false;
  }
});

async function handleSave() {
  if (!canEdit.value) return;
  submitting.value = true;
  fieldErrors.value = {};
  try {
    await wsStore.updateSettings(workspaceId.value, {
      name: form.name,
      description: form.description || null,
      highRiskThreshold: form.highRiskThreshold,
      mediumRiskThreshold: form.mediumRiskThreshold,
      riskScoringEnabled: form.riskScoringEnabled,
    });
    toast.add({
      severity: 'success',
      summary: 'Settings saved',
      detail: 'Workspace settings have been updated.',
      life: 4000,
    });
  } catch (err) {
    const n = normalizeError(err, 'Could not save settings.');
    fieldErrors.value = n.fieldErrors;
    if (!Object.keys(n.fieldErrors).length) {
      toast.add({ severity: 'error', summary: 'Error', detail: n.message, life: 6000 });
    }
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <section class="max-w-xl">
    <h1 class="text-2xl font-bold text-ink-900">Workspace Settings</h1>
    <p class="mt-2 text-sm text-ink-500">
      Configure how this workspace behaves for all its members.
    </p>

    <Message
      v-if="!canEdit && !loading"
      severity="info"
      :closable="false"
      class="mt-4"
    >
      You do not have permission to edit workspace settings.
    </Message>

    <div v-if="loading" class="mt-8 flex flex-col gap-3">
      <Skeleton height="2.5rem" />
      <Skeleton height="6rem" />
      <Skeleton height="2.5rem" />
    </div>

    <form v-else class="mt-8 flex flex-col gap-6" novalidate @submit.prevent="handleSave">

      <!-- General -->
      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">General</h2>
        <div class="mt-4 flex flex-col gap-1.5">
          <label for="wsName" class="text-xs font-medium text-ink-600">Workspace name</label>
          <InputText
            id="wsName"
            v-model="form.name"
            :disabled="!canEdit"
            class="w-full"
            maxlength="100"
            placeholder="Workspace name"
            :invalid="!!firstFieldError(fieldErrors, 'name')"
            @update:model-value="clearField('name')"
          />
          <small v-if="firstFieldError(fieldErrors, 'name')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'name') }}
          </small>
        </div>
        <div class="mt-4 flex flex-col gap-1.5">
          <label for="wsDesc" class="text-xs font-medium text-ink-600">Description</label>
          <Textarea
            id="wsDesc"
            v-model="form.description"
            rows="3"
            maxlength="500"
            :disabled="!canEdit"
            class="w-full resize-none"
            placeholder="Optional description visible to workspace members"
            :invalid="!!firstFieldError(fieldErrors, 'description')"
            @update:model-value="clearField('description')"
          />
          <small v-if="firstFieldError(fieldErrors, 'description')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'description') }}
          </small>
        </div>
      </div>

      <!-- Risk Scoring -->
      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">Risk Scoring</h2>
        <p class="mt-1 text-xs text-ink-500">
          ML-based closure and churn scores are shown on deals in this workspace.
        </p>

        <div class="mt-4 flex items-center justify-between gap-4">
          <span class="text-sm text-ink-700">Enable risk scoring</span>
          <ToggleSwitch
            v-model="form.riskScoringEnabled"
            :disabled="!canEdit"
          />
        </div>

        <template v-if="form.riskScoringEnabled">
          <div class="mt-5 flex flex-col gap-4">
            <div class="flex flex-col gap-1.5">
              <label for="highThreshold" class="text-xs font-medium text-ink-600">
                High risk threshold (0 – 1)
              </label>
              <InputNumber
                id="highThreshold"
                v-model="form.highRiskThreshold"
                :min="0"
                :max="1"
                :step="0.01"
                :min-fraction-digits="2"
                :max-fraction-digits="2"
                :disabled="!canEdit"
                class="w-full"
                :invalid="!!firstFieldError(fieldErrors, 'highRiskThreshold')"
                @update:model-value="clearField('highRiskThreshold')"
              />
              <small v-if="firstFieldError(fieldErrors, 'highRiskThreshold')" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'highRiskThreshold') }}
              </small>
            </div>

            <div class="flex flex-col gap-1.5">
              <label for="medThreshold" class="text-xs font-medium text-ink-600">
                Medium risk threshold (0 – 1, must be less than high)
              </label>
              <InputNumber
                id="medThreshold"
                v-model="form.mediumRiskThreshold"
                :min="0"
                :max="1"
                :step="0.01"
                :min-fraction-digits="2"
                :max-fraction-digits="2"
                :disabled="!canEdit"
                class="w-full"
                :invalid="!!firstFieldError(fieldErrors, 'mediumRiskThreshold')"
                @update:model-value="clearField('mediumRiskThreshold')"
              />
              <small v-if="firstFieldError(fieldErrors, 'mediumRiskThreshold')" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'mediumRiskThreshold') }}
              </small>
            </div>
          </div>
        </template>
      </div>

      <div v-if="canEdit" class="flex justify-end">
        <Button type="submit" label="Save settings" :loading="submitting" />
      </div>
    </form>
  </section>
</template>
