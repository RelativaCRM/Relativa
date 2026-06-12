<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import QRCode from 'qrcode';
import InputText from 'primevue/inputtext';
import CodeInput from '@/components/feedback/CodeInput.vue';
import FormError from '@/components/feedback/FormError.vue';
import { authApi } from '@/api/auth';
import { normalizeError } from '@/api/errors';
import { useAuthStore } from '@/stores/auth';

type View = 'loading' | 'idle' | 'setup' | 'codes' | 'prompt';
type PendingAction = 'disable' | 'backup' | 'master';

const { t } = useI18n();
const auth = useAuthStore();

const view = ref<View>('loading');
const enabled = ref(false);
const busy = ref(false);
const error = ref<string | null>(null);
const copied = ref('');

const secret = ref('');
const qrDataUrl = ref('');
const setupCode = ref('');
const challengeCode = ref('');
const pending = ref<PendingAction>('disable');

const backupCodes = ref<string[]>([]);
const masterCode = ref('');
const codesKind = ref<'full' | 'backup' | 'master'>('full');

onMounted(loadStatus);

async function loadStatus() {
  try {
    const status = await authApi.twoFactorStatus();
    enabled.value = status.enabled;
  } catch {
    enabled.value = auth.user?.twoFactorEnabled ?? false;
  }
  view.value = 'idle';
}

function syncStore() {
  if (auth.user) auth.user.twoFactorEnabled = enabled.value;
}

async function beginSetup() {
  error.value = null;
  busy.value = true;
  try {
    const setup = await authApi.twoFactorSetup();
    secret.value = setup.secret;
    qrDataUrl.value = await QRCode.toDataURL(setup.otpauthUri, { width: 208, margin: 1 });
    setupCode.value = '';
    view.value = 'setup';
  } catch (err) {
    error.value = normalizeError(err).message;
  } finally {
    busy.value = false;
  }
}

async function confirmEnable() {
  if (setupCode.value.length !== 6 || busy.value) return;
  error.value = null;
  busy.value = true;
  try {
    const result = await authApi.twoFactorEnable(setupCode.value);
    backupCodes.value = result.backupCodes;
    masterCode.value = result.masterCode;
    codesKind.value = 'full';
    enabled.value = true;
    syncStore();
    view.value = 'codes';
  } catch (err) {
    error.value = codeError(err);
    setupCode.value = '';
  } finally {
    busy.value = false;
  }
}

function startPrompt(action: PendingAction) {
  pending.value = action;
  challengeCode.value = '';
  error.value = null;
  view.value = 'prompt';
}

async function confirmPrompt() {
  if (challengeCode.value.trim().length < 6 || busy.value) return;
  error.value = null;
  busy.value = true;
  const code = challengeCode.value.trim();
  try {
    if (pending.value === 'disable') {
      await authApi.twoFactorDisable(code);
      enabled.value = false;
      syncStore();
      view.value = 'idle';
    } else if (pending.value === 'backup') {
      backupCodes.value = (await authApi.twoFactorRegenerateBackupCodes(code)).codes;
      codesKind.value = 'backup';
      view.value = 'codes';
    } else {
      masterCode.value = (await authApi.twoFactorRegenerateMasterCode(code)).masterCode;
      codesKind.value = 'master';
      view.value = 'codes';
    }
    challengeCode.value = '';
  } catch (err) {
    error.value = codeError(err);
  } finally {
    busy.value = false;
  }
}

function codeError(err: unknown): string {
  const normalized = normalizeError(err);
  return normalized.code === 'invalid_two_factor_code'
    ? t('settings.twoFactor.invalidCode')
    : normalized.message;
}

function goIdle() {
  error.value = null;
  challengeCode.value = '';
  view.value = 'idle';
}

async function copyText(key: string, text: string) {
  try {
    await navigator.clipboard.writeText(text);
    copied.value = key;
    setTimeout(() => (copied.value = ''), 1500);
  } catch {
    copied.value = '';
  }
}

