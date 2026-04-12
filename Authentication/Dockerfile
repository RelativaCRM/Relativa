FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project references resolve to repo-root siblings (see Infrastructure -> Persistence).
COPY Authentication/src/Relativa.Authentication.Domain/Relativa.Authentication.Domain.csproj Authentication/src/Relativa.Authentication.Domain/
COPY Authentication/src/Relativa.Authentication.Application/Relativa.Authentication.Application.csproj Authentication/src/Relativa.Authentication.Application/
COPY Persistence/src/Relativa.Persistence/Relativa.Persistence.csproj Persistence/src/Relativa.Persistence/
COPY Authentication/src/Relativa.Authentication.Infrastructure/Relativa.Authentication.Infrastructure.csproj Authentication/src/Relativa.Authentication.Infrastructure/
COPY Authentication/src/Relativa.Authentication/Relativa.Authentication.csproj Authentication/src/Relativa.Authentication/
RUN dotnet restore Authentication/src/Relativa.Authentication/Relativa.Authentication.csproj

COPY Authentication/src/ Authentication/src/
COPY Persistence/src/ Persistence/src/
RUN dotnet publish Authentication/src/Relativa.Authentication/Relativa.Authentication.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8081

ENTRYPOINT ["dotnet", "Relativa.Authentication.dll"]
