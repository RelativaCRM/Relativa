import { useAuthStore } from '@/stores/auth';
import { useWorkspaceStore } from '@/stores/workspace';
import { notifyGlobal } from '@/api/errorToast';
import { HttpStatus } from '@/api/httpStatus';

export { HttpStatus };

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

/**
 * Per-request options understood by the api helpers. Extends RequestInit
 * with a `silent` flag that suppresses the centralized error toast — use it
 * when the caller already renders its own field-level or inline message.
 */
export interface ApiRequestInit extends RequestInit {
  silent?: boolean;
}

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
  const controller = new AbortController();
  const timerId = setTimeout(() => controller.abort(), 30_000);
  return fetch(url, { ...init, headers, signal: controller.signal }).finally(() =>
    clearTimeout(timerId),
  );
}

const TOAST_STATUSES: ReadonlySet<number> = new Set([
  HttpStatus.BadRequest,
  HttpStatus.Forbidden,
  HttpStatus.Conflict,
  HttpStatus.UnprocessableEntity,
  HttpStatus.InternalServerError,
  HttpStatus.BadGateway,
  HttpStatus.ServiceUnavailable,
  HttpStatus.GatewayTimeout,
]);

async function parseResponse<T>(res: Response, silent = false): Promise<T> {
  const text = await res.text();
  const body = text ? safeJson(text) : undefined;

  if (!res.ok) {
    // Some endpoints (e.g. Graph's `entity-graph/create`) return the error
    // message as a bare JSON-encoded string, not a ProblemDetails object —
    // capture it so the toast surfaces the real reason instead of "Bad Request".
    const rawString =
      typeof body === 'string' && body.trim().length > 0 ? body.trim() : '';
    const detail =
      body &&
      typeof body === 'object' &&
      typeof (body as { detail?: unknown }).detail === 'string'
        ? String((body as { detail: string }).detail).trim()
        : '';
    const message =
      (detail.length > 0 ? detail : undefined) ??
      (body && typeof body === 'object' && 'title' in body
        ? String((body as { title: unknown }).title)
        : undefined) ??
      (body && typeof body === 'object' && 'message' in body
        ? String((body as { message: unknown }).message)
        : undefined) ??
      (rawString.length > 0 ? rawString : undefined) ??
      res.statusText ??
      `Request failed (${res.status})`;

    if (res.status === HttpStatus.Unauthorized) {
      const auth = useAuthStore();
      auth.clearSession();
      import('@/router').then(({ default: r }) => r.push({ name: 'login' }));
    }

    const apiError = new ApiError(res.status, message, body);

    if (!silent && TOAST_STATUSES.has(res.status)) {
      notifyGlobal(apiError);
    }

    throw apiError;
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

function splitInit(init?: ApiRequestInit): { silent: boolean; rest: RequestInit } {
  if (!init) return { silent: false, rest: {} };
  const { silent, ...rest } = init;
  return { silent: silent === true, rest };
}

export const api = {
  get<T>(path: string, init?: ApiRequestInit): Promise<T> {
    const { silent, rest } = splitInit(init);
    return gatewayFetch(path, { ...rest, method: 'GET' }).then((r) =>
      parseResponse<T>(r, silent),
    );
  },
  post<T>(path: string, body?: object, init?: ApiRequestInit): Promise<T> {
    const { silent, rest } = splitInit(init);
    return gatewayFetch(path, {
      ...rest,
      method: 'POST',
      headers: jsonHeaders(rest.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then((r) => parseResponse<T>(r, silent));
  },
  put<T>(path: string, body?: object, init?: ApiRequestInit): Promise<T> {
    const { silent, rest } = splitInit(init);
    return gatewayFetch(path, {
      ...rest,
      method: 'PUT',
      headers: jsonHeaders(rest.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then((r) => parseResponse<T>(r, silent));
  },
  patch<T>(path: string, body?: object, init?: ApiRequestInit): Promise<T> {
    const { silent, rest } = splitInit(init);
    return gatewayFetch(path, {
      ...rest,
      method: 'PATCH',
      headers: jsonHeaders(rest.headers),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then((r) => parseResponse<T>(r, silent));
  },
  del<T>(path: string, init?: ApiRequestInit): Promise<T> {
    const { silent, rest } = splitInit(init);
    return gatewayFetch(path, { ...rest, method: 'DELETE' }).then((r) =>
      parseResponse<T>(r, silent),
    );
  },
};
