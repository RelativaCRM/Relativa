<script setup lang="ts">
import { reactive, ref, computed } from 'vue';
import { useRouter, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Checkbox from 'primevue/checkbox';
import Message from 'primevue/message';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { ApiError } from '@/api/http';

const router = useRouter();
const auth = useAuthStore();

const form = reactive({
  email: '',
  password: '',
  rememberMe: true,
});
const submitting = ref(false);
const serverError = ref<string | null>(null);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const emailInvalid = computed(
  () => form.email.length > 0 && !emailPattern.test(form.email),
);
const canSubmit = computed(
  () =>
    !submitting.value &&
    emailPattern.test(form.email) &&
    form.password.length > 0,
);

async function handleSubmit() {
  if (!canSubmit.value) return;
  serverError.value = null;
  submitting.value = true;
  try {
    await auth.login({ email: form.email, password: form.password });
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch (err) {
    if (err instanceof ApiError) {
      serverError.value =
        err.status === 401
          ? 'Invalid email or password.'
          : err.message || 'Sign in failed.';
    } else {
      serverError.value = 'Network error. Please try again.';
    }
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
    <p class="mt-1 text-[13px] text-ink-500">
      Sign in to your Relativa workspace
    </p>

    <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
      <div class="flex flex-col gap-1.5">
        <label for="email" class="text-xs font-medium text-ink-600">
          Email address <span class="text-danger">*</span>
        </label>
        <InputText
          id="email"
          v-model="form.email"
          type="email"
          autocomplete="email"
          :invalid="emailInvalid"
          placeholder="you@example.com"
          class="!h-10"
        />
        <small v-if="emailInvalid" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />Invalid email format
        </small>
      </div>

      <div class="flex flex-col gap-1.5">
        <label for="password" class="text-xs font-medium text-ink-600">
          Password <span class="text-danger">*</span>
        </label>
        <Password
          v-model="form.password"
          input-id="password"
          :feedback="false"
          toggle-mask
          autocomplete="current-password"
          placeholder="••••••••"
          input-class="!h-10 w-full"
          class="w-full"
        />
      </div>

      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Checkbox v-model="form.rememberMe" input-id="remember" binary />
          <label for="remember" class="text-[13px] font-medium text-ink-600">
            Remember me
          </label>
        </div>
        <a href="#" class="text-[13px] font-medium text-brand-600 hover:underline">
          Forgot password?
        </a>
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
        :disabled="!canSubmit"
        :loading="submitting"
        class="!h-11 !rounded-[10px] !font-semibold"
      >
        {{ canSubmit ? 'Sign in to Relativa' : 'Fill all required fields' }}
      </Button>

      <p class="text-center text-[13px] text-ink-500">
        Don’t have an account?
        <RouterLink
          :to="{ name: 'register' }"
          class="font-medium text-brand-600 hover:underline"
        >
          Create one
        </RouterLink>
      </p>
    </form>
  </AuthLayout>
</template>
