<script setup lang="ts">
import BrandMark from '@/components/layout/BrandMark.vue';
</script>

<template>
  <main class="auth-shell min-h-screen w-full flex items-center justify-center px-4 py-10">
    <div class="auth-shell__bg" aria-hidden="true"></div>

    <div class="relative w-full max-w-[440px] flex flex-col">
      <header class="flex flex-col items-center">
        <BrandMark size="lg" />
      </header>

      <section
        class="mt-7 rounded-2xl border border-line/80 bg-white/95 backdrop-blur-sm shadow-card px-9 py-10"
      >
        <slot />
      </section>

      <footer v-if="$slots.footer" class="mt-6 text-center text-[11px] text-ink-400">
        <slot name="footer" />
      </footer>
    </div>
  </main>
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
