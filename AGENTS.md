# Piro — Agent Guide

## Repo layout

```
apps/
  web/          Next.js 16 public status page (pnpm)
  admin/        Vite + React admin SPA (pnpm)
src/
  Piro.Api/         ASP.NET Core 10 REST API
  Piro.Application/ Application services & interfaces
  Piro.Domain/      Entities, enums, domain exceptions
  Piro.Infrastructure/ EF Core, repositories, alert dispatchers, Quartz jobs
  Piro.Worker/      Standalone check-runner (multi-region)
tests/
nginx/          Reverse proxy config
docker-compose.yml   Production stack (uses GHCR images — no source needed)
```

## How to run

### API (development)
```bash
cd src/Piro.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
```
Runs on `http://localhost:5117`. Migrations apply automatically on startup.

### Public web app
```bash
cd apps/web && pnpm install && pnpm dev   # http://localhost:3000
```

### Admin panel
```bash
cd apps/admin && pnpm install && pnpm dev  # http://localhost:5173
```

### Full stack
```bash
docker compose up   # proxy :80, web, api :8080, postgres :5432
```

## Build & verify

```bash
# .NET
dotnet build Piro.slnx --configuration Release
dotnet test

# Frontend
cd apps/web && pnpm build
cd apps/admin && pnpm build
```

Always run `dotnet build` after backend changes and the relevant `pnpm build` after frontend changes to catch type errors before committing.

## Key conventions

- **Package manager:** pnpm only — never npm or yarn, including in Dockerfiles and scripts.
- **Branches & PRs:** every change on a feature branch; never push directly to `main`; never merge without explicit approval.
- **Commits:** conventional commits (`feat:`, `fix:`, `ci:`, `refactor:`).
- **No SvelteKit:** the project was fully migrated to Next.js + Vite. Ignore any legacy SvelteKit references in older docs.

## Adding a new alert dispatcher (backend)

1. Add the value to `TriggerType` enum in `src/Piro.Domain/Enums/TriggerType.cs`.
2. Create `src/Piro.Infrastructure/Alerts/YourTypeTriggerDispatcher.cs` implementing `ITriggerDispatcher`.
3. Register it in `InfrastructureServiceExtensions.AddInfrastructure()`.
4. Add the type to the trigger form in `apps/admin/` (`TRIGGER_TYPES` array, `buildMetaJson`, `loadMeta`, validation, UI section).
5. Update `TriggerType` in `apps/admin/src/lib/api.ts`.

## Docker releases

| Image | Tag | Workflow trigger |
|---|---|---|
| `ghcr.io/heva-co/piro-api` | `api/v*` | `release-api.yml` |
| `ghcr.io/heva-co/piro-worker` | `worker/v*` | `release-worker.yml` |
| `ghcr.io/heva-co/piro-web` | `web/v*` | `release-web.yml` |
| `ghcr.io/heva-co/piro-proxy` | `proxy/v*` | `release-proxy.yml` |

To publish a release, push a tag: `git tag api/v0.3.0 && git push origin api/v0.3.0`.
