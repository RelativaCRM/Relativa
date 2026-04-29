import { useAuthStore } from '@/stores/auth';
import { useWorkspaceStore } from '@/stores/workspace';

function gatewayBase(): string {
  return (import.meta.env.VITE_GATEWAY_URL ?? 'http://localhost:8080').replace(
    /\/$/,
    '',
  );
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly payload?: unknown,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

type Json = Record<string, unknown> | Array<unknown>;

export async function gatewayFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const auth = useAuthStore();
  const wsStore = useWorkspaceStore();
  const headers = new Headers(init.headers);
  if (auth.accessToken) {
    headers.set('Authorization', `Bearer ${auth.accessToken}`);
  }
  if (wsStore.currentWorkspaceId) {
    headers.set('X-Workspace-ID', String(wsStore.currentWorkspaceId));
  }
  const url = path.startsWith('http') ? path : `${gatewayBase()}${path}`;
  return fetch(url, { ...init, headers });
}

const AUTH_PATHS = ['/auth/me', '/auth/refresh'];

function isAuthEndpoint(url: string): boolean {
  return AUTH_PATHS.some((p) => url.includes(p));
}

async function parseResponse<T>(res: Response): Promise<T> {
  const text = await res.text();
  const body = text ? safeJson(text) : undefined;

  if (!res.ok) {
    const message =
      (body && typeof body === 'object' && 'title' in body
        ? String((body as { title: unknown }).title)
        : undefined) ??
      (body && typeof body === 'object' && 'message' in body
        ? String((body as { message: unknown }).message)
        : undefined) ??
      res.statusText ??
      `Request failed (${res.status})`;

    if (res.status === 401 && isAuthEndpoint(res.url)) {
      const auth = useAuthStore();
      auth.clearSession();
    }

    throw new ApiError(res.status, message, body);
  }

  return body as T;
}

export function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function jsonHeaders(extra?: HeadersInit): HeadersInit {
  return { 'Content-Type': 'application/json', ...(extra ?? {}) };
}

export const api = {
  get<T>(path: string, init?: RequestInit): Promise<T> {
    return gatewayFetch(path, { ...init, method: 'GET' }).then(parseResponse<T>);
  },
  post<T>(path: string, body?: Json | undefined, init?: RequestInit): Promise<T> {
    return gatewayFetch(path, {
      ...init,
      method: 'POST',
      headers: jsonHeaders(init?.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then(parseResponse<T>);
  },
  put<T>(path: string, body?: Json | undefined, init?: RequestInit): Promise<T> {
    return gatewayFetch(path, {
      ...init,
      method: 'PUT',
      headers: jsonHeaders(init?.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then(parseResponse<T>);
  },
  patch<T>(path: string, body?: Json | undefined, init?: RequestInit): Promise<T> {
    return gatewayFetch(path, {
      ...init,
      method: 'PATCH',
      headers: jsonHeaders(init?.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then(parseResponse<T>);
  },
  del<T>(path: string, init?: RequestInit): Promise<T> {
    return gatewayFetch(path, { ...init, method: 'DELETE' }).then(parseResponse<T>);
  },
};
