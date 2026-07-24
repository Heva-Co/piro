---
rfc: 16
title: "Integration SDK: self-describing integrations with an open discriminator"
status: implemented
created: 2026-07-22
depends-on: ["0003", "0009", "0011", "0012", "0015"]
tracking-issue: 216
proposal-pr: 215
implementation-pr: null
superseded-by: null
---

# RFC 0016 — Integration SDK: self-describing integrations with an open discriminator

Status: implemented
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-22

> **Implementation note (2026-07-24).** This RFC is implemented. The design below is
> preserved as proposed; where the shipped implementation refined a decision, an
> inline "As shipped" note records what actually landed, and §10 summarizes the whole
> delta in one place. Two shifts are worth calling out up front, because they touch
> the core contract: (a) the manifest's `SupportedEvents` shipped as `string[]` of
> stable wire names with wildcard support, not `NotificationEventType[]`, keeping the
> "integrations know nothing" boundary tighter; and (b) the three send/post/trigger
> dispatcher interfaces collapsed into a single `IIntegrationEventHandler`. The
> **PagerDuty** integration (RFC 0004) was also **withdrawn** in this work — Piro now
> owns on-call via escalation policies (RFC 0015), so an external paging provider is
> redundant; see RFC 0004, now marked `withdrawn`.

## 1. Problem

Adding an integration to Piro today means editing it in **five places that don't
know about each other**, all gated by a single closed enum. "What an integration
is" is scattered across three assemblies plus a hand-maintained DI list:

