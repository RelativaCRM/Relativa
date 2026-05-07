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
import { normalizeError } from '@/api/errors';

const router = useRouter();
const auth = useAuthStore();

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
    await auth.login({ email: form.email, password: form.password });
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
        label="Sign in"
        :loading="submitting"
        class="!h-11 !rounded-[10px] !font-semibold"
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
