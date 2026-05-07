<script setup lang="ts">
import { reactive, ref, computed } from 'vue';
import { useRouter, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/stores/auth';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';

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
const serverFieldErrors = ref<FieldErrors>({});

function clearFieldError(field: keyof typeof form) {
  if (serverFieldErrors.value[field]) {
    const next = { ...serverFieldErrors.value };
    delete next[field];
    serverFieldErrors.value = next;
  }
}

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const emailInvalid = computed(
  () => form.email.length > 0 && !emailPattern.test(form.email),
);
const passwordTooShort = computed(
  () => form.password.length > 0 && form.password.length < 8,
);
const firstNameTooShort = computed(
  () => form.firstName.trim().length > 0 && form.firstName.trim().length < 2,
);
const lastNameTooShort = computed(
  () => form.lastName.trim().length > 0 && form.lastName.trim().length < 2,
);

const canSubmit = computed(
  () =>
    !submitting.value &&
    form.firstName.trim().length >= 2 &&
    form.lastName.trim().length >= 2 &&
    emailPattern.test(form.email) &&
    form.password.length >= 8,
);

async function handleSubmit() {
  if (!canSubmit.value) return;
  serverError.value = null;
  successMessage.value = null;
  serverFieldErrors.value = {};
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
    const normalized = normalizeError(err, 'Registration failed.');
    serverFieldErrors.value = normalized.fieldErrors;
    serverError.value = normalized.isConflict
      ? 'A user with this email already exists.'
      : normalized.message;
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
            :invalid="firstNameTooShort || !!firstFieldError(serverFieldErrors, 'firstName')"
            @update:model-value="clearFieldError('firstName')"
          />
          <small v-if="firstNameTooShort" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />First name must be at
            least 2 characters
          </small>
          <small
            v-else-if="firstFieldError(serverFieldErrors, 'firstName')"
            class="text-xs text-danger"
          >
            <i class="pi pi-exclamation-circle mr-1" />{{
              firstFieldError(serverFieldErrors, 'firstName')
            }}
          </small>
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
            :invalid="lastNameTooShort || !!firstFieldError(serverFieldErrors, 'lastName')"
            @update:model-value="clearFieldError('lastName')"
          />
          <small v-if="lastNameTooShort" class="text-xs text-danger">
            <i class="pi pi-exclamation-circle mr-1" />Last name must be at
            least 2 characters
          </small>
          <small
            v-else-if="firstFieldError(serverFieldErrors, 'lastName')"
            class="text-xs text-danger"
          >
            <i class="pi pi-exclamation-circle mr-1" />{{
              firstFieldError(serverFieldErrors, 'lastName')
            }}
          </small>
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
          :invalid="emailInvalid || !!firstFieldError(serverFieldErrors, 'email')"
          placeholder="you@example.com"
          class="!h-10"
          @update:model-value="clearFieldError('email')"
        />
        <small v-if="emailInvalid" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />Invalid email format
        </small>
        <small
          v-else-if="firstFieldError(serverFieldErrors, 'email')"
          class="text-xs text-danger"
        >
          <i class="pi pi-exclamation-circle mr-1" />{{
            firstFieldError(serverFieldErrors, 'email')
          }}
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
          :invalid="!!firstFieldError(serverFieldErrors, 'password')"
          @update:model-value="clearFieldError('password')"
        />
        <small v-if="passwordTooShort" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />Password must be at least 8 characters
        </small>
        <small
          v-else-if="firstFieldError(serverFieldErrors, 'password')"
          class="text-xs text-danger"
        >
          <i class="pi pi-exclamation-circle mr-1" />{{
            firstFieldError(serverFieldErrors, 'password')
          }}
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
        :loading="submitting"
        :class="[
          '!h-11 !rounded-none !font-semibold w-full transition-colors',
          canSubmit
            ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
            : '!bg-slate-200 !border-slate-200 !text-slate-400',
        ]"
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
