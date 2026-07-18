---
rfc: 10
title: "Script check type (sandboxed JavaScript, operator-driven HTTP)"
status: proposed
created: 2026-07-17
depends-on: ["0011"]
---

# RFC 0010 — Script check type (sandboxed JavaScript, operator-driven HTTP)

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-17

## 1. Problem

Piro can already fetch an HTTP endpoint and assert against its body, but it cannot **build a human-meaningful message out of what it read**, and it cannot express any logic more complex than a flat list of single-path assertions.

Concretely, the HTTP check's response rules (`HttpResponseRule`, `src/Piro.Application/Models/TypeData/HttpCheckData.cs:62-78`) evaluate one JSONPath/XPath/substring/regex per rule, and on failure emit a **fixed, machine-shaped message** built by string interpolation in `HttpCheckExecutor.EvaluateJsonPath` (`src/Piro.Infrastructure/Checks/HttpCheckExecutor.cs:152-155`):

```
JSONPath '$.status.indicator' = 'minor', expected 'none'.
```

Watching a status page like Stripe's Atlassian-style feed (`GET /api/v2/status.json` → `{ "status": { "indicator": "minor", "description": "Elevated Issuing API Errors" } }`) exposes both limits at once:

1. **The message is the wrong text.** The operator wants the alert to read *"Elevated Issuing API Errors"* — the value at `$.status.description`, which is sitting right there in the same body the rule already parsed — not the enum `minor` echoed back with the expected value. The rule has no way to pull one field into the message while asserting on another.
2. **The logic ceiling is too low.** Real status feeds need conditional/derived logic: *"DOWN only if `indicator` is `major` or `critical`; DEGRADED if `minor`; and if there's an active incident, put its `name` in the message."* That is three coupled decisions over two-to-three fields — impossible to express as an ordered list of independent `(path, expected)` rules, where the first failure wins and nothing composes.

A brief detour proved a plain "custom failure message with `{$.json.path}` placeholders" is a dead end: a template can substitute a field but can't branch, and the moment the operator needs "DOWN vs DEGRADED depending on the value" the template has to grow an expression language — at which point it *is* a scripting language, minus the sandbox story.

This RFC proposes a first-class **`Script` check type**: the operator writes a small JavaScript function that **makes its own HTTP request(s)** — importing an `http` module for that — runs arbitrary read-only logic over the response(s), and returns `{ status, message }`. Piro does **not** pre-fetch anything; the script is the sole driver of what to call, when, and how many times. Its output is mapped into the **exact same `CheckExecutionResult`** every other executor produces (`src/Piro.Application/Models/CheckExecutionResult.cs:6-11`), so alerting, dedup, and notifications work through the existing pipeline with **no special path** (§4.5).

The contract is:

```js
import http from 'piro:http';

export function check() {
  const r = http.get('https://www.stripestatus.com/api/v2/status.json');
  if (r.json.status.indicator === 'none') return { status: 'UP' };
  return { status: 'DOWN', message: 'Stripe: ' + r.json.status.description };
}
```

`http` arrives via `import` (not as a `check` parameter) deliberately: it makes the capability surface **extensible** — a future module (`piro:dns`, `piro:crypto`, a templating helper) is just another `import x from 'piro:x'`, with no change to `check`'s signature and no growing parameter list. The set of importable modules is a Piro-controlled allowlist (§4.2).

## 2. Non-goals

- **A general compute/automation runtime.** This is a *check* — it observes and returns a verdict. It is not a place to run cron logic, mutate Piro state, or orchestrate side effects. The only egress is read-only HTTP GET via the `piro:http` module (§4.3), and the only output is `{ status, message }`.
- **Write access to anything.** No filesystem, no database, no environment variables, no Piro entities, no CLR interop. The sandbox is deny-by-default (§4.4): the script can only `import` modules Piro has put on the allowlist (§4.2), and today that is exactly one — `piro:http`.
- **Arbitrary npm / ES modules.** `import` resolves **only** Piro-provided `piro:*` modules from an in-memory allowlist (§4.2); there is no `node_modules`, no filesystem module resolution, no network module fetch. `import x from 'node:fs'` (or any unlisted specifier) fails at load.
- **Languages other than JavaScript.** Lua/WASM/C#-scripting were weighed (§7); v1 is JavaScript via Jint. Not a plugin system for arbitrary engines.
- **`POST`/other verbs or a full HTTP client in `http`.** v1 exposes `http.get` only (§4.3). Verbs with bodies widen the abuse surface for no status-page use case on the table. A future `http.post` is an additive change to the same module, not a new contract.
- **Persisting script logs in production.** `console.log` is captured only in the on-demand debug run (§4.6); in scheduled production runs it is a no-op. Piro grows **no** log-storage column for scripts (§5).
- **Replacing the HTTP check.** The HTTP check with response rules stays exactly as-is for the common "assert status code + one path" case. Script is the escape hatch for logic that doesn't fit, not a deprecation of rules.
- **A general per-job wall-clock deadline in the dispatcher.** Today each executor owns its own timeout (`HttpCheckExecutor` via `client.Timeout`, `HttpCheckExecutor.cs:34`); this RFC follows that convention (§4.4) rather than introducing a dispatcher-level deadline for all check types.

## 3. Design principle

**The script is a self-contained `check() → {status, message}` function that pulls its capabilities via `import` and whose output is an ordinary `CheckExecutionResult`; every hard problem it raises — sandboxing, egress, timeout — is solved at the executor boundary, and nothing downstream of the executor learns that a script was involved.** Everything below traces to this: the executor is one more `ICheckExecutor` picked up by the existing dictionary dispatch (§4.1); capabilities are a Piro-controlled module allowlist so the surface grows without changing the contract (§4.2); the result flows through `CheckResultIngesterService`/`AlertEvaluationService` untouched (§4.5); the two "modes" (§4.6) differ **only** in what `console.log` does, so there is a single execution path and no "worked in test, failed in prod" gap.

## 4. Design

