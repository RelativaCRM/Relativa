<script setup lang="ts">
import { reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import {
  normalizeError,
  firstFieldError,
  type FieldErrors,
} from '@/api/errors';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const router = useRouter();
const toast = useToast();

const profileForm = reactive({
  firstName: '',
  lastName: '',
});
const profileSubmitting = ref(false);
const profileFieldErrors = ref<FieldErrors>({});

watch(
  () => auth.user,
  (u) => {
    if (u) {
      profileForm.firstName = u.firstName;
      profileForm.lastName = u.lastName;
    }
  },
  { immediate: true },
);

function clearProfileField(field: 'firstName' | 'lastName') {
  if (!profileFieldErrors.value[field]) return;
  const next = { ...profileFieldErrors.value };
  delete next[field];
  profileFieldErrors.value = next;
}

const canSaveProfile = ref(true);

async function handleSaveProfile() {
  if (!auth.user) return;
  profileSubmitting.value = true;
  profileFieldErrors.value = {};
  try {
    await auth.updateProfile({
      firstName: profileForm.firstName.trim(),
      lastName: profileForm.lastName.trim(),
    });
    toast.add({
      severity: 'success',
      summary: 'Profile updated',
      detail: 'Your name has been saved.',
      life: 4000,
    });
  } catch (err) {
    const n = normalizeError(err, 'Could not update profile.');
    profileFieldErrors.value = n.fieldErrors;
    if (!Object.keys(n.fieldErrors).length) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: n.message,
        life: 6000,
      });
    }
  } finally {
    profileSubmitting.value = false;
  }
}

/* ── Delete account ───────────────────────────────────── */
const showDeleteDialog = ref(false);
const deleteSubmitting = ref(false);

async function handleConfirmDelete() {
  deleteSubmitting.value = true;
  try {
    await auth.deleteAccount();
    orgStore.clear();
    wsStore.clear();
    entityStore.clear();
    toast.add({
      severity: 'success',
      summary: 'Account closed',
      detail: 'Your session has ended.',
      life: 4000,
    });
    showDeleteDialog.value = false;
    await router.push({ name: 'login' });
  } catch (err) {
    const n = normalizeError(err, 'Could not delete account.');
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: n.message,
      life: 6000,
    });
  } finally {
    deleteSubmitting.value = false;
  }
}
</script>

<template>
  <section class="max-w-xl">
    <h1 class="text-2xl font-bold text-ink-900">Account</h1>
    <p class="mt-1 text-sm text-ink-500">
      Update how your name appears across Relativa. Email cannot be changed here.
    </p>

    <div class="mt-8 rounded-xl border border-line bg-white p-6">
      <h2 class="text-sm font-semibold text-ink-900">Profile</h2>
      <form
        class="mt-4 flex flex-col gap-4"
        novalidate
        @submit.prevent="handleSaveProfile"
      >
        <div class="flex flex-col gap-1.5">
          <label for="acctEmail" class="text-xs font-medium text-ink-600">
            Email
          </label>
          <InputText
            id="acctEmail"
            :model-value="auth.user?.email ?? ''"
            type="email"
            disabled
            class="!h-10 opacity-70"
          />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="acctFirst" class="text-xs font-medium text-ink-600">
            First name <span class="text-danger">*</span>
          </label>
          <InputText
            id="acctFirst"
            v-model="profileForm.firstName"
            maxlength="100"
            class="!h-10"
            :invalid="!!firstFieldError(profileFieldErrors, 'firstName')"
            @update:model-value="clearProfileField('firstName')"
          />
          <small
            v-if="firstFieldError(profileFieldErrors, 'firstName')"
            class="text-xs text-danger"
          >
            <i class="pi pi-exclamation-circle mr-1" />{{
              firstFieldError(profileFieldErrors, 'firstName')
            }}
          </small>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="acctLast" class="text-xs font-medium text-ink-600">
            Last name <span class="text-danger">*</span>
          </label>
          <InputText
            id="acctLast"
            v-model="profileForm.lastName"
            maxlength="100"
            class="!h-10"
            :invalid="!!firstFieldError(profileFieldErrors, 'lastName')"
            @update:model-value="clearProfileField('lastName')"
          />
          <small
            v-if="firstFieldError(profileFieldErrors, 'lastName')"
            class="text-xs text-danger"
          >
            <i class="pi pi-exclamation-circle mr-1" />{{
              firstFieldError(profileFieldErrors, 'lastName')
            }}
          </small>
        </div>
        <div class="flex justify-end">
          <Button
            type="submit"
            label="Save changes"
            :loading="profileSubmitting"
          />
        </div>
      </form>
    </div>

    <div class="mt-6 rounded-xl border border-danger/30 bg-white p-6">
      <h2 class="text-sm font-semibold text-danger">Danger zone</h2>
      <p class="mt-2 text-sm text-ink-600">
        Permanently close your account. You will be signed out and will need a
        new invitation or join request to access organizations again.
      </p>
      <Button
        class="mt-4"
        label="Delete account"
        severity="danger"
        outlined
        @click="showDeleteDialog = true"
      />
    </div>

    <Dialog
      v-model:visible="showDeleteDialog"
      header="Delete account?"
      modal
      :style="{ width: '420px' }"
    >
      <Message severity="warn" :closable="false" class="!my-0">
        This archives your user record. This action cannot be undone from the
        app.
      </Message>
      <div class="flex justify-end gap-2 mt-6">
        <Button
          label="Cancel"
          severity="secondary"
          text
          :disabled="deleteSubmitting"
          @click="showDeleteDialog = false"
        />
        <Button
          label="Delete my account"
          severity="danger"
          :loading="deleteSubmitting"
          @click="handleConfirmDelete"
        />
      </div>
    </Dialog>
  </section>
</template>
