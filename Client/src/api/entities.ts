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
}

export interface EntityTypeDto {
  id: number;
  name: string;
  properties: EntityTypePropertyDto[];
}

export interface EntityPropertyValueDto {
  propertyId: number;
  name: string;
  value: string | null;
}

export interface EntityListItemDto {
  id: number;
  entityTypeId: number;
  entityTypeName: string;
}

export interface EntityDetailDto {
  id: number;
  entityTypeId: number;
  entityTypeName: string;
  propertyValues: EntityPropertyValueDto[];
}

export interface EntityPropertyInput {
  propertyId: number;
  value: string | null;
}

export interface CreateEntityRequest {
  entityTypeId: number;
  properties: EntityPropertyInput[];
}

export interface UpdateEntityRequest {
  properties: EntityPropertyInput[];
}

const CORE = '/core/api/v1';

export const entityApi = {
  listTypes(): Promise<EntityTypeDto[]> {
    return api.get<EntityTypeDto[]>(`${CORE}/entity-types`);
  },
  list(workspaceId: number): Promise<EntityListItemDto[]> {
    return api.get<EntityListItemDto[]>(
      `${CORE}/workspaces/${workspaceId}/entities`,
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
