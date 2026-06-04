<script setup lang="ts">
import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import FloatLabel from 'primevue/floatlabel';
import AuthLayout from '@/layouts/AuthLayout.vue';
import FormError from '@/components/feedback/FormError.vue';
import { authApi } from '@/api/auth';
import { normalizeError } from '@/api/errors';

const { t } = useI18n();

const email = ref('');
const submitting = ref(false);
const submitted = ref(false);
const serverError = ref<string | null>(null);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const emailInvalid = computed(() => email.value.length > 0 && !emailPattern.test(email.value));
const isValid = computed(() => emailPattern.test(email.value));

async function handleSubmit() {
  if (!isValid.value || submitting.value) return;
  serverError.value = null;
  submitting.value = true;
  try {
    await authApi.forgotPassword(email.value);
    submitted.value = true;
  } catch (err) {
    serverError.value = normalizeError(err).message;
  } finally {
    submitting.value = false;
  }
}

function tryAgain() {
  submitted.value = false;
  email.value = '';
}
</script>

<template>
  <AuthLayout>
    <template v-if="submitted">
      <div class="flex flex-col items-center text-center pt-2 pb-1">
        <div class="w-12 h-12 rounded-full bg-brand-50 flex items-center justify-center mb-4">
          <i class="pi pi-envelope text-brand-600 text-xl" />
        </div>
        <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">{{ t('auth.checkInboxTitle') }}</h1>
        <i18n-t keypath="auth.checkInboxBody" tag="p" class="mt-2 text-[13px] text-ink-500 leading-relaxed" scope="global">
          <template #email>
            <span class="font-medium text-ink-700">{{ email }}</span>
          </template>
        </i18n-t>
        <button
          type="button"
          class="mt-5 text-[13px] text-brand-600 hover:underline font-medium"
          @click="tryAgain"
        >
          {{ t('auth.tryDifferentEmail') }}
        </button>
        <div class="mt-3 border-t border-line w-full pt-4">
          <RouterLink :to="{ name: 'login' }" class="text-[13px] text-ink-500 hover:text-ink-700">
            {{ t('auth.backToSignIn') }}
          </RouterLink>
        </div>
      </div>
    </template>

    <template v-else>
      <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
        {{ t('auth.forgotTitle') }}
      </h1>
      <p class="mt-1 text-[13px] text-ink-500">
        {{ t('auth.forgotSubtitle') }}
      </p>

      <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
        <div class="flex flex-col gap-1.5">
          <FloatLabel variant="on">
            <InputText
              id="email"
              v-model="email"
              type="email"
              autocomplete="email"
              :invalid="emailInvalid"
              class="!h-11 w-full"
            />
            <label for="email">{{ t('auth.emailLabel') }}</label>
          </FloatLabel>
          <small v-if="emailInvalid" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.invalidEmailFormat') }}
          </small>
        </div>

        <FormError v-if="serverError" :message="serverError" />

        <Button
          type="submit"
          label="Send reset link"
          :loading="submitting"
          :class="[
            '!h-11 !rounded-none !font-semibold w-full transition-colors',
            isValid
              ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
              : '!bg-slate-200 !border-slate-200 !text-slate-400',
          ]"
        />

        <p class="text-center text-[13px] text-ink-500">
          <RouterLink :to="{ name: 'login' }" class="font-medium text-brand-600 hover:underline">
            Back to sign in
          </RouterLink>
        </p>
      </form>
    </template>
  </AuthLayout>
</template>
