# ─── Etapa 1: compilar ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# El simulador no depende de ninguna otra parte del proyecto, y eso es
# deliberado: si compartiera codigo con la API, podria pasar una prueba por
# usar las mismas suposiciones que el sistema que esta simulando.
COPY src/FevCore.DianSimulator/FevCore.DianSimulator.csproj src/FevCore.DianSimulator/
RUN dotnet restore src/FevCore.DianSimulator/FevCore.DianSimulator.csproj

COPY src/FevCore.DianSimulator/ src/FevCore.DianSimulator/
RUN dotnet publish src/FevCore.DianSimulator/FevCore.DianSimulator.csproj \
    --configuration Release \
    --output /app \
    --no-restore

# ─── Etapa 2: ejecutar ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FevCore.DianSimulator.dll"]