| Fact about an integration | Where it lives today |
|---|---|
| Identity + capabilities + config type + label + icon | a `[IntegrationManifest]` attribute on a **field of the `IntegrationType` enum** (`src/Piro.Domain/Enums/IntegrationType.cs`) |
| Config shape + secrets | a `*Config` class in `src/Piro.Domain/Integrations/Config/` |
| How it sends / posts / triggers | an `I*Dispatcher` in `src/Piro.Infrastructure/Alerts/` |
| What actions it offers | a second attribute (`[IntegrationAction]`) on the same enum field + an `IIntegrationAction` in Infrastructure |
| Which catalog events it handles | an `INotificationSubscriber` set — **registered but never read** (issue #212) |
| Registration | ~22 hand-written lines in `InfrastructureServiceExtensions.cs` (`:193-309`) |

The `IntegrationType` enum sits at the center: every dispatcher keys on it, every
manifest hangs off one of its fields, and adding a provider means editing that
enum, adding a config class in `Piro.Domain`, adding a dispatcher in
`Piro.Infrastructure`, and adding registration lines. The provider is not a
*thing*; it is a value smeared across the codebase.

This RFC makes each integration a **self-describing unit** — one place that owns
its manifest, config, dispatchers, actions, and its own identity — **discovered by
an explicit compile-time registry** rather than enumerated in a central enum. The
closed `IntegrationType` enum is replaced by an open `string IntegrationId` each
integration declares about itself. It also fixes issue #212 and the
capabilities-drift risk (RFC 0003 §8), which both dissolve once an integration
owns its whole contract in one place.

Two verified facts make the discriminator change cheap, and shape the design:

- **The discriminator is already a string on disk and on the wire.**
  `Integration.Type`, `NotificationDeliveryLog.IntegrationType`, and
  `EscalationDeliveryLog.ChannelType` are persisted via `HasConversion<string>()` —
  the stored value is the enum *name* (`"Jira"`), never the ordinal
  (`IntegrationConfiguration.cs:16`, `NotificationDeliveryLogConfiguration.cs:17`,
  `EscalationDeliveryLogConfiguration.cs:16`; snapshot confirms `Property<string>`).
  `api-types.ts`'s `IntegrationType` is already a string union (`:8820`), and
  `IntegrationTypeMetaDto.Type` is *already* `string` (`IntegrationTypeMetaDto.cs:9`,
  via `type.ToString()`). **Killing the enum needs no database migration** — but a
  `dotnet ef migrations add` no-op check must *prove* an empty snapshot diff, not
  assume it. (The `[Obsolete]` values' "keep the ordinal or corrupt DB rows"
  comments are false given `HasConversion<string>()`; this RFC corrects them.)
- **Nothing dispatches via `switch`.** Zero `switch (integrationType)` in the
  dispatch path; every resolution is `ToDictionary(d => d.Type)`
  (`SubscriptionMatchingProcessor.cs:35-44`, `EscalationCheckerService.cs:26`,
  `UserManagementService.cs:27`, `ActionRegistry.cs:26`). A `string`-keyed
  dictionary works identically. (The `case IntegrationType.X` hits are enum-literal
  *declarations*, not dispatch switches; three residual `switch`es map a type to an
  OAuth provider-id *string* — `IntegrationOAuthController.cs:140`,
  `OAuthTokenProvider.cs:98`, `AlertSourceExtensions.cs:12` — code that already
  wants the string this RFC makes canonical.)

`IntegrationType` appears in **~64 files / ~149 references** (measured). The swap is
mechanical (no switches, no migration), but it is a large diff — §7 sequences it so
each commit stays green.

## 2. Non-goals

- **Runtime hot-loading of third-party plugins.** No external-DLL loading, no
  sandbox, no versioned plugin API. The set of integrations is **closed at build
  time** — it is exactly the entries in the compile-time registry (§4.3). Hot-load,
  sandboxing, and API versioning are a later RFC if a real third-party packaging
  need appears.
- **A shared integrations "pile."** This RFC does **not** keep integrations mixed
  together in `Piro.Infrastructure`. Each gets its own assembly (§4.1) precisely so
  the boundary "an integration knows nothing about Piro" (§4.2b) is enforced by the
  reference graph, not by convention. The one exception is Email, whose transport is
  core infrastructure (§4.1).
- **The `OnStart(host)` / UI-extension seam.** A prior draft added an integration
  lifecycle hook for future UI contribution. It has no consumer today; it belongs to
  the RFC that actually *builds* the UI-extension surface (the `ExtendsUserInterface`
  work, §4.6), not this one. This RFC keeps the capability *flag* rename
  (`ProvidesActions` → `ExtendsUserInterface`) but does **not** introduce the host
  hook. YAGNI.
- **Changing any integration's runtime behavior.** Every dispatcher's send logic,
  action execution, and OAuth flow behaves identically after. This RFC relocates
  *where* an integration is defined and *how* it's discovered, and adds the two #212
  guards — nothing else.
- **A `NotificationEventType` redesign.** That enum stays in `Piro.Domain` (verified:
  imports only `Piro.Domain.Attributes`, couples to no entity). It discriminates
  *events*, not *providers*; out of scope. The integration contract sits *above*
  Domain (§4.5) so it references `NotificationEventType` freely — no move.
- **A frontend rewrite.** Per-type `*Config.tsx` files are already gone
  (schema-driven since `beb41db`). The frontend work is the narrow gap in §6.

## 3. Design principle

**An integration declares its own identity and is discovered from an explicit
registry, not enumerated in a central enum.** Today a provider is a value in a
closed list every subsystem must be edited to know about. After this RFC, a provider
is a self-describing class that carries its manifest + config + dispatchers, added to
one explicit registry — open (a `string` id, not an enum value) yet **statically
total** (the registry is compile-time code, greppable, exhaustive).

Three constraints follow:

- **The `IntegrationId` is a permanent contract.** It is persisted in every
  `Integration` row, and it **must equal the current enum member name verbatim**
  (`"Jira"`, `"Twilio"`, `"GcpCloudMonitoringWebhook"`) — that is exactly the string
  already in the database, which is what keeps "no migration" true. Re-casing it
  (kebab, lower) would orphan every stored row. Declared once, immutable, like RFC
  0009's "wire names are permanent once shipped."
- **Discovery is explicit and compile-time-total.** The registry is code that names
  every `IIntegration` type (§4.3). This is *not* `AppDomain.GetAssemblies()`
  reflection — that returns *loaded* assemblies, not *referenced* ones (.NET loads
  lazily), which would silently drop an integration that hadn't been touched yet.
  "Open discriminator" means the *type* isn't a closed enum, not that the *set* is
  discovered by a runtime scan.
- **The open discriminator does not weaken persistence or dispatch.** Persistence is
  already string; dispatch is already dictionary-keyed. The one safety lost —
  compile-time exhaustiveness of the enum — is bought back by the honesty test
  (§4.8), which asserts the registry, the manifests, and the dispatchers all agree.
- **Integrations know nothing about Piro.** An integration lives in its own assembly
  (§4.1) and reaches the rest of the system only through a narrow, allow-listed host
  window (§4.2b) — never `IServiceProvider`, a repository, `PiroDbContext`, or an
  ambient `HttpClient`. It asks the host for what it needs; the host decides what it
  may have. This is enforced by the reference graph and the honesty test, not by
  convention.

## 4. Design

### 4.1 One assembly per integration (except Email)

An integration becomes a **self-describing class** (`IIntegration`, §4.2) that lives,
with its config and dispatcher(s), in **its own assembly**: `Piro.Integrations.Twilio`,
`Piro.Integrations.Jira`, `Piro.Integrations.Telegram`, `Piro.Integrations.Ntfy`,
`Piro.Integrations.GoogleChat`, `Piro.Integrations.Webhook`, `Piro.Integrations.Gcp`,
`Piro.Integrations.GoogleCloud`. `Piro.Api` references each one. Each references
`Piro.Integrations.Abstractions` (the contract) and nothing of another integration. This
is the structural expression of "an integration is a self-contained thing": one project,
one provider, its own dependency list.

> **As shipped.** The list above is the set that shipped. `Piro.Integrations.PagerDuty`
> is **not** among them: PagerDuty was withdrawn (§4.9, RFC 0004 now `withdrawn`).
> `Piro.Integrations.GoogleCloud` (the Cloud Run Job check provider, §4.6) joined the set.

The point is not primarily dependency isolation (only Twilio carries a heavy SDK); it
is **boundary**. A separate assembly makes it *impossible* for an integration to
reach into Piro's internals by accident, because the reference graph doesn't allow it
— an integration assembly references the contract, not `Piro.Infrastructure`, not
`Piro.Domain`'s repositories, not `PiroDbContext`. That boundary is what makes §4.2's
"integrations know nothing" a compile-time fact rather than a convention.

**Email is the one exception, and stays in `Piro.Infrastructure`.** Its SMTP transport
(`Email/EmailService.cs`, the only holder of `MailKit`) is **core infrastructure**
used by account-setup and password-reset (`SetupController`, `EmailConfigController`,
RFC 0014) and by `IVerificationCodeSender`, independent of the integration system.
Giving Email its own assembly would force that assembly to expose the `EmailService`
the core depends on, inverting the dependency. So Email's `IIntegration` lives in
`Piro.Infrastructure` alongside the transport it shares with the core. It is the
documented exception to the one-assembly rule, not a precedent.

Not split, and out of scope: **gRPC** (`Grpc.*`) belongs to the GoogleCloud *check
executor*, not an integration dispatcher. The **obsolete stubs** (Discord, Slack,
MSTeams, Opsgenie, Pushover) have no manifest and no registration today; they get no
`IIntegration` and no assembly, and are deleted or left as `[Obsolete]` stubs
separately.

### 4.2 `IIntegration` — the self-describing declaration

Each live integration is a class carrying its identity and manifest:

```csharp
// Piro.Integrations.Abstractions
public interface IIntegration
{
    /// <summary>Stable, permanent identifier — the discriminator persisted in every Integration row.
    /// MUST equal the current enum member name verbatim ("Jira", "Twilio", …) so no data migrates.
    /// Immutable once shipped (§3).</summary>
    string IntegrationId { get; }

    /// <summary>Everything the manifest declared before: capabilities, config type, label, icon,
    /// category, supported events (§4.5). Pure data — no behavior, no injected services, safe to
    /// read at design time (§4.3).</summary>
    IntegrationManifest Manifest { get; }
}
```

`[IntegrationManifest]` and `[IntegrationAction]` stop being `AttributeTargets.Field`
attributes on enum values and become **class-level** metadata on the `IIntegration`
implementation. The manifest content is unchanged; only its anchor moves from a
central enum field to the class that owns it. `IIntegration` is **pure data** — it
holds no injected services and does nothing at construction, so the registry can
instantiate it cheaply and safely even at design time (§4.3). Behavior stays in the
dispatchers/actions, which remain DI-constructed (§4.3).

There is **no lifecycle hook** in this RFC (§2). When the UI-extension work lands, it
adds the seam it needs then, against a proven consumer.

### 4.2b Integrations know nothing — a filtered service window

An integration must not reach into Piro. It does not see `IServiceProvider`, does not
touch `PiroDbContext`, a repository, an application service, or a raw secret, and does
not `new` its own `HttpClient` off the ambient environment. Its own assembly (§4.1)
makes most of this a *compile-time* fact: it references `Piro.Integrations.Abstractions`
and its provider SDK, not `Piro.Infrastructure` or `Piro.Domain`'s repositories, so the
types simply aren't on the reference graph to reach for.

What an integration's behavior *does* get is a **host that hands it a narrow, allow-listed
window** onto the services it may use. The host wraps DI but resolves only permitted
types:

```csharp
// Piro.Integrations.Abstractions — passed to a dispatcher/action, never IServiceProvider itself
public interface IIntegrationHost
{
    /// <summary>Resolve a service the integration is ALLOWED to use. Throws if the requested type is
    /// not on the allow-list — the integration can ask for an HttpClient or a logger, never a
    /// repository, DbContext, or another integration's internals. This is a filtered DI window, not
    /// the container: "the integration reaches Piro only through this bounded surface."</summary>
    T GetRequiredService<T>() where T : notnull;
}
```

The allow-list is small and explicit (settled at implementation, but shaped like):
`HttpClient`/`IHttpClientFactory`, `ILogger<T>`, the OAuth bearer-token provider (RFC
0004), the config accessor for *this* integration's own `ConfigJson`, and the outbound
external-reference writer (RFC 0012's `LinkExternalAsync`). Ask for anything else and
`GetRequiredService<T>` throws. So:

- **`HttpClient` is requested, not ambient.** A dispatcher that needs HTTP calls
  `host.GetRequiredService<HttpClient>()` (a pre-configured, pooled, timeout-bounded
  client the host owns), rather than constructing one. An integration that needs no
  HTTP asks for nothing and gets nothing.
- **This generalizes RFC 0012's `IActionHost`.** `IActionHost` is already documented as
  "the internal SDK a plugin consumes, reaching Piro through a bounded surface, not its
  persistence" (`IActionHost.cs:5-14`). Its fixed methods (`GetTargetAsync`,
  `LinkExternalAsync`, `GetBearerTokenAsync`) become facets exposed through this host;
  the filtered `GetRequiredService<T>` is the general form of the same idea.
- **It also secures the design-time boot (§4.3).** Because behavior only ever resolves
  services *lazily through the host at call time* — never at construction or startup —
  the OpenAPI build-time boot instantiates nothing that touches config or network.

The honesty test (§4.8) asserts the boundary: an integration assembly that references a
forbidden Piro type fails, and resolving any integration's dispatchers touches only
allow-listed services.

### 4.3 Explicit compile-time registry (not a reflection scan)

Discovery is an **explicit registry**, resolved at startup inside `AddInfrastructure`
(`Program.cs:137`) or a new `AddIntegrations(...)`. Two acceptable mechanisms, decided
at implementation:

- **A hand-maintained static array** in one file: `IntegrationRegistry.All = [ new
  JiraIntegration(), new TwilioIntegration(), … ]`. Dead simple, greppable, one line
  per integration — the explicit successor to the DI block it replaces.
- **`services.Scan()` (Scrutor)** over the *referenced* integration types, or a small
  **source generator** emitting the array from `[IntegrationManifest]`-marked classes.
  These keep "add a class, it's registered" without a hand-edited list.

Either way it is **compile-time-total**: the set of integrations is code
`Piro.Api`/`Piro.Infrastructure` compile against, not the runtime-populated result of
`AppDomain.CurrentDomain.GetAssemblies()`. That reflection API returns *loaded*
assemblies, and .NET loads lazily — an integration whose types hadn't been touched
would be silently absent, dropping it from `GET /integrations/types` with no error.
The registry avoids that class of bug entirely: if an integration isn't in the
registry, it fails to compile or is visibly missing from a greppable list, never
silently dropped at runtime.

For each registry entry the loader:

1. Reads `IntegrationId` + `Manifest` from the (pure-data) `IIntegration` instance.
2. Registers its dispatcher / action / options-provider / OAuth-descriptor **types**
   into DI (`AddScoped(typeof(...))`), keyed by `IntegrationId` — it **never `new`s a
   dispatcher**, because dispatchers have real DI dependencies (`IHttpClientFactory`,
   `ILogger<T>`, the secret protector). Only the pure-data `IIntegration` is
   instantiated directly.

**Design-time safety (required).** `Piro.Api.csproj` uses
`Microsoft.Extensions.ApiDescription.Server` (`GetDocument.Insider`), which **boots
the full host at build time** to emit the OpenAPI document that `apps/admin`'s
`api-types.ts` is generated from — so `AddInfrastructure` and the registry run during
`dotnet build`, with no Twilio/SMTP/OAuth connectivity. Therefore: **reading the
registry and manifests must be side-effect-free** (no config reads, no network, no
secret access at registration time). `IIntegration` being pure data (§4.2) and
dispatchers being DI-*registered* but not *constructed* at startup satisfies this;
the honesty test (§4.8) asserts it by resolving the registry with no live
connections.

Adding an integration = a new `IIntegration` class + one registry line (or, with
scan/source-gen, just the class). Removing one = delete both. The registry file (or
generated output) is the single greppable place the set of integrations is declared.

### 4.4 Killing the `IntegrationType` enum

The enum is deleted; `IntegrationId` (a `string`) takes over as the discriminator.
Concretely, across ~64 files:

- **Interfaces** (`IPersonalNotificationDispatcher`, `IChannelNotificationDispatcher`,
  `ISystemEventDispatcher`, `IIntegrationAction`, `IOptionsProvider`,
  `IActionRegistry`, `IVerificationCodeSender`): `IntegrationType Type` → `string
  IntegrationId`.

> **As shipped.** The three delivery dispatcher interfaces
> (`IPersonalNotificationDispatcher` / `IChannelNotificationDispatcher` /
> `ISystemEventDispatcher`) **collapsed into a single `IIntegrationEventHandler`** with
> one method: `Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx,
> IIntegrationHost host, CancellationToken ct)`. The engine resolves the target and the
> delivery mode (`Personal` / `Channel`, on `EventDeliveryContext`) and hands the
> integration the neutral `Event`; the integration decides how to render and deliver, or
> returns `false` to ignore it. This became possible once the system-event/paging path
> was removed with PagerDuty (§4.9, RFC 0004 withdrawn): with no third-party paging
> destination left, there was no distinct "system event" dispatch shape to keep separate
> from notification delivery. `IIntegrationAction` / `IOptionsProvider` / `IActionRegistry` still exist for
> RFC 0012 UI actions; `IVerificationCodeSender` was removed (setup uses the core
> `EmailService` directly).
- **Dispatch tables** (five `ToDictionary(d => d.Type)` sites): key type enum →
  `string`. No `switch` to rewrite.
- **Entities** (`Integration.Type`, `NotificationDeliveryLog.IntegrationType`,
  `EscalationDeliveryLog.ChannelType`): `IntegrationType` → `string`. **No EF
  migration** (columns already string), proven by an empty `migrations add` diff.
- **DTOs / wire** already serialize as strings; the only visible change is the
  `api-types.ts` `IntegrationType` union widening to `string`. A frontend *type*
  change (loss of admin-side exhaustiveness), not a data change — the JSON is
  identical, and the values remain discoverable via `GET /integrations/types`.
- **The three OAuth `switch`es** collapse: the `IntegrationId` *is* the provider-id
  string they were mapping to.

The honesty test (§4.8) asserts every `IntegrationId` is unique, non-empty, and that
the id set exactly matches the retired enum names — so nothing silently changes
spelling during the swap.

### 4.5 The events-available contract (fixes #212)

`INotificationSubscriber.SupportedEvents` is declared, DI-registered six times, and
**never read** at any decision point (grep-confirmed — a subscription can wire any
event to any destination). The events an integration handles move onto its manifest:

```csharp
// IntegrationManifest (Piro.Integrations.Abstractions)
public IReadOnlyList<string> SupportedEvents { get; init; } = [];
```

> **As shipped.** `SupportedEvents` is `IReadOnlyList<string>` of **stable event wire
> names** ("alert:created", "incident:resolved"), not `NotificationEventType[]`. This is
> stricter about the boundary than the proposal: an integration declares which public
> events it handles *without depending on Piro's `NotificationEventType` enum at all*,
> exactly like RFC 0009's permanent wire names. It also supports **wildcards** —
> `Manifest.HandlesEvent(wireName)` matches `"*"` (everything), a prefix like `"alert:*"`,
> or an exact name — so Telegram declares `["alert:*", "incident:*"]` rather than
> enumerating every event. The create-time guard and the honesty test both validate each
> declared name against the live catalog, so a typo'd or retired wire name fails fast
> even though the field is now a free string.

A new capability **`SubscribesToEvents`** is a **hard precondition**: an integration
must declare it (with a non-empty `SupportedEvents`) to be an event-subscription
destination. Enforcement:

- **Build-time** (honesty test): `SubscribesToEvents` ⟺ non-empty `SupportedEvents`.
- **Run-time** (create-time guard): `NotificationSubscriptionAppService.ValidateAsync`
  rejects (HTTP 400) a subscription whose destination does not declare
  `SubscribesToEvents`, then rejects any event outside its `SupportedEvents`. The
  service reads the manifest via the registry — no Infrastructure reach (what was
  impossible when the data was `internal` to Infrastructure).

`INotificationSubscriber` / `EventSubscriber` and their six registrations are
**deleted**; the policy they carried (event set + `TargetKind`) is now manifest data
(`TargetKind` derived from capabilities, §4.6). The UI event menu is scoped per
destination via a `?integrationId=` filter on `GET /api/v1/event-subscriptions/events`.

This is the highest-value, lowest-risk part of the RFC and needs no enum kill or
assembly change — it is Track A (§7).

### 4.6 Capabilities and derived `Direction`

`IntegrationCapability` stays a manifest flag set, but becomes machine-checked (§4.8):

- **`Direction` is removed.** `IntegrationDirection` is declared on every manifest and
  projected to the DTO but read by *nothing* (grep-confirmed: no `if`/`==`/`switch`/
  `where` reader). It is derivable from capabilities (`CreatesAlerts` ⇒ inbound;
  `Sends*`/`ExtendsUserInterface` ⇒ outbound) and is deleted from the manifest, derived
  by a helper; the DTO field stays on the wire, now computed. `TargetKind` derives the
  same way.

> **As shipped.** `Direction` is no longer a *declared* manifest field — it is a computed
> property (`Manifest.Direction => DeriveDirection(Capabilities)`), exactly as proposed,
> so no hand-set value can disagree with the capabilities beside it. The
> `IntegrationDirection` enum itself was kept but marked `[Obsolete]` (capabilities are the
> source of truth; callers should read those). It still projects to the DTO badge for the
> admin. Fully deleting the enum is deferred cleanup, tracked as a follow-up.
- **`ProvidesActions` → `ExtendsUserInterface`** — a rename so the capability isn't
  wedded to one UI surface (buttons) as future surfaces appear. The actual
  multi-surface model and its host seam are a *later* RFC (§2); this RFC only renames
  the flag so that RFC doesn't have to.

> **As shipped — integrations that ship checks.** A capability the proposal didn't
> anticipate landed here because the check subsystem (RFC 0011) went through the same
> self-describing extraction in parallel: **`ProvidesChecks`**. A provider integration
> whose data is probed by a check returns that check from `IIntegration.ProvidedChecks()`,
> and the check lives in the integration's own assembly — available in the catalog only
> while the integration is registered. `Piro.Integrations.GoogleCloud` ships the Cloud Run
> Job check this way; Piro core never hardcodes the check→integration link. The honesty
> test (§4.8) asserts `ProvidesChecks` is set iff `ProvidedChecks()` is non-empty. The
> checks half of this (the `ICheck` / `Check<TConfig>` / `ICheckRegistry` contracts in
> `Piro.Checks.Abstractions`, the `DimensionSpec` binding model, and the shared
> `Piro.Contracts` config-schema engine used by both, §4.7) is the sibling of this
> integration work and shipped in the same branch.

### 4.7 `Piro.Contracts` — the shared config-schema engine

`ConfigSchemaBuilder`, `ConfigFieldSchemaDto`, `ConfigFieldType`, and the config-field
attributes are shared by **both integrations and check types**
(`CheckTypeManifestExtensions`, `CheckTypeMetaDto` — RFC 0011). They are **not**
integration-specific, so they move into a neutrally-named **`Piro.Contracts`**
assembly both reference, rather than an integrations-named one that the Check
subsystem would have to depend on. `Piro.Integrations.Abstractions` (the integration
contract: `IIntegration`, manifest, dispatcher/action interfaces) references
`Piro.Contracts`, `Piro.Domain`, and `Piro.Application`.

### 4.8 The capabilities-honesty test

A unit test in `Piro.UnitTests` builds the integration registry from a **minimal
`ServiceCollection` with only the integration registrations** (not the full API host,
so no live-connection dependency, and it doubles as the design-time-safety check of
§4.3) and asserts, **one-directional in the dangerous direction**: *every registered
dispatcher / action / options-provider's `IntegrationId` carries the matching
capability flag on its integration's manifest.* This catches under-declaration (a
wired-up component whose manifest forgot the flag — the case that silently drops a
working integration from the UI), and it catches the free-string typo risk the open
discriminator introduces (a dispatcher whose `IntegrationId` matches no registered
integration fails the test).