```
  CONFIG (Check.TypeDataJson, deserialized to ScriptCheckData)
    { script, timeoutMs, maxResponseBytes }          ← no url/method/headers; the script drives HTTP
        │
        ▼
  ScriptCheckExecutor.ExecuteAsync(check, ct)                    [Infrastructure/Checks]
        │  1. new Jint Engine { TimeoutInterval=timeoutMs, MaxStatements, LimitMemory, no CLR }
        │  2. register ESM module  'piro:http'  (default export: SSRF-guarded http.get)
        │            + console  (→ buffer in debug | no-op in production)
        │  3. import the operator's script as a module; resolve its exported check()
        │  4. sw = start;  invoke check();  LatencyMs = wall-clock of the whole script
        │  5. map return → CheckExecutionResult
        ▼
  CheckExecutionResult(Status ∈ {UP,DEGRADED,DOWN} | FAILURE, LatencyMs=script wall-clock, ErrorMessage=message)
        │                                    ▲ invalid return / throw / timeout / disallowed import ⇒ FAILURE
        │
        ├── production run ──▶ CheckResultIngesterService.IngestAsync           [unchanged]
        │                        → CheckDataPoint(ErrorMessage=message)
        │                        → AlertEvaluationService → AlertLifecycleService
        │                          (fingerprint dedup, OccurrenceCount, notifications)
        │
        └── debug run ───────▶ POST /…/checks/{slug}/test
                                 → { result: {status,message,latencyMs}, logs: [...] }   (not persisted)

  user script:   import http from 'piro:http';
                 export function check() { const r = http.get(url); return { status, message } }
```

### 4.1 `ScriptCheckExecutor` and the `Script` check type

A new `CheckType.Script` value (appended to the enum at `src/Piro.Domain/Enums/CheckType.cs:6-13`, after `GCP_CloudRunJob`) and a new `ScriptCheckExecutor : ICheckExecutor` (`src/Piro.Infrastructure/Checks/ScriptCheckExecutor.cs`, new file) whose `CheckType` property returns `CheckType.Script` (mirroring `HttpCheckExecutor.cs:21`).

This reuses the executor plumbing **entirely**. Executors are registered as plain scoped `ICheckExecutor` services (`InfrastructureServiceExtensions.cs:119-124`), and each dispatcher builds a `Dictionary<CheckType, ICheckExecutor>` at runtime via `.ToDictionary(e => e.CheckType)` (`LocalCheckJobDispatcher.cs:23-24`, `RemoteCheckJobDispatcher.cs:37`, `WorkerSignalRService.cs:125-127`). So once `ScriptCheckExecutor` is registered, dispatch picks it up by its `CheckType` with **zero dispatcher changes** — the only coupling is the property value.

It must be registered in **both** DI blocks — `AddInfrastructure` (`InfrastructureServiceExtensions.cs:119-124`, the API/in-process worker) **and** `AddWorkerInfrastructure` (`:274-278`, the standalone worker) — because both the standalone worker (`WorkerSignalRService.cs:145`) and the API-as-worker (`RemoteCheckJobDispatcher.cs:91-93`, when `PIRO_API_WORKER=true`, the single-region default) invoke `ExecuteAsync`. Since executors live in `Piro.Infrastructure`, which both API and Worker reference, the Jint dependency (§5) becomes available in both processes automatically.

Like `HttpCheckExecutor`, the executor injects `IHttpClientFactory` via primary constructor and resolves the named `"piro-http"` client (`HttpCheckExecutor.cs:17,33`) — but here that client backs the `piro:http` module's `http.get` (§4.3), the **only** outbound path (there is no separate Piro-issued primary fetch). The client carries the new SSRF `ConnectCallback` (§4.4).

**`CheckTypeExtensions.AllowedAlertFors` must gain a `Script` case.** Its `switch` has a `default` that `throw`s `NotSupportedException` (`src/Piro.Domain/Extensions/CheckTypeExtensions.cs`), so a new `CheckType` with no case is a hard runtime failure the moment anything asks for its allowed `AlertFor`s. A script returns a status verdict (UP/DEGRADED/DOWN), so its allowed set mirrors `HTTP`'s status-oriented set, not a metric one.

### 4.2 The script contract, the module system, and injected APIs

The script is an **ES module** that imports what it needs and exports a parameterless function named `check`:

```js
import http from 'piro:http';

export function check() {
  const r = http.get('https://www.stripestatus.com/api/v2/status.json');
  return { status: 'UP' | 'DEGRADED' | 'DOWN', message?: string };
}
```

`check()` takes **no parameters** — every capability arrives through `import`. There is no Piro-issued "primary response": the script decides which URL(s) to fetch and when. This is what makes the Stripe case (§1) and multi-endpoint feeds (a summary + an incidents call) both natural — the script simply calls `http.get` as many times as it needs.

**The module allowlist.** `import` resolves against a Piro-controlled, **in-memory** allowlist — not `node_modules`, not the filesystem, not the network. Each allowed module is registered on the Jint engine before the script runs; any `import` of an unregistered specifier fails at module-load and maps to `FAILURE` (§4.4). A spike confirmed both halves: `import http from 'piro:http'` resolves to a C#-backed object and its export is invocable, while `import fs from 'node:fs'` is rejected with *"Module 'node:fs' is not available."* The v1 allowlist is exactly one module:

**`piro:http`** — default export is the `http` object; its one method is `http.get` (§4.3).

Extending the surface later is purely additive: register `piro:dns`, `piro:crypto`, a `piro:template` helper, etc., on the engine and document it; existing scripts and the `check()` signature are unaffected. This is the core reason `import` was chosen over passing capabilities as `check(...)` arguments.

**`console.log`** — a global (not an import), mode-dependent (§4.6): captures to a buffer in a debug run, no-op in production. Same script, same verdict either way.

**Standard JS** available via Jint: `JSON`, `String`, `Number`, `Boolean`, `Array`, `Object`, `Math`, `RegExp`, `Date`. **Absent by construction:** `fetch`, `XMLHttpRequest`, `require`, `process`, `setTimeout`/timers, and any CLR/`System.*` type (§4.4). `import` exists but resolves *only* the allowlist.

