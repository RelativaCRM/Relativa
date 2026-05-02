FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Persistence/src/Relativa.Persistence/Relativa.Persistence.csproj Persistence/src/Relativa.Persistence/
COPY Messaging/src/Relativa.Messaging/Relativa.Messaging.csproj Messaging/src/Relativa.Messaging/
COPY Core/src/Relativa.Core.Domain/Relativa.Core.Domain.csproj Core/src/Relativa.Core.Domain/
COPY Core/src/Relativa.Core.Application/Relativa.Core.Application.csproj Core/src/Relativa.Core.Application/
COPY Core/src/Relativa.Core.Infrastructure/Relativa.Core.Infrastructure.csproj Core/src/Relativa.Core.Infrastructure/
COPY Core/src/Relativa.Core/Relativa.Core.csproj Core/src/Relativa.Core/
RUN dotnet restore Core/src/Relativa.Core/Relativa.Core.csproj

COPY Core/src/ Core/src/
COPY Persistence/src/ Persistence/src/
COPY Messaging/src/ Messaging/src/
RUN dotnet publish Core/src/Relativa.Core/Relativa.Core.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8082

ENTRYPOINT ["dotnet", "Relativa.Core.dll"]
