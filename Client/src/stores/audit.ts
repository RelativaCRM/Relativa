import { defineStore } from "pinia";
import { ref } from "vue";

export type AuditRow = { id: string; message: string };

export const useAuditStore = defineStore("audit", () => {
  const rows = ref<AuditRow[]>([]);

  function setRows(next: AuditRow[]) {
    rows.value = next;
  }

  return { rows, setRows };
});
