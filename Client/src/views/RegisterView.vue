<script setup lang="ts">
import { reactive, ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter, useRoute, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';
import FloatLabel from 'primevue/floatlabel';
import DatePicker from 'primevue/datepicker';
import AuthLayout from '@/layouts/AuthLayout.vue';
import EmailVerifyPanel from '@/components/feedback/EmailVerifyPanel.vue';
import PhoneInput from '@/components/feedback/PhoneInput.vue';
import FieldInfo from '@/components/feedback/FieldInfo.vue';
import { useAuthStore } from '@/stores/auth';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { EMAIL_PATTERN } from '@/utils/email';

const { t } = useI18n();
const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const prefillEmail = typeof route.query.email === 'string' ? route.query.email : '';
const form = reactive({
  firstName: '',
  lastName: '',
  birthDate: null as Date | null,
  email: prefillEmail,
  phone: '',
  password: '',
});
const step = ref<'identity' | 'credentials'>('identity');
const submitting = ref(false);
const serverError = ref<string | null>(null);
const serverFieldErrors = ref<FieldErrors>({});
const registered = ref(false);

const today = new Date();

function clearFieldError(field: string) {
  if (serverFieldErrors.value[field]) {
    const next = { ...serverFieldErrors.value };
    delete next[field];
    serverFieldErrors.value = next;
  }
}

const firstNameTooShort = computed(
  () => form.firstName.trim().length > 0 && form.firstName.trim().length < 2,
);
const lastNameTooShort = computed(
  () => form.lastName.trim().length > 0 && form.lastName.trim().length < 2,
);
const emailInvalid = computed(
  () => form.email.length > 0 && !EMAIL_PATTERN.test(form.email),
);
const passwordTooShort = computed(
  () => form.password.length > 0 && form.password.length < 8,
);

const identityValid = computed(
  () =>
    form.firstName.trim().length >= 2 &&
    form.lastName.trim().length >= 2 &&
    form.birthDate !== null &&
    form.birthDate < today,
);
const canSubmit = computed(
  () =>
    !submitting.value &&
    EMAIL_PATTERN.test(form.email) &&
    form.phone.length > 0 &&
    form.password.length >= 8,
);

function toIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function goToCredentials() {
  if (identityValid.value) step.value = 'credentials';
}

async function handleSubmit() {
  if (!canSubmit.value || !form.birthDate) return;
  serverError.value = null;
  serverFieldErrors.value = {};
  submitting.value = true;
  try {
    await auth.register({
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      email: form.email,
      password: form.password,
      phone: form.phone,
      dateOfBirth: toIsoDate(form.birthDate),
    });
    registered.value = true;
  } catch (err) {
    const normalized = normalizeError(err, t('auth.registrationFailed'));
    serverFieldErrors.value = normalized.fieldErrors;
    serverError.value = normalized.isConflict
      ? t('auth.emailExists')
      : normalized.message;
  } finally {
    submitting.value = false;
  }
}

async function onVerified() {
  try {
    await auth.login({ email: form.email, password: form.password });
    router.push({ name: 'home' });
  } catch {
    router.push({ name: 'login' });
  }
}
</script>

<template>
  <AuthLayout>
    <div v-if="registered">
      <EmailVerifyPanel :email="form.email" :phone="form.phone" @verified="onVerified" />
      <div class="mt-4 border-t border-line w-full pt-4 text-center">
        <RouterLink :to="{ name: 'login' }" class="text-[13px] text-ink-500 hover:text-ink-700">
          {{ t('auth.backToSignIn') }}
        </RouterLink>
      </div>
    </div>

    <template v-else>
      <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
        {{ t('auth.createTitle') }}
      </h1>
      <p class="mt-1 text-[13px] text-ink-500">{{ t('auth.createSubtitle') }}</p>

      <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
        <template v-if="step === 'identity'">
          <div class="grid grid-cols-2 gap-3">
            <div class="flex flex-col gap-1.5">
              <FloatLabel variant="on">
                <InputText id="firstName" v-model="form.firstName" autocomplete="given-name" class="!h-11 w-full" :invalid="firstNameTooShort" />
                <label for="firstName">{{ t('auth.firstNameLabel') }}</label>
              </FloatLabel>
              <small v-if="firstNameTooShort" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.firstNameTooShort') }}
              </small>
            </div>
            <div class="flex flex-col gap-1.5">
              <FloatLabel variant="on">
                <InputText id="lastName" v-model="form.lastName" autocomplete="family-name" class="!h-11 w-full" :invalid="lastNameTooShort" />
                <label for="lastName">{{ t('auth.lastNameLabel') }}</label>
              </FloatLabel>
              <small v-if="lastNameTooShort" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.lastNameTooShort') }}
              </small>
            </div>
          </div>

          <div class="flex flex-col gap-1.5">
            <div class="flex items-center gap-1.5">
              <label for="birthDate" class="text-xs font-medium text-ink-600">{{ t('auth.birthDateLabel') }}</label>
              <FieldInfo :text="t('auth.birthDateInfo')" />
            </div>
            <DatePicker
              v-model="form.birthDate"
              input-id="birthDate"
              date-format="yy-mm-dd"
              :max-date="today"
              show-icon
              :manual-input="true"
              :placeholder="t('auth.birthDatePlaceholder')"
              input-class="!h-11"
              class="w-full"
            />
          </div>

          <Button
            type="button"
            :label="t('auth.next')"
            :disabled="!identityValid"
            :class="[
              '!h-11 !rounded-none !font-semibold w-full transition-colors',
              identityValid
                ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
                : '!bg-slate-200 !border-slate-200 !text-slate-400',
            ]"
            @click="goToCredentials"
          />
        </template>

        <template v-else>
          <div class="flex flex-col gap-1.5">
            <FloatLabel variant="on">
              <InputText
                id="email"
                v-model="form.email"
                type="email"
                autocomplete="email"
                :invalid="emailInvalid || !!firstFieldError(serverFieldErrors, 'email')"
                class="!h-11 w-full"
                @update:model-value="clearFieldError('email')"
              />
              <label for="email">{{ t('auth.emailLabel') }}</label>
            </FloatLabel>
            <small v-if="emailInvalid" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.invalidEmailFormat') }}
            </small>
            <small v-else-if="firstFieldError(serverFieldErrors, 'email')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(serverFieldErrors, 'email') }}
            </small>
          </div>

          <div class="flex flex-col gap-1.5">
            <div class="flex items-center gap-1.5">
              <label class="text-xs font-medium text-ink-600">{{ t('auth.phoneLabel') }}</label>
              <FieldInfo :text="t('auth.phoneInfo')" />
            </div>
            <PhoneInput v-model="form.phone" />
            <small v-if="firstFieldError(serverFieldErrors, 'phone')" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ firstFieldError(serverFieldErrors, 'phone') }}
            </small>
          </div>

          <div class="flex flex-col gap-1.5">
            <FloatLabel variant="on">
              <Password
                v-model="form.password"
                input-id="password"
                toggle-mask
                autocomplete="new-password"
                input-class="!h-11 w-full"
                class="w-full"
                :invalid="passwordTooShort"
              />
              <label for="password">{{ t('auth.passwordLabel') }}</label>
            </FloatLabel>
            <small v-if="passwordTooShort" class="text-xs text-danger">
              <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.passwordTooShort') }}
            </small>
          </div>

          <Message v-if="serverError" severity="error" :closable="false" class="!my-0">
            {{ serverError }}
          </Message>

          <Button
            type="submit"
            :loading="submitting"
            :disabled="!canSubmit"
            :class="[
              '!h-11 !rounded-none !font-semibold w-full transition-colors',
              canSubmit
                ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
                : '!bg-slate-200 !border-slate-200 !text-slate-400',
            ]"
          >
            {{ t('auth.createAccount') }}
          </Button>

          <button type="button" class="text-[13px] text-brand-600 hover:underline font-medium" @click="step = 'identity'">
            {{ t('common.back') }}
          </button>
        </template>

        <p class="text-center text-[13px] text-ink-500">
          {{ t('auth.haveAccount') }}
          <RouterLink :to="{ name: 'login' }" class="font-medium text-brand-600 hover:underline">
            {{ t('auth.signIn') }}
          </RouterLink>
        </p>
      </form>
    </template>
  </AuthLayout>
</template>
