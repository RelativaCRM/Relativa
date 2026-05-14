FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Relativa.Gateway/Relativa.Gateway.csproj src/Relativa.Gateway/
RUN dotnet restore src/Relativa.Gateway/Relativa.Gateway.csproj

COPY src/Relativa.Gateway/ src/Relativa.Gateway/
RUN dotnet publish src/Relativa.Gateway/Relativa.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Relativa.Gateway.dll"]