**Editor typing (JS runtime, TS-grade ergonomics).** The runtime is plain JavaScript — Jint executes JS, not TypeScript, and no transpile step is introduced (rejected in §7). To give operators autocomplete and type-checking *as they write*, the admin editor (§4.7) loads a hand-maintained **`.d.ts`** describing the `piro:http` module, the `HttpResponse` shape, and the `check` return type. The operator gets a TypeScript-like authoring experience; what is stored and run is the JS they typed. The ESM `import … export function check()` syntax is valid in both JS and TS, so nothing about the contract changes.

**The Stripe case, fully expressed** (the motivating example from §1):

```js
import http from 'piro:http';

export function check() {
  const s = http.get('https://www.stripestatus.com/api/v2/status.json').json.status;
  if (s.indicator === 'none')  return { status: 'UP' };
  if (s.indicator === 'minor') return { status: 'DEGRADED', message: 'Stripe: ' + s.description };
  return { status: 'DOWN', message: 'Stripe: ' + s.description + ' (' + s.indicator + ')' };
}
```

### 4.3 The `piro:http` module — GET only, full-object return, opt-in per-call timeout

`http.get` is the script's only network egress. It is deliberately minimal:

```js
const r = http.get(url);                          // simplest form
const r = http.get(url, { headers: {...} });      // custom headers
const r = http.get(url, { timeoutMs: 3000 });     // opt-in per-call timeout
// r.statusCode, r.body, r.json, r.headers
```

- **GET only** (§2) — a future `http.post` is additive to this same module.
- **Returns the full object** `{ statusCode, body, json, headers }` (not just parsed JSON), so a script can branch on status code / content-type and degrade gracefully when a body isn't JSON (`r.json` is `null` then).
- **SSRF-guarded** (§4.4) on every call, including re-validation of the resolved IP (anti-rebinding).
- **`body` capped** at `maxResponseBytes` (§4.4, §5).
- **Per-call timeout is opt-in, not a config field.** There is no global `httpGetTimeoutMs`; the whole-script `timeoutMs` (§4.4) is the only budget Piro imposes. If an operator wants to bound a *specific* slow call, they pass `{ timeoutMs }` to that `http.get` — fine-grained control lives in the script, where the operator can see which call it applies to, rather than as an opaque global. An un-timed `http.get` is still bounded by the script-wide `timeoutMs` (a hung call can never exceed the total budget).

### 4.4 Sandbox and resource limits

The sandbox is **deny-by-default** — a fresh `Jint.Engine` exposes no host capabilities; the script can touch only the globals and the allowlisted modules §4.2 registers. On top of that, three limits, a constrained module loader, and one network guard:

```csharp
var engine = new Engine(o => o
    .EnableModules(new AllowlistModuleLoader())                  // in-memory; rejects any non-piro: specifier
    .TimeoutInterval(TimeSpan.FromMilliseconds(data.TimeoutMs))  // whole-script wall-clock kill
    .MaxStatements(MaxStatements)                                // CPU-spin / infinite-loop guard
    .LimitMemory(MaxMemoryBytes)                                 // allocation ceiling
    .Strict());                                                  // no sloppy-mode footguns
engine.Modules.Add("piro:http", b => b.ExportObject("default", new ScriptHttp(httpClient)));
// console bound per mode (§4.6). No engine.SetValue of any other CLR type;
// Jint CLR interop is left OFF (default) — the primary escape vector, kept shut.
```

The `AllowlistModuleLoader` is a custom `IModuleLoader` that resolves specifiers **in memory** (no filesystem base path) and refuses to load anything not pre-registered — the spike verified `node:fs` is rejected cleanly this way. `TimeoutInterval` and `MaxStatements` are complementary: a spike proved `while(true){}` is caught by `MaxStatements` almost instantly, while `TimeoutInterval` bounds a script blocked on a slow `http.get`.

**The `Engine` is ephemeral — one per `ExecuteAsync`, never shared or cached.** Jint's `Engine` is **not thread-safe**, and Piro runs checks concurrently (Quartz `MaxConcurrency = ProcessorCount * 2`, `InfrastructureServiceExtensions.cs:136`), so distinct script checks execute on parallel threads. A shared/pooled engine would corrupt state across those threads. The executor therefore constructs a fresh `Engine` inside each `ExecuteAsync` call and discards it when the method returns — the construction cost is negligible next to the network calls the script makes, and it guarantees no cross-check state leakage (a script cannot stash a value in one run and read it in another). This is a hard invariant, not an optimization choice.

**Timeout is kill-and-report against the whole script.** `timeoutMs` (default 10 000, §5) is the total budget for `check()` — all its logic plus every `http.get` it makes. If the script runs past it (e.g. a 10 s budget reached at 11 s), Jint's `TimeoutInterval` aborts it and the executor returns `FAILURE` with `"Script timed out after {timeoutMs} ms."`. There is no separate network budget; a per-call `{ timeoutMs }` on an individual `http.get` (§4.3) is an optional refinement *within* this total.

**Latency = whole-script wall-clock.** With no Piro-issued primary fetch, `CheckExecutionResult.LatencyMs` is the wall-clock time of the entire `check()` invocation (JS logic + all `http.get` calls) — measured by a stopwatch around `engine.Invoke(check)`. This is what the operator perceives as "how long the check took," and it feeds the same latency datapoint every other check type reports.

