<script setup lang="ts">
import { onMounted } from 'vue';
import { RouterView } from 'vue-router';
import Toast from 'primevue/toast';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();

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
  <Toast position="top-right" />
</template>
