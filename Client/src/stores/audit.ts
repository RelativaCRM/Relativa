import { defineStore } from 'pinia';
import { ref } from 'vue';
import { auditApi, type AuditLogEntryDto } from '@/api/audit';
import { normalizeError } from '@/api/errors';

export const useAuditStore = defineStore('audit', () => {
  const rows = ref<AuditLogEntryDto[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function fetchRows(): Promise<AuditLogEntryDto[]> {
    loading.value = true;
    error.value = null;
    try {
      rows.value = await auditApi.list();
      return rows.value;
    } catch (err) {
      error.value = normalizeError(err, 'Failed to load audit log.').message;
      throw err;
    } finally {
      loading.value = false;
    }
  }

  function setRows(next: AuditLogEntryDto[]) {
    rows.value = next;
  }

  function clear() {
    rows.value = [];
    error.value = null;
  }

  return { rows, loading, error, fetchRows, setRows, clear };
});
