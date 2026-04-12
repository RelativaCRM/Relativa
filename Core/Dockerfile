FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Relativa.Core.Domain/Relativa.Core.Domain.csproj src/Relativa.Core.Domain/
COPY src/Relativa.Core.Application/Relativa.Core.Application.csproj src/Relativa.Core.Application/
COPY src/Relativa.Core.Infrastructure/Relativa.Core.Infrastructure.csproj src/Relativa.Core.Infrastructure/
COPY src/Relativa.Core/Relativa.Core.csproj src/Relativa.Core/
RUN dotnet restore src/Relativa.Core/Relativa.Core.csproj

COPY src/ src/
RUN dotnet publish src/Relativa.Core/Relativa.Core.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8082

ENTRYPOINT ["dotnet", "Relativa.Core.dll"]
