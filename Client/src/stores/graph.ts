import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { graphApi, type GraphNodeDto, type GraphEdgeDto } from '@/api/graph';
import { normalizeError } from '@/api/errors';

export const useGraphStore = defineStore('graph', () => {
  const nodes = ref<GraphNodeDto[]>([]);
  const edges = ref<GraphEdgeDto[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const nodeCount = computed(() => nodes.value.length);
  const edgeCount = computed(() => edges.value.length);

  async function fetchGraph(organizationId: number): Promise<void> {
    isLoading.value = true;
    error.value = null;
    try {
      const data = await graphApi.getGraph(organizationId);
      nodes.value = data.nodes;
      edges.value = data.edges;
    } catch (err) {
      error.value = normalizeError(err, 'Failed to load graph data.').message;
    } finally {
      isLoading.value = false;
    }
  }

  function clear(): void {
    nodes.value = [];
    edges.value = [];
    error.value = null;
  }

  return { nodes, edges, isLoading, error, nodeCount, edgeCount, fetchGraph, clear };
});
