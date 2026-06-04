<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter, useRoute, RouterLink } from 'vue-router';
import Password from 'primevue/password';
import Button from 'primevue/button';
import AuthLayout from '@/layouts/AuthLayout.vue';
import FormError from '@/components/feedback/FormError.vue';
import { authApi } from '@/api/auth';
import { normalizeError } from '@/api/errors';

const { t } = useI18n();
const router = useRouter();
const route = useRoute();

const token = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const submitting = ref(false);
const validating = ref(true);
const serverError = ref<string | null>(null);
const expired = ref(false);

onMounted(async () => {
  const t = route.query.token;
  if (!t || typeof t !== 'string') {
    router.replace({ name: 'forgot-password' });
    return;
  }
  token.value = t;
  try {
    await authApi.validateResetToken(t);
  } catch {
    expired.value = true;
  } finally {
    validating.value = false;
  }
});

const passwordsMatch = computed(
  () => newPassword.value.length > 0 && newPassword.value === confirmPassword.value,
);
const confirmMismatch = computed(
  () => confirmPassword.value.length > 0 && newPassword.value !== confirmPassword.value,
);
const isValid = computed(
  () => newPassword.value.length >= 8 && passwordsMatch.value,
);

async function handleSubmit() {
  if (!isValid.value || submitting.value) return;
  serverError.value = null;
  submitting.value = true;
  try {
    await authApi.resetPassword(token.value, newPassword.value);
    await router.push({ name: 'login', query: { reset: 'success' } });
  } catch (err) {
    const normalized = normalizeError(err);
    if (normalized.isValidation) {
      expired.value = true;
    } else {
      serverError.value = normalized.message;
    }
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <AuthLayout>
    <template v-if="validating">
      <div class="flex justify-center py-10">
        <i class="pi pi-spin pi-spinner text-2xl text-ink-400" />
      </div>
    </template>

    <template v-else-if="expired">
      <div class="flex flex-col items-center text-center pt-2 pb-1">
        <div class="w-12 h-12 rounded-full bg-red-50 flex items-center justify-center mb-4">
          <i class="pi pi-clock text-danger text-xl" />
        </div>
        <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">Link expired</h1>
        <p class="mt-2 text-[13px] text-ink-500 leading-relaxed">
          This password reset link is invalid or has already been used.
          Reset links expire after 1 hour.
        </p>
        <RouterLink
          :to="{ name: 'forgot-password' }"
          class="mt-5 inline-block text-[13px] font-medium text-brand-600 hover:underline"
        >
          Request a new reset link
        </RouterLink>
        <div class="mt-3 border-t border-line w-full pt-4">
          <RouterLink :to="{ name: 'login' }" class="text-[13px] text-ink-500 hover:text-ink-700">
            Back to sign in
          </RouterLink>
        </div>
      </div>
    </template>

    <template v-else-if="!expired">
      <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
        Set a new password
      </h1>
      <p class="mt-1 text-[13px] text-ink-500">
        Choose a strong password for your account.
      </p>

      <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
        <div class="flex flex-col gap-1.5">
          <label for="newPassword" class="text-xs font-medium text-ink-600">
            New password
          </label>
          <Password
            v-model="newPassword"
            input-id="newPassword"
            :feedback="false"
            toggle-mask
            autocomplete="new-password"
            placeholder="At least 8 characters"
            input-class="!h-10 w-full"
            class="w-full"
          />
          <small v-if="newPassword.length > 0 && newPassword.length < 8" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />Password must be at least 8 characters
          </small>
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="confirmPassword" class="text-xs font-medium text-ink-600">
            Confirm password
          </label>
          <Password
            v-model="confirmPassword"
            input-id="confirmPassword"
            :feedback="false"
            toggle-mask
            autocomplete="new-password"
            input-class="!h-10 w-full"
            class="w-full"
            :invalid="confirmMismatch"
          />
          <small v-if="confirmMismatch" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />Passwords do not match
          </small>
        </div>

        <FormError v-if="serverError" :message="serverError" />

        <Button
          type="submit"
          label="Reset password"
          :loading="submitting"
          :class="[
            '!h-11 !rounded-none !font-semibold w-full transition-colors',
            isValid
              ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
              : '!bg-slate-200 !border-slate-200 !text-slate-400',
          ]"
        />
      </form>
    </template>
  </AuthLayout>
</template>
