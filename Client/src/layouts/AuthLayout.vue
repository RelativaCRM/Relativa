<script setup lang="ts">
import BrandMark from '@/components/layout/BrandMark.vue';

defineProps<{ tagline?: string }>();
</script>

<template>
  <main
    class="min-h-screen w-full bg-surface flex items-center justify-center px-4 py-10"
  >
    <div class="w-full max-w-[440px] flex flex-col">
      <header class="flex flex-col items-center gap-3">
        <BrandMark />
        <p class="text-sm text-ink-500">
          {{ tagline ?? 'B2B CRM Platform · Secure Workspace' }}
        </p>
      </header>

      <section
        class="mt-6 rounded-2xl border border-line bg-white shadow-card px-9 py-10"
      >
        <slot />
      </section>

      <footer class="mt-6 text-center text-[11px] text-ink-400">
        <slot name="footer">
          Secured with JWT · bcrypt password hashing · HTTPS/TLS
        </slot>
      </footer>
    </div>
  </main>
</template>
