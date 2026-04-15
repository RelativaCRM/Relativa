<script setup lang="ts">
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
</script>

<template>
  <section class="max-w-3xl">
    <h1 class="text-2xl font-bold text-ink-900">Welcome to Relativa</h1>
    <p class="mt-2 text-sm text-ink-500">
      All API calls go through the gateway only
      (<code class="px-1 py-0.5 bg-surface rounded">VITE_GATEWAY_URL</code>).
    </p>

    <div class="mt-6 rounded-xl border border-line bg-white p-5">
      <h2 class="text-sm font-semibold text-ink-900">Session</h2>
      <dl class="mt-3 text-sm text-ink-700 grid grid-cols-[auto,1fr] gap-x-6 gap-y-2">
        <dt class="text-ink-500">Authenticated</dt>
        <dd>{{ auth.isAuthenticated ? 'Yes' : 'No' }}</dd>
        <dt class="text-ink-500">Token expiry</dt>
        <dd>{{ auth.expiresAt ?? '—' }}</dd>
      </dl>
    </div>
  </section>
</template>
