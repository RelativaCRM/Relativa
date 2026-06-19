import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth';

function hubUrl(): string {
  const base = (import.meta.env.VITE_GATEWAY_URL ?? 'http://localhost:8080').replace(/\/$/, '');
  return `${base}/core/hubs/core`;
}

export function buildCoreHubConnection(): signalR.HubConnection {
  const auth = useAuthStore();
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl(), {
      accessTokenFactory: () => auth.accessToken ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}
