# API de Mundialito (§12). Multi-stage: se compila con el SDK y se ejecuta con la imagen liviana de ASP.NET.

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: mientras no cambien las dependencias, Docker reutiliza el restore cacheado.
COPY src/Mundialito.Domain/Mundialito.Domain.csproj src/Mundialito.Domain/
COPY src/Mundialito.Application/Mundialito.Application.csproj src/Mundialito.Application/
COPY src/Mundialito.Infrastructure/Mundialito.Infrastructure.csproj src/Mundialito.Infrastructure/
COPY src/Mundialito.Api/Mundialito.Api.csproj src/Mundialito.Api/
RUN dotnet restore src/Mundialito.Api/Mundialito.Api.csproj

COPY src/ src/
RUN dotnet publish src/Mundialito.Api/Mundialito.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# La imagen de ASP.NET escucha en 8080 y trae un usuario sin privilegios: no se corre como root.
EXPOSE 8080
USER $APP_UID

# Al arrancar: espera a SQL Server, migra y siembra (DatabaseInitializer), y recién entonces escucha.
ENTRYPOINT ["dotnet", "Mundialito.Api.dll"]
