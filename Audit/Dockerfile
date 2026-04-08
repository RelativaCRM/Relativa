FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Relativa.Audit/Relativa.Audit.csproj src/Relativa.Audit/
RUN dotnet restore src/Relativa.Audit/Relativa.Audit.csproj

COPY src/Relativa.Audit/ src/Relativa.Audit/
RUN dotnet publish src/Relativa.Audit/Relativa.Audit.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8086

ENTRYPOINT ["dotnet", "Relativa.Audit.dll"]
