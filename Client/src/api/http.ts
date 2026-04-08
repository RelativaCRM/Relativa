import { useAuthStore } from "@/stores/auth";

function gatewayBase(): string {
  return (import.meta.env.VITE_GATEWAY_URL ?? "http://localhost:8080").replace(
    /\/$/,
    "",
  );
}

export async function gatewayFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const auth = useAuthStore();
  const headers = new Headers(init.headers);
  if (auth.accessToken) {
    headers.set("Authorization", `Bearer ${auth.accessToken}`);
  }
  if (auth.workspaceId) {
    headers.set("X-Workspace-ID", auth.workspaceId);
  }
  const url = path.startsWith("http") ? path : `${gatewayBase()}${path}`;
  return fetch(url, { ...init, headers });
}