It does **not** assert the reverse (flag ⇒ registration) as a hard failure —
context-typed dispatchers (Email registers for Alert *and* Incident contexts but
carries one `SendsPersonalNotification` flag) and inbound-only flags
(`CreatesAlerts`/`SupportsEscalationPolicy`/`SupportsCheckCorrelation`, no dispatcher)
make a bidirectional `⟺` a false-positive generator. The reverse is a **non-failing
report** the test prints; the inbound flags are documented-skip.

It also asserts: every `IntegrationId` unique and non-empty; `SubscribesToEvents` ⟺
non-empty `SupportedEvents`; `ProvidesChecks` ⟺ non-empty `ProvidedChecks()`;
`ExtendsUserInterface` ⟺ ≥1 declared action.

**And it enforces the "integrations know nothing" boundary (§4.2b), the part that is
only real if tested:**

- **No forbidden references.** An architecture assertion (e.g. via `NetArchTest` or a
  reference-graph check) that no `Piro.Integrations.*` assembly references
  `Piro.Infrastructure`, `PiroDbContext`, a repository, or an application service — only
  `Piro.Integrations.Abstractions` and its own provider SDK. A split assembly that reaches
  into Piro fails the build.
- **Only allow-listed services resolve.** Building each integration's dispatchers through
  the `IIntegrationHost` window resolves only allow-listed types; a dispatcher that asks
  the host for anything off the list surfaces here rather than in production.

