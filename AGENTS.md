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
- **Prefer regular method bodies over lambdas.** Write `private async Task<Foo> DoThing(...) { ... }`, not `private Func<Task<Foo>> DoThing = async (...) => { ... }`. Reserve lambdas for short inline callbacks passed to LINQ/collection methods (`.Where(x => ...)`, `.Select(x => ...)`, `.OrderBy(x => ...)`) — not for defining methods or services.

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
- **API types are generated from the backend's OpenAPI spec**, not hand-written. Run `pnpm run generate:api-types` in `apps/admin` (requires the API buildable locally — it invokes `dotnet build -t:GenerateOpenApiDocuments` under `ASPNETCORE_ENVIRONMENT=Development`, so a working `appsettings.Development.json` connection string is needed) to regenerate `src/lib/api-types.ts` after changing a DTO. New endpoint modules should alias the generated `components["schemas"][...]` types (see `src/lib/actions/services/index.ts` for the pattern) instead of hand-writing interfaces that can silently drift from the backend. Not every endpoint has been migrated yet — `src/lib/api.ts` still has hand-written interfaces for anything not yet moved to `lib/actions/*`.
- **Dates/times**: never call `toLocaleString`/`toLocaleDateString`/`new Date().toString()` directly in a component. Always format through the centralized helper in `src/utils/date.ts` (and its `useFormattedDate`-style hook, backed by `TimezoneProvider`). The display timezone defaults to the user's profile `timeZone` (`GET /api/v1/auth/me`), with the browser's detected timezone (`Intl.DateTimeFormat().resolvedOptions().timeZone`) offered as an override when it differs — same UX pattern as Google Calendar.
- **One component per `.tsx` file.** A page file (e.g. `AlertsPage.tsx`) exports exactly one component — the page itself. Never define helper components (`StatItem`, a skeleton, a row renderer, etc.) inline in a page or dialog file. Extract each to its own file under that feature's `components/` folder (e.g. `features/alerts/components/StatItem.tsx`, `features/alerts/components/StatItemSkeleton.tsx`), even if it's only used once — pages stay short and each piece is independently reusable/testable.
- **Loading skeletons are reusable components, not inline JSX.** Never hand-roll `<div className="animate-pulse ...">` blocks in a page. Build the skeleton as its own component (e.g. `TableSkeleton.tsx`, `StatItemSkeleton.tsx`) using the shadcn `Skeleton` primitive (`components/ui/skeleton.tsx`), shaped to mirror the real content it stands in for — so it can be reused anywhere that same shape of data loads.
- **Component props: never destructure inline in the parameter list.** Always take a single `props: Props` parameter and destructure inside the function body — `function Foo(props: Props) { const { a, b } = props; ... }`, never `function Foo({ a, b }: Props) { ... }`. Not ESLint-enforced (no reliable AST rule without false positives on non-component functions) — enforce via code review.

### Common
- Use `pnpm` in all scripts and Dockerfiles
- No SvelteKit — the project was fully migrated to Next.js + Vite
- **Type-check with `pnpm exec tsc -b`, not `tsc --noEmit`.** Both `apps/web` and `apps/admin` build via `tsc -b && vite build` (project references), and `-b` catches errors that a plain `--noEmit` run can miss or under-report due to incremental/composite project caching. Always run `tsc -b` before considering a frontend change verified — it's what CI actually runs.

---

## Docker images (GHCR)

A single `release.yml` workflow (triggered on GitHub Release publish) builds and pushes all four images, tagged `vX.Y.Z`:

| Image | Dockerfile |
|---|---|
| `ghcr.io/heva-co/piro-api` | `Dockerfile` |
| `ghcr.io/heva-co/piro-worker` | `src/Piro.Worker/Dockerfile` |
| `ghcr.io/heva-co/piro-web` | `apps/web/Dockerfile` |
| `ghcr.io/heva-co/piro-proxy` | `nginx/Dockerfile` |

- The `latest` tag is only added when the GitHub Release is **not** marked as a pre-release — publishing a release candidate (e.g. `v0.5.0-rc1`, checked "Set as a pre-release") never overwrites `latest`.
- All four images always ship under the same version — there's one `PIRO_VERSION` to pin, not four.
- The workflow also generates a `docker-compose.release.yml` (this repo's `docker-compose.yml` with `PIRO_VERSION` resolved to the release tag) and attaches it as a release asset, so `docker compose -f docker-compose.release.yml up` runs that exact release without needing to set any env var.

`docker-compose.yml` uses pre-built images — no source code needed to run the stack.

---

## Workflow

- All changes go through a feature branch + PR — never push directly to `main`.
- Never merge a PR without explicit user approval.
- Use conventional commits (`feat:`, `fix:`, `ci:`, etc.).
