<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { useToast } from 'primevue/usetoast';
import InputText from 'primevue/inputtext';
import DatePicker from 'primevue/datepicker';
import Dialog from 'primevue/dialog';
import PhoneInput from '@/components/feedback/PhoneInput.vue';
import FieldInfo from '@/components/feedback/FieldInfo.vue';
import TwoFactorIcon from '@/components/feedback/TwoFactorIcon.vue';
import MailIcon from '@/components/feedback/MailIcon.vue';
import TwoFactorSettings from '@/components/layout/TwoFactorSettings.vue';
import EmailAddresses from '@/components/layout/EmailAddresses.vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { authApi } from '@/api/auth';

const { t } = useI18n();
const auth = useAuthStore();
const orgStore = useOrganizationStore();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const router = useRouter();
const toast = useToast();

const today = new Date();

const profileForm = reactive({
  firstName: '',
  lastName: '',
  phone: '',
  birthDate: null as Date | null,
});
const profileSubmitting = ref(false);
const profileFieldErrors = ref<FieldErrors>({});

function fromIsoDate(iso?: string | null): Date | null {
  if (!iso) return null;
  const [y, m, d] = iso.split('-').map(Number);
  if (!y || !m || !d) return null;
  return new Date(y, m - 1, d);
}

function toIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

watch(
  () => auth.user,
  (u) => {
    if (u) {
      profileForm.firstName = u.firstName;
      profileForm.lastName = u.lastName;
      profileForm.phone = u.phone ?? '';
      profileForm.birthDate = fromIsoDate(u.dateOfBirth);
    }
  },
  { immediate: true },
);

onMounted(() => {
  auth.fetchProfile().catch(() => undefined);
});

const initials = computed(() => {
  const f = auth.user?.firstName?.[0] ?? '';
  const l = auth.user?.lastName?.[0] ?? '';
  const both = (f + l).toUpperCase();
  return both || (auth.user?.email?.[0] ?? '?').toUpperCase();
});

const fullName = computed(() =>
  [auth.user?.firstName, auth.user?.lastName].filter(Boolean).join(' ') || auth.user?.email || '',
);

function clearProfileField(field: string) {
  if (!profileFieldErrors.value[field]) return;
  const next = { ...profileFieldErrors.value };
  delete next[field];
  profileFieldErrors.value = next;
}

async function handleSaveProfile() {
  if (!auth.user) return;
  profileSubmitting.value = true;
  profileFieldErrors.value = {};
  try {
    await auth.updateProfile({
      firstName: profileForm.firstName.trim(),
      lastName: profileForm.lastName.trim(),
      phone: profileForm.phone ? profileForm.phone : null,
      dateOfBirth: profileForm.birthDate ? toIsoDate(profileForm.birthDate) : null,
    });
    toast.add({ severity: 'success', summary: t('account.saved'), detail: t('account.savedDetail'), life: 4000 });
  } catch (err) {
    const n = normalizeError(err, t('account.saveError'));
    profileFieldErrors.value = n.fieldErrors;
    if (!Object.keys(n.fieldErrors).length) {
      toast.add({ severity: 'error', summary: t('account.error'), detail: n.message, life: 6000 });
    }
  } finally {
    profileSubmitting.value = false;
  }
}

const passwordResetSending = ref(false);

async function handleSendPasswordReset() {
  if (!auth.user) return;
  passwordResetSending.value = true;
  try {
    await authApi.forgotPassword(auth.user.email);
    toast.add({
      severity: 'success',
      summary: t('account.passwordSent'),
      detail: t('account.passwordSentDetail', { email: auth.user.email }),
      life: 6000,
    });
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err, t('account.passwordError')).message, life: 6000 });
  } finally {
    passwordResetSending.value = false;
  }
}

const showDeleteDialog = ref(false);
const deleteSubmitting = ref(false);

async function handleConfirmDelete() {
  deleteSubmitting.value = true;
  try {
    await auth.deleteAccount();
    orgStore.clear();
    wsStore.clear();
    entityStore.clear();
    toast.add({ severity: 'success', summary: t('account.deleteClosed'), detail: t('account.deleteClosedDetail'), life: 4000 });
    showDeleteDialog.value = false;
    await router.push({ name: 'login' });
  } catch (err) {
    toast.add({ severity: 'error', summary: t('account.error'), detail: normalizeError(err).message, life: 6000 });
  } finally {
    deleteSubmitting.value = false;
  }
}
</script>