**PagerDuty note (superseded by §4.9).** The proposal opened with reconciling a stale
`PagerDutyDispatcher` comment. That is moot in the shipped result: PagerDuty was
**withdrawn** during implementation (Piro owns on-call via escalation policies, RFC 0015;
RFC 0004 now `withdrawn`, §4.9). Its dispatcher no longer exists, so there is no comment
left to reconcile and nothing for the honesty test to assert about it.

### 4.9 PagerDuty withdrawn (RFC 0004)

The PagerDuty integration (specified in **RFC 0004** — OAuth connect, a real Events API
v2 enqueue that paged the on-call team, per-service routing-key discovery) was **removed**
in this work, because *Piro now provides on-call itself via escalation policies* (RFC
0015): an external paging provider is redundant with the product's own capability. Its
assembly, config, dispatcher, OAuth descriptor, discovery service, and the
service-integration-mapping controller were all deleted.

RFC 0004 is therefore now marked **`withdrawn`** (`superseded-by: 15`). This is a
reversible state at the RFC level, not a claim about a code lifecycle field: the
`"PagerDuty"` discriminator was a permanent string (§3), so if a future need to page
through PagerDuty specifically returns, the integration can be re-introduced as a fresh
`IIntegration` under that same id — stored rows (if any) would keep their identity, no
data migration required. Withdrawing an integration is thus a documented, reversible
decision, recorded in the withdrawn RFC rather than as a code-level "retired" flag (which
was considered and rejected as premature — there is exactly one withdrawn integration and
no consumer for such a flag today; YAGNI, consistent with §2).

