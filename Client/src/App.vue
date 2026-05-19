<script setup lang="ts">
import { onMounted } from 'vue';
import { RouterView } from 'vue-router';
import Toast from 'primevue/toast';
import { useToast } from 'primevue/usetoast';
import { useAuthStore } from '@/stores/auth';
import { setGlobalToast } from '@/api/errorToast';

const auth = useAuthStore();

// Expose the app-level toast service so the http interceptor and global
// error handlers (which run outside any component) can show toasts.
setGlobalToast(useToast());

onMounted(async () => {
  if (auth.isAuthenticated) {
    try {
      await auth.fetchProfile();
    } catch {
      auth.logout();
    }
  }
});
</script>

<template>
  <RouterView />
  <Toast position="bottom-right" />
</template>
