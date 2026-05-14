FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project references resolve to repo-root siblings (see Infrastructure -> Persistence).
COPY Audit/src/Relativa.Audit.Domain/Relativa.Audit.Domain.csproj Audit/src/Relativa.Audit.Domain/
COPY Audit/src/Relativa.Audit.Application/Relativa.Audit.Application.csproj Audit/src/Relativa.Audit.Application/
COPY Persistence/src/Relativa.Persistence/Relativa.Persistence.csproj Persistence/src/Relativa.Persistence/
COPY Audit/src/Relativa.Audit.Infrastructure/Relativa.Audit.Infrastructure.csproj Audit/src/Relativa.Audit.Infrastructure/
COPY Audit/src/Relativa.Audit/Relativa.Audit.csproj Audit/src/Relativa.Audit/
RUN dotnet restore Audit/src/Relativa.Audit/Relativa.Audit.csproj

COPY Audit/src/ Audit/src/
COPY Persistence/src/ Persistence/src/
RUN dotnet publish Audit/src/Relativa.Audit/Relativa.Audit.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8086

ENTRYPOINT ["dotnet", "Relativa.Audit.dll"]
