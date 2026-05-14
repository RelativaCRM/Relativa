# relativa-client

SPA на **Vue 3.5** та **Vite 6** для Relativa. Ініціалізовано офіційним ланцюжком `create-vue` (TypeScript, Vue Router, Pinia), далі закріплено версії **Vite 6.x**, **Vue Router 4.x**, **Pinia 2.x**. **D3 7** зазначено для майбутньої роботи з графом; у скелеті використано **vis-network 9** як мінімальний force-directed placeholder.

## Порт

- Dev-сервер: **3000** (`vite.config.ts`).

## Правила

- **Увесь трафік до бекенду — лише через relativa-gateway** (за замовчуванням `http://localhost:8080`). Задайте `VITE_GATEWAY_URL` у `.env` (див. `.env.example`). Не викликайте з браузера напряму Auth, Core, Graph, ML чи Audit.
- HTTP-хелпер: `src/api/http.ts` — додає `Authorization: Bearer …` та `X-Workspace-ID` з Pinia (`useAuthStore`).
- **Сесія:** JWT-заглушка в **localStorage** (`useAuthStore`); після перезавантаження токен зберігається, доки його не скинути.
- **Маршрутизація:** `src/router/index.ts` — заглушка `beforeEach` використовує `useAuthStore().roles` (реальний RBAC після інтеграції з gateway/auth).

## Структура

- `src/stores/` — `useAuthStore`, `useGraphStore`, `useEntityStore`, `useAuditStore` (заглушки).
- `src/views/` — Home + Graph (демо vis-network).
- `src/api/` — обгортка fetch лише до gateway.

## Команди

```bash
npm install
npm run dev
npm run build
```

## Розкладка .NET

Не застосовується — репозиторій лише для фронтенду.
