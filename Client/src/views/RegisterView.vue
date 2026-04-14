<script setup lang="ts">
import { reactive, ref, computed } from 'vue';
import { useRouter, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { ApiError } from '@/api/http';

const router = useRouter();
const auth = useAuthStore();

const form = reactive({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
});
const submitting = ref(false);
const serverError = ref<string | null>(null);
const successMessage = ref<string | null>(null);

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const emailInvalid = computed(
  () => form.email.length > 0 && !emailPattern.test(form.email),
);
const passwordTooShort = computed(
  () => form.password.length > 0 && form.password.length < 8,
);

const canSubmit = computed(
  () =>
    !submitting.value &&
    form.firstName.trim().length > 0 &&
    form.lastName.trim().length > 0 &&
    emailPattern.test(form.email) &&
    form.password.length >= 8,
);

async function handleSubmit() {
  if (!canSubmit.value) return;
  serverError.value = null;
  successMessage.value = null;
  submitting.value = true;
  try {
    await auth.register({
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      email: form.email,
      password: form.password,
    });
    await auth.login({ email: form.email, password: form.password });
    router.push({ name: 'home' });
  } catch (err) {
    if (err instanceof ApiError) {
      serverError.value =
        err.status === 409
          ? 'A user with this email already exists.'
          : err.message || 'Registration failed.';
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
      Create your account
    </h1>
    <p class="mt-1 text-[13px] text-ink-500">
      Start building your Relativa workspace
    </p>

    <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="handleSubmit">
      <div class="grid grid-cols-2 gap-3">
        <div class="flex flex-col gap-1.5">
          <label for="firstName" class="text-xs font-medium text-ink-600">
            First name <span class="text-danger">*</span>
          </label>
          <InputText
            id="firstName"
            v-model="form.firstName"
            autocomplete="given-name"
            placeholder="Jane"
            class="!h-10"
          />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="lastName" class="text-xs font-medium text-ink-600">
            Last name <span class="text-danger">*</span>
          </label>
          <InputText
            id="lastName"
            v-model="form.lastName"
            autocomplete="family-name"
            placeholder="Doe"
            class="!h-10"
          />
        </div>
      </div>

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
          toggle-mask
          autocomplete="new-password"
          placeholder="At least 8 characters"
          input-class="!h-10 w-full"
          class="w-full"
        />
        <small v-if="passwordTooShort" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />Password must be at least 8 characters
        </small>
      </div>

      <Message
        v-if="serverError"
        severity="error"
        :closable="false"
        class="!my-0"
      >
        {{ serverError }}
      </Message>
      <Message
        v-if="successMessage"
        severity="success"
        :closable="false"
        class="!my-0"
      >
        {{ successMessage }}
      </Message>

      <Button
        type="submit"
        :disabled="!canSubmit"
        :loading="submitting"
        class="!h-11 !rounded-[10px] !font-semibold"
      >
        {{ canSubmit ? 'Create account' : 'Fill all required fields' }}
      </Button>

      <p class="text-center text-[13px] text-ink-500">
        Already have an account?
        <RouterLink
          :to="{ name: 'login' }"
          class="font-medium text-brand-600 hover:underline"
        >
          Sign in
        </RouterLink>
      </p>
    </form>
  </AuthLayout>
</template>
