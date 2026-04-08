# relativa-audit

API аудиту на **ASP.NET Core 10** (читання/запис; у повній архітектурі — єдина точка запису в `entity_audit_log`). **Solution** у корені; **Web API** у `src/Relativa.Audit/`.

## Порт

- **8086**

## Авторизація (заглушка)

- Політика **`AuditReaders`** — потрібні JWT-клейми `role=Admin` або `role=Analyst` (той самий парсер токена-заглушки, що в інших сервісах: реальна перевірка підпису поки відсутня).

## API

- `GET /audit-log` — порожній масив-заглушка; потрібна політика `AuditReaders`.

## Команди

```bash
dotnet restore Relativa.Audit.sln
dotnet build Relativa.Audit.sln
dotnet run --project src/Relativa.Audit
```

## Архітектура

Core (та інші) публікуватимуть доменні події, які споживаються тут, щоб **аудит лишався єдиною точкою збереження** журналу. Клієнти як і раніше звертаються лише до **gateway**.