**SSRF guard — new, because none exists to reuse.** There is today **no** private-IP / metadata-endpoint protection anywhere: `piro-http`/`piro-http-noredirect` have no `ConnectCallback` at all (`InfrastructureServiceExtensions.cs:94-96`), and the two handlers that *do* have one (`oidc-http` `:76-82`, `piro-webhook` `:110-116`) resolve DNS only to force IPv4, with an explicit comment to that effect (`:109`) and no address validation. So this RFC **introduces** a guard: a shared `SocketsHttpHandler.ConnectCallback` that resolves the host and **rejects** loopback (`127/8`, `::1`), link-local / cloud metadata (`169.254/16`, notably `169.254.169.254`), and RFC-1918 private ranges (`10/8`, `172.16/12`, `192.168/16`), plus `localhost`/`metadata.google.internal` by name. Because the guard validates the **resolved IP** (not the hostname), it also defeats DNS-rebinding — a host that resolves to a public IP at check-authoring time but a private one at run time is caught at connect. The guard is applied to the `"piro-http"` client that backs `piro:http` — and since *all* of a script's network egress goes through that one module, there is a single guarded choke point with no unguarded path around it. **It is retrofittable to `piro-webhook` and the HTTP check's own clients** (which are equally unguarded today), so this RFC's guard doubles as the fix for a pre-existing exposure — but hardening those other paths is called out as follow-up, not folded into this RFC's blast radius.

**Threat model.** The script author is the Piro **operator**, not an anonymous end user — someone who already has server access. The guard therefore targets *accidental* SSRF (a copy-pasted script pointed at an internal URL) and defense-in-depth against a compromised-config scenario, not a fully hostile tenant. This is why deny-by-default + resource limits + IP guard is proportionate, and a full VM/WASM jail (§7) is not.

**Failure mapping.** A script that throws, times out, exceeds a limit, returns a non-object, or returns a `status` outside `{UP, DEGRADED, DOWN}` produces `CheckExecutionResult(ServiceStatus.FAILURE, …)` with a diagnostic `ErrorMessage`. This is deliberate: `FAILURE` is the "executor itself couldn't produce a verdict" state (`ServiceStatus` doc-comment, `src/Piro.Domain/Enums/ServiceStatus.cs`), and — crucially — `CheckResultIngesterService` **skips alert evaluation when status is `FAILURE`** (`src/Piro.Application/Services/CheckResultIngesterService.cs:65`). So a broken script does **not** spam alerts; it records a `FAILURE` datapoint the operator sees on the check, exactly as an unregistered check type or a crashed executor does today.

The script may return only `UP`/`DEGRADED`/`DOWN`. `MAINTENANCE` is Piro-owned (maintenance windows) and `FAILURE`/`NO_DATA` are executor-internal — none are script-selectable.

### 4.5 What flows downstream — reuse, no parallel path

The executor returns a `CheckExecutionResult` **identical in shape** to every other executor's, so the entire post-execution pipeline is reused verbatim:

- `LocalCheckJobDispatcher` (or the worker) calls `CheckResultIngesterService.IngestAsync` (`LocalCheckJobDispatcher.cs:51-52`).
- The `message` lands in `CheckDataPoint.ErrorMessage` (`CheckResultIngesterService.cs:43`; field at `src/Piro.Domain/Entities/CheckDataPoint.cs:31`).
- `AlertEvaluationService.EvaluateAsync` runs (`CheckResultIngesterService.cs:67`), and `BuildMessage` uses `latest.ErrorMessage` as the alert's frozen text (`AlertEvaluationService.cs:164-168`) — i.e. **the script's `message` becomes the alert message with no new code**.
- `AlertLifecycleService.RecordOccurrenceAsync` fingerprints that message and folds repeats into `OccurrenceCount` (`AlertLifecycleService.cs:35-43`).

The check is configured with an ordinary `AlertConfig` (`src/Piro.Domain/Entities/AlertConfig.cs`) whose severity/thresholds/escalation behave the same as for an HTTP check. **No `AlertConfig`, `AlertLifecycleService`, `AlertEvaluationService`, notification-dispatcher, or escalation change is required** — this is the point of mapping the script to `CheckExecutionResult` rather than inventing a script-specific alert path.

**The one downstream subtlety — dedup (documented, not code-changed).** Fingerprinting is exact-match over the normalized message (`AlertLifecycleService.Fingerprint`, `:143-148`: trim + lowercase + collapse whitespace). If a script embeds a **volatile** value in `message` (a timestamp, a rotating incident id, a counter), every run yields a different fingerprint, so each occurrence opens a *new* alert instead of incrementing `OccurrenceCount` on the existing one — the "Occurrence: 13" folding breaks. This is not a Script-specific bug (any check whose message varies per run hits it), but Script makes it easy to trigger — most directly via `Date` (available in the JS environment, §4.2): a `message` that includes `new Date().toISOString()` or `Date.now()` changes every run. The mitigation is **documentation + UI guidance** (§4.7), not a mechanism change: advise keeping `message` stable for a given failure condition and putting volatile detail (timestamps, ids) in `console.log` (debug-only) rather than the returned message. Auto-normalizing volatile substrings out of the fingerprint is explicitly rejected (§7) — it guesses at intent and would mask genuinely distinct failures.

### 4.6 Two run modes, one execution path

There are two ways a script runs, differing in **exactly one thing** — what `console.log` does:

| | Production (scheduled) | Debug (`POST …/test`) |
|---|---|---|
| Trigger | Quartz `CheckExecutionJob` → dispatcher | Operator clicks "Test" in the admin |
| `console.log` | **no-op** (executes without error, captures nothing) | captured to an in-memory buffer, returned to the caller |
| `http.get` | real, SSRF-guarded | **real, SSRF-guarded** (identical — not mocked) |
| Result persisted? | yes (`CheckDataPoint`, alert eval) | **no** (returned in the HTTP response only) |
| Output | `CheckExecutionResult` | `{ result: {status, message, latencyMs}, logs: [...] }` |

Keeping `http.get` **real in debug** (a design decision) means the operator tests the *exact* code path production runs — eliminating the classic "passed in test, failed live" gap that a mocked egress would create. The only asymmetry is log capture, which cannot change the verdict.

Production is a no-op-`console` rather than "persist logs only on failure" (an earlier idea) because that heuristic is muddy — a permanently-failing check would spam log rows — and Piro has **no** column to store them anyway (§5). Debug logs live only for the duration of the HTTP response.

