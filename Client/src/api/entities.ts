import { api, gatewayFetch, ApiError } from '@/api/http';

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

async function patchJson<T>(path: string, body: unknown): Promise<T> {
  const res = await gatewayFetch(path, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  const payload = text ? safeJson(text) : undefined;
  if (!res.ok) {
    const message =
      (payload && typeof payload === 'object' && 'title' in payload
        ? String((payload as { title: unknown }).title)
        : undefined) ??
      (payload && typeof payload === 'object' && 'message' in payload
        ? String((payload as { message: unknown }).message)
        : undefined) ??
      res.statusText ??
      `Request failed (${res.status})`;
    throw new ApiError(res.status, message, payload);
  }
  return payload as T;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

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
    return patchJson<EntityDetailDto>(
      `${CORE}/workspaces/${workspaceId}/entities/${entityId}`,
      body,
    );
  },
  archive(workspaceId: number, entityId: number): Promise<void> {
    return api.del<void>(
      `${CORE}/workspaces/${workspaceId}/entities/${entityId}`,
    );
  },
};
