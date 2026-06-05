<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import Popover from 'primevue/popover';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Button from 'primevue/button';
import FloatLabel from 'primevue/floatlabel';
import { useLocaleSwitch } from '@/i18n/useLocale';
import { normalizeError } from '@/api/errors';
import { notifyGlobal } from '@/api/errorToast';
import { supportApi } from '@/api/support';
import { isValidEmail } from '@/utils/email';
import type { AppLocale } from '@/i18n';

const { t } = useI18n();
const { current, changeLocale, locales } = useLocaleSwitch();

const expanded = ref(true);
const activeTile = ref<string | null>(null);
const langPanel = ref<InstanceType<typeof Popover>>();
const themePanel = ref<InstanceType<typeof Popover>>();
const supportPanel = ref<InstanceType<typeof Popover>>();
const theme = ref<'light' | 'dark' | 'system'>('system');

const support = reactive({ name: '', email: '', subject: '', message: '' });
type SupportField = 'email' | 'subject' | 'message';
const supportErrors = reactive({ email: '', subject: '', message: '' });
const supportTouched = reactive({ email: false, subject: false, message: false });
const supportSubmitting = ref(false);
const supportSent = ref(false);
const supportError = ref<string | null>(null);

function validateSupportField(field: SupportField) {
  if (field === 'email') {
    supportErrors.email = !support.email.trim()
      ? t('support.emailRequired')
      : !isValidEmail(support.email)
        ? t('support.emailInvalid')
        : '';
  } else if (field === 'subject') {
    supportErrors.subject = support.subject.trim() ? '' : t('support.subjectRequired');
  } else {
    supportErrors.message = support.message.trim() ? '' : t('support.messageRequired');
  }
}

function onSupportInput(field: SupportField) {
  if (supportTouched[field]) validateSupportField(field);
}

function onSupportBlur(field: SupportField) {
  supportTouched[field] = true;
  validateSupportField(field);
}

function validateSupport(): boolean {
  (['email', 'subject', 'message'] as const).forEach((f) => {
    supportTouched[f] = true;
    validateSupportField(f);
  });
  return !supportErrors.email && !supportErrors.subject && !supportErrors.message;
}

async function submitSupport() {
  if (supportSubmitting.value) return;
  if (!validateSupport()) return;
  supportError.value = null;
  supportSubmitting.value = true;
  try {
    await supportApi.contact({
      name: support.name.trim(),
      email: support.email.trim(),
      subject: support.subject.trim(),
      message: support.message.trim(),
    });
    supportSent.value = true;
  } catch (err) {
    supportError.value = normalizeError(err, t('support.sendFailed')).message;
  } finally {
    supportSubmitting.value = false;
  }
}

const languageOptions = computed(() =>
  locales.map((code) => ({ value: code, label: t(`language.${code}`) })),
);

const themeOptions = computed(() => [
  { value: 'light' as const, label: t('prefs.themeLight') },
  { value: 'dark' as const, label: t('prefs.themeDark') },
  { value: 'system' as const, label: t('prefs.themeSystem') },
]);

async function selectLanguage(next: AppLocale) {
  langPanel.value?.hide();
  if (next === current.value) return;
  try {
    await changeLocale(next);
  } catch (err) {
    notifyGlobal(err, { fallback: normalizeError(err).message });
  }
}
</script>

