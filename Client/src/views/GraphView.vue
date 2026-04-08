<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef } from "vue";
import { Network } from "vis-network/standalone";
import { useGraphStore } from "@/stores/graph";

const container = ref<HTMLDivElement | null>(null);
const network = shallowRef<Network | null>(null);
const graphStore = useGraphStore();

onMounted(() => {
  if (!container.value) return;
  const nodes = [
    { id: 1, label: "A" },
    { id: 2, label: "B" },
    { id: 3, label: "C" },
  ];
  const edges = [
    { from: 1, to: 2 },
    { from: 2, to: 3 },
  ];
  network.value = new Network(
    container.value,
    { nodes, edges },
    { physics: { enabled: true } },
  );
  graphStore.setStats(nodes.length, edges.length);
});

onUnmounted(() => {
  network.value?.destroy();
  network.value = null;
});
</script>

<template>
  <main class="graph-page">
    <h1>Graph</h1>
    <p>Placeholder force-directed graph (vis-network). D3 is available for other layouts.</p>
    <div ref="container" class="graph-host" />
  </main>
</template>

<style scoped>
.graph-page {
  padding: 1rem;
}
.graph-host {
  width: 100%;
  height: 420px;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 6px;
}
</style>