Mechanically, the mode is a parameter to a shared internal run method; both call sites build the same engine and register the same `piro:http` module, differing only in the `console` object bound (`{ log: buffer.Add }` vs `{ log: _ => {} }`).

### 4.7 UI — Script config editor and the Test panel (`apps/admin`)

The admin panel is the Vite SPA (`apps/admin`); its API types are generated from the backend OpenAPI spec, so every shape below derives from the `ScriptCheckData` DTO (§5) and the test endpoint (§4.8). Two surfaces:

**(a) `ScriptConfig.tsx` — the per-type config editor.** The check form (`apps/admin/src/features/checks/pages/CheckFormPage.tsx`) renders per-type fields through a renderer registry, `CHECK_TYPE_CONFIG_RENDERERS` (`apps/admin/src/features/checks/components/CheckTypeConfigFields.tsx:10-19`), with each type's component under `.../components/check-types/` (`HttpConfig.tsx`, `SslConfig.tsx`, …). This RFC adds `check-types/ScriptConfig.tsx` and registers it in that map. Modeled on `HttpConfig.tsx` (which uses `useFormContext`), it renders:

Because the script drives its own HTTP (§4.2), there is **no** URL/method/headers form — those live inside the script. The editor renders:

- **Script** — a real **code editor** (not a plain textarea) bound to `script`, required. This is a new shared `CodeEditor.tsx` component wrapping **CodeMirror 6** (`@uiw/react-codemirror` + `@codemirror/lang-javascript`), giving JavaScript syntax highlighting, line numbers, bracket matching, and auto-indent. It is theme-aware (light/dark, matching the admin's existing theme) and reused anywhere the admin later needs code entry. A **`.d.ts`** describing `piro:http`, the `HttpResponse` shape, and the `check` return type is loaded into the editor's language support so the operator gets autocomplete and type-checking as they write (JS runtime, TS-grade ergonomics — §4.2). The editor seeds a template (`import http from 'piro:http'; export function check() { … }`) and shows an inline warning: *"Keep `message` stable for a given failure — volatile values (timestamps, ids) split one alert into many"* (the §4.5 dedup guidance, surfaced where it's authored). CodeMirror's `@codemirror/lint` gutter is wired so that a FAILURE from the Test panel (§4.7b) — a Jint parse/runtime error carrying a line — is rendered as an inline diagnostic on the offending line, not just as text below.

  CodeMirror 6 (not Monaco) is chosen because it is modular and lightweight, bundles cleanly under Vite without the web-worker configuration Monaco's editor requires, and is proportionate to editing a ~20-line function. The admin already ships `highlight.js` (`apps/admin/src/lib/highlight.ts`) but only for **read-only** rendering (`PayloadDialog.tsx:36`, JSON only) — it is not an editable editor and only registers the `json` language, so it does not serve this need.
- **Limits** (numbers with defaults, §5): `timeoutMs` (whole-script wall-clock) and `maxResponseBytes`. There is no `httpGetTimeoutMs` field — a per-call timeout is opt-in inside the script (`http.get(url, { timeoutMs })`, §4.3).

Validation joins the existing flat Zod schema: fields added to `baseCheckSchema`, a `case 'Script'` in the per-type `superRefine`, and defaults in `CHECK_CONFIG_DEFAULTS` (`apps/admin/src/features/checks/validations.ts`). The `typeDataJson` serializer gains a `Script` branch (`apps/admin/src/features/checks/utils/typeDataJson.ts:5-41`).

**(b) The Test panel.** A **"Test"** button in `ScriptConfig.tsx` (distinct from the existing "Run now" on `CheckDetailPage.tsx:293-300`, which triggers the *real, persisted* scheduled run) posts the current form's config to the debug endpoint (§4.8) and renders the response inline: the returned **status** (colored badge — green UP / amber DEGRADED / red DOWN, or a distinct FAILURE state), the **message**, **latencyMs**, and a scrollable **logs** list (the captured `console.log` lines). This is the payoff of debug mode — the operator iterates the script against the live endpoint and sees both the verdict and their log output without saving or firing an alert. A new action module method (`checksApi.test`, mirroring the existing `run`/`logs` in `apps/admin/src/lib/actions/checks/index.ts`) calls it.

The FAILURE state (invalid return, timeout, thrown error, blocked SSRF) renders its diagnostic `ErrorMessage` in the panel so the operator can fix the script — this is where blocked-host and timeout messages surface.

### 4.8 API — the debug endpoint

One new endpoint on `ChecksController` (`src/Piro.Api/Controllers/ChecksController.cs`, route `api/v1/services/{serviceSlug}/checks`), alongside the existing `POST /{checkSlug}/run` (`:74-83`):

```
POST /api/v1/services/{serviceSlug}/checks/{checkSlug}/test   -> { result, logs }
```

Unlike `run` — which calls `checkRunner.RunAsync` and goes through the full persist-and-alert path — `test` invokes the executor **in debug mode** and returns `{ result: { status, message, latencyMs }, logs: string[] }` **without** writing a `CheckDataPoint` or touching alert evaluation. To support editing-before-saving, it accepts the candidate `ScriptCheckData` in the request body (so the operator tests unsaved edits), falling back to the persisted config when the body is empty. `http.get` runs for real under the same SSRF guard.

### 4.9 What does NOT change

- **The dispatch mechanism.** `RoutingCheckJobDispatcher`/`LocalCheckJobDispatcher`/`RemoteCheckJobDispatcher` and the SignalR `WorkerExecuteMessage` contract are untouched — a `Script` check is dispatched by the same `Dictionary<CheckType, ICheckExecutor>` lookup as every other type (§4.1), and the script rides the existing `TypeDataJson` field of `WorkerExecuteMessage` with no new wire field (subject to the size ceiling noted in §8). The worker binary needs no code change; it gains Jint transitively via `Piro.Infrastructure` (§5).
- **The alert pipeline.** `AlertConfig`, `AlertEvaluationService`, `AlertLifecycleService`, `CheckResultIngesterService`, `INotificationDispatcher` and every dispatcher under `src/Piro.Infrastructure/Alerts/`, and `EscalationCheckerService` are reused as-is (§4.5). Script is a new *producer* of an ordinary `CheckExecutionResult`, not a parallel evaluator.
- **`CheckExecutionResult` and `CheckDataPoint` schema.** No new fields (§5). The script's `message` reuses `ErrorMessage`; debug logs are never persisted, so they need no column.
- **The HTTP check.** `HttpCheckExecutor` and `HttpResponseRule` are unchanged; Script is additive, not a replacement (§2).
- **The YAML importer.** `YamlImportService` parses `type:` via `Enum.TryParse<CheckType>(…, ignoreCase: true)` (`YamlImportService.cs:101`) and stores the free-form `type_data:` map verbatim into `TypeDataJson` (`:104,153`), so a `type: script` with a `type_data:` carrying `script`/`timeout`/`maxResponseBytes` works with **no importer change** (§5).
- **`ServiceStatus` semantics and the public status page.** No new status value; a script returns existing UP/DEGRADED/DOWN, and a broken script is `FAILURE` (non-alerting), exactly as today.

## 5. Data / schema scope

- **New enum value:** `CheckType.Script`, appended after `GCP_CloudRunJob` (`src/Piro.Domain/Enums/CheckType.cs:13`). `CheckType` is persisted as a string, so ordinal position is irrelevant; append for tidiness.
- **New config record:** `ScriptCheckConfig` in `src/Piro.Domain/Checks/Config/` (alongside the other `*CheckConfig` records — RFC 0011 moved them there and out of `Application/Models/TypeData/`), round-tripped through the existing `Check.TypeDataJson` string column (`src/Piro.Domain/Entities/Check.cs:26`). No `url`/`method`/`headers` — the script issues its own requests via `piro:http` (§4.2). Fields:

  ```csharp
  public record ScriptCheckConfig
  {
      [CodeField]  // renders in the code editor (RFC 0011's ConfigFieldType.Code)
      public string Script { get; init; } = string.Empty;   // the ESM: import + export function check()
      [JsonPropertyName("timeout")]
      public int TimeoutMs { get; init; } = 10_000;          // whole-script wall-clock; kill-and-report on overrun
      public int MaxResponseBytes { get; init; } = 1_048_576;// cap on any http.get body (1 MiB)
  }
  ```

  A per-`http.get` timeout is **not** a field here — it is passed opt-in inside the script (`http.get(url, { timeoutMs })`, §4.3), always within the whole-script `TimeoutMs` budget.

- **Script size limit — 4 KB (v1).** The `Script` string is capped at **4096 bytes (UTF-8)**, validated on write in `CheckAppService` (the same guard site as `EnsureScheduleWithinBounds`, RFC 0011) and mirrored in the editor for immediate feedback; the backend is authoritative. 4 KB is a conservative v1 ceiling — comfortably under the SignalR frame (§8), and generous for a status-page check (the Stripe example is ~200 bytes) while foreclosing pathological payloads. It is deliberately raisable later (a named constant, not a magic number), hence "v1".

- **Minimum interval: 5 minutes, declared in the check manifest (RFC 0011), not hard-coded here.** Because a script runs arbitrary code, its schedule floor is more conservative than other types' — 5 minutes vs the 1-minute global floor. This RFC does **not** invent the interval-validation mechanism: it declares `Script`'s `minCron = 5m` as an entry in the per-`CheckType` manifest that RFC 0011 introduces, and RFC 0011's validation (`timeoutMs < interval`, `interval ≥ type.minCron`) enforces it uniformly. Until RFC 0011 lands, `Script` ships with a local guard of the same rule so it is never unbounded. `TimeoutMs`'s effective ceiling is therefore the check's own interval (a 5-minute script can have at most a ~5-minute timeout), which combined with `[DisallowConcurrentExecution]` (`CheckExecutionJob.cs:10`) means a script run always finishes before its next scheduled fire — no overlap accumulation.

- **No new DB migration.** `Script` config lives in the existing `TypeDataJson` column; no new entity, table, or column. `CheckDataPoint`/`Alert`/`AlertConfig`/`CheckExecutionResult` are unchanged (§4.9).
- **New NuGet dependency:** `Jint` added to `src/Piro.Infrastructure/Piro.Infrastructure.csproj` (the repo uses **decentralized** package management — inline `Version` per `.csproj`, no `Directory.Packages.props`). Because both `Piro.Api` and `Piro.Worker` reference `Piro.Infrastructure`, Jint reaches both processes with no additional `.csproj` edits.
- **New frontend dependencies (`apps/admin`):** `@uiw/react-codemirror`, `@codemirror/lang-javascript`, and (for inline lint) `@codemirror/lint`, added via `pnpm`. These back the `CodeEditor.tsx` component (§4.7). No backend or `apps/web` impact.
- **No changes to:** `CheckExecutionResult`, `CheckDataPoint`, `Alert`, `AlertConfig`, `ServiceStatus`, `AlertSource`, `AlertFor`, `AlertSeverity`; the SignalR `WorkerExecuteMessage`/`WorkerResultMessage`; the YAML models (the loose `type_data:` map already accommodates it).

## 6. Phased plan

Each phase is independently shippable.

1. **Executor + sandbox core (backend).** `CheckType.Script`, `ScriptCheckData`, `ScriptCheckExecutor` with the Jint engine + limits (`EnableModules` with the allowlist loader, `TimeoutInterval`=`timeoutMs`/`MaxStatements`/`LimitMemory`/no-CLR), the ESM import + `export function check()` invocation, whole-script wall-clock latency, the return→`CheckExecutionResult` mapping (incl. FAILURE cases: throw, timeout, disallowed import, bad return), the `CheckTypeExtensions.AllowedAlertFors` case, and DI registration in both blocks. `console.log` is a no-op (production semantics). Ships with the `piro:http` module and its guard (Phase 2) — because the script *cannot do anything* without egress, this phase and Phase 2 are effectively co-dependent and may ship together; they are split only to let the security surface be reviewed on its own.
2. **SSRF guard + `piro:http` module.** The shared `ConnectCallback` IP guard on `"piro-http"`, and the `piro:http` module exposing `http.get` (GET only, opt-in per-call `timeoutMs`, `maxResponseBytes` cap, full-object return). This is the security-sensitive surface, reviewed on its own.
3. **Debug mode + test endpoint (backend).** The shared run method parameterized by mode, `console.log` buffer capture in debug, and `POST …/checks/{slug}/test` returning `{result, logs}`.
4. **Admin UI.** The shared `CodeEditor.tsx` (CodeMirror 6) component, `ScriptConfig.tsx` + renderer-registry entry, Zod/serializer wiring, and the Test panel calling Phase 3 (with FAILURE diagnostics fed back into the editor's lint gutter). Depends on 3.
5. **Retrofit the SSRF guard to `piro-webhook` and the HTTP check clients (optional, follow-up).** Extend Phase 2's guard to the other currently-unguarded outbound paths (`InfrastructureServiceExtensions.cs:94-96,110-116`). Outside this feature's core but closes a pre-existing exposure the guard makes trivial to fix.

## 7. Alternatives considered

- **Custom failure message with `{$.json.path}` placeholders on `HttpResponseRule`.** Rejected — a template can substitute a field into the message but cannot branch (DOWN vs DEGRADED by value) or combine fields conditionally (§1 #2). The moment it needs conditionals it becomes a scripting language without the sandbox story. Script subsumes it.
- **Extend `HttpResponseRule` with an expression language (JSONata/CEL/JMESPath).** Rejected — a second, weaker DSL to learn and sandbox, still short of "compose a message from multiple fields with branching." If we're taking on an evaluator, a real (sandboxed) scripting language is more capable for the same integration cost.
- **ClearScript + V8 instead of Jint.** Rejected for v1 — V8 is faster and more JS-complete, but ships a **native binary** (complicating the multi-arch Docker builds in `release.yml`) and is a full engine whose sandboxing needs more work to lock down. A status-page check evaluates a tiny body; Jint's pure-C# interpreter is fast enough and sandbox-friendly by default (deny-by-default, `MaxStatements`/`LimitMemory`/`TimeoutInterval` built in). Revisit only if scripts grow CPU-heavy.
- **Lua (MoonSharp) instead of JavaScript.** Rejected — MoonSharp is also pure-C# and sandbox-friendly, but JavaScript is far more familiar to the operators who administer status pages, and the response bodies are JSON (native to JS). Lua buys nothing here.
- **C# via Roslyn scripting.** Rejected outright — full access to the .NET runtime, no practical sandbox. It is the opposite of deny-by-default.
- **WASM (Wasmtime/Extism).** Rejected as massively over-scoped — strongest isolation, but a huge dependency and authoring-toolchain burden for evaluating a JSON body, against a threat model (trusted operator, §4.4) that doesn't demand VM-grade isolation.
- **`POST`/full HTTP client in `http.get` for v1.** Rejected — no status-page case on the table needs a request body, and verbs-with-bodies widen the SSRF/abuse surface. GET-only now; revisit as an additive `http.post` on the same module (§2, §4.3).
- **Passing capabilities as `check(...)` parameters instead of `import`.** Rejected — parameters don't scale: every new capability (`http`, then `dns`, `crypto`, templating) grows the signature and every existing script must be aware of positions. `import x from 'piro:x'` lets a script pull *only* what it uses, keeps `check()` parameterless and stable, and makes the capability set a documented allowlist rather than an ever-widening argument list (§4.2). The spike confirmed Jint's ESM support makes this clean.
- **Piro pre-fetching a "primary response" and injecting it as `res`.** Rejected (this was an earlier draft of this very RFC) — it forced a `url`/`method`/`headers` config *and* let the script fetch more via `http.get`, so there were two egress paths (one guarded implicitly, one explicitly) and an awkward split between "the response Piro got for you" and "responses you got yourself." Letting the script make *all* calls through the single `piro:http` module is simpler, gives one guarded choke point (§4.4), and makes multi-endpoint feeds first-class instead of a bolt-on.
- **TypeScript as the script language (transpiled at runtime).** Rejected — Jint executes JavaScript, not TypeScript, so this would require a TS→JS transpiler running inside the .NET backend (the TS compiler is itself JS/Node, absent here) — a heavy, fragile dependency for the benefit. Instead the runtime is plain JS and the *editor* loads a `.d.ts` (§4.2, §4.7) to give TypeScript-grade autocomplete and type-checking while authoring; the ESM `import`/`export` syntax is identical in both, so the operator writes what reads like typed code and Jint runs the JS unchanged.
- **A script-specific alert path / new `AlertSource`.** Rejected — the whole design principle (§3) is that a script produces an ordinary `CheckExecutionResult`, so it flows through `AlertEvaluationService`/`AlertLifecycleService` unchanged (§4.5). A parallel path would duplicate the pipeline for no gain.
- **Auto-normalizing volatile substrings out of the fingerprint** (to "fix" the §4.5 dedup foot-gun automatically). Rejected — stripping timestamps/ids by heuristic guesses at intent and would silently merge genuinely distinct failures. Guidance + a stable-message convention (§4.5, §4.7) is correct; the fingerprint stays exact-match.
- **A plain monospace `<textarea>` (or the `highlight.js`-overlay hack) instead of CodeMirror for the script editor.** Rejected — a textarea gives no line numbers, no highlighting, and nowhere to render Jint's line-anchored errors; the "transparent textarea over a highlighted `<pre>`" trick reuses the already-installed `highlight.js` but is fragile (scroll/wrap desync, no reliable gutter, no lint). For a field where operators author real logic and need error feedback, a proper editor (CodeMirror 6) is worth one modular dependency. **Monaco** was rejected in turn: it is VS Code's editor, heavy, and needs web-worker configuration under Vite — over-scoped for a ~20-line function.
- **Persisting `console.log` output in production** (e.g. a new `CheckDataPoint.Logs` column). Rejected — no clear retention story (a permanently-failing check spams rows), it adds a schema column for a debug-only concern, and the debug endpoint (§4.8) already gives operators logs when they need them. Production `console.log` is a no-op (§4.6).

## 8. Risks

- **DNS-rebinding against `http.get`.** A host that resolves to a public IP when the operator authors/tests the script but to `169.254.169.254` (or an internal address) at scheduled run time would bypass a hostname-based allowlist. Mitigation: the guard validates the **resolved IP at connect time** on every call (§4.4), not the hostname, so rebinding is caught on the connection that actually matters — including inside `http.get`, which re-runs the guarded `ConnectCallback` per request.
- **Jint performance / a pathological script starving the worker.** A script with heavy allocation or deep recursion could consume CPU/memory on the worker (which is the same process running other regions' checks). Mitigation: `MaxStatements` + `LimitMemory` + `TimeoutInterval` bound each run (§4.4); a runaway script is killed and mapped to `FAILURE`. Residual risk: many scripts scheduled densely could still add aggregate load — acceptable for a trusted-operator feature, and observable via the `FAILURE` datapoints and latency.
- **Volatile-message dedup breakage (§4.5).** The sharpest *behavioral* foot-gun: a script that interpolates a changing value into `message` turns one alert into a stream of new alerts, defeating `OccurrenceCount` and potentially the notification throttling that rides on a stable active alert. Mitigation is guidance-only by design (§4.5, §4.7) — surfaced in the editor help text — because the alternative (fuzzy fingerprinting) is worse (§7). Worth watching whether operators trip on it enough to justify a lint/warning in the Test panel later.
- **`http.get` memory under a huge response.** A script that fetches a multi-hundred-MB endpoint would blow memory before any logic runs. Mitigation: `maxResponseBytes` (default 1 MiB, §5) caps every `http.get` body; the read is bounded, not "download then check."
- **A script that never calls `http.get` (all-local logic) or loops on many calls.** Since the script drives egress, a buggy script could make zero requests (always returns a static verdict) or fire a burst of `http.get`s in a loop. The former is harmless (just a useless check the operator sees in Test); the latter is bounded by the whole-script `timeoutMs` and `MaxStatements` (§4.4), and every call is SSRF-guarded and size-capped — so a loop can slow the one check but cannot exhaust the worker or reach internal hosts.
- **The SSRF guard is genuinely new code on a security-sensitive path.** Getting the blocked-range list wrong (missing IPv6 private ranges like `fc00::/7`, or `0.0.0.0`, or IPv4-mapped IPv6) would leave a hole. Mitigation: the guard is its own phase (§6 Phase 2) with focused review and tests per range, and it is written once and shared — the same code that (Phase 5) hardens `piro-webhook`, so it gets scrutiny beyond just the script feature.
- **CodeMirror bundle weight / Vite build.** Adding a code editor to `apps/admin` grows the bundle and is a new build-time dependency. Mitigation: CodeMirror 6 is tree-shakeable and modular (only `lang-javascript` + `lint` are pulled in), imports cleanly under Vite without Monaco's web-worker setup, and is loadable lazily (the editor only mounts on the Script check form). If bundle size regresses meaningfully, the `CodeEditor.tsx` import can be `React.lazy`-split so non-Script pages don't pay for it. Verified prerequisite: `apps/admin` is React 19 + Vite 8 (`apps/admin/package.json`), both supported by current `@uiw/react-codemirror`.
- **Script size vs SignalR message limit (remote workers).** A `Script` check reaches a remote worker inside `WorkerExecuteMessage.TypeDataJson` (`src/Piro.Application/Models/Worker/WorkerMessages.cs:20`), sent over SignalR on every execution. No `MaximumReceiveMessageSize` is configured anywhere (grep is empty), so SignalR's **default 32 KB** cap applies to the whole message — a very large script (plus the rest of the check payload) could exceed it and fail the send to the worker, silently in a multi-region setup. A normal script is a few hundred bytes, so this is a ceiling, not a routine concern. Mitigation: the **4 KB script cap (§5)**, validated on write in `CheckAppService` and mirrored in the editor, sits an order of magnitude under the SignalR frame — the whole `WorkerExecuteMessage` stays well within 32 KB even with the rest of the check payload. In-process (`PIRO_API_WORKER=true`, the single-region default) is unaffected — nothing crosses SignalR.
- **A broken script is invisible without looking at the check.** Because a script error maps to `FAILURE` and `FAILURE` skips alert evaluation (§4.4), a script that has been failing for days produces `FAILURE` datapoints but **no alert** — the operator only sees it by opening the check. This is deliberate (a broken script must not page as if the monitored service were down), but it is a real observability gap: there is no "your script check itself is broken" signal. Accepted for v1; a possible follow-up is a distinct low-severity notification for a check stuck in `FAILURE`, which would apply to every check type (an unregistered type, a crashing executor), not just Script — so it belongs to a general check-health effort, not this RFC.
- **Who may author a script check (authorization).** A script is code executing on Piro's server, so the permission to create/edit a `Script` check is more sensitive than for an HTTP check. This RFC assumes the existing "edit check" permission gates it (the same guard `ChecksController` already applies) and does **not** add a separate, elevated permission. If a deployment has roles that may edit checks but should not run arbitrary code, that is an authorization gap to close separately — called out here so it is a conscious acceptance, not an oversight. The trusted-operator threat model (§4.4) rests on this assumption.
- **Operator confusion between "Run now" and "Test".** Two buttons that both "execute the check" — one persists and can fire alerts (`CheckDetailPage` "Run now"), one doesn't ("Test" in the config editor). Mitigation: label and place them distinctly (§4.7) — "Test" lives in the script editor as a dry-run with logs; "Run now" stays on the detail page as the real trigger — and the Test panel's non-persisted, log-carrying output makes the difference visually obvious.
```
