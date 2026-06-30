# Piro — Claude Code Guide

## Project overview

Piro is an open-source uptime and status-page platform. It consists of:

| Component | Tech | Location |
|---|---|---|
| API | ASP.NET Core 10 (.NET 10) | `src/Piro.Api/` |
| Worker | ASP.NET Core 10 | `src/Piro.Worker/` |
| Public status page | Next.js 16 + React 19 + Tailwind 4 | `apps/web/` |
| Admin panel | Vite + React (SPA) | `apps/admin/` |
| Reverse proxy | nginx | `nginx/` |

Package manager: **pnpm** (never npm or yarn).

---

## Architecture

```
Browser → nginx (proxy) → Next.js web app (apps/web)
                       → ASP.NET Core API (src/Piro.Api)
                            ↓
                       PostgreSQL (db)
                            ↓
                       Piro.Worker (optional, multi-region checks via SignalR)
```

- The API runs checks in-process when `PIRO_API_WORKER=true` (default in development).
- The standalone Worker is for multi-region setups; not required for single-region.
- `apps/admin` is served as a static SPA — it communicates directly with the API.

---

## Running locally

### API
```bash
cd src/Piro.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
# http://localhost:5117
```
`appsettings.Development.json` is loaded automatically. Migrations run on startup.

### Public status page (Next.js)
```bash
cd apps/web
pnpm install
pnpm dev
# http://localhost:3000
```

### Admin panel
```bash
cd apps/admin
pnpm install
pnpm dev
# http://localhost:5173
```

### Full stack (Docker)
```bash
docker compose up
```
Services: proxy (`:80`), web, api, db (postgres).

---

## Backend (.NET)

### Project structure
- `Piro.Domain` — entities, enums, exceptions (no dependencies)
- `Piro.Application` — interfaces, DTOs, services (depends on Domain)
- `Piro.Infrastructure` — EF Core, repositories, alert dispatchers, jobs (depends on Application)
- `Piro.Api` — ASP.NET Core controllers, middleware, Program.cs
- `Piro.Worker` — standalone worker process

### Key patterns
- **Alert dispatchers** implement `ITriggerDispatcher` and live in `src/Piro.Infrastructure/Alerts/`. Each dispatcher handles one `TriggerType` enum value. Register in `InfrastructureServiceExtensions.cs`.
- **Background jobs** use Quartz.NET with a persistent PostgreSQL store.
- **Check execution** routes through `RoutingCheckJobDispatcher` → `LocalCheckJobDispatcher` (in-process) or `RemoteCheckJobDispatcher` (SignalR workers).

### Build & test
```bash
dotnet build Piro.slnx
dotnet test
```

---

## Frontend

### apps/web (Next.js — public status page)
- Next.js 16, React 19, Tailwind CSS 4, TypeScript
- Communicates with the API via `INTERNAL_API_URL` (server-side) or a proxy

### apps/admin (Vite SPA — admin panel)
- Vite, React, Tailwind CSS 4, TypeScript
- All API calls go through `/admin/api` server route

### Common
- Use `pnpm` in all scripts and Dockerfiles
- No SvelteKit — the project was fully migrated to Next.js + Vite

---

## Docker images (GHCR)

| Image | Tag pattern | Trigger |
|---|---|---|
| `ghcr.io/heva-co/piro-api` | `api/v*` | `release-api.yml` |
| `ghcr.io/heva-co/piro-worker` | `worker/v*` | `release-worker.yml` |
| `ghcr.io/heva-co/piro-web` | `web/v*` | `release-web.yml` |
| `ghcr.io/heva-co/piro-proxy` | `proxy/v*` | `release-proxy.yml` |

`docker-compose.yml` uses pre-built images — no source code needed to run the stack.

---

## Workflow

- All changes go through a feature branch + PR — never push directly to `main`.
- Never merge a PR without explicit user approval.
- Use conventional commits (`feat:`, `fix:`, `ci:`, etc.).
