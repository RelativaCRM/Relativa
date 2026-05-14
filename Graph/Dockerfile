FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Persistence/src/Relativa.Persistence/Relativa.Persistence.csproj Persistence/src/Relativa.Persistence/
COPY Graph/src/Relativa.Graph/Relativa.Graph.csproj Graph/src/Relativa.Graph/
RUN dotnet restore Graph/src/Relativa.Graph/Relativa.Graph.csproj

COPY Graph/src/ Graph/src/
COPY Persistence/src/ Persistence/src/
RUN dotnet publish Graph/src/Relativa.Graph/Relativa.Graph.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8083

ENTRYPOINT ["dotnet", "Relativa.Graph.dll"]