## 5. Data / schema scope

- **New assemblies:** `Piro.Contracts` (shared config-schema engine, used by both
  integrations and checks), `Piro.Integrations.Abstractions` (integration contract +
  `IIntegrationHost` + `IIntegrationEventHandler`), one `Piro.Integrations.<Provider>` per
  live integration (Twilio, Jira, Telegram, Ntfy, GoogleChat, Webhook, Gcp, GoogleCloud),
  and — from the sibling check work (§4.6) — `Piro.Checks.Abstractions` and `Piro.Checks`.
  `Piro.Api` references each. Email is the exception and its `IIntegration` stays in
  `Piro.Infrastructure` (§4.1). PagerDuty ships no assembly (retired, §4.9).
- **Deleted:** the `IntegrationType` enum; `INotificationSubscriber` + `EventSubscriber`
  + six registrations; the central dispatcher/action DI block
  (`InfrastructureServiceExtensions.cs:193-309`), replaced by the explicit registry.
- **No database migration.** All three enum-backed columns are `HasConversion<string>()`;
  C# type changes `IntegrationType` → `string`, stored values do not. Proven by an
  empty `dotnet ef migrations add` diff (not assumed). The `[Obsolete]` "ordinals
  matter" comments are corrected (false).
- **Manifest:** `SupportedEvents` (`string[]` wire names + wildcards) + `SubscribesToEvents`
  added; `ProvidesChecks` added (§4.6); `Direction` now derived (enum kept `[Obsolete]`);
  `ProvidesActions` renamed to `ExtendsUserInterface`.
