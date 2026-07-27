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
COPY src/Piro.Worker/Piro.Worker.csproj                                 src/Piro.Worker/
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
