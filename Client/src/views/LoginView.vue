<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter, useRoute, RouterLink } from 'vue-router';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Popover from 'primevue/popover';
import FloatLabel from 'primevue/floatlabel';
import AuthLayout from '@/layouts/AuthLayout.vue';
import FormError from '@/components/feedback/FormError.vue';
import FormSuccess from '@/components/feedback/FormSuccess.vue';
import EmailVerifyPanel from '@/components/feedback/EmailVerifyPanel.vue';
import TwoFactorChallenge from '@/components/feedback/TwoFactorChallenge.vue';
import { useAuthStore } from '@/stores/auth';
import { normalizeError } from '@/api/errors';
import { EMAIL_PATTERN } from '@/utils/email';
import { useOAuth } from '@/composables/useOAuth';

const { t } = useI18n();
const router = useRouter();
const rememberInfo = ref<InstanceType<typeof Popover>>();
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
const needsVerification = ref(false);
const needsTwoFactor = ref(false);
const twoFactorError = ref<string | null>(null);

const emailInvalid = computed(
  () => form.email.length > 0 && !EMAIL_PATTERN.test(form.email),
);
const isStepValid = computed(
  () => EMAIL_PATTERN.test(form.email) && form.password.length > 0,
);

async function signIn() {
  showValidation.value = true;
  if (!form.password) {
    serverError.value = t('auth.enterPassword');
    return;
  }
  serverError.value = null;
  needsVerification.value = false;
  submitting.value = true;
  try {
    await auth.login({ email: form.email, password: form.password }, form.rememberMe);
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch (err) {
    const normalized = normalizeError(err, t('auth.signInFailed'));
    if (normalized.code === 'email_not_verified') {
      needsVerification.value = true;
    } else if (normalized.code === 'two_factor_required') {
      needsTwoFactor.value = true;
    } else {
      serverError.value = normalized.isUnauthorized
        ? t('auth.invalidCredentials')
        : normalized.message;
    }
  } finally {
    submitting.value = false;
  }
}

async function submitTwoFactor(code: string) {
  twoFactorError.value = null;
  submitting.value = true;
  try {
    await auth.login({ email: form.email, password: form.password, twoFactorCode: code }, form.rememberMe);
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch (err) {
    const normalized = normalizeError(err, t('auth.signInFailed'));
    twoFactorError.value = normalized.code === 'invalid_two_factor_code'
      ? t('auth.twoFactorInvalid')
      : normalized.message;
  } finally {
    submitting.value = false;
  }
}

async function onVerified() {
  try {
    await auth.login({ email: form.email, password: form.password }, form.rememberMe);
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch {
    needsVerification.value = false;
  }
}

function onSubmit() {
  if (submitting.value) return;
  void signIn();
}

const {
  googleEnabled,
  microsoftEnabled,
  anyOAuthEnabled,
  warmGoogle,
  signInWithGoogle,
  signInWithMicrosoft,
} = useOAuth();

async function completeOAuth(provider: string, token: string) {
  serverError.value = null;
  submitting.value = true;
  try {
    await auth.oauthLogin(provider, token, form.rememberMe);
    const redirect = router.currentRoute.value.query.redirect as string | undefined;
    router.push(redirect ?? { name: 'home' });
  } catch (err) {
    serverError.value = normalizeError(err, t('auth.oauthFailed')).message;
  } finally {
    submitting.value = false;
  }
}

async function onMicrosoft() {
  serverError.value = null;
  try {
    const idToken = await signInWithMicrosoft();
    await completeOAuth('microsoft', idToken);
  } catch (err) {
    const code = (err as { errorCode?: string })?.errorCode;
    if (code !== 'user_cancelled') {
      console.error('[oauth:microsoft]', err);
      serverError.value = t('auth.oauthFailed');
    }
  }
}

function onGoogle() {
  serverError.value = null;
  signInWithGoogle((accessToken) => completeOAuth('google', accessToken));
}

onMounted(() => {
  const prefill = route.query.email;
  if (typeof prefill === 'string' && prefill) {
    form.email = prefill;
  }
  warmGoogle();
});
</script>

<template>
  <AuthLayout>
    <EmailVerifyPanel v-if="needsVerification" :email="form.email" @verified="onVerified" />

    <TwoFactorChallenge
      v-else-if="needsTwoFactor"
      :loading="submitting"
      :error="twoFactorError"
      @submit="submitTwoFactor"
    />

    <template v-else>
    <h1 class="text-[26px] font-bold text-ink-900 leading-tight">
      {{ t('auth.welcomeBack') }}
    </h1>
    <div class="mt-1 h-px w-full bg-line" />
    <p class="mt-3 text-[13px] text-ink-500">{{ t('auth.welcomeSubtitle') }}</p>

    <form class="mt-6 flex flex-col gap-5" novalidate @submit.prevent="onSubmit">
      <div class="flex flex-col gap-1.5">
        <FloatLabel variant="on">
          <InputText
            id="email"
            v-model="form.email"
            type="email"
            autocomplete="email"
            :invalid="emailInvalid || (showValidation && !form.email)"
            class="!h-11 w-full"
          />
          <label for="email">{{ t('auth.emailLabel') }}</label>
        </FloatLabel>
        <small v-if="emailInvalid" class="text-xs text-danger">
          <i class="pi pi-exclamation-circle mr-1" />{{ t('auth.invalidEmailFormat') }}
        </small>
      </div>

      <div class="flex flex-col gap-1.5">
        <FloatLabel variant="on">
          <Password
            v-model="form.password"
            input-id="password"
            :feedback="false"
            toggle-mask
            autocomplete="current-password"
            input-class="!h-11 w-full"
            class="w-full"
            :invalid="showValidation && !form.password"
          />
          <label for="password">{{ t('auth.passwordLabel') }}</label>
        </FloatLabel>
      </div>

      <div class="flex items-center gap-2">
        <input
          id="remember"
          v-model="form.rememberMe"
          type="checkbox"
        />
        <label for="remember" class="text-[13px] font-medium text-ink-600 cursor-pointer">
          {{ t('auth.rememberMe') }}
        </label>
        <button
          type="button"
          class="w-4 h-4 flex items-center justify-center text-brand-600 hover:text-brand-700"
          :aria-label="t('auth.rememberMeInfo')"
          @click="rememberInfo?.toggle($event)"
        >
          <i class="pi pi-info-circle text-[13px]" />
        </button>
        <Popover ref="rememberInfo">
          <p class="max-w-[260px] text-[13px] text-ink-600 leading-relaxed">
            {{ t('auth.rememberMeInfo') }}
          </p>
        </Popover>
      </div>

      <FormError v-if="serverError" :message="serverError" />

      <Button
        type="submit"
        :label="t('auth.signIn')"
        :loading="submitting"
        :disabled="!isStepValid"
        :class="[
          '!h-11 !rounded-none !font-semibold w-full transition-colors',
          isStepValid
            ? '!bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white'
            : '!bg-slate-200 !border-slate-200 !text-slate-400',
        ]"
      />

      <FormSuccess v-if="resetSuccess" :message="t('auth.resetSuccess')" />

      <template v-if="anyOAuthEnabled">
        <div class="flex items-center gap-3">
          <span class="h-px flex-1 bg-line" />
          <span class="text-[11px] uppercase tracking-wide text-ink-400">
            {{ t('auth.orContinueWith') }}
          </span>
          <span class="h-px flex-1 bg-line" />
        </div>

        <button
          v-if="microsoftEnabled"
          type="button"
          class="flex items-center justify-center gap-2.5 w-full h-11 border border-line bg-white text-[14px] font-semibold text-ink-900 transition-colors hover:bg-surface hover:border-ink-400 active:bg-line disabled:opacity-55 disabled:cursor-not-allowed"
          :disabled="submitting"
          @click="onMicrosoft"
        >
          <svg width="18" height="18" viewBox="0 0 21 21" aria-hidden="true">
            <rect x="1" y="1" width="9" height="9" fill="#f25022" />
            <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
            <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
            <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
          </svg>
          <span>Microsoft</span>
        </button>

        <button
          v-if="googleEnabled"
          type="button"
          class="flex items-center justify-center gap-2.5 w-full h-11 border border-line bg-white text-[14px] font-semibold text-ink-900 transition-colors hover:bg-surface hover:border-ink-400 active:bg-line disabled:opacity-55 disabled:cursor-not-allowed"
          :disabled="submitting"
          @click="onGoogle"
        >
          <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
            <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
            <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
            <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.28-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
            <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
          </svg>
          <span>Google</span>
        </button>
      </template>

      <div class="flex items-center justify-center gap-2 text-[13px]">
        <RouterLink :to="{ name: 'register' }" class="font-medium text-brand-600 hover:underline">
          {{ t('auth.noAccount') }}
        </RouterLink>
        <span class="text-ink-300" aria-hidden="true">·</span>
        <RouterLink :to="{ name: 'forgot-password' }" class="font-medium text-brand-600 hover:underline">
          {{ t('auth.cantSignIn') }}
        </RouterLink>
      </div>
    </form>
    </template>
  </AuthLayout>
</template>

<style scoped>
input[type='checkbox'] {
  -webkit-appearance: none;
  appearance: none;
  width: 1.4rem;
  height: 1.4rem;
  border: 1.5px solid #64748b;
  background: #fff;
  cursor: pointer;
  flex-shrink: 0;
  position: relative;
  transition: background 0.15s, border-color 0.15s, transform 0.1s ease, box-shadow 0.15s ease;
}

input[type='checkbox']:hover {
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
}

input[type='checkbox']:active {
  transform: scale(0.9);
}

input[type='checkbox']:checked {
  background: #2563eb;
  border-color: #2563eb;
}

input[type='checkbox']:checked:hover {
  background: #1d4ed8;
  border-color: #1d4ed8;
}

input[type='checkbox']:checked::after {
  content: '';
  position: absolute;
  left: 50%;
  top: 50%;
  width: 12px;
  height: 12px;
  background: #fff;
  transform: translate(-50%, -50%);
}

input[type='checkbox']:checked:active::after {
  transform: translate(-50%, -50%) scale(0.85);
}

input[type='checkbox']:focus-visible {
  outline: 2px solid #2563eb;
  outline-offset: 2px;
}
</style>