- **Wire:** `api-types.ts` `IntegrationType` union widens to `string`. No other DTO
  shape change.
- **Frontend:** unify the two `DynamicConfigField` renderers into one
  `config-form/FieldControl`; scope the event-subscription form to manifest
  `SupportedEvents`.

## 6. Frontend

Two narrow gaps (per-type config forms are already schema-driven):

- **Two `DynamicConfigField` implementations.** Checks use `config-form/FieldControl`
  (full type switch, no secret/generated/upload); integrations use their own
  `features/integrations/DynamicConfigField.tsx` (secret/generated/upload, missing
  type branches). Fold the integration-only concerns into `FieldControl` as
  schema-flag-driven branches (`isSecret`, `isGenerated`, `supportsFileUpload` — all
  already on `ConfigFieldSchemaDto`), route `IntegrationConfigForm` through the shared
  `DynamicConfigForm`, delete the duplicate. One renderer for integration config,
  check config, and action dialogs.
- **The event-subscription form** (`SubscriptionFormModal.tsx`) scopes its event
  `MultiSelect` to the selected destination's `SupportedEvents` via `?integrationId=`
  — the frontend half of #212.

## 7. Phased plan

Two tracks. **Track A ships all user-visible value with no new assembly and no enum
change**, so it is not blocked by the restructure and closes #212 fast. Track B is
the structural change, sequenced so each commit stays green — the phase-5 enum kill is
split into green sub-commits (a prior single 64-file cutover could not stay green).