<template>
  <div class="flex flex-col items-center">
    <button
      type="button"
      class="ribbon"
      :aria-label="t('settings.title')"
      @click="expanded = !expanded"
    >
      <i :class="['pi text-[10px]', expanded ? 'pi-chevron-down' : 'pi-chevron-up']" />
    </button>

    <div v-if="expanded" class="mt-4 flex items-start justify-center gap-7">
      <div class="flex flex-col items-center gap-1.5">
        <button
          type="button"
          :class="['pref-tile', { 'pref-tile--active': activeTile === 'language' }]"
          :aria-label="t('language.label')"
          @click="langPanel?.toggle($event)"
        >
          <i class="pi pi-language text-xl" />
        </button>
        <span class="text-xs text-ink-500">{{ t('language.label') }}</span>
      </div>

      <div class="flex flex-col items-center gap-1.5">
        <button
          type="button"
          :class="['pref-tile', { 'pref-tile--active': activeTile === 'theme' }]"
          :aria-label="t('prefs.theme')"
          @click="themePanel?.toggle($event)"
        >
          <span class="theme-glyph" />
        </button>
        <span class="text-xs text-ink-500">{{ t('prefs.theme') }}</span>
      </div>

      <div class="flex flex-col items-center gap-1.5">
        <button
          type="button"
          :class="['pref-tile', { 'pref-tile--active': activeTile === 'support' }]"
          :aria-label="t('prefs.support')"
          @click="supportPanel?.toggle($event)"
        >
          <i class="pi pi-question-circle text-xl" />
        </button>
        <span class="text-xs text-ink-500">{{ t('prefs.support') }}</span>
      </div>
    </div>

    <Popover ref="langPanel" append-to="body" @show="activeTile = 'language'" @hide="activeTile = null">
      <ul class="max-h-60 overflow-y-auto min-w-[160px] py-1">
        <li v-for="opt in languageOptions" :key="opt.value">
          <button
            type="button"
            :class="[
              'w-full flex items-center justify-between gap-3 px-3 py-2 text-sm text-left transition-colors hover:bg-brand-50',
              opt.value === current ? 'text-brand-700 font-medium' : 'text-ink-700',
            ]"
            @click="selectLanguage(opt.value)"
          >
            <span class="truncate">{{ opt.label }}</span>
            <i v-if="opt.value === current" class="pi pi-check text-xs text-brand-600 shrink-0" />
          </button>
        </li>
      </ul>
    </Popover>

    <Popover ref="themePanel" append-to="body" @show="activeTile = 'theme'" @hide="activeTile = null">
      <div class="flex gap-3 p-1">
        <label
          v-for="opt in themeOptions"
          :key="opt.value"
          :class="[
            'flex flex-col items-center gap-2 p-2 cursor-pointer border transition-colors',
            theme === opt.value ? 'border-brand-300 bg-brand-50' : 'border-transparent hover:bg-surface',
          ]"
        >
          <span :class="['tp', `tp--${opt.value}`]">
            <span class="tp-bar">
              <span class="tp-dot" />
              <span class="tp-dot" />
              <span class="tp-dot" />
            </span>
            <span class="tp-body">
              <span class="tp-side" />
              <span class="tp-content">
                <span class="tp-line" />
                <span class="tp-line tp-line--short" />
                <span class="tp-block" />
              </span>
            </span>
          </span>
          <span class="text-xs text-ink-700">{{ opt.label }}</span>
          <input
            type="radio"
            class="pref-radio"
            :value="opt.value"
            :checked="theme === opt.value"
            @change="theme = opt.value"
          />
        </label>
      </div>
    </Popover>

    <Popover ref="supportPanel" append-to="body" @show="activeTile = 'support'" @hide="activeTile = null">
      <div class="min-w-[300px] p-1">
        <template v-if="supportSent">
          <div class="flex flex-col items-center text-center py-4">
            <i class="pi pi-check-circle text-emerald-600 text-2xl mb-2" />
            <p class="text-sm text-ink-700">{{ t('support.sent') }}</p>
          </div>
        </template>
        <template v-else>
          <p class="text-sm font-semibold text-ink-900 mb-4">{{ t('support.title') }}</p>
          <div class="flex flex-col gap-4">
            <div class="flex flex-col gap-1">
              <FloatLabel variant="on">
                <InputText
                  id="support-email"
                  v-model="support.email"
                  type="email"
                  :invalid="!!supportErrors.email"
                  class="!h-10 w-full text-sm"
                  @update:model-value="onSupportInput('email')"
                  @blur="onSupportBlur('email')"
                />
                <label for="support-email">{{ t('support.emailLabel') }}</label>
              </FloatLabel>
              <small v-if="supportErrors.email" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ supportErrors.email }}
              </small>
            </div>

            <div class="flex flex-col gap-1">
              <FloatLabel variant="on">
                <InputText
                  id="support-subject"
                  v-model="support.subject"
                  :invalid="!!supportErrors.subject"
                  class="!h-10 w-full text-sm"
                  @update:model-value="onSupportInput('subject')"
                  @blur="onSupportBlur('subject')"
                />
                <label for="support-subject">{{ t('support.subjectLabel') }}</label>
              </FloatLabel>
              <small v-if="supportErrors.subject" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ supportErrors.subject }}
              </small>
            </div>

            <div class="flex flex-col gap-1">
              <FloatLabel variant="on">
                <Textarea
                  id="support-message"
                  v-model="support.message"
                  rows="4"
                  :invalid="!!supportErrors.message"
                  class="text-sm !border-ink-400"
                  style="resize: both; min-height: 5.5rem; min-width: 280px; max-width: 70vw; width: 100%"
                  @update:model-value="onSupportInput('message')"
                  @blur="onSupportBlur('message')"
                />
                <label for="support-message">{{ t('support.messageLabel') }}</label>
              </FloatLabel>
              <small v-if="supportErrors.message" class="text-xs text-danger">
                <i class="pi pi-exclamation-circle mr-1" />{{ supportErrors.message }}
              </small>
            </div>

            <p v-if="supportError" class="text-xs text-danger -mt-1">{{ supportError }}</p>
            <Button
              :label="t('support.send')"
              :loading="supportSubmitting"
              class="!h-10 !rounded-none !font-semibold w-full transition-colors !bg-blue-600 !border-blue-600 hover:!bg-blue-700 hover:!border-blue-700 active:!bg-blue-800 !text-white"
              @click="submitSupport"
            />
          </div>
        </template>
      </div>
    </Popover>
  </div>
