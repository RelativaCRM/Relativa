FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /repo
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
COPY . .
RUN dotnet restore Relativa.Migration.sln && dotnet build src/Relativa.Migration/Relativa.Migration.csproj -c Release --no-restore
ENV DOTNET_NOLOGO=1
ENTRYPOINT ["/entrypoint.sh"]