**Track A — correctness, current layout (reversible, high value):**

0. **Reconcile the PagerDuty comment** (§4.8) — tiny, low-risk (the dispatcher pages;
   the comment is wrong).
1. **Fix #212**: `SupportedEvents` onto the manifest + `SubscribesToEvents` capability;
   delete `INotificationSubscriber`/`EventSubscriber`; two-step create-time guard;
   `?integrationId=` catalog filter. **Closes #212.**
2. **Capability cleanup**: derive+remove `Direction`; rename `ProvidesActions` →
   `ExtendsUserInterface`; the one-directional honesty test.
3. **Frontend unification** (§6).

**Track B — the SDK restructure (open discriminator + registry):**

4. **`Piro.Contracts`**: extract the shared config-schema engine (§4.7). Pure move;
   checks + integrations reference it; build/test green, OpenAPI unchanged.
5. **`Piro.Integrations.Abstractions` + `IIntegration` + kill the enum**, as green
   sub-commits:
   - **5a** — introduce `string IntegrationId` *alongside* the enum; interfaces gain
     the string, dispatchers set it from their enum value. Both exist; green.
   - **5b** — migrate the five dispatch tables + all resolution sites to key on the
     string; the enum is now unused in dispatch. Green.
   - **5c** — delete the enum; re-anchor manifests onto `IIntegration` classes; prove
     an empty `ef migrations` diff. Green.
6. **The explicit registry**: replace the central DI block with the registry (§4.3),
   dispatchers registered as *types*; assert design-time safety via the honesty test.
7. **Split each integration into its own assembly** (§4.1), one provider per commit so
   each stays green: move that provider's config + dispatcher(s) + `IIntegration` (and
   its `PackageReference`, e.g. Twilio's SDK) into `Piro.Integrations.<Provider>`,
   referenced by `Piro.Api`; the dispatcher now takes its `HttpClient`/logger through
   `IIntegrationHost` (§4.2b) rather than ambient DI. Start with Twilio (the real
   dependency-isolation case) to prove the pattern, then the `HttpClient`-only
   providers. Email is **not** split (§4.1). The registry (phase 6) picks up each
   provider as its assembly lands.

**Deferred to future RFCs:** the UI-extension host seam / lifecycle hook (§2); runtime
hot-load of third-party assemblies (§2).

## 8. Alternatives considered

- **Keep the enum; just fix #212.** The minimal fix (Track A alone). Rejected as the
  *whole* answer — leaves the five-edit-sites problem and the drift risk. But Track A
  *is* shipped first, so the bug fix never waits on the restructure.
- **Co-locate all integrations in `Piro.Infrastructure` (no physical split).** A
  smaller step considered — the string discriminator and registry work without new
  assemblies. Rejected as the endpoint: keeping integrations in `Piro.Infrastructure`
  means "an integration knows nothing about Piro" (§4.2b) is only a *convention* the
  reference graph doesn't enforce — a dispatcher could still `new` an `HttpClient` or
  reach a repository. A per-integration assembly makes the boundary a compile-time
  fact. Only Twilio isolates a *heavy* dependency, but boundary enforcement (not
  dependency isolation) is the reason every provider (except Email) is split.
