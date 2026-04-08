import { defineStore } from "pinia";
import { ref } from "vue";

export const useGraphStore = defineStore("graph", () => {
  const nodeCount = ref(0);
  const edgeCount = ref(0);

  function setStats(nodes: number, edges: number) {
    nodeCount.value = nodes;
    edgeCount.value = edges;
  }

  return { nodeCount, edgeCount, setStats };
});
