# ─── Etapa 1: compilar ───────────────────────────────────────────────
# Usa la imagen con el SDK completo, que pesa mucho pero sabe compilar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copian primero solo los .csproj y se restauran las dependencias.
# Docker guarda en cache el resultado de cada instruccion: mientras estos
# archivos no cambien, no vuelve a descargar paquetes aunque cambie el codigo.
COPY src/FevCore.Domain/FevCore.Domain.csproj                 src/FevCore.Domain/
COPY src/FevCore.Application/FevCore.Application.csproj       src/FevCore.Application/
COPY src/FevCore.Infrastructure/FevCore.Infrastructure.csproj src/FevCore.Infrastructure/
COPY src/FevCore.Api/FevCore.Api.csproj                       src/FevCore.Api/
RUN dotnet restore src/FevCore.Api/FevCore.Api.csproj

# Ahora si el codigo fuente.
COPY src/ src/
RUN dotnet publish src/FevCore.Api/FevCore.Api.csproj \
    --configuration Release \
    --output /app \
    --no-restore

# ─── Etapa 2: ejecutar ───────────────────────────────────────────────
# Imagen mucho mas liviana: solo sabe ejecutar, no compilar.
# El SDK y el codigo fuente no viajan a la imagen final.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Npgsql busca esta biblioteca al arrancar, por si la conexion usara
# autenticacion Kerberos. La nuestra usa usuario y contrasena, asi que no
# la necesita, pero sin ella imprime un error rojo en cada arranque que
# parece grave y no lo es. Dos megabytes a cambio de registros limpios.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FevCore.Api.dll"]
