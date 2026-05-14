# relativa-migrations

Запуск у стилі **init-container** для **EF Core** проти **PostgreSQL**. **Solution** у корені; опційний **console**-проєкт у `src/Relativa.Migration/` посилається на `Microsoft.EntityFrameworkCore.Design` для документування інструментів — **без дублювання DbContext**; міграції залишаються в **relativa-core**.

## Структура

- `Relativa.Migration.sln` — корінь.
- `src/Relativa.Migration/` — консоль-заглушка + пакет Design.
- `Dockerfile` — `mcr.microsoft.com/dotnet/sdk:10.0`, збірка проєкту, `ENTRYPOINT` викликає `entrypoint.sh`.
- `entrypoint.sh` — скелет: виводить підказку щодо env, **`exit 0`**. Замінити на `dotnet ef database update` або **EF migrations bundle** з артефактів CI Core.

## Оточення

- `ConnectionStrings__Default` — рядок підключення до PostgreSQL (стиль ключів конфігурації ASP.NET для контейнерів).

## CI / експлуатація

1. Зібрати Core; опублікувати збірку міграцій або виконати `dotnet ef migrations bundle`.
2. Цей образ (або job) запускає bundle/update проти `ConnectionStrings__Default`.
3. Успіх — код виходу **0**.

## Локальна збірка

```bash
dotnet restore Relativa.Migration.sln
dotnet build Relativa.Migration.sln
```

```bash
docker build -t relativa-migrations .
```
