FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY Persistence/src/Relativa.Persistence/Relativa.Persistence.csproj Persistence/src/Relativa.Persistence/
COPY Migration/src/Relativa.Migration/Relativa.Migration.csproj Migration/src/Relativa.Migration/
RUN dotnet restore Migration/src/Relativa.Migration/Relativa.Migration.csproj

COPY Migration/src/ Migration/src/
COPY Persistence/src/ Persistence/src/
RUN dotnet publish Migration/src/Relativa.Migration/Relativa.Migration.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY Migration/entrypoint.sh /app/entrypoint.sh

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/* \
    && chmod +x /app/entrypoint.sh

ENV DOTNET_NOLOGO=1

ENTRYPOINT ["/app/entrypoint.sh"]
