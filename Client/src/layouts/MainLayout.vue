<script setup lang="ts">
import { useRouter } from 'vue-router';
import Button from 'primevue/button';
import BrandMark from '@/components/layout/BrandMark.vue';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
const router = useRouter();

function handleLogout() {
  auth.logout();
  router.push({ name: 'login' });
}
</script>

<template>
  <div class="min-h-screen flex flex-col bg-surface">
    <header
      class="h-16 border-b border-line bg-white flex items-center justify-between px-6"
    >
      <BrandMark size="sm" />
      <Button
        label="Sign out"
        severity="secondary"
        text
        icon="pi pi-sign-out"
        @click="handleLogout"
      />
    </header>

    <div class="flex-1 flex">
      <aside class="w-60 border-r border-line bg-white py-6 px-4 hidden md:block">
        <nav class="flex flex-col gap-1 text-sm text-ink-700">
          <RouterLink
            to="/"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-home mr-2" />Home
          </RouterLink>
          <RouterLink
            to="/graph"
            class="px-3 py-2 rounded-lg hover:bg-surface"
            active-class="bg-brand-50 text-brand-700 font-medium"
          >
            <i class="pi pi-share-alt mr-2" />Graph
          </RouterLink>
        </nav>
      </aside>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
