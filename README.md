<div align="center">
  <img src="apps/web/public/piro.svg" alt="Piro" width="80" height="80" />
  <h1>Piro</h1>
  <p><strong>Enterprise-grade open-source status page and uptime monitoring</strong></p>
  <p>
    <a href="https://github.com/Heva-Co/piro/releases"><img src="https://img.shields.io/github/v/release/Heva-Co/piro?include_prereleases&label=release&color=0ea5e9&logo=docker" alt="Release" /></a>
    <a href="https://github.com/Heva-Co/piro/actions/workflows/release.yml"><img src="https://github.com/Heva-Co/piro/actions/workflows/release.yml/badge.svg" alt="Release build" /></a>
    <a href="https://github.com/Heva-Co/piro/actions/workflows/ci.yml"><img src="https://github.com/Heva-Co/piro/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-22c55e" alt="License" /></a>
    <a href="https://github.com/Heva-Co/piro/issues"><img src="https://img.shields.io/github/issues/Heva-Co/piro" alt="Issues" /></a>
  </p>
  <p>
    <a href="https://github.com/Heva-Co/piro/wiki">Documentation</a> ·
    <a href="https://github.com/Heva-Co/piro/wiki/Self-Hosting">Self-Hosting Guide</a> ·
    <a href="https://github.com/Heva-Co/piro/issues/new?template=bug_report.md">Report a Bug</a> ·
    <a href="https://github.com/Heva-Co/piro/issues/new?template=feature_request.md">Request a Feature</a>
  </p>
</div>

---

Piro is a **self-hosted, enterprise-grade status page, uptime monitoring, and incident-response platform** built for engineering teams that demand full control over their infrastructure. Run checks from any region, page the right on-call engineer, and give your users a real-time status page — all on servers you own, with no data leaving your infrastructure.

It pairs active monitoring (HTTP, DNS, SSL, TCP, Ping, gRPC, sandboxed JavaScript, and inbound heartbeats) with the operational layer most status pages leave out: on-call rotations, escalation policies, verified personal alert channels, end-to-end encrypted mobile push, incidents, postmortems, and maintenance windows. Configure it through the admin panel, the REST API, or YAML in Git via the `piro` CLI.

