<script setup lang="ts">
import { computed, watch, onMounted } from 'vue';
import { useRoute, RouterView } from 'vue-router';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';

const route = useRoute();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();

const workspaceId = computed(() => Number(route.params.workspaceId));

watch(workspaceId, (id, prev) => {
  if (!Number.isFinite(id) || id <= 0) return;
  if (prev && prev !== id) {
    entityStore.clearWorkspace(prev);
  }
  wsStore.setCurrentWorkspace(id);
});

onMounted(() => {
  const id = workspaceId.value;
  if (Number.isFinite(id) && id > 0) {
    wsStore.setCurrentWorkspace(id);
  }
});
</script>

<template>
  <RouterView />
</template>
