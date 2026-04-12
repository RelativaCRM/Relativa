FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Relativa.Migration/Relativa.Migration.csproj src/Relativa.Migration/
RUN dotnet restore src/Relativa.Migration/Relativa.Migration.csproj

COPY src/ src/
RUN dotnet publish src/Relativa.Migration/Relativa.Migration.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_NOLOGO=1

ENTRYPOINT ["dotnet", "Relativa.Migration.dll"]
