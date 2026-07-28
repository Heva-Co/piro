FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

ARG VERSION=0.0.0

# Restore dependencies first (layer cache-friendly): copying only the .csproj
# files keeps this layer valid across source edits.
# Every project in src/ must be listed, or restore misses it and the publish
# below fails with NETSDK1004. CI rebuilds these images on any .csproj
# change, so a missing entry fails the PR rather than the release.
COPY Piro.slnx ./
COPY src/Piro.Domain/Piro.Domain.csproj                                 src/Piro.Domain/
COPY src/Piro.Contracts/Piro.Contracts.csproj                           src/Piro.Contracts/
COPY src/Piro.Application/Piro.Application.csproj                       src/Piro.Application/
COPY src/Piro.Infrastructure/Piro.Infrastructure.csproj                 src/Piro.Infrastructure/
COPY src/Piro.Checks.Abstractions/Piro.Checks.Abstractions.csproj       src/Piro.Checks.Abstractions/
COPY src/Piro.Checks/Piro.Checks.csproj                                 src/Piro.Checks/
COPY src/Piro.Integrations.Abstractions/Piro.Integrations.Abstractions.csproj src/Piro.Integrations.Abstractions/
COPY src/Piro.Integrations.Gcp/Piro.Integrations.Gcp.csproj             src/Piro.Integrations.Gcp/
COPY src/Piro.Integrations.GoogleChat/Piro.Integrations.GoogleChat.csproj src/Piro.Integrations.GoogleChat/
COPY src/Piro.Integrations.GoogleCloud/Piro.Integrations.GoogleCloud.csproj src/Piro.Integrations.GoogleCloud/
COPY src/Piro.Integrations.Jira/Piro.Integrations.Jira.csproj           src/Piro.Integrations.Jira/
COPY src/Piro.Integrations.MobilePush/Piro.Integrations.MobilePush.csproj src/Piro.Integrations.MobilePush/
COPY src/Piro.Integrations.Ntfy/Piro.Integrations.Ntfy.csproj           src/Piro.Integrations.Ntfy/
COPY src/Piro.Integrations.Telegram/Piro.Integrations.Telegram.csproj   src/Piro.Integrations.Telegram/
COPY src/Piro.Integrations.Twilio/Piro.Integrations.Twilio.csproj       src/Piro.Integrations.Twilio/
COPY src/Piro.Integrations.Webhook/Piro.Integrations.Webhook.csproj     src/Piro.Integrations.Webhook/
COPY src/Piro.Api/Piro.Api.csproj                                       src/Piro.Api/
RUN dotnet restore src/Piro.Api/Piro.Api.csproj

# Build and publish
COPY src/ src/
RUN dotnet publish src/Piro.Api/Piro.Api.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=${VERSION} \
    --no-restore

# ── Runtime image ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Non-root user for security
RUN useradd --system --no-create-home appuser

# Create the mount points and hand them to appuser *before* dropping privileges. Docker creates a
# missing volume target as root:root, so without this the Data Protection key ring cannot be written:
# the API then fails to read its keyring and every request that encrypts a secret (an integration's
# token, a password-reset token, a stored OAuth token) returns a 500. Pre-creating the directory
# means Docker preserves this ownership when it mounts the volume over it.
#
# Both key paths are covered on purpose. /app/keys is what this repo's compose sets via
# DataProtection__KeysDirectory, but the code falls back to ./EncryptionKeys when that variable is
# absent — and a deployment that mounts its volume there instead hits exactly this failure. Covering
# the fallback means the image is safe whichever path an operator mounts.
RUN mkdir -p /app/keys /app/EncryptionKeys /app/wwwroot/uploads \
    && chown -R appuser:appuser /app

USER appuser

COPY --from=build --chown=appuser:appuser /app/publish ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Piro.Api.dll"]
