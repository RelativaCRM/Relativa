FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Relativa.Authentication/Relativa.Authentication.csproj src/Relativa.Authentication/
RUN dotnet restore src/Relativa.Authentication/Relativa.Authentication.csproj

COPY src/Relativa.Authentication/ src/Relativa.Authentication/
RUN dotnet publish src/Relativa.Authentication/Relativa.Authentication.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8081

ENTRYPOINT ["dotnet", "Relativa.Authentication.dll"]
