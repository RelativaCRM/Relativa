<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useToast } from 'primevue/usetoast';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Select from 'primevue/select';
import FloatLabel from 'primevue/floatlabel';
import Message from 'primevue/message';
import Skeleton from 'primevue/skeleton';
import { useOrganizationStore } from '@/stores/organization';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const { t } = useI18n();
const orgStore = useOrganizationStore();
const toast = useToast();
const { notify } = useApiErrorHandler();

const loading = ref(false);
const submitting = ref(false);
const fieldErrors = ref<FieldErrors>({});

const canEdit = computed(() =>
  orgStore.currentOrg?.myPermissions?.includes('manage_org_settings') ?? false,
);

const joinPolicyOptions = computed(() => [
  { label: t('orgSettings.joinPolicyOpenOption'), value: 'open' },
  { label: t('orgSettings.joinPolicyInviteOnlyOption'), value: 'invite_only' },
]);

const defaultRoleOptions = computed(() => {
  const memberDisplayName = orgStore.roles.find((r) => r.name === 'org_member')?.displayName ?? t('orgSettings.memberFallback');
  const none = { label: t('orgSettings.systemDefault', { role: memberDisplayName }), value: null as number | null };
  return [
    none,
    ...orgStore.roles
      .filter((r) => !r.isSystem || r.name !== 'org_owner')
      .map((r) => ({ label: r.displayName, value: r.id })),
  ];
});

const form = reactive({
  name: '',
  description: '' as string | null,
  joinPolicy: 'open' as 'open' | 'invite_only',
  defaultOrgRoleId: null as number | null,
});

function populateForm() {
  const s = orgStore.orgSettings;
  if (!s) return;
  form.name = s.name;
  form.description = s.description;
  form.joinPolicy = s.joinPolicy;
  form.defaultOrgRoleId = s.defaultOrgRoleId;
}

function clearField(field: string) {
  const next = { ...fieldErrors.value };
  delete next[field];
  fieldErrors.value = next;
}

onMounted(async () => {
  loading.value = true;
  try {
    await Promise.all([
      orgStore.fetchSettings(),
      orgStore.fetchRoles(),
    ]);
    populateForm();
  } catch (err) {
    notify(err, { fallback: t('orgSettings.loadError') });
  } finally {
    loading.value = false;
  }
});

async function handleSave() {
  if (!canEdit.value) return;
  submitting.value = true;
  fieldErrors.value = {};
  try {
    await orgStore.updateSettings({
      name: form.name,
      description: form.description || null,
      joinPolicy: form.joinPolicy,
      defaultOrgRoleId: form.defaultOrgRoleId,
    });
    toast.add({
      severity: 'success',
      summary: t('settings.savedSummary'),
      detail: t('orgSettings.savedDetail'),
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
      <h1 class="text-xl font-bold text-ink-900 leading-tight">{{ t('orgSettings.title') }}</h1>
      <p class="mt-1 text-[13px] text-ink-500">
        {{ t('orgSettings.subtitle') }}
      </p>
    </header>

    <Message
      v-if="!canEdit && !loading"
      severity="info"
      :closable="false"
      class="mb-4"
    >
      {{ t('orgSettings.noPermission') }}
    </Message>

    <div v-if="loading" class="flex flex-col gap-3">
      <Skeleton height="2.75rem" />
      <Skeleton height="6rem" />
      <Skeleton height="2.75rem" />
    </div>

    <form v-else class="flex flex-col gap-5" novalidate @submit.prevent="handleSave">
      <div class="border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">{{ t('orgSettings.general') }}</h2>
        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <InputText
              id="orgName"
              v-model="form.name"
              :disabled="!canEdit"
              class="!h-11 w-full"
              maxlength="100"
              :invalid="!!firstFieldError(fieldErrors, 'name')"
              @update:model-value="clearField('name')"
            />
            <label for="orgName">{{ t('orgSettings.nameLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'name')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'name') }}
          </small>
        </div>
        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <Textarea
              id="orgDesc"
              v-model="form.description"
              rows="3"
              maxlength="500"
              :disabled="!canEdit"
              class="w-full resize-none"
              :invalid="!!firstFieldError(fieldErrors, 'description')"
              @update:model-value="clearField('description')"
            />
            <label for="orgDesc">{{ t('orgSettings.descriptionLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'description')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'description') }}
          </small>
        </div>
      </div>

      <div class="border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">{{ t('orgSettings.membership') }}</h2>

        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <Select
              input-id="joinPolicy"
              v-model="form.joinPolicy"
              :options="joinPolicyOptions"
              option-label="label"
              option-value="value"
              :disabled="!canEdit"
              class="w-full"
              :invalid="!!firstFieldError(fieldErrors, 'joinPolicy')"
              @update:model-value="clearField('joinPolicy')"
            />
            <label for="joinPolicy">{{ t('orgSettings.joinPolicyLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'joinPolicy')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'joinPolicy') }}
          </small>
          <small class="text-xs text-ink-500">
            {{ t('orgSettings.joinPolicyHint') }}
          </small>
        </div>

        <div class="mt-5 flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <Select
              input-id="defaultRole"
              v-model="form.defaultOrgRoleId"
              :options="defaultRoleOptions"
              option-label="label"
              option-value="value"
              :disabled="!canEdit"
              class="w-full"
              :invalid="!!firstFieldError(fieldErrors, 'defaultOrgRoleId')"
              @update:model-value="clearField('defaultOrgRoleId')"
            />
            <label for="defaultRole">{{ t('orgSettings.defaultRoleLabel') }}</label>
          </FloatLabel>
          <small v-if="firstFieldError(fieldErrors, 'defaultOrgRoleId')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'defaultOrgRoleId') }}
          </small>
          <small class="text-xs text-ink-500">
            {{ t('orgSettings.defaultRoleHint') }}
          </small>
        </div>
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
