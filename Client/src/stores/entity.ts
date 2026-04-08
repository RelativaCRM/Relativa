import { defineStore } from "pinia";
import { ref } from "vue";

export const useEntityStore = defineStore("entity", () => {
  const selectedId = ref<string | null>(null);

  function select(id: string | null) {
    selectedId.value = id;
  }

  return { selectedId, select };
});