function downloadCodes() {
  const lines = [...backupCodes.value];
  if (masterCode.value) lines.push('', `MASTER: ${masterCode.value}`);
  const blob = new Blob([lines.join('\n') + '\n'], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'relativa-2fa-codes.txt';
  a.click();
  URL.revokeObjectURL(url);
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <template v-if="view === 'idle'">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <span
          :class="[
            'inline-flex items-center gap-1 px-2 py-0.5 text-[11px] font-semibold rounded-full',
            enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500',
          ]"
        >
          <i :class="enabled ? 'pi pi-check-circle' : 'pi pi-lock'" class="text-[11px]" />
          {{ enabled ? t('settings.twoFactor.statusEnabled') : t('settings.twoFactor.statusDisabled') }}
        </span>

        <button v-if="!enabled" class="btn btn-primary btn-sm" :disabled="busy" @click="beginSetup">
          <i :class="busy ? 'pi pi-spin pi-spinner' : 'pi pi-shield'" />
          {{ t('settings.twoFactor.enable') }}
        </button>
        <div v-else class="flex flex-wrap gap-2 justify-end">
          <button class="btn btn-outline btn-sm" @click="startPrompt('backup')">
            <i class="pi pi-refresh" />{{ t('settings.twoFactor.regenerate') }}
          </button>
          <button class="btn btn-outline btn-sm" @click="startPrompt('master')">
            <i class="pi pi-key" />{{ t('settings.twoFactor.regenerateMaster') }}
          </button>
          <button class="btn btn-danger btn-sm" @click="startPrompt('disable')">
            <i class="pi pi-times-circle" />{{ t('settings.twoFactor.disable') }}
          </button>
        </div>
      </div>
      <FormError v-if="error" :message="error" />
    </template>

    <template v-else-if="view === 'setup'">
      <p class="text-[12px] text-ink-600">{{ t('settings.twoFactor.setupScan') }}</p>
      <div class="flex justify-center">
        <img v-if="qrDataUrl" :src="qrDataUrl" alt="QR" class="border border-line" width="208" height="208" />
      </div>
      <p class="text-[12px] text-ink-500">{{ t('settings.twoFactor.setupManual') }}</p>
      <div class="flex items-stretch gap-2">
        <code class="flex-1 px-3 py-2 bg-surface border border-line text-[13px] font-mono tracking-wider break-all select-all">{{ secret }}</code>
        <button type="button" class="px-3 border border-line text-[12px] font-medium text-ink-600 hover:bg-surface" @click="copyText('secret', secret)">
          {{ copied === 'secret' ? t('settings.twoFactor.copied') : t('settings.twoFactor.copy') }}
        </button>
      </div>
      <p class="mt-1 text-[12px] font-medium text-ink-700">{{ t('settings.twoFactor.setupVerify') }}</p>
      <CodeInput v-model="setupCode" numeric :disabled="busy" @complete="confirmEnable" />
      <FormError v-if="error" :message="error" />
      <div class="flex gap-2 mt-1 justify-end">
        <button class="btn btn-outline" @click="goIdle">{{ t('settings.twoFactor.cancel') }}</button>
        <button class="btn btn-primary" :disabled="busy || setupCode.length !== 6" @click="confirmEnable">
          <i :class="busy ? 'pi pi-spin pi-spinner' : 'pi pi-check'" />{{ t('settings.twoFactor.enable') }}
        </button>
      </div>
    </template>

    <template v-else-if="view === 'codes'">
      <template v-if="codesKind !== 'master'">
        <p class="text-sm font-semibold text-ink-900">{{ t('settings.twoFactor.backupTitle') }}</p>
        <p class="text-[12px] text-amber-700 bg-amber-50 border border-amber-200 px-3 py-2 leading-relaxed">{{ t('settings.twoFactor.backupWarning') }}</p>
        <div class="grid grid-cols-2 gap-2">
          <code v-for="c in backupCodes" :key="c" class="px-3 py-2 bg-surface border border-line text-[13px] font-mono tracking-wider text-center select-all">{{ c }}</code>
        </div>
      </template>

      <template v-if="codesKind !== 'backup'">
        <p class="text-sm font-semibold text-ink-900" :class="codesKind === 'full' ? 'mt-2' : ''">{{ t('settings.twoFactor.masterTitle') }}</p>
        <p class="text-[12px] text-ink-500 leading-relaxed">{{ t('settings.twoFactor.masterIntro') }}</p>
        <div class="flex items-stretch gap-2">
          <code class="flex-1 px-3 py-3 bg-brand-50 border border-brand-200 text-[15px] font-mono font-semibold tracking-[0.25em] text-center text-brand-700 select-all">{{ masterCode }}</code>
          <button type="button" class="px-3 border border-line text-[12px] font-medium text-ink-600 hover:bg-surface" @click="copyText('master', masterCode)">
            {{ copied === 'master' ? t('settings.twoFactor.copied') : t('settings.twoFactor.copy') }}
          </button>
        </div>
      </template>

      <div class="flex flex-wrap gap-2 mt-1">
        <button v-if="codesKind !== 'master'" class="btn btn-outline btn-sm" @click="copyText('all', backupCodes.join('\n'))">
          <i class="pi pi-copy" />{{ copied === 'all' ? t('settings.twoFactor.copied') : t('settings.twoFactor.copyAll') }}
        </button>
        <button class="btn btn-outline btn-sm" @click="downloadCodes">
          <i class="pi pi-download" />{{ t('settings.twoFactor.download') }}
        </button>
        <button class="btn btn-primary btn-sm ml-auto" @click="goIdle">
          <i class="pi pi-check" />{{ t('settings.twoFactor.done') }}
        </button>
      </div>
    </template>

    <template v-else-if="view === 'prompt'">
      <p class="text-[12px] text-ink-600">
        {{ pending === 'disable' ? t('settings.twoFactor.disablePrompt')
          : pending === 'backup' ? t('settings.twoFactor.regeneratePrompt')
          : t('settings.twoFactor.masterRegenPrompt') }}
      </p>
      <InputText
        v-model="challengeCode"
        class="!h-11 w-full text-center !tracking-[0.3em] !font-semibold"
        :placeholder="t('settings.twoFactor.codePlaceholder')"
        autocomplete="one-time-code"
        @keyup.enter="confirmPrompt"
      />
      <FormError v-if="error" :message="error" />
      <div class="flex gap-2 mt-1">
        <button
          :class="['btn', pending === 'disable' ? 'btn-danger' : 'btn-primary']"
          :disabled="busy || challengeCode.trim().length < 6"
          @click="confirmPrompt"
        >
          <i v-if="busy" class="pi pi-spin pi-spinner" />
          {{ pending === 'disable' ? t('settings.twoFactor.disable') : t('settings.twoFactor.continue') }}
        </button>
        <button class="btn btn-outline" @click="goIdle">{{ t('settings.twoFactor.cancel') }}</button>
      </div>
    </template>
  </div>
</template>
