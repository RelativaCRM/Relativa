<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import Textarea from 'primevue/textarea';
import FloatLabel from 'primevue/floatlabel';
import ToggleSwitch from 'primevue/toggleswitch';
import Message from 'primevue/message';
import Skeleton from 'primevue/skeleton';
import { useWorkspaceStore } from '@/stores/workspace';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const { t } = useI18n();
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
    notify(err, { fallback: t('wsSettings.loadError') });
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
      summary: t('settings.savedSummary'),
      detail: t('wsSettings.savedDetail'),
      life: 4000,
    });
  } catch (err) {
    const n = normalizeError(err, t('settings.saveError'));
    fieldErrors.value = n.fieldErrors;
    if (!Object.keys(n.fieldErrors).length) {
      toast.add({ severity: 'error', summary: t('settings.errorSummary'), detail: n.message, life: 6000 });
    }
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <section class="max-w-2xl mx-auto px-4">
    <header class="mb-6">
      <h1 class="text-xl font-bold text-ink-900 leading-tight">{{ t('wsSettings.title') }}</h1>
      <p class="mt-1 text-[13px] text-ink-500">{{ t('wsSettings.subtitle') }}</p>
    </header>

    <Message
      v-if="!canEdit && !loading"
      severity="info"
      :closable="false"
      class="mb-4"
    >
      {{ t('wsSettings.noPermission') }}
    </Message>

    <div v-if="loading" class="flex flex-col gap-3">
      <Skeleton height="2.75rem" />
      <Skeleton height="6rem" />
      <Skeleton height="2.75rem" />
    </div>

    <form v-else class="flex flex-col gap-5" novalidate @submit.prevent="handleSave">
      <div class="border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">{{ t('wsSettings.general') }}</h2>
        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <InputText
              id="wsName"
              v-model="form.name"
              :disabled="!canEdit"
              class="!h-11 w-full"
              maxlength="100"
              :invalid="!!firstFieldError(fieldErrors, 'name')"
              @update:model-value="clearField('name')"
            />
            <label for="wsName">{{ t('wsSettings.nameLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'name')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'name') }}
          </small>
        </div>
        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <Textarea
              id="wsDesc"
              v-model="form.description"
              rows="3"
              maxlength="500"
              :disabled="!canEdit"
              class="w-full resize-none"
              :invalid="!!firstFieldError(fieldErrors, 'description')"
              @update:model-value="clearField('description')"
            />
            <label for="wsDesc">{{ t('wsSettings.descriptionLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'description')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'description') }}
          </small>
        </div>
      </div>

      <div class="border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">{{ t('wsSettings.riskScoring') }}</h2>
        <p class="mt-1 text-xs text-ink-500">{{ t('wsSettings.riskScoringHint') }}</p>

        <div class="mt-4 flex items-center justify-between gap-4">
          <span class="text-sm text-ink-700">{{ t('wsSettings.enableRiskScoring') }}</span>
          <ToggleSwitch v-model="form.riskScoringEnabled" :disabled="!canEdit" />
        </div>

        <template v-if="form.riskScoringEnabled">
          <div class="mt-6 flex flex-col gap-5">
            <div class="flex flex-col gap-1.5">
              <FloatLabel variant="on">
                <InputNumber
                  input-id="highThreshold"
                  v-model="form.highRiskThreshold"
                  :min="0"
                  :max="1"
                  :step="0.01"
                  :min-fraction-digits="2"
                  :max-fraction-digits="2"
                  :disabled="!canEdit"
                  class="w-full"
                  input-class="!h-11 w-full"
                  :invalid="!!firstFieldError(fieldErrors, 'highRiskThreshold')"
                  @update:model-value="clearField('highRiskThreshold')"
                />
                <label for="highThreshold">{{ t('wsSettings.highThresholdLabel') }}</label>
              </FloatLabel>
              <small v-if="firstFieldError(fieldErrors, 'highRiskThreshold')" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'highRiskThreshold') }}
              </small>
            </div>

            <div class="flex flex-col gap-1.5">
              <FloatLabel variant="on">
                <InputNumber
                  input-id="medThreshold"
                  v-model="form.mediumRiskThreshold"
                  :min="0"
                  :max="1"
                  :step="0.01"
                  :min-fraction-digits="2"
                  :max-fraction-digits="2"
                  :disabled="!canEdit"
                  class="w-full"
                  input-class="!h-11 w-full"
                  :invalid="!!firstFieldError(fieldErrors, 'mediumRiskThreshold')"
                  @update:model-value="clearField('mediumRiskThreshold')"
                />
                <label for="medThreshold">{{ t('wsSettings.mediumThresholdLabel') }}</label>
              </FloatLabel>
              <small v-if="firstFieldError(fieldErrors, 'mediumRiskThreshold')" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'mediumRiskThreshold') }}
              </small>
            </div>
          </div>
        </template>
      </div>

      <div v-if="canEdit" class="flex justify-end">
        <button type="submit" class="btn btn-primary" :disabled="submitting">
          <i v-if="submitting" class="pi pi-spin pi-spinner text-xs" />
          {{ t('settings.save') }}
        </button>
      </div>
    </form>
  </section>
</template>
