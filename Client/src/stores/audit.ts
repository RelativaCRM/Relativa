import { defineStore } from 'pinia';
import { ref } from 'vue';
import {
  auditApi,
  type AuditLogEntryDto,
  type AuditLogListResponse,
  type AuditLogQuery,
} from '@/api/audit';

export const useAuditStore = defineStore('audit', () => {
  const rows = ref<AuditLogEntryDto[]>([]);
  const total = ref(0);
  const page = ref(1);
  const perPage = ref(20);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function fetchRows(query: AuditLogQuery): Promise<AuditLogListResponse> {
    loading.value = true;
    error.value = null;
    try {
      const res = await auditApi.list(query);
      rows.value = res.data;
      total.value = res.total;
      page.value = res.page;
      perPage.value = res.perPage;
      return res;
    } catch (err) {
      error.value = err instanceof Error ? err.message : String(err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  function clear() {
    rows.value = [];
    total.value = 0;
    page.value = 1;
    perPage.value = 20;
    error.value = null;
  }

  return { rows, total, page, perPage, loading, error, fetchRows, clear };
});
