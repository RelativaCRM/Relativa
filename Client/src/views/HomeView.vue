<script setup lang="ts">
import { useAuthStore } from "@/stores/auth";

const auth = useAuthStore();
</script>

<template>
  <main class="home">
    <h1>Relativa</h1>
    <p>All API calls go through the gateway only (<code>VITE_GATEWAY_URL</code>).</p>
    <p v-if="auth.isAuthenticated">JWT present (localStorage).</p>
    <p v-else>No JWT stored.</p>
  </main>
</template>

<style scoped>
.home {
  padding: 1rem;
}
</style>
