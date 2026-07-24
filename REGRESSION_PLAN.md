# Regression Test Plan — RFC 0016 Dimension Model + Check SDK cutover

Manual regression pass after the dimension-model refactor. Run the sections **in order** — each
depends on the state the previous one leaves behind. Within a section, the ⚠️ items are the ones this
change directly touched (highest risk); the others are smoke checks that the surrounding flow still works.

## What changed (why this plan exists)

- Checks moved to the `Piro.Checks` SDK; the 7 per-type executors were replaced by one registry-backed
  adapter. The `CheckType` manifest attribute is gone (metadata now comes from each check's `CheckManifest`).
- The GCP Cloud Run Job check now lives **inside** the GoogleCloud integration and is only available
  when that integration is registered (it ships via `IIntegration.ProvidedChecks()`).
- Alerts: the `AlertFor` enum is gone. An `AlertConfig` now targets a `Dimension` (string) with a
  `Comparison` (Threshold/Equality) and `Direction`, evaluated generically.
- `CheckDataPoint` dropped `LatencyMs`/`MetricValue` columns; all numeric measurements live in a single
  `Dimensions` jsonb column. `Status` stays a column.
- Multi-region worker protocol (`WorkerResultMessage`) now carries the full `Dimensions` dict.
- Migrations collapsed from 54 to 2 (`InitialCreate` + `QuartzSchema`), applied on startup via
  `Database.Migrate()`.
- The YAML config-as-code import feature (endpoint, service, admin page) was removed.

---

## Pre-flight (do once, before anything)

- [ ] ⚠️ **Fresh DB comes up clean.** Point at an **empty** Postgres and start the API. Confirm startup
      applies both migrations with no error, creates the `qrtz_*` tables, and the app reaches "listening".
- [ ] ⚠️ Confirm `__EFMigrationsHistory` has exactly `InitialCreate` and `QuartzSchema`.
- [ ] Backend build + tests green: `dotnet build Piro.slnx` and `dotnet test`.
- [ ] Admin build green: `cd apps/admin && pnpm exec tsc -b && pnpm build`.
- [ ] Web build green: `cd apps/web && pnpm exec tsc -b && pnpm build`.

---

## 1. Startup / first-run wizard

*Depends on: a clean DB (pre-flight). Nothing else exists yet.*

- [ ] First-run wizard appears on a fresh DB and completes (admin account creation).
- [ ] Log in with the created account; land on the dashboard with no data.
- [ ] `GET /api/v1/auth/me` returns the profile (timezone, etc.) — the app shell renders.

## 2. Integrations (must come before checks — the GCP check needs one)

*Depends on: being logged in. Independent of services/checks.*

- [ ] Integrations list loads; every integration type appears in the "add" catalog.
- [ ] ⚠️ **GoogleCloud integration is present and connectable.** Create a GoogleCloud integration with a
      service-account JSON. It saves and shows connected. (This is the gate for the GCP check in §4.)
- [ ] A notification integration (e.g. Telegram/Ntfy/Webhook) connects and a test notification sends.
- [ ] Encryption round-trip: after saving an integration with a secret, reload the page — the secret
      field is masked, and the integration still works (DataProtection keyring intact).

## 3. Services

*Depends on: logged in. Checks attach to a service, so this precedes §4.*

- [ ] Create a service (slug, name). It appears in the list and its detail page opens.
- [ ] Edit service metadata; changes persist.
- [ ] Public status page renders the service (uptime is incident-derived — expect 100% / no data yet).

## 4. Checks — creation & the check-type catalog ⚠️ (core of this change)

*Depends on: a service (§3), and for the GCP case, the GoogleCloud integration (§2).*

- [ ] ⚠️ **Check-type picker loads from the registry.** `GET /api/v1/checks/types` returns every type
      (HTTP, DNS, TCP, Ping, SSL, gRPC, GCP Cloud Run Job) with label, description, config schema, and
      its `dimensions` list. No type is missing; none is duplicated.
- [ ] ⚠️ **GCP check gating.** The GCP Cloud Run Job type shows `requiredIntegrationType = "GoogleCloud"`.
      Creating one **without** a connected GoogleCloud integration is blocked in the form (the required-
      integration picker forces the choice). With the integration from §2 selected, it saves.
- [ ] Create one check of **each** type against a real target. Each saves and schedules.
      - [ ] ⚠️ HTTP (with `FollowRedirects` on **and** off — confirms the named-client selection).
      - [ ] DNS (with an expected value / name servers).
      - [ ] TCP, Ping.
      - [ ] SSL (against an HTTPS host with a valid cert).
      - [ ] gRPC (against a gRPC health endpoint).
      - [ ] ⚠️ GCP Cloud Run Job (project/region/job + the GoogleCloud integration instance).
- [ ] Interval/timeout validation still rejects a too-tight interval and a timeout ≥ interval.

## 5. Check execution & data points ⚠️ (the jsonb storage cutover)

*Depends on: checks exist (§4) and have run at least once (wait for the scheduler, or trigger a run).*

- [ ] ⚠️ **A data point is written per execution.** After a check runs, its recent-logs view shows rows
      with Status and the expected measurements.
- [ ] ⚠️ **Dimensions land in jsonb.** Inspect a `CheckDataPoints` row in the DB: `Dimensions` is a jsonb
      object with the right keys per type — HTTP/TCP/Ping/gRPC → `Latency`; SSL → `Latency` + `CertExpiry`;
      DNS → `Latency` + `FailedNameServers`; GCP → `LastRunAge` + `FailedTasks`. `Status` is a column.
- [ ] ⚠️ **Latency charts still render.** The check detail latency graph and daily stats show values
      (these queries were rewritten to read latency out of the jsonb column).
- [ ] Outcome mapping: a reachable target → UP; an unreachable/failed target → DOWN; a misconfigured
      check (e.g. bad host) → does not crash, logs an executor error (FAILURE), no alert fired.
- [ ] A soft HTTP body-rule failure keeps the check UP but records `BodyRuleFailures` > 0 in Dimensions.

## 6. Alerts — configuration & evaluation ⚠️ (AlertFor → Dimension)

*Depends on: a check with recent data points (§5). Alerts evaluate against a check's history.*

- [ ] ⚠️ **Alert form is data-driven.** On a check's Alert Configurations section, the "Dimension"
      picker lists exactly that check type's dimensions (from `meta.dimensions`) — e.g. SSL offers
      Status + CertExpiry, DNS offers Status + Latency + FailedNameServers. No stale hardcoded list.
- [ ] ⚠️ **Status alert (Equality).** Add an alert on `Status = DOWN`. The value input is a status
      **select** (not a number). Save; reload — it persists with Comparison=Equality.
- [ ] ⚠️ **Numeric alert (Threshold + Direction).** Add an alert on `Latency ≥ N` (HigherIsWorse) and,
      on the SSL check, `CertExpiry ≤ N` (LowerIsWorse). The value input is numeric. Save; reload —
      persists with Comparison=Threshold and the correct Direction.
- [ ] ⚠️ **Alert fires correctly.** Drive a check to breach its threshold (e.g. point HTTP latency alert
      very low, or Status=DOWN on an unreachable target). After `FailureThreshold` consecutive breaching
      points, an Alert is created; a notification dispatches to the linked integration.
- [ ] ⚠️ **Alert recovers.** Fix the target; after `SuccessThreshold` consecutive good points, the alert
      auto-resolves.
- [ ] ⚠️ **Consecutive-window still holds.** With `FailureThreshold = 3`, one bad point does **not** fire;
      three in a row do. (The streak keys on the AlertConfig, unchanged by the rename.)
- [ ] Alert detail page shows the fired alert's **Dimension** (not "Alert For") and value.

## 7. Multi-region worker (only if you run the standalone worker) ⚠️

*Depends on: a check marked multi-region (§4) and a running `Piro.Worker`.*

- [ ] ⚠️ Start a remote worker; it connects over SignalR and picks up multi-region checks.
- [ ] ⚠️ **Remote results carry full dimensions.** A multi-region SSL/DNS/GCP check's data points written
      from the remote worker contain the same `Dimensions` keys as the in-process path (not just Latency).
      This is the protocol change — verify a non-latency dimension (e.g. CertExpiry) survives the round-trip.
- [ ] Multi-region aggregation still produces one status per minute; alerts evaluate on it.

## 8. Incidents, escalation & status page (smoke — mostly untouched, verify no fallout)

*Depends on: an alert that fired (§6).*

- [ ] Link the fired alert to an incident; the incident appears on the admin and public status page.
- [ ] On-call escalation still delivers through connected integrations (escalation policy steps advance).
- [ ] Public status page shows the incident and the service status; uptime % recomputes.
- [ ] Resolve the incident; status page returns to operational.

## 9. Regression sweep for removed surface

- [ ] ⚠️ **YAML import is gone.** `POST /api/v1/config/import` returns 404. There is no "Import" page or
      route in the admin (navigating to `/admin/configuration/import` does not resolve). No dead nav link.
- [ ] No references to `AlertFor`, `LatencyMs`/`MetricValue` columns, or the old executors remain in any
      user-facing behavior (covered by build, but confirm no runtime 500s on the alert/check pages).

---

## Fast automated backstop (run first, catches the obvious)

```bash
dotnet test Piro.slnx                 # 159 unit + 38 integration
cd apps/admin && pnpm exec tsc -b && pnpm build
cd ../web    && pnpm exec tsc -b && pnpm build
```

If any of these fail, stop and fix before the manual pass — they cover the compile-level and
data-round-trip regressions cheaply.
