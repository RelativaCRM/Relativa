import { api } from '@/api/http';

export type EntityPropertyDataType =
  | 'String'
  | 'Int'
  | 'Decimal'
  | 'Bool'
  | 'Date';

export interface AllowedValueDto {
  value: string;
  displayName: string;
}

export interface EntityTypePropertyDto {
  propertyId: number;
  name: string;
  displayName: string;
  dataType: EntityPropertyDataType;
  isRequired: boolean;
  /** From `property.is_readonly`; if all properties are readonly, creation/editing is blocked in the UI. */
  isReadonly: boolean;
  /** Non-empty when only specific string values are allowed (e.g. deal status, contract_status). */
  allowedValues: AllowedValueDto[];
}

export interface OutgoingRelationshipDto {
  relationshipTypeId: number;
  name: string;
  displayName: string;
  targetEntityTypeId: number;
  targetEntityTypeName: string;
  targetEntityTypeDisplayName: string;
  isRequired: boolean;
  relationshipCardinality: string;
}

export interface IncomingRelationshipDto {
  relationshipTypeId: number;
  name: string;
  displayName: string;
  sourceEntityTypeId: number;
  sourceEntityTypeName: string;
  sourceEntityTypeDisplayName: string;
  isRequired: boolean;
  relationshipCardinality: string;
}

export interface EntityTypeDto {
  id: number;
  name: string;
  displayName: string;
  isStandalone: boolean;
  outgoingRelationships: OutgoingRelationshipDto[];
  incomingRelationships: IncomingRelationshipDto[];
  properties: EntityTypePropertyDto[];
}

export interface EntityPropertyValueDto {
  propertyId: number;
  propertyName: string;
  displayName: string;
  dataType: EntityPropertyDataType;
  value: string | number | boolean | null;
  isReadonly: boolean;
}

export interface EntityRelationshipRefDto {
  relationshipId: number;
  relationshipTypeId: number;
  relationshipName: string;
  relationshipDisplayName: string;
  relatedEntityId: number;
  relatedEntityTypeName: string;
  relatedEntityTypeDisplayName: string;
  previewPropertyValues: EntityPropertyValueDto[];
}

export interface CreateEntityRelationshipRequest {
  sourceEntityId: number;
  targetEntityId: number;
  relationshipTypeId: number;
}

export interface ReassignEntityRelationshipRequest {
  newSourceEntityId?: number;
  newTargetEntityId?: number;
}

export interface EntityListItemDto {
  id: number;
  entityTypeId: number;
  entityTypeName: string;
  entityTypeDisplayName: string;
  propertyValues: EntityPropertyValueDto[];
}

export interface EntityDetailDto extends EntityListItemDto {
  isArchived: boolean;
  outboundRelationships: EntityRelationshipRefDto[];
  inboundRelationships: EntityRelationshipRefDto[];
}

export interface EntityPropertyInput {
  propertyId: number;
  value: string | null;
}

export interface EntityRelationshipLinkInput {
  relationshipTypeId: number;
  targetEntityId: number;
}

export interface CreateEntityRequest {
  entityTypeId: number;
  properties: EntityPropertyInput[];
  links?: EntityRelationshipLinkInput[];
}

export interface UpdateEntityRequest {
  properties: EntityPropertyInput[];
}

export interface ListEntitiesQuery {
  entityTypeId?: number;
  q?: string;
  take?: number;
  excludeLinkedSourceRelTypeId?: number;
  excludeLinkedTargetRelTypeId?: number;
}

// Core /workspaces/{id}/entities switched to a paged envelope (items + total/skip/take).
// We keep the public list() signature returning a flat array — all callsites assume that —
// and unwrap `items` here. Drop this shim when pagination is plumbed through the UI.
interface EntityPagedResult {
  items: EntityListItemDto[];
  total: number;
  skip: number;
  take: number;
}

const CORE = '/core/api/v1';

function buildEntityListQuery(q?: ListEntitiesQuery): string {
  if (!q) return '';
  const params = new URLSearchParams();
  if (q.entityTypeId != null) params.set('entityTypeId', String(q.entityTypeId));
  if (q.q != null && q.q.trim()) params.set('q', q.q.trim());
  if (q.take != null) params.set('take', String(q.take));
  if (q.excludeLinkedSourceRelTypeId != null) params.set('excludeLinkedSourceRelTypeId', String(q.excludeLinkedSourceRelTypeId));
  if (q.excludeLinkedTargetRelTypeId != null) params.set('excludeLinkedTargetRelTypeId', String(q.excludeLinkedTargetRelTypeId));
  const s = params.toString();
  return s ? `?${s}` : '';
}

export const entityApi = {
  listTypes(): Promise<EntityTypeDto[]> {
    return api.get<EntityTypeDto[]>(`${CORE}/entity-types`);
  },
  async list(workspaceId: number, query?: ListEntitiesQuery): Promise<EntityListItemDto[]> {
    const qs = buildEntityListQuery(query);
    const res = await api.get<EntityPagedResult | EntityListItemDto[]>(
      `${CORE}/workspaces/${workspaceId}/entities${qs}`,
    );
    // Tolerate both shapes so the client survives a future Core rollback as well.
    return Array.isArray(res) ? res : (res?.items ?? []);
  },
  get(workspaceId: number, entityId: number): Promise<EntityDetailDto> {
    return api.get<EntityDetailDto>(
      `${CORE}/workspaces/${workspaceId}/entities/${entityId}`,
    );
  },
  create(
    workspaceId: number,
    body: CreateEntityRequest,
  ): Promise<EntityDetailDto> {
    return api.post<EntityDetailDto>(
      `${CORE}/workspaces/${workspaceId}/entities`,
      body as unknown as Record<string, unknown>,
    );
  },
  update(
    workspaceId: number,
    entityId: number,
    body: UpdateEntityRequest,
  ): Promise<EntityDetailDto> {
    return api.patch<EntityDetailDto>(
      `${CORE}/workspaces/${workspaceId}/entities/${entityId}`,
      body as unknown as Record<string, unknown>,
    );
  },
  archive(workspaceId: number, entityId: number): Promise<void> {
    return api.del<void>(
      `${CORE}/workspaces/${workspaceId}/entities/${entityId}`,
    );
  },
  createRelationship(workspaceId: number, body: CreateEntityRelationshipRequest): Promise<EntityRelationshipRefDto> {
    return api.post<EntityRelationshipRefDto>(
      `${CORE}/workspaces/${workspaceId}/entity-relationships`,
      body as unknown as Record<string, unknown>,
    );
  },
  deleteRelationship(workspaceId: number, relationshipId: number): Promise<void> {
    return api.del<void>(
      `${CORE}/workspaces/${workspaceId}/entity-relationships/${relationshipId}`,
    );
  },
  reassignRelationship(
    workspaceId: number,
    relationshipId: number,
    body: ReassignEntityRelationshipRequest,
  ): Promise<EntityRelationshipRefDto> {
    return api.put<EntityRelationshipRefDto>(
      `${CORE}/workspaces/${workspaceId}/entity-relationships/${relationshipId}`,
      body,
    );
  },
};
