<script setup lang="ts">
import { reactive, ref, computed } from 'vue';
import { useRouter, useRoute, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { normalizeError } from '@/api/errors';

const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const resetSuccess = computed(() => route.query.reset === 'success');

const form = reactive({
  email: '',
  password: '',
  rememberMe: true,
});
const submitting = ref(false);
const serverError = ref<string | null>(null);
const showValidation = ref(false);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const emailInvalid = computed(
  () => form.email.length > 0 && !emailPattern.test(form.email),
);
const isFormValid = computed(
  () => emailPattern.test(form.email) && form.password.length > 0,
);

async function handleSubmit() {
  showValidation.value = true;
  if (!isFormValid.value || submitting.value) {
    if (!form.email && !form.password) {
      serverError.value = 'Please enter your email and password to sign in.';
    } else if (!emailPattern.test(form.email)) {
      serverError.value = 'Please enter a valid email address.';
    } else if (!form.password) {
      serverError.value = 'Please enter your password.';
    }
    return;
  }
  serverError.value = null;
  submitting.value = true;
  try {
    await auth.login({ email: form.email, password: form.password }, form.rememberMe);
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch (err) {
    const normalized = normalizeError(err, 'Sign in failed.');
    serverError.value = normalized.isUnauthorized
      ? 'Invalid email or password.'
      : normalized.message;
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <AuthLayout>
    <h1 class="text-[22px] font-bold text-ink-900 leading-[33px]">
      Welcome back
    </h1>

    <Message v-if="resetSuccess" severity="success" :closable="false" class="mt-4 !mb-0">
      Your password has been reset. You can now sign in.
    </Message>

    <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
      <div class="flex flex-col gap-1.5">
        <label for="email" class="text-xs font-medium text-ink-600">
          Email address
        </label>
        <InputText
          id="email"
          v-model="form.email"
          type="email"
          autocomplete="email"
          :invalid="emailInvalid || (showValidation && !form.email)"
          class="!h-10"
        />
        <small v-if="emailInvalid" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />Invalid email format
        </small>
      </div>

      <div class="flex flex-col gap-1.5">
        <label for="password" class="text-xs font-medium text-ink-600">
          Password
        </label>
        <Password
          v-model="form.password"
          input-id="password"
          :feedback="false"
          toggle-mask
          autocomplete="current-password"
          input-class="!h-10 w-full"
          class="w-full"
          :invalid="showValidation && !form.password"
        />
      </div>

      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <input
            id="remember"
            v-model="form.rememberMe"
            type="checkbox"
          />
          <label for="remember" class="text-[13px] font-medium text-ink-600 cursor-pointer">
            Remember me
          </label>
        </div>
        <RouterLink :to="{ name: 'forgot-password' }" class="text-[13px] font-medium text-brand-600 hover:underline">
          Forgot password?
        </RouterLink>
      </div>

      <Message
        v-if="serverError"
        severity="error"
        :closable="false"
        class="!my-0"
      >
        {{ serverError }}
      </Message>

      <Button
        type="submit"
        label="Sign in"
        :loading="submitting"
        :disabled="!isFormValid"
        :class="[
          '!h-11 !rounded-none !font-semibold w-full transition-colors',
          isFormValid
            ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
            : '!bg-slate-200 !border-slate-200 !text-slate-400',
        ]"
      />

      <p class="text-center text-[13px] text-ink-500">
        Don’t have an account?
        <RouterLink
          :to="{ name: 'register' }"
          class="font-medium text-brand-600 hover:underline"
        >
          Sign up
        </RouterLink>
      </p>
    </form>
  </AuthLayout>
</template>

<style scoped>
input[type='checkbox'] {
  appearance: none;
  -webkit-appearance: none;
  width: 1.25rem;
  height: 1.25rem;
  border: 1.5px solid #64748b;
  background: #fff;
  cursor: pointer;
  flex-shrink: 0;
  position: relative;
  transition: background 0.15s, border-color 0.15s;
}

input[type='checkbox']:checked {
  background: #2563eb;
  border-color: #2563eb;
}

input[type='checkbox']:checked::after {
  content: '';
  position: absolute;
  left: 50%;
  top: 50%;
  width: 6px;
  height: 10px;
  border: 2px solid #fff;
  border-top: none;
  border-left: none;
  transform: translate(-50%, -60%) rotate(45deg);
}

input[type='checkbox']:focus-visible {
  outline: 2px solid #2563eb;
  outline-offset: 2px;
}
</style>
