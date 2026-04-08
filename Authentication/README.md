# relativa-auth

Сервіс автентифікації на **ASP.NET Core 10**. **Solution** у корені; **Web API** у `src/Relativa.Authentication/`.

## Порт

- **8081**

## Стек (пакети)

- `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt` — видача/перевірка JWT (підключити в наступних ітераціях).
- `BCrypt.Net-Next` — хешування паролів (підключити в наступних ітераціях).

## Конфігурація

- `Jwt:AccessTokenMinutes` — **15** (цільовий TTL access-токена).
- `Jwt:RefreshTokenDays` — **7** (цільовий TTL refresh-токена).

## API-заглушки

- `POST /login` → **501 Not Implemented** (місце під контракт).
- `POST /refresh` → **501 Not Implemented**.

## Команди

```bash
dotnet restore Relativa.Authentication.sln
dotnet build Relativa.Authentication.sln
dotnet run --project src/Relativa.Authentication
```

## Примітки

Чорний список токенів і повний flow логіну поза межами цього скелета; реалізація має узгоджуватися з тими ж JWT-налаштуваннями, що очікує gateway.
