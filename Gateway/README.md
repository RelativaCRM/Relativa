# relativa-gateway

Зворотний проксі на **ASP.NET Core 10** для Relativa. **Solution** у корені репозиторію (`Relativa.Gateway.sln`); **веб-проєкт** у `src/Relativa.Gateway/` (файли `.sln` та основного `.csproj` не в одній теці).

## Порт

- **8080** (`Urls` у `appsettings.json` та launch profile).

## Стек

- **YARP** — маршрути `/auth`, `/core`, `/graph`, `/ml`, `/audit` з `PathRemovePrefix` до upstream на localhost **8081**, **8082**, **8083**, **8084**, **8086**.
- **JWT Bearer** — заглушка валідації (підпис/issuer поки не перевіряються); задуманий порядок: forwarded headers → authentication → authorization → reverse proxy.
- **RBAC** — заглушка middleware у `Program.cs` (розширити після визначення політик).

## Ендпоінти

- `GET /health` — анонімна перевірка здоров’я.
- Проксовані шляхи вимагають Bearer-токен (`MapReverseProxy().RequireAuthorization()`).

## Команди

```bash
dotnet restore Relativa.Gateway.sln
dotnet build Relativa.Gateway.sln
dotnet run --project src/Relativa.Gateway
```

## Інтеграція з клієнтом

Браузери та **relativa-client** мають звертатися лише до цього хоста, з заголовками `Authorization` та `X-Workspace-ID` на кожному запиті.
