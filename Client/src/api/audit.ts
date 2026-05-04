import { api } from '@/api/http';

export type AuditScope = 'entity' | 'workspace' | 'organization' | 'user';

export interface ActorDto {
  userId: number | null;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
}

export interface EntityAuditContextDto {
  id: number | null;
  entityTypeId: number | null;
  entityTypeName: string | null;
  isArchived: boolean | null;
}

export interface WorkspaceAuditContextDto {
  id: number | null;
  name: string | null;
  organizationId: number | null;
  organizationName: string | null;
}

export interface OrganizationAuditContextDto {
  id: number | null;
  name: string | null;
}

export interface UserAuditContextDto {
  id: number | null;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
}

export interface PropertyDefinitionDto {
  propertyId: number;
  name: string;
  dataType: string;
}

export interface PropertyChangeDto {
  propertyId: number;
  propertyName: string;
  dataType: string;
  oldValue: unknown;
  newValue: unknown;
}

export interface AuditLogEntryDto {
  id: string;
  /** Audit scope category emitted by the BE (entity|workspace|organization|user). */
  entity_type: string;
  action: string;
  fieldName: string | null;
  changedAt: string;
  actor: ActorDto | null;
  oldValue: unknown;
  newValue: unknown;
  entity: EntityAuditContextDto | null;
  workspace: WorkspaceAuditContextDto | null;
  organization: OrganizationAuditContextDto | null;
  targetUser: UserAuditContextDto | null;
  entityDeleted: boolean | null;
  entityTypeIdFromEvent: string | null;
  propertyDefinitionsForEntityType: PropertyDefinitionDto[] | null;
  propertyChanges: PropertyChangeDto[] | null;
}

export interface FilterContextWorkspace {
  id: number;
  name: string;
  organizationId: number;
  organizationName: string;
}

export interface FilterContextOrganization {
  id: number;
  name: string;
}

export interface AuditFilterContextDto {
  workspace: FilterContextWorkspace | null;
  organization: FilterContextOrganization | null;
}

export interface AuditLogListResponse {
  data: AuditLogEntryDto[];
  total: number;
  page: number;
  perPage: number;
  filterContext: AuditFilterContextDto | null;
}

export interface AuditLogQuery {
  entityType: AuditScope;
  workspaceId?: number;
  organizationId?: number;
  /** ISO datetime string. */
  dateFrom?: string;
  /** ISO datetime string. */
  dateTo?: string;
  action?: string;
  /** 1-based page index. */
  index?: number;
  /** Max 100 (BE-enforced). */
  pageSize?: number;
  entityId?: number;
  domainEntityType?: string;
  actorUserId?: number;
  targetUserId?: number;
}

const AUDIT = '/audit';

function buildQuery(q: AuditLogQuery): string {
  const params = new URLSearchParams();
  params.set('entity_type', q.entityType);
  if (q.workspaceId !== undefined)
    params.set('workspace_id', String(q.workspaceId));
  if (q.organizationId !== undefined)
    params.set('organization_id', String(q.organizationId));
  if (q.dateFrom) params.set('date_from', q.dateFrom);
  if (q.dateTo) params.set('date_to', q.dateTo);
  if (q.action) params.set('action', q.action);
  if (q.index !== undefined) params.set('index', String(q.index));
  if (q.pageSize !== undefined) params.set('page_size', String(q.pageSize));
  if (q.entityId !== undefined) params.set('entity_id', String(q.entityId));
  if (q.domainEntityType) params.set('domain_entity_type', q.domainEntityType);
  if (q.actorUserId !== undefined)
    params.set('actor_user_id', String(q.actorUserId));
  if (q.targetUserId !== undefined)
    params.set('target_user_id', String(q.targetUserId));
  return params.toString();
}

export const auditApi = {
  list(query: AuditLogQuery): Promise<AuditLogListResponse> {
    return api.get<AuditLogListResponse>(
      `${AUDIT}/audit-log?${buildQuery(query)}`,
    );
  },
};