- **Discover integrations via `AppDomain.GetAssemblies()` reflection.** A prior draft.
  Rejected as a **bug**: that API returns *loaded* (lazy) assemblies, not *referenced*
  ones, so a referenced-but-untouched integration is silently dropped from the UI with
  no error, and the honesty test would stay green (its process loads the assembly). The
  explicit compile-time registry (§4.3) is total and greppable and has no load-order
  dependency.
- **An `OnStart(host)` lifecycle hook now.** Rejected — no consumer today; it belongs to
  the RFC that builds the UI-extension surface (§2). Adding a seam with zero call sites
  is a guess dressed as architecture.
- **Runtime hot-load now.** Rejected — out of scope (§2). The explicit registry gives
  the open discriminator without hot-load's cost or risk.

## 9. Risks

- **Large mechanical diff (~64 files, ~149 refs) for the enum swap.** Mitigation: no
  switches, no migration; phase 5 is split into green sub-commits (5a/5b/5c) so no
  intermediate red state, honoring the "small reviewable commits" goal.
- **Design-time OpenAPI generation runs the registry** (`GetDocument.Insider` boots the
  host). Mitigation (§4.3): `IIntegration` is pure data; dispatchers are DI-registered,
  not constructed, at startup; the honesty test resolves the registry with no live
  connections, proving side-effect-freedom. **Spike this before phase 6** — run
  `pnpm run generate:api-types` against the registry to confirm it doesn't break.
- **`ef migrations add` might not be a true no-op.** Mitigation: run it in phase 5c and
  require an empty diff, don't assume from the snapshot.
- **Open discriminator loses compile-time exhaustiveness** (a typo'd `IntegrationId` no
  longer fails to compile). Mitigation: the honesty test asserts every dispatcher's
  `IntegrationId` matches a registered integration and the id set matches the retired
  enum names.
- **Frontend loses the closed union** (`IntegrationType` → `string`). Mitigation: the
  admin renders types from `GET /integrations/types` (the manifest list), not a
  hard-coded union.
- **Scope.** This is materially larger than "abstract with least pain": a per-integration
  assembly split plus the open-discriminator restructure. Mitigation: Track A (the #212
  fix + cleanup) is fully independent, reversible, and delivers the highest RICE on its
  own; Track B's phase 5-6 (open discriminator + registry) can land without phase 7, and
  phase 7 is one provider per commit, so the split can pause at any provider if it stops
  paying off. The RFC is stoppable after Track A, after phase 6, or after any single
  provider split.
- **The boundary must actually be enforced, not just declared.** "Integrations know
  nothing" (§4.2b) is only real if the honesty test proves it — a per-integration
  assembly referencing a forbidden Piro type, or a dispatcher resolving a non-allow-listed
  service through the host, must fail the build. Mitigation: §4.8 makes both assertions
  explicit; without them the boundary is aspirational.

## 10. What actually shipped (implementation reconciliation)

This RFC was implemented on branch `implements-rfc/0016-contracts-abstractions`. The
design above is the accepted proposal; the deltas below are where the implementation
refined it. Each is also flagged inline as an "As shipped" note next to the relevant
section.

| Area | Proposed | Shipped |
|---|---|---|
| Manifest `SupportedEvents` (§4.5) | `NotificationEventType[]` | `IReadOnlyList<string>` of stable wire names, with wildcard matching (`"alert:*"`) — tighter boundary, no enum dependency |
| Delivery dispatchers (§4.4) | three interfaces (`IPersonal*`/`IChannel*`/`ISystemEvent*`) | one `IIntegrationEventHandler.HandleAsync(Event, EventDeliveryContext, IIntegrationHost)` |
| `IVerificationCodeSender` | reworked to `IntegrationId` | removed — setup uses the core `EmailService` directly |
| `Direction` (§4.6) | removed from manifest | derived (computed property); `IntegrationDirection` enum kept `[Obsolete]`, full deletion deferred |
| Checks (§4.6) | not in scope | sibling extraction landed here: `ProvidesChecks` capability, `IIntegration.ProvidedChecks()`, `Piro.Checks[.Abstractions]`, `DimensionSpec` binding, GoogleCloud ships the Cloud Run Job check |
| PagerDuty (§4.9) | shipped assembly; phase-0 comment fix | **withdrawn** — Piro owns on-call via escalation policies (RFC 0015); assembly/dispatcher/config deleted; RFC 0004 marked `withdrawn`. The permanent `"PagerDuty"` id makes re-introduction migration-free if ever needed (no code-level lifecycle flag was added — YAGNI) |
| `Piro.Contracts` (§4.7) | shared config-schema | shipped, and also holds the shared `ThresholdDirection` / `DimensionComparison` enums for the check binding model |

Everything else landed as designed: the open `string IntegrationId` discriminator with
no database migration; the explicit compile-time registry (a hand-maintained static array,
§4.3) over a reflection scan; the per-assembly boundary; the filtered `IIntegrationHost`
window; the #212 events-available guards (build-time honesty test + create-time
validation); and the capabilities-honesty test.