</template>

<style scoped>
.ribbon {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 1.25rem;
  padding: 0 0.5rem;
  border: 1px solid #e2e8f0;
  border-radius: 0.375rem;
  background: rgba(255, 255, 255, 0.8);
  color: rgb(148 163 184);
  transition: color 0.15s ease, border-color 0.15s ease;
}
.ribbon:hover {
  color: rgb(37 99 235);
  border-color: rgb(147 197 253);
}

.pref-tile {
  width: 3.5rem;
  height: 3.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #e2e8f0;
  border-radius: 0.625rem;
  background: #fff;
  color: rgb(71 85 105);
  transition: transform 0.18s ease, box-shadow 0.18s ease, color 0.15s ease, border-color 0.15s ease, background-color 0.15s ease;
}
.pref-tile:hover,
.pref-tile--active {
  color: rgb(37 99 235);
  border-color: rgb(147 197 253);
  background: rgb(239 246 255);
  transform: scale(1.1) translateY(-3px);
  box-shadow: 0 8px 18px -6px rgba(37, 99, 235, 0.35);
}

.theme-glyph {
  width: 1.4rem;
  height: 1.4rem;
  border-radius: 9999px;
  border: 1px solid #94a3b8;
  background: linear-gradient(90deg, #ffffff 50%, #0f172a 50%);
}

.pref-radio {
  width: 1rem;
  height: 1rem;
  accent-color: rgb(37 99 235);
  cursor: pointer;
}

.tp {
  width: 4.75rem;
  height: 3.4rem;
  border: 1px solid #cbd5e1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
}
.tp-bar {
  height: 0.7rem;
  display: flex;
  align-items: center;
  gap: 2px;
  padding: 0 4px;
}
.tp-dot {
  width: 3px;
  height: 3px;
}
.tp-body {
  flex: 1;
  display: flex;
}
.tp-side {
  width: 1.1rem;
}
.tp-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: 4px;
}
.tp-line {
  height: 3px;
  width: 100%;
}
.tp-line--short {
  width: 55%;
}
.tp-block {
  margin-top: auto;
  height: 0.85rem;
  width: 100%;
}

.tp--light { background: #ffffff; }
.tp--light .tp-bar { background: #eef2f7; }
.tp--light .tp-dot { background: #cbd5e1; }
.tp--light .tp-side { background: #e2e8f0; }
.tp--light .tp-line { background: #e2e8f0; }
.tp--light .tp-block { background: #dbeafe; }

.tp--dark { background: #0f172a; }
.tp--dark .tp-bar { background: #1e293b; }
.tp--dark .tp-dot { background: #475569; }
.tp--dark .tp-side { background: #1e293b; }
.tp--dark .tp-line { background: #334155; }
.tp--dark .tp-block { background: #1d4ed8; }

.tp--system {
  background: linear-gradient(135deg, #ffffff 0 50%, #0f172a 50% 100%);
}
.tp--system .tp-bar { background: rgba(100, 116, 139, 0.28); }
.tp--system .tp-dot { background: rgba(203, 213, 225, 0.85); }
.tp--system .tp-side { background: rgba(100, 116, 139, 0.25); }
.tp--system .tp-line { background: rgba(148, 163, 184, 0.5); }
.tp--system .tp-block { background: rgba(59, 130, 246, 0.6); }
</style>
