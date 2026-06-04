<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import BrandMark from '@/components/layout/BrandMark.vue';
import PreferencesBar from '@/components/layout/PreferencesBar.vue';

const { t } = useI18n();
const year = new Date().getFullYear();
</script>

<template>
  <div class="auth-shell min-h-screen w-full flex flex-col">
    <div class="auth-shell__bg" aria-hidden="true"></div>

    <main class="relative flex-1 flex items-center justify-center px-4 py-10">
      <div class="w-full max-w-[440px] flex flex-col">
        <section
          class="border border-line/80 bg-white/95 backdrop-blur-sm shadow-card px-9 py-10"
        >
          <div class="flex justify-center mb-3">
            <BrandMark size="xl" />
          </div>
          <slot />
        </section>

        <footer v-if="$slots.footer" class="mt-6 text-center text-[11px] text-ink-400">
          <slot name="footer" />
        </footer>

        <PreferencesBar class="mt-4" />
      </div>
    </main>

    <footer
      class="relative z-10 border-t border-line bg-white/70 backdrop-blur-sm px-6 py-3 flex items-center justify-center gap-2 text-[11px] text-ink-400"
    >
      <span>{{ t('footer.copyright', { year }) }}</span>
      <span aria-hidden="true">·</span>
      <span>{{ t('footer.rights') }}</span>
    </footer>
  </div>
</template>

<style scoped>
.auth-shell {
  position: relative;
  background-color: #f8fafc;
  isolation: isolate;
  overflow: hidden;
}

.auth-shell__bg {
  position: absolute;
  inset: 0;
  z-index: -1;
  background-image:
    radial-gradient(
      circle at 12% 18%,
      rgba(37, 99, 235, 0.10) 0%,
      rgba(37, 99, 235, 0) 42%
    ),
    radial-gradient(
      circle at 88% 82%,
      rgba(59, 130, 246, 0.08) 0%,
      rgba(59, 130, 246, 0) 45%
    ),
    linear-gradient(180deg, #ffffff 0%, #f1f5fb 100%);
}

.auth-shell::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -1;
  background-image:
    linear-gradient(to right, rgba(37, 99, 235, 0.06) 1px, transparent 1px),
    linear-gradient(to bottom, rgba(37, 99, 235, 0.06) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.6), transparent 70%);
  -webkit-mask-image: radial-gradient(ellipse at center, rgba(0, 0, 0, 0.6), transparent 70%);
}
</style>
