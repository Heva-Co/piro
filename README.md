<div align="center">
  <img src="apps/web/public/piro.svg" alt="Piro" width="80" height="80" />
  <h1>Piro</h1>
  <p><strong>Enterprise-grade open-source status page and uptime monitoring</strong></p>
  <p>
    <a href="https://github.com/Heva-Co/piro/releases"><img src="https://img.shields.io/github/v/release/Heva-Co/piro?label=release&color=0ea5e9&logo=docker" alt="Release" /></a>
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

Piro is a **self-hosted, enterprise-grade status page, uptime monitoring platform and incident response platform** built for engineering teams that demand full control over their infrastructure. Run it on your own servers, connect distributed workers from any region, and give your users real-time visibility into your services — without sending your data to a third party.

Built by [Heva](https://heva.co) and released as open source to give every team access to the kind of monitoring tooling that was previously only available in expensive SaaS platforms.

## Why open source?

Monitoring and status pages are critical infrastructure. We believe teams shouldn't have to choose between vendor lock-in and building from scratch. Piro is our contribution to the community — a production-ready, self-hostable alternative to StatusPage, Instatus, or Betterstack that you own completely.

We built this for ourselves first, running it internally at Heva to monitor our own services. After reaching a stable, feature-complete core, we decided to open it up. We welcome contributions, bug reports, and the kind of real-world feedback that makes software better.

## Features

### Core platform
- **Multi-region monitoring** — Deploy lightweight workers to any cloud, on-prem server, or bare metal machine. Workers connect back to the API over SignalR and can be scaled independently across regions
- **Public status page** — Fully branded, real-time status page with uptime history, latency trends, and incident timeline
- **Incident management** — Structured incidents with timeline updates, severity levels, and manual alert-to-incident linking — you decide when an alert becomes a customer-facing incident, never automatically
- **Maintenance windows** — Schedule maintenance windows with automatic notifications and status suppression during the window
- **Config-as-code** — Define your entire monitoring setup in YAML and import via the admin panel or API; version-control your observability config

### On-call & escalation
- **On-call schedules** — RRULE-based rotation layers with overrides, timezone-aware, visualized on a Gantt-style calendar
- **Escalation policies** — Per-service policies with ordered steps, delays, and re-escalation after inactivity; a policy can be reused across any number of services
- **Personal on-call calendar** — Every user sees their own upcoming shifts and an "on-call now" indicator in their profile
- **Verified notification channels** — Personal alert channels (Email, Telegram, SMS via Twilio, ntfy) require a one-time code confirmation before they're used for paging, so a typo never means a missed page

### Alerting
- **Flexible alert rules** — Configure thresholds on status, latency, or uptime percentage with tunable failure/success thresholds to reduce noise
- **Personal, prioritized delivery** — Each on-call user configures their own ordered list of notification channels; escalation tries them in priority order and falls back to email if every configured channel fails
- **Custom templates** — Scriban-based message templates per channel (Email, Telegram, SMS, Pushover, ntfy, and more)

### Enterprise & security
- **OIDC / SSO** — Single sign-on with Google, Microsoft, or any standard OIDC/OAuth2 provider; enforce SSO-only login policy
- **RBAC** — Owner, Admin, and Member roles with email-based invitations; audit-ready permission model
- **API-first** — Full REST API with OpenAPI 3.1 spec; every admin operation is available programmatically
- **Self-hosted** — Your data stays on your infrastructure; no telemetry required (opt-out in settings)
- **Branding** — Upload logo, favicon, and social preview image; customize site name, URL, and meta tags

### Roadmap
The following capabilities are planned for upcoming releases:

- **Voice call alerts** — Direct phone paging via Twilio Programmable Voice for critical incidents, alongside the existing SMS channel
- **SLA / uptime reports** — Exportable uptime SLA reports per service, per time range
- **Synthetic transaction monitoring** — Multi-step HTTP sequences (login → action → assert) for end-to-end checks
- **Status page subscriptions** — Email and webhook subscriptions for end-users to receive incident updates
- **Audit log** — Full audit trail of admin actions for compliance requirements
- **Two-factor authentication** — TOTP-based 2FA for local accounts

## Architecture

```
Browser ──▶ nginx (proxy) ──▶ Next.js public status page (apps/web)
                          ──▶ Vite admin panel (apps/admin)
                          ──▶ Piro API (ASP.NET Core 10) ──▶ PostgreSQL
                                       │ SignalR
                          ┌────────────┼────────────┐
                          ▼            ▼            ▼
                     Worker (EU)  Worker (US)  Worker (custom)
```

The API can execute checks in-process (single-region setups) or dispatch them to standalone Workers over SignalR for multi-region coverage. Workers are stateless, self-contained binaries — they receive check assignments, execute HTTP / DNS / SSL / TCP / heartbeat checks, and stream results back in real time.

## Docker Images

| Image | Latest | Platforms |
|---|---|---|
| `ghcr.io/heva-co/piro-api` | `latest` | `linux/amd64`, `linux/arm64` |
| `ghcr.io/heva-co/piro-worker` | `latest` | `linux/amd64`, `linux/arm64` |
| `ghcr.io/heva-co/piro-web` | `latest` | `linux/amd64` |
| `ghcr.io/heva-co/piro-proxy` | `latest` | `linux/amd64` |

Pre-built binaries for Linux, macOS, and Windows are available on the [Releases](https://github.com/Heva-Co/piro/releases) page. Self-contained, no runtime required.

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

Copyright © 2025 [Heva Inc.](https://heva.co)

---

<div align="center">
  <sub>Built with ♥ by <a href="https://heva.co">Heva</a> · <a href="mailto:devops@heva.co">devops@heva.co</a></sub>
</div>
