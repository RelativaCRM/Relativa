<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Select from 'primevue/select';
import Message from 'primevue/message';
import Skeleton from 'primevue/skeleton';
import { useOrganizationStore } from '@/stores/organization';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useApiErrorHandler } from '@/api/errorToast';

const orgStore = useOrganizationStore();
const toast = useToast();
const { notify } = useApiErrorHandler();

const loading = ref(false);
const submitting = ref(false);
const fieldErrors = ref<FieldErrors>({});

const canEdit = computed(() =>
  orgStore.currentOrg?.myPermissions?.includes('manage_org_settings') ?? false,
);

const joinPolicyOptions = [
  { label: 'Open — anyone can submit a join request', value: 'open' },
  { label: 'Invite only — join requests are blocked', value: 'invite_only' },
];

const defaultRoleOptions = computed(() => {
  const none = { label: 'System default (org_member)', value: null as number | null };
  return [
    none,
    ...orgStore.roles
      .filter((r) => !r.isSystem || r.name !== 'org_owner')
      .map((r) => ({ label: r.name, value: r.id })),
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
    notify(err, { fallback: 'Could not load organization settings.' });
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
      summary: 'Settings saved',
      detail: 'Organization settings have been updated.',
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
    <h1 class="text-2xl font-bold text-ink-900">Organization Settings</h1>
    <p class="mt-2 text-sm text-ink-500">
      Configure organization-wide behavior and policies.
    </p>

    <Message
      v-if="!canEdit && !loading"
      severity="info"
      :closable="false"
      class="mt-4"
    >
      You do not have permission to edit organization settings.
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
          <label for="orgName" class="text-xs font-medium text-ink-600">Organization name</label>
          <InputText
            id="orgName"
            v-model="form.name"
            :disabled="!canEdit"
            class="w-full"
            maxlength="100"
            placeholder="Organization name"
            :invalid="!!firstFieldError(fieldErrors, 'name')"
            @update:model-value="clearField('name')"
          />
          <small v-if="firstFieldError(fieldErrors, 'name')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'name') }}
          </small>
        </div>
        <div class="mt-4 flex flex-col gap-1.5">
          <label for="orgDesc" class="text-xs font-medium text-ink-600">Description</label>
          <Textarea
            id="orgDesc"
            v-model="form.description"
            rows="3"
            maxlength="500"
            :disabled="!canEdit"
            class="w-full resize-none"
            placeholder="Optional description shown to members"
            :invalid="!!firstFieldError(fieldErrors, 'description')"
            @update:model-value="clearField('description')"
          />
          <small v-if="firstFieldError(fieldErrors, 'description')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'description') }}
          </small>
        </div>
      </div>

      <!-- Membership -->

      <div class="rounded-xl border border-line bg-white p-6">
        <h2 class="text-sm font-semibold text-ink-900">Membership</h2>

        <div class="mt-4 flex flex-col gap-1.5">
          <label for="joinPolicy" class="text-xs font-medium text-ink-600">Join policy</label>
          <Select
            id="joinPolicy"
            v-model="form.joinPolicy"
            :options="joinPolicyOptions"
            option-label="label"
            option-value="value"
            :disabled="!canEdit"
            class="w-full"
            :invalid="!!firstFieldError(fieldErrors, 'joinPolicy')"
            @update:model-value="clearField('joinPolicy')"
          />
          <small v-if="firstFieldError(fieldErrors, 'joinPolicy')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'joinPolicy') }}
          </small>
          <small class="text-xs text-ink-500">
            When set to <strong>Invite only</strong>, users cannot submit join requests — only invited users can join.
          </small>
        </div>

        <div class="mt-4 flex flex-col gap-1.5">
          <label for="defaultRole" class="text-xs font-medium text-ink-600">
            Default member role
          </label>
          <Select
            id="defaultRole"
            v-model="form.defaultOrgRoleId"
            :options="defaultRoleOptions"
            option-label="label"
            option-value="value"
            :disabled="!canEdit"
            class="w-full"
            :invalid="!!firstFieldError(fieldErrors, 'defaultOrgRoleId')"
            @update:model-value="clearField('defaultOrgRoleId')"
          />
          <small v-if="firstFieldError(fieldErrors, 'defaultOrgRoleId')" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(fieldErrors, 'defaultOrgRoleId') }}
          </small>
          <small class="text-xs text-ink-500">
            Role assigned to new members when no specific role is specified (join request approvals, default invitations).
          </small>
        </div>
      </div>

      <div v-if="canEdit" class="flex justify-end">
        <Button type="submit" label="Save settings" :loading="submitting" />
      </div>
    </form>
  </section>
</template>
