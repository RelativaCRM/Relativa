import { api } from '@/api/http';

export interface AuditLogEntryDto {
  id: string;
  entityId: string | null;
  entityType: string | null;
  action: string;
  changedBy: number | string | null;
  fieldName: string | null;
  oldValue: unknown;
  newValue: unknown;
  changedAt: string;
}

const AUDIT = '/audit';

export const auditApi = {
  list(): Promise<AuditLogEntryDto[]> {
    return api.get<AuditLogEntryDto[]>(`${AUDIT}/audit-log`);
  },
};