<template>
  <section class="mx-auto max-w-2xl pb-16">
    <header class="flex flex-col items-center text-center pt-2">
      <div class="w-20 h-20 rounded-full bg-brand-600 text-white text-2xl font-semibold flex items-center justify-center shadow-sm">
        {{ initials }}
      </div>
      <h1 class="mt-3 text-xl font-bold text-ink-900">{{ fullName }}</h1>
      <p class="text-sm text-ink-500">{{ auth.user?.email }}</p>
    </header>

    <div class="mt-8 border border-line bg-white">
      <div class="flex items-center gap-2 px-6 py-4 border-b border-line">
        <i class="pi pi-user text-brand-600" />
        <h2 class="text-sm font-semibold text-ink-900">{{ t('account.profileTitle') }}</h2>
      </div>
      <form class="p-6 flex flex-col gap-4" novalidate @submit.prevent="handleSaveProfile">
        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">{{ t('account.firstName') }}</label>
            <InputText
              v-model="profileForm.firstName"
              maxlength="100"
              class="!h-10"
              :invalid="!!firstFieldError(profileFieldErrors, 'firstName')"
              @update:model-value="clearProfileField('firstName')"
            />
            <small v-if="firstFieldError(profileFieldErrors, 'firstName')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(profileFieldErrors, 'firstName') }}
            </small>
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-ink-600">{{ t('account.lastName') }}</label>
            <InputText
              v-model="profileForm.lastName"
              maxlength="100"
              class="!h-10"
              :invalid="!!firstFieldError(profileFieldErrors, 'lastName')"
              @update:model-value="clearProfileField('lastName')"
            />
            <small v-if="firstFieldError(profileFieldErrors, 'lastName')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(profileFieldErrors, 'lastName') }}
            </small>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1.5">
            <div class="flex items-center gap-1.5">
              <label class="text-xs font-medium text-ink-600">{{ t('account.phone') }}</label>
              <FieldInfo :text="t('auth.phoneInfo')" />
            </div>
            <PhoneInput v-model="profileForm.phone" dense @update:model-value="clearProfileField('phone')" />
            <small v-if="firstFieldError(profileFieldErrors, 'phone')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(profileFieldErrors, 'phone') }}
            </small>
          </div>
          <div class="flex flex-col gap-1.5">
            <div class="flex items-center gap-1.5">
              <label for="acctDob" class="text-xs font-medium text-ink-600">{{ t('account.dateOfBirth') }}</label>
              <FieldInfo :text="t('auth.birthDateInfo')" />
            </div>
            <DatePicker
              v-model="profileForm.birthDate"
              input-id="acctDob"
              date-format="yy-mm-dd"
              :max-date="today"
              show-icon
              :manual-input="true"
              input-class="!h-10"
              class="w-full"
              @update:model-value="clearProfileField('dateOfBirth')"
            />
            <small v-if="firstFieldError(profileFieldErrors, 'dateOfBirth')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(profileFieldErrors, 'dateOfBirth') }}
            </small>
          </div>
        </div>

        <div class="flex justify-end">
          <button type="submit" class="btn btn-primary" :disabled="profileSubmitting">
            <i :class="profileSubmitting ? 'pi pi-spin pi-spinner' : 'pi pi-check'" />
            {{ t('account.save') }}
          </button>
        </div>
      </form>
    </div>

    <div class="mt-6 border border-line bg-white">
      <div class="flex items-center gap-2 px-6 py-4 border-b border-line">
        <MailIcon :size="18" class="text-brand-600" />
        <h2 class="text-sm font-semibold text-ink-900">{{ t('account.emails.title') }}</h2>
      </div>
      <div class="p-6">
        <p class="text-[13px] text-ink-500 mb-4">{{ t('account.emails.intro') }}</p>
        <EmailAddresses />
      </div>
    </div>

    <div class="mt-6 border border-line bg-white">
      <div class="flex items-center gap-2 px-6 py-4 border-b border-line">
        <i class="pi pi-shield text-brand-600" />
        <h2 class="text-sm font-semibold text-ink-900">{{ t('account.securityTitle') }}</h2>
      </div>
      <div class="divide-y divide-line">
        <div class="px-6 py-5 flex items-center justify-between gap-4">
          <div>
            <p class="text-sm font-medium text-ink-800">{{ t('account.passwordTitle') }}</p>
            <i18n-t keypath="account.passwordIntro" tag="p" class="mt-0.5 text-[13px] text-ink-500" scope="global">
              <template #email><span class="font-medium text-ink-700">{{ auth.user?.email }}</span></template>
            </i18n-t>
          </div>
          <button class="btn btn-outline btn-sm shrink-0" :disabled="passwordResetSending" @click="handleSendPasswordReset">
            <i :class="passwordResetSending ? 'pi pi-spin pi-spinner' : 'pi pi-envelope'" />
            {{ t('account.passwordSend') }}
          </button>
        </div>

        <div class="px-6 py-5">
          <div class="flex items-start gap-3">
            <div class="w-11 h-11 shrink-0 rounded-full bg-brand-50 flex items-center justify-center text-brand-600">
              <TwoFactorIcon :size="24" />
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium text-ink-800">{{ t('account.twoFactorTitle') }}</p>
              <p class="mt-0.5 text-[13px] text-ink-500 leading-relaxed">{{ t('account.twoFactorIntro') }}</p>
            </div>
          </div>
          <div class="mt-4">
            <TwoFactorSettings />
          </div>
        </div>
      </div>
    </div>

    <div class="mt-6 flex items-center justify-between gap-4 px-1">
      <p class="text-[13px] text-ink-500">{{ t('account.closeIntro') }}</p>
      <button class="btn btn-danger btn-sm shrink-0" @click="showDeleteDialog = true">
        <i class="pi pi-trash" />
        {{ t('account.delete') }}
      </button>
    </div>

    <Dialog v-model:visible="showDeleteDialog" :header="t('account.deleteDialogTitle')" modal :style="{ width: '420px' }">
      <p class="text-sm text-ink-600 leading-relaxed">{{ t('account.deleteDialogWarn') }}</p>
      <div class="flex justify-end gap-2 mt-6">
        <button class="btn btn-outline btn-sm" :disabled="deleteSubmitting" @click="showDeleteDialog = false">
          {{ t('account.cancel') }}
        </button>
        <button class="btn btn-danger btn-sm" :disabled="deleteSubmitting" @click="handleConfirmDelete">
          <i v-if="deleteSubmitting" class="pi pi-spin pi-spinner" />
          {{ t('account.deleteConfirm') }}
        </button>
      </div>
    </Dialog>
  </section>
</template>
