FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

ARG VERSION=0.0.0

# Restore dependencies first (layer cache-friendly)
COPY Piro.slnx ./
COPY src/Piro.Domain/Piro.Domain.csproj           src/Piro.Domain/
COPY src/Piro.Application/Piro.Application.csproj src/Piro.Application/
COPY src/Piro.Infrastructure/Piro.Infrastructure.csproj src/Piro.Infrastructure/
COPY src/Piro.Worker/Piro.Worker.csproj           src/Piro.Worker/
RUN dotnet restore src/Piro.Worker/Piro.Worker.csproj

# Build and publish
COPY src/ src/
RUN dotnet publish src/Piro.Worker/Piro.Worker.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=${VERSION} \
    --no-restore

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --system --no-create-home appuser
USER appuser

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Piro.Worker.dll"]