Built by [heva](https://heva.co) and released under the AGPL. Read the story and the reasoning behind it in **[MOTIVATION.md](MOTIVATION.md)**.

## Why it exists

Monitoring and status pages are critical infrastructure, yet the good tooling has long sat behind expensive per-seat SaaS. We believe teams shouldn't have to choose between vendor lock-in and building it all from scratch. Piro is our contribution to the community — a production-ready, self-hostable alternative to StatusPage, Instatus, or Better Stack that you own completely.

We built it for ourselves first, running it internally at heva to monitor our own services, and we dogfood it in production. We welcome contributions, bug reports, and the kind of real-world feedback that makes software better. The full backstory is in **[MOTIVATION.md](MOTIVATION.md)**.

## Features

### Core platform
- **Multi-region monitoring** — Deploy lightweight workers to any cloud, on-prem server, or bare-metal machine. Workers connect back to the API over SignalR and scale independently across regions; single-region setups run checks in-process with no separate worker
- **Many check types** — HTTP, DNS (with expected-value matching), SSL certificate expiry, TCP, Ping, gRPC health, plus GCP Cloud Run Job when that integration is configured — each with tunable intervals and per-region assignment
- **Script checks** — Write a check in JavaScript when the built-in types don't fit: drive your own HTTP calls, assert on any part of the response, and report custom dimensions. Runs in a sandbox with no filesystem, no CLR access, no timers, a response-size cap, and an SSRF guard that rejects loopback, link-local, metadata, and private addresses
- **Heartbeat checks** — For cron jobs, batch pipelines, and anything Piro cannot reach: the job pings Piro on completion and the check goes down when a ping fails to arrive within its grace period
- **Public status page** — Branded, real-time status page with uptime history, latency trends, incidents, and scheduled maintenances
- **Incident management** — Structured incidents with timeline updates, severity levels, and manual alert-to-incident linking — you decide when an alert becomes a customer-facing incident, never automatically
- **Postmortems** — Standalone post-incident reviews linked to one or more incidents, with a timeline, configurable field templates so every review asks the same questions, and PDF export for sharing outside the tool
- **Maintenance windows** — Schedule maintenance windows that automatically suppress a service's public status during the window
- **Service dependencies** — Declare which services depend on which, and let a failure propagate to what it actually affects. Cycles are rejected when the edge is created, not discovered later
- **Tags** — Label services, checks, and workers, and pin a check to workers carrying specific tags, so a check that must run inside a particular network only ever lands on a worker there

### On-call & escalation
- **On-call schedules** — RRULE-based rotation layers with overrides, timezone-aware, visualized on a Gantt-style calendar
- **Escalation policies** — Per-service policies with ordered steps, delays, per-step retries, and re-escalation after inactivity; a policy can be reused across any number of services
- **Personal on-call calendar** — Every user sees their own upcoming shifts and an "on-call now" indicator in their profile
- **Verified notification channels** — Personal alert channels (Email, Telegram, SMS via Twilio, ntfy) require a one-time code confirmation before they're used for paging, so a typo never means a missed page

### Alerting
- **Flexible alert rules** — Configure thresholds on status, latency, certificate expiry, or failed name servers, with tunable failure/success thresholds to reduce noise
- **Personal, prioritized delivery** — Each on-call user configures their own ordered list of notification channels; escalation tries them in priority order and falls back to email if every configured channel fails
- **Per-channel message formatting** — Built-in Scriban templates render each channel's messages appropriately (Email, Telegram, SMS, ntfy)

### Integrations
- **Personal paging** — Email, Telegram, Twilio SMS, and ntfy, each verified by a one-time code before it is ever paged
- **Team channels** — Post to a Google Chat space, or subscribe it to the events you care about
- **Mobile push** — Android and iOS apps (in this repo under `apps/mobile`, build them yourself) with end-to-end encrypted push: the payload is sealed for the device, so neither Apple, Google, nor a relay can read what an alert says. Send straight from your own instance, or through a relay when you'd rather not expose it to APNs and FCM
- **Jira** — Create an issue from an alert or incident, with project and issue-type discovery and OAuth connection
- **Generic outbound webhook** — Zapier and Make compatible, for anything without a first-class integration
- **GCP Cloud Monitoring** — Ingest Google Cloud Monitoring alert webhooks as Piro alerts
- **Integration SDK** — Integrations are self-describing: one class declares its config schema, capabilities, and actions, and the admin UI builds its forms from that. Adding one takes no changes to Piro's core

### Configuration as code
- **`piro.yaml`** — Declare services, checks, and alert rules in YAML and keep them in Git, reviewed like any other change. What the file does not declare, it does not touch, so a field set in the admin panel survives an apply and adopting it for two of twenty services leaves the other eighteen alone
- **The `piro` CLI** — A single self-contained binary for Linux, macOS, and Windows. `piro plan` diffs against your instance and writes nothing, `piro apply` applies in one transaction, `piro export` bootstraps a file from an instance you already run
- **CI-native** — Plan on a pull request, apply on merge. Exit codes are a contract, so CI can gate on pending changes without parsing output. Piro holds no forge credentials and reaches out to nothing
- **Editor support** — `piro schema` downloads a JSON Schema generated from your instance's own check types, so an editor completes and validates each check's fields, including check types your instance has and stock Piro doesn't
- **Browser sign-in** — `piro login` authenticates through your browser, so it works on an SSO-only instance where there is no password for a prompt to collect. The CLI gets its own revocable session, not your browser's

### Enterprise & security
- **OIDC and SAML 2.0 SSO** — Single sign-on with Google, Microsoft, GitHub, any standard OIDC/OAuth2 provider, or a SAML 2.0 identity provider such as Okta, Entra ID, or Keycloak; enforce an SSO-only login policy
- **RBAC** — Owner, Admin, Member, and Viewer roles with email-based invitations
- **Per-device sessions** — Signing in on a second device no longer evicts the first. Each session refreshes and is revoked independently, so ending one leaves the rest alone
- **Encrypted secrets at rest** — Integration credentials and secret fields are encrypted in the database
- **API-first** — Full REST API with an OpenAPI 3.1 spec; every admin operation is available programmatically
- **Self-hosted** — Your data stays on your infrastructure. No product telemetry, tracking, or phone-home
- **Branding** — Upload logo, favicon, and social preview image; customize site name, URL, and meta tags

## Architecture

```mermaid
flowchart LR
    B[Browser] --> P[nginx proxy]
    P --> W["Public status page<br/>(Next.js · apps/web)"]
    P --> A["Admin panel<br/>(Vite SPA · apps/admin)"]
    P --> API["Piro API<br/>(ASP.NET Core 10)"]
    API --> DB[(PostgreSQL)]
    API -. SignalR .-> WK1["Worker (EU)"]
    API -. SignalR .-> WK2["Worker (US)"]
    API -. SignalR .-> WK3["Worker (custom)"]
```

The API can execute checks in-process (single-region setups) or dispatch them to standalone Workers over SignalR for multi-region coverage. Workers are stateless, self-contained binaries: they receive check assignments, execute them, and stream results back in real time. A check can be pinned to workers carrying specific tags, so one that must run inside a particular network only ever lands on a worker there.

The `piro` CLI is a separate, self-contained binary that talks to the same REST API the admin panel uses. It runs on a laptop or in CI and needs no runtime installed.

## Docker Images

| Image | Latest | Platforms |
|---|---|---|
| `ghcr.io/heva-co/piro-api` | `latest` | `linux/amd64`, `linux/arm64` |
| `ghcr.io/heva-co/piro-worker` | `latest` | `linux/amd64`, `linux/arm64` |
| `ghcr.io/heva-co/piro-web` | `latest` | `linux/amd64` |
| `ghcr.io/heva-co/piro-proxy` | `latest` | `linux/amd64` |

All four images ship under the same version tag on every [release](https://github.com/Heva-Co/piro/releases), so there's a single `PIRO_VERSION` to pin. Each release also attaches a ready-to-run `docker-compose.release.yml` with the version resolved — `docker compose -f docker-compose.release.yml up` runs that exact release, no source checkout required.

## The `piro` CLI

Every release also attaches `piro` binaries for `linux-x64`, `linux-arm64`, `osx-arm64`, and `win-x64`. They are self-contained: no .NET runtime to install.

```bash
# Point it at your instance and authenticate
export PIRO_URL=https://status.example.com
piro login                    # or: export PIRO_API_KEY=<a Full-scope key>

piro init                     # scaffold piro.config.yml and an example piro.yaml
piro export -o piro.yaml      # bootstrap from an instance you already run
piro plan                     # show what would change; writes nothing
piro apply                    # apply it
```

`piro plan` exits `0` when nothing would change and `2` when changes are pending, so CI can gate a pull request without parsing output. See [`docs/config-as-code/github-actions.yml`](docs/config-as-code/github-actions.yml) for a workflow that plans on a pull request and applies on merge.

→ **[Self-Hosting Guide](https://github.com/Heva-Co/piro/wiki/Self-Hosting)** — Docker Compose quickstart and full configuration reference.

## Contributing

We welcome contributions from the community. See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on opening issues, submitting pull requests, and running the project locally.

Good first issues are tagged [`good first issue`](https://github.com/Heva-Co/piro/issues?q=label%3A%22good+first+issue%22).

## Security

Please **do not** report security vulnerabilities via GitHub issues. Email [devops@heva.co](mailto:devops@heva.co) instead. We aim to respond within 48 hours.

## License

Piro is open-source software released under the [GNU Affero General Public License v3.0](LICENSE) (AGPL-3.0).

**You are free to:** deploy Piro on your own infrastructure, use it internally, and make private modifications for internal use — with no obligation to publish anything.

**You may not:** host Piro as a paid or public managed service for third parties without publishing your modifications under the same license. If you offer Piro (modified or not) as a service to others, the AGPL requires you to make your source code available.

Copyright © 2025 [heva Inc.](https://heva.co)

---

<div align="center">
  <sub>Built with ♥ by <a href="https://heva.co">heva</a> · <a href="mailto:devops@heva.co">devops@heva.co</a></sub>
</div>
