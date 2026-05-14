import { api } from '@/api/http';

export type EntityPropertyDataType =
  | 'String'
  | 'Int'
  | 'Decimal'
  | 'Bool'
  | 'Date';

export interface EntityTypePropertyDto {
  propertyId: number;
  name: string;
  dataType: EntityPropertyDataType;
  isRequired: boolean;
  /** From `property.is_readonly`; if all properties are readonly, creation/editing is blocked in the UI. */
  isReadonly: boolean;
}

export interface OutgoingRelationshipDto {
  relationshipTypeId: number;
  name: string;
  targetEntityTypeId: number;
  targetEntityTypeName: string;
  isRequired: boolean;
  relationshipCardinality: string;
}

export interface IncomingRelationshipDto {
  relationshipTypeId: number;
  name: string;
  sourceEntityTypeId: number;
  sourceEntityTypeName: string;
  isRequired: boolean;
  relationshipCardinality: string;
}

export interface EntityTypeDto {
  id: number;
  name: string;
  isStandalone: boolean;
  outgoingRelationships: OutgoingRelationshipDto[];
  incomingRelationships: IncomingRelationshipDto[];
  properties: EntityTypePropertyDto[];
}

export interface EntityPropertyValueDto {
  propertyId: number;
  propertyName: string;
  dataType: EntityPropertyDataType;
  value: string | number | boolean | null;
  isReadonly: boolean;
}

export interface EntityRelationshipRefDto {
  relationshipTypeId: number;
  relationshipName: string;
  relatedEntityId: number;
  relatedEntityTypeName: string;
  previewPropertyValues: EntityPropertyValueDto[];
}

export interface EntityListItemDto {
  id: number;
  entityTypeId: number;
  entityTypeName: string;
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
}

const CORE = '/core/api/v1';

function buildEntityListQuery(q?: ListEntitiesQuery): string {
  if (!q) return '';
  const params = new URLSearchParams();
  if (q.entityTypeId != null) params.set('entityTypeId', String(q.entityTypeId));
  if (q.q != null && q.q.trim()) params.set('q', q.q.trim());
  if (q.take != null) params.set('take', String(q.take));
  const s = params.toString();
  return s ? `?${s}` : '';
}

export const entityApi = {
  listTypes(): Promise<EntityTypeDto[]> {
    return api.get<EntityTypeDto[]>(`${CORE}/entity-types`);
  },
  list(workspaceId: number, query?: ListEntitiesQuery): Promise<EntityListItemDto[]> {
    const qs = buildEntityListQuery(query);
    return api.get<EntityListItemDto[]>(
      `${CORE}/workspaces/${workspaceId}/entities${qs}`,
    );
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
};
