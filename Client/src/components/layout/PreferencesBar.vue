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
const supportPanel = ref<InstanceType<typeof Popover>>();

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

function onLangShow() {
  activeTile.value = 'language';
  const inst = langPanel.value as unknown as
    | { container: HTMLElement | null; target: HTMLElement | null }
    | undefined;
  const el = inst?.container;
  const target = inst?.target;
  if (!el || !target) return;
  const rect = target.getBoundingClientRect();
  el.style.top = `${rect.top + window.scrollY - el.offsetHeight - 8}px`;
  el.setAttribute('data-p-popover-flipped', 'true');
  el.classList.add('p-popover-flipped');
}

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
          :class="['pref-tile', { 'pref-tile--active': activeTile === 'support' }]"
          :aria-label="t('prefs.support')"
          @click="supportPanel?.toggle($event)"
        >
          <i class="pi pi-question-circle text-xl" />
        </button>
        <span class="text-xs text-ink-500">{{ t('prefs.support') }}</span>
      </div>
    </div>

    <Popover ref="langPanel" append-to="body" @show="onLangShow" @hide="activeTile = null">
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
                  class="text-sm !border-ink-400 support-message"
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

.support-message {
  resize: both;
  min-height: 5.5rem;
  min-width: 280px;
  max-width: 70vw;
  width: 100%;
}
</style>
