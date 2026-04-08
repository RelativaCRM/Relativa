FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Relativa.Graph/Relativa.Graph.csproj src/Relativa.Graph/
RUN dotnet restore src/Relativa.Graph/Relativa.Graph.csproj

COPY src/Relativa.Graph/ src/Relativa.Graph/
RUN dotnet publish src/Relativa.Graph/Relativa.Graph.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8083

ENTRYPOINT ["dotnet", "Relativa.Graph.dll"]
