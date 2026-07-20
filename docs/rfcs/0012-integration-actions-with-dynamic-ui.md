---
rfc: 12
title: "Integration actions with dynamic UI (Jira ticket creation as first consumer)"
status: accepted
created: 2026-07-18
depends-on: ["0003", "0004", "0011"]
---

# RFC 0012 — Integration actions with dynamic UI (Jira ticket creation as first consumer)

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-18

## 1. Problem

`IntegrationType.Jira` (`src/Piro.Domain/Enums/IntegrationType.cs:20-29`) is a fully declared, creatable integration: it has a manifest, the label "Jira", the description *"Create and track Jira tickets from alerts,"* an `logos:jira` icon, and a complete `JiraConfig` (`src/Piro.Domain/Integrations/Config/JiraConfig.cs`) with `BaseUrl`, `Email`, `ApiToken`, `ProjectKey`, `IssueType`. An admin can create a Jira integration today and fill in every field. And then **nothing happens** — its manifest capability is honestly `IntegrationCapability.None` (`IntegrationType.cs:23`), no code deserializes `JiraConfig`, no code calls the Jira API, and there is no way to actually create a ticket. The description on the type is a promise the codebase does not keep.

The concrete thing a user wants is small and specific: while looking at an alert that just fired, click a button, confirm a title and a description, and get a Jira ticket created and **linked back** to that alert — so the ticket is one click away from Piro and Piro doesn't create a second ticket next time the button is pressed. The same want extends to incidents and maintenance windows.

Making that work surfaces a gap that is bigger than "Jira" and is the real subject of this RFC:

1. **Piro has no notion of a user-initiated action that an integration contributes to the UI.** Every existing integration behavior is *automatic and headless*: `INotificationDispatcher` (`src/Piro.Application/Interfaces/INotificationDispatcher.cs:8-28`) fires on escalation, inbound webhooks create alerts. None of them put a button in front of a human who decides whether and when to act, or collects input from that human. There is no interface, no endpoint, and no frontend component for "an integration offers an action on this kind of object."

2. **There is nowhere to record that Piro created an external thing for a local object.** `Alert` already carries `ExternalId` (`src/Piro.Domain/Entities/Alert.cs:107`) and `SourceUrl` (`Alert.cs:114`) — but those are *inbound* (RFC 0001: an external system created the alert, these point back at its origin). `Incident` and `Maintenance` have no external-reference fields at all (`Incident.cs`, `Maintenance.cs`). There is no *outbound* concept — "Piro created Jira OPS-123 for this alert, here is its URL" — on any of the three.

3. **Calling Jira securely requires credentials Piro can't hold safely today.** `JiraConfig` (`JiraConfig.cs`) models email + API token — a long-lived static credential that would sit in `Integration.ConfigJson` as plaintext `jsonb` (`Integration.cs:12`, `IntegrationConfiguration.cs:17`). This RFC deliberately does **not** create tickets with a plaintext API token. It authenticates to Jira via **OAuth 2.0 (3LO)** — access + refresh tokens, encrypted at rest, refreshed on use — which is exactly the capability **RFC 0004** designs (`IOAuthClient` + an encrypted token store via `IDataProtector`, RFC 0004 §4.3). Jira is named in RFC 0004 as a future consumer of that framework; this RFC makes it the real one. **RFC 0004 is therefore a hard prerequisite of this RFC, from Phase 1** (§4.6, §4.10) — not just of the Phase 2 auto-create.

This RFC proposes the missing action layer generically, with **Jira ticket creation as the first and only implemented consumer**, authenticated over OAuth via RFC 0004's framework, and defines the persistence needed to link what an action creates back to the local object it was invoked from.

## 2. Non-goals

- **Automatic ticket creation on alert firing.** This RFC ships *manual, user-initiated* actions only (§6, Phase 1). "According to the integration's settings, auto-create a ticket when an alert fires" is real and wanted, but it is a distinct axis (an *outbound event dispatcher*, not a UI action) and is deferred to Phase 2 (§4.9, §6). The action handler built here is the single execution point both the button and a future auto-dispatcher call — so Phase 2 adds a *caller*, not a second Jira implementation.
- **Runtime / third-party plugins.** The action *contract* (§4) is deliberately shaped so a hot-loaded plugin could one day populate the same descriptors and register the same handlers without touching the frontend, the database, or the wire format (§4.10). But this RFC does **not** build a plugin loader, an `AssemblyLoadContext` sandbox, or a WASM host. `IntegrationType` stays a closed compiled enum here; the plugin door is left *openable*, not *opened*.
- **Two-way sync from Jira back to Piro.** Once a ticket is created, Jira owns its lifecycle. Reflecting a Jira status change back onto the Piro alert/incident (via Jira webhooks) is a separate future RFC, same principle as RFC 0001 §3 and RFC 0004's inbound non-goal.
- **Building the OAuth framework itself.** This RFC *consumes* RFC 0004's OAuth framework (`IOAuthClient`, encrypted token store, refresh-on-use, per-provider OAuth-app config) — it does not re-implement it. Registering Jira as an OAuth provider (its authorize/token endpoints, scopes, the `cloudId` discovery quirk, §4.6) is in scope; the generic flow, PKCE, and token persistence are 0004's and are assumed present. If 0004 is not yet implemented, this RFC is blocked (§6). **Basic auth (email + API token) is explicitly rejected** as the Phase 1 mechanism — see §7 — so `JiraConfig`'s current `Email`/`ApiToken` fields are superseded by the OAuth connection (§5).
- **Per-user Jira identity.** Tickets are created as a **single service identity per integration** — one admin connects Piro↔Jira via OAuth once, and every ticket is created under that connection's token (the token-per-`Integration` model RFC 0004 already uses). Per-user OAuth (each operator connecting their own Jira account so the ticket shows their name) is a larger scope — a per-user token store, forcing every operator to connect — and is not built here. Who clicked the button is recorded in the ticket body instead (§4.6).
- **Editing or deleting the created Jira ticket from Piro.** Phase 1 creates and links. Managing the ticket afterward happens in Jira. (A future "add comment" or "transition" action is a natural second `IIntegrationAction`, explicitly out of scope now.)
- **Actions on objects other than Alert / Incident / Maintenance.** The context enum (§4.2) is closed to these three. Adding `Check` or `Service` later is a one-value enum addition plus a frontend placement, by design — but not done here.

## 3. Design principle

**The backend classifies actions by *what kind of object* they apply to; the frontend decides *where* the button goes.** The server never encodes UI layout — it answers exactly one question ("which actions apply to an object of context X?") and returns descriptors. Each page already knows what it is (the alert page knows it shows an alert) and drops a generic `<ActionButtons context="Alert" targetId={id}>` wherever it wants the buttons. This keeps the server ignorant of the UI and lets a new integration's button appear on all three pages without a frontend change.

Two constraints follow from this and shape everything in §4:

- **One source of truth for an action's input.** The C# `InputType` that produces the dialog's schema (rendered by the *existing* config-as-schema engine) is the *same* type that validates the execution `POST`. The form the user sees and the payload the server accepts cannot drift, because they are reflected from one class — exactly the RFC 0003/0011 config-as-schema bargain, reused for action inputs instead of connection config.
- **Reuse the manifest + dispatcher-registry patterns already in the codebase, don't invent parallel ones.** Actions are declared as manifest metadata (like `IntegrationManifestAttribute`) and resolved from DI by a discriminator property (like `IEnumerable<INotificationDispatcher>.ToDictionary(d => d.Type)`), so a reviewer already fluent in Piro's integration code recognizes the shape.

## 4. Design

### 4.1 End-to-end flow

Everything marked **[new]** does not exist today. `ConfigSchemaBuilder`, the shared `DynamicConfigForm`, `PageHeader actions`, the toast + `MarkdownEditor` components, and `IIntegrationRepository` all exist and are reused unchanged. Everything marked **[0004]** is provided by RFC 0004's OAuth framework and consumed here, not built.

```
CONNECT  [0004]                                       (one-time, per integration — RFC 0004 §4.3)
  Admin clicks "Connect Jira" on the Jira integration
        → IOAuthClient builds the Atlassian authorize URL (auth code + PKCE, scopes:
          write:jira-work read:jira-work offline_access) → user consents
        → callback → code-for-token exchange → access + refresh tokens stored ENCRYPTED
          (IDataProtector), keyed by IntegrationId; Jira cloudId discovered once
          (GET /oauth/token/accessible-resources) and stored alongside

DISCOVER  [new]                                       (frontend places the container, backend fills it)
  AlertDetailPage renders <ActionButtons context="Alert" targetId={alert.id}/>
        → GET /api/v1/integrations/actions?context=Alert
        → backend: for each CONFIGURED integration of the tenant,
                   take its registered IIntegrationAction whose Contexts include Alert,
                   project each to a descriptor { integrationId, actionId, label, icon, hasInput, hasDraft }
        → frontend renders one <Button> per descriptor (into PageHeader's actions slot)

DRAFT  [new, optional per action]                     (pre-fill the dialog from context)
  user clicks "Create Jira ticket"
        → GET /api/v1/integrations/{integrationId}/actions/create-issue/draft?context=Alert&targetId=42
        → JiraCreateIssueAction.BuildDraftAsync(ctx) loads Alert #42, returns
          { title: "[Piro] checkout-service — HTTP check failing",
            description: "**Alert #42** fired at …\n\n- Severity: Critical\n- [View in Piro](…)" }
        → dialog opens, rendered by DynamicConfigForm from create-issue's reflected InputType schema,
          pre-populated with the draft values (title input + markdown textarea)

EXECUTE  [new]
  user confirms (optionally edits title/description)
        → POST /api/v1/integrations/{integrationId}/actions/create-issue/execute
             { context: "Alert", targetId: 42, input: { title: "...", description: "...(md)..." } }
        → backend resolves IIntegrationAction by (integration.Type, "create-issue")
        → deserializes `input` into JiraCreateIssueInput, VALIDATES it (same DataAnnotations as the dialog)
        → JiraCreateIssueAction.ExecuteAsync: gets a fresh bearer token from RFC 0004's token
          provider (refreshes if near expiry, persists rotation), reads projectKey/issueType/cloudId
          from the integration's OAuth-mapping, converts description markdown → ADF,
          POSTs https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue
          returns ActionResult { externalId: "OPS-123", url: ".../browse/OPS-123", label: "OPS-123" }
        → persists an ExternalReference row (Alert, 42, integrationId, "OPS-123", url, "OPS-123")  [new §4.5]
        → 200 → frontend invalidates the alert query, toast.success, the link now shows on the page

RE-VISIT
  next render of <ActionButtons> also GETs existing ExternalReferences for (Alert, 42)
        → the page shows "🔗 OPS-123" linking out to Jira; the button can offer "create another" but
          the operator sees a ticket already exists (no accidental duplicate)
```

### 4.2 The action contract — `IIntegrationAction`

A new Application-layer interface, sibling to `INotificationDispatcher`, following the same discriminator-property shape (`INotificationDispatcher.Type`, `INotificationDispatcher.cs:10`):

```csharp
namespace Piro.Application.Interfaces;

/// <summary>
/// A user-initiated action an integration contributes to the UI — surfaced as a button on an
/// Alert / Incident / Maintenance, executed on demand with human-supplied input. Contrast with
/// INotificationDispatcher (headless, per-person) and RFC 0004's IAlertEventDispatcher (headless,
/// automatic outbound events).
/// </summary>
public interface IIntegrationAction
{
    /// <summary>Which integration type this action belongs to (resolution discriminator).</summary>
    IntegrationType Type { get; }

    /// <summary>Stable id, unique within a Type. Part of the route and the persisted reference.</summary>
    string ActionId { get; }              // e.g. "create-issue"

    /// <summary>Human label + icon for the button (icon uses the same Iconify convention as the manifest).</summary>
    string Label { get; }                 // "Create Jira ticket"
    string? IconifyIcon { get; }          // "logos:jira"

    /// <summary>Object kinds this action applies to — drives which pages show the button (§4.3).</summary>
    IReadOnlyList<ActionContext> Contexts { get; }

    /// <summary>The DataAnnotations-annotated input class, or null for a no-input action.
    /// Its ConfigSchemaBuilder.For(InputType) schema renders the dialog AND validates the POST.</summary>
    Type? InputType { get; }

    /// <summary>Whether this action can pre-fill its input from the target object (§4.6).</summary>
    bool SupportsDraft { get; }

    /// <summary>Whether the integration is ready to run this action (Jira: OAuth-connected, RFC 0004).
    /// A false result drops the action from discovery entirely — no button is shown (§4.4).
    /// Non-OAuth actions return true.</summary>
    Task<bool> IsReadyAsync(Integration integration, CancellationToken ct = default);

    /// <summary>Pre-fill the input for a specific target (only called when SupportsDraft).</summary>
    Task<object?> BuildDraftAsync(ActionExecutionContext ctx, CancellationToken ct = default);

    /// <summary>Perform the action and return the external reference it created.</summary>
    Task<ActionResult> ExecuteAsync(ActionExecutionContext ctx, CancellationToken ct = default);
}

public enum ActionContext { Alert, Incident, Maintenance }

/// <summary>Everything a handler needs: the resolved integration (for ConfigJson), the target, the input.</summary>
public sealed record ActionExecutionContext(
    Integration Integration,
    ActionContext Context,
    int TargetId,                 // Alert.Id / Incident.Id / Maintenance.Id — all int (§5)
    object? Input);               // deserialized into the action's InputType; null for draft calls

/// <summary>The outbound reference an action produced, persisted as an ExternalReference (§4.5).</summary>
public sealed record ActionResult(string ExternalId, string Url, string Label);
```

Key deliberate choices:

- **`Type` is `IntegrationType` in Phase 1**, matching the existing dispatcher pattern exactly. §4.10 explains why the *registry* that resolves actions is introduced now as a seam, and why a later `providerKey` string is the only change needed to admit plugins — but the enum stays the discriminator until then, so this RFC does not widen it speculatively.
- **`InputType` is nullable** — a no-input action (a future "resync status" button) skips the dialog and executes on click.
- **`BuildDraftAsync` returns `object?`** shaped like `InputType`, serialized with the same options as `execute` so the dialog round-trips cleanly.

### 4.3 Placement: how the backend "knows where the button goes" (it doesn't)

The backend classifies; the frontend places. An action declares the object kinds it applies to via `Contexts`; the Jira create-issue action declares `[Alert, Incident, Maintenance]`. That is the entire "modeling" of placement on the server. It never expresses *where on a page* — only *for what kind of object*.

Each detail page owns the *where* by dropping the generic container at the chosen spot. For alerts, that spot already exists: `AlertDetailPage` renders a `PageHeader` with an `actions` slot (`apps/admin/src/features/alerts/pages/AlertDetailPage.tsx:112-140`, currently holding the Acknowledge button). `<ActionButtons context="Alert" targetId={alert.id}/>` goes there. Incident and maintenance detail pages get the same container in their own header (§4.8).

If sub-placement is ever needed (a button in a table row vs. the header), it is expressed by adding a context value (e.g. `AlertRow`) — not by teaching the server about pixels. Out of scope now; noted so the enum's role is clear.

### 4.4 API surface — three endpoints on the existing controller

All three hang off `IntegrationsController` (`src/Piro.Api/Controllers/IntegrationsController.cs`), inheriting its `[Route("api/v1/integrations")]` (`:14`) and its `[Authorize(Roles = "Owner,Admin")]` gate (`:12`) — no new controller, no new auth surface.

| Verb + route | Purpose | Returns |
|---|---|---|
| `GET api/v1/integrations/actions?context={ctx}` | discovery — which buttons to render | `IReadOnlyList<IntegrationActionDescriptorDto>` |
| `GET api/v1/integrations/{id:guid}/actions/{actionId}/draft?context={ctx}&targetId={n}` | pre-fill dialog | `object` (shaped like the action's `InputType`) |
| `POST api/v1/integrations/{id:guid}/actions/{actionId}/execute` | run the action | `IntegrationActionResultDto` |

Descriptor DTO (new, `src/Piro.Application/DTOs/`), the wire shape the frontend renders from:

```csharp
public record IntegrationActionDescriptorDto(
    Guid IntegrationId,            // which configured integration offers it
    string IntegrationLabel,      // "Jira" / the integration's Name
    string ActionId,              // "create-issue"
    string Label,                 // "Create Jira ticket"
    string? IconifyIcon,          // "logos:jira"
    bool HasInput,                // InputType != null → open a dialog
    bool SupportsDraft,           // pre-fetch a draft before showing the dialog
    IReadOnlyList<ConfigFieldSchemaDto> InputSchema);   // ConfigSchemaBuilder.For(InputType); [] if none
    // No "requires connection" field: a not-ready action is absent from discovery (§4.4), never disabled.
```

`InputSchema` reuses the exact same `ConfigFieldSchemaDto` (`src/Piro.Application/DTOs/ConfigFieldSchemaDto.cs:11-48`) and `ConfigSchemaBuilder.For(Type)` (`src/Piro.Application/Extensions/ConfigSchemaBuilder.cs:31-32`, statically cached) that the integration and check config forms already use. No new schema machinery.

**Discovery logic** (a new method on `IntegrationAppService`, `src/Piro.Application/Services/IntegrationAppService.cs`): load the tenant's configured `Integration` rows (existing `IIntegrationRepository.GetAllAsync`), and for each, look up its registered `IIntegrationAction`s whose `Contexts.Contains(ctx)`, projecting each `(integration, action)` pair to a descriptor. Actions are injected as `IEnumerable<IIntegrationAction>` and indexed by `Type` exactly like `EscalationCheckerService` does with dispatchers (`EscalationCheckerService.cs:25-26`).

An action is only surfaced for an integration that is **ready to run it** — and if it isn't, **the button simply doesn't appear**. Discovery already filters to configured integrations, so if no Jira integration exists, there is no button. On top of that, a Jira integration that exists but isn't *OAuth-connected* (no stored token, RFC 0004 §4.3) also yields no button: readiness is asked of the action (`IIntegrationAction.IsReadyAsync(Integration)`, which Jira answers by checking for a live token; non-OAuth actions default to ready), and a not-ready action is **dropped from the discovery result entirely** — the frontend never receives a descriptor for it, so there is nothing to render. Connecting Jira happens on the integration's own page (§4.8), not from an alert; the alert surface only ever shows buttons that will actually work. (This means the descriptor needs no "requires connection" state at all — an un-connected action is absent, not disabled.)

**Execution logic** (new method on the same service):
1. Load the `Integration` by id (existing `IIntegrationRepository.GetByIdAsync`, `IIntegrationRepository.cs:8`; throws `NotFoundException` if absent — existing pattern, `IntegrationAppService.cs:28-30`).
2. Resolve the `IIntegrationAction` by `(integration.Type, actionId)`; 404 if none.
3. Deserialize `input` into `action.InputType`, then validate with `Validator.TryValidateObject` against its DataAnnotations. Invalid → `400` with the field errors. (This is the "one source of truth" guarantee — the very annotations that drove the dialog now gate the payload.)
4. Build `ActionExecutionContext` and call `ExecuteAsync`.
5. Persist the returned `ActionResult` as an `ExternalReference` (§4.5), then return it to the client.

### 4.5 Persistence — `ExternalReference` (new entity)

The outbound link Piro currently cannot store. One polymorphic table rather than columns-per-entity, because the alternative forces a schema migration for every (entity × integration) pair and can't represent "an alert with both a Jira and a future Linear ticket." This mirrors RFC 0004's reasoning for a single `ServiceIntegrationMapping` blob over typed per-provider columns (RFC 0004 §7).

```csharp
namespace Piro.Domain.Entities;

public class ExternalReference
{
    public int Id { get; set; }                         // int PK — matches Alert/Incident/Maintenance (§5)
    public ActionContext TargetType { get; set; }       // Alert / Incident / Maintenance
    public int TargetId { get; set; }                   // the local object's int Id
    public Guid IntegrationId { get; set; }             // FK → Integration (Guid PK, Integration.cs:7)
    public Integration Integration { get; set; } = null!;
    public string ActionId { get; set; } = "";          // which action created it ("create-issue")
    public string ExternalId { get; set; } = "";        // "OPS-123"
    public string Url { get; set; } = "";               // deep link into Jira
    public string Label { get; set; } = "";             // display text ("OPS-123")
    public DateTime CreatedAt { get; set; }             // auto-stamped by PiroDbContext convention
    public DateTime UpdatedAt { get; set; }
}
```

- **No hard FK to the target.** `TargetType` + `TargetId` is a polymorphic pointer, deliberately *not* a foreign key — the same pragmatic choice the codebase already makes for cross-cutting references (there is no base entity to FK against; the three targets have unrelated `int` PKs). Cleanup on target deletion is handled in the delete path (§8), not by cascade.
- **FK to `Integration` is real** (`Guid`, `OnDelete Cascade`) — if the integration is deleted, its references go with it, consistent with how `Integration`'s other relations behave (`IntegrationConfiguration.cs:21`).
- **Multiple references per target are allowed** — the table has no uniqueness constraint on `(TargetType, TargetId)`; a target can accumulate a Jira ticket and, later, other providers'. A non-unique index on `(TargetType, TargetId)` serves the "list references for this object" read.
- **Audit stamps are automatic** — naming the fields `CreatedAt`/`UpdatedAt` opts into `PiroDbContext.SetAuditTimestamps()` (`PiroDbContext.cs:74-85`) with no extra wiring.
- EF config goes in `src/Piro.Infrastructure/Persistence/Configurations/ExternalReferenceConfiguration.cs` (auto-discovered via `ApplyConfigurationsFromAssembly`, `PiroDbContext.cs:59`); a `DbSet<ExternalReference>` is added to `PiroDbContext`; one migration into `src/Piro.Infrastructure/Migrations/`.

A read endpoint exposes a target's references to the page (so it can show "🔗 OPS-123"): `GET api/v1/integrations/references?context={ctx}&targetId={n}` → `IReadOnlyList<ExternalReferenceDto>`. Consumed by `<ActionButtons>` alongside the descriptor fetch.

### 4.6 The Jira consumer — `JiraCreateIssueAction`

The one implemented `IIntegrationAction`, in a new `src/Piro.Infrastructure/Actions/` folder (or alongside the dispatchers — placement settled at implementation). For the HTTP-call shape it follows the existing dispatcher template (`IHttpClientFactory` "piro-webhook" client, log non-2xx bodies, as `PushoverDispatcher`/`TwilioSmsDispatcher` do). For **authentication it does not touch `ConfigJson`** — it asks RFC 0004's token provider for a fresh bearer token keyed by `IntegrationId` (the generalization of `GcpTokenProvider`, RFC 0004 §4.3), which refreshes and persists the rotated token transparently. The Jira-specific connection data an OAuth ticket needs — the `cloudId` (discovered at connect time), the target `projectKey`, and `issueType` — lives in the integration's OAuth mapping, not in a plaintext `JiraConfig` (§5).

```csharp
public sealed class JiraCreateIssueAction : IIntegrationAction
{
    public IntegrationType Type => IntegrationType.Jira;
    public string ActionId => "create-issue";
    public string Label => "Create Jira ticket";
    public string? IconifyIcon => "logos:jira";
    public IReadOnlyList<ActionContext> Contexts =>
        [ActionContext.Alert, ActionContext.Incident, ActionContext.Maintenance];
    public Type? InputType => typeof(JiraCreateIssueInput);
    public bool SupportsDraft => true;

    public Task<object?> BuildDraftAsync(ActionExecutionContext ctx, CancellationToken ct = default) { ... }
    public Task<ActionResult> ExecuteAsync(ActionExecutionContext ctx, CancellationToken ct = default) { ... }
}
```

Its input class — the single source of truth for the dialog and the payload, annotated with the same attributes as every config class (`ConfigFieldAttribute`, `MultilineFieldAttribute` → `ConfigFieldType.Multiline`, `ConfigSchemaBuilder.cs`):

```csharp
public sealed class JiraCreateIssueInput
{
    [Required]
    [ConfigField("Title", Placeholder = "Short summary of the ticket")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MultilineField]                    // renders as a markdown-capable textarea (§4.8)
    [ConfigField("Description", HelpText = "Markdown supported.")]
    public string Description { get; set; } = string.Empty;
}
```

`ProjectKey` and `IssueType` are **not** here — they are connection-level settings the admin picks once (a per-ticket "which project" prompt would be friction and would drift from the connection). They live in the integration's OAuth mapping, not the dialog. `Title`/`Description` are the only per-ticket human decisions.

**`BuildDraftAsync`** loads the target by `(Context, TargetId)` and returns a pre-filled `JiraCreateIssueInput` — title from the alert's service/check + status, description as a markdown summary with a deep link back to the Piro alert page **and the Piro user who clicked** (the service-identity model means the Jira author is the shared connection, so the acting operator is recorded in the body — §2). For an `Incident`/`Maintenance` target it summarizes that object instead. (Loading uses the existing repositories for each entity; the handler switches on `ctx.Context`.)

**`ExecuteAsync`** gets a bearer token from RFC 0004's token provider (keyed by `IntegrationId`; refreshed if near expiry), converts `Description` markdown → **ADF** (Atlassian Document Format, the JSON body shape Jira Cloud REST v3 requires — it does not accept raw markdown), and `POST`s to the **Atlassian OAuth gateway**: `https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue` with `Authorization: Bearer <token>`, body `{ fields: { project: { key }, issuetype: { name }, summary, description: <ADF> } }`. Note this is **not** `{BaseUrl}/rest/api/3/issue` — 3LO OAuth routes through `api.atlassian.com/ex/jira/{cloudId}`, which is why the `cloudId` is discovered and stored at connect time (§4.1 CONNECT). On success it returns `ActionResult("OPS-123", "{siteUrl}/browse/OPS-123", "OPS-123")` (the human-facing `siteUrl` also captured at connect time). On a Jira 4xx it throws a mapped `ExternalServiceException` so the endpoint returns a clean error and no `ExternalReference` is written; a `401` after refresh means the connection was revoked in Jira — surfaced as "reconnect Jira," not a generic error. The markdown→ADF converter is a small, well-scoped Infrastructure helper (headings, bold/italic, lists, links, code — the subset a Piro-generated summary uses); unsupported markdown degrades to a paragraph, never fails the call.

Manifest change: flip Jira's capability from `IntegrationCapability.None` to a new `IntegrationCapability.ProvidesActions = 1 << 6` (`1 << 5` is claimed by RFC 0004's `SendsAlertEvents`; this RFC takes the next bit) in `src/Piro.Domain/Enums/IntegrationCapability.cs`, documented *"Has one or more registered IIntegrationActions."* This is the same honest-manifest discipline RFC 0004 §4.8 applies with `SendsAlertEvents` — the flag states a fact about registration, and a test asserts it can't lie (§4.8).

### 4.7 Frontend — `<ActionButtons>` and the action dialog

New, in a shared location (`apps/admin/src/components/integration-actions/`), used by all three detail pages:

- **`ActionButtons.tsx`** — props `{ context: ActionContext; targetId: number }`. On mount it fetches the descriptors (`GET …/actions?context=…`) and the existing references (`GET …/references?context=…&targetId=…`) via tanstack-query (the app's data-fetching standard, already used throughout `AlertDetailPage`). Renders one shadcn `<Button>` per descriptor (label + Iconify icon), plus a compact list of existing references as outbound links ("🔗 OPS-123 ↗"). Clicking a descriptor with `hasInput === false` executes immediately; with input, it opens `ActionDialog`.
- **`ActionDialog.tsx`** — a shadcn `Dialog` (the same primitive `AlertDetailPage` already uses for "Attach to incident," `AlertDetailPage.tsx:349-383`). If `supportsDraft`, it first fetches the draft and seeds the values; then it renders the **shared** `DynamicConfigForm` (`apps/admin/src/components/config-form/DynamicConfigForm.tsx:24`) with `schema={descriptor.inputSchema}`, `values`, `onChange`, and an `errors` map — following the **checks** precedent (`SchemaConfigSection.tsx:21,45`), *not* the older integrations config form, which uses a separate flat-`Record<string,string>` stack and its own `DynamicConfigField`. The shared form already handles every `ConfigFieldType` including `Multiline` and `visibleWhen` conditionals — no new field rendering is written. On confirm it `POST`s `…/execute`, then:
  - `onSuccess`: invalidate the target's query (`qc.invalidateQueries`, as `AlertDetailPage.tsx:79-80` already does) so the new reference appears, and `toast.success(...)`.
  - `onError`: `toast.error(apiErrorMessage(err, …))` — reusing the `apiErrorMessage` helper pattern in `AlertDetailPage.tsx:35-37`.

**Markdown field.** A `MarkdownEditor` (TipTap WYSIWYG that emits markdown) already exists — `apps/admin/src/components/MarkdownEditor.tsx:16`, props `{ value, onChange, placeholder? }`, used by the incident pages (`IncidentDetailPage.tsx:468`, `IncidentFormPage.tsx:162`). But the shared `FieldControl`'s `Multiline`/`Code` types currently render a plain `<Textarea>`, not that editor (`FieldControl.tsx`). Phase 1 ships the description as that plain markdown-capable textarea with the `HelpText` "Markdown supported" — sufficient and zero-new-dependency. Wiring the existing `MarkdownEditor` into the `Multiline` control (or adding a `Markdown` field type that routes to it) is a small, well-scoped enhancement, tracked but not blocking. Reusing the incident editor keeps the description UX consistent with how incident bodies are already authored.

**Toasts.** The admin currently mixes two toast libraries: `AlertDetailPage` uses `react-toastify` (`:5`), while the newer shadcn-integrated `sonner` (`@/components/ui/sonner`, mounted in `App.tsx:86`) is what recent config pages use. The action components standardize on **`sonner`** for success/error, matching the newer code — both `Toaster`s are already mounted, so either works at runtime; sonner is the forward-looking choice.

**Types**: the descriptor, reference, and result DTOs are consumed via the generated OpenAPI types (`apps/admin/src/lib/api-types.ts`), aliased from `components["schemas"][...]` in a new `apps/admin/src/lib/actions/integration-actions/index.ts`, per the established convention (AGENTS.md; the pattern in `lib/actions/*`). Regenerate with `pnpm run generate:api-types` after the DTOs land.

### 4.8 UI on all three detail pages

The container is placed once per page, in the page's own header/actions region:

- **Alert** — `AlertDetailPage.tsx:112` `PageHeader actions` slot (next to Acknowledge). `<ActionButtons context="Alert" targetId={alert.id}/>`.
- **Incident** — `apps/admin/src/features/incidents/pages/IncidentDetailPage.tsx`, its header actions region. `<ActionButtons context="Incident" targetId={incident.id}/>`.
- **Maintenance** — `apps/admin/src/features/maintenances/pages/MaintenanceDetailPage.tsx`, its header actions region. `<ActionButtons context="Maintenance" targetId={maintenance.id}/>`.

Each page also shows any existing references inline (the "🔗 OPS-123" links `<ActionButtons>` renders). The one change to the **integration create/edit form** (`IntegrationFormPage.tsx`): Jira's connection step gains a **"Connect Jira" (OAuth)** button in place of the plaintext `Email`/`ApiToken` fields, plus a project/issue-type picker shown after connection — this is RFC 0004's OAuth-connect UI applied to Jira (RFC 0004 §4.3/§4.5 define the connect flow and mapping UI; this RFC supplies Jira's scopes and the project/issue-type selection). No other page changes.

### 4.9 What does NOT change

- **`INotificationDispatcher`** and its five real implementations (Email/Telegram/Twilio/Ntfy + the obsolete stubs) — untouched. `IIntegrationAction` is a sibling interface, a different axis (user-initiated + UI vs. headless per-person). A single class could implement both later, but none does here.
- **`EscalationCheckerService`** and the escalation/paging path — untouched. Actions are invoked from the HTTP request, not from any background job, in Phase 1.
- **`ConfigSchemaBuilder`, `ConfigFieldSchemaDto`, `DynamicConfigForm`, `FieldControl`** — reused verbatim to render action-input dialogs. No changes to the schema engine; action inputs are just another annotated class fed through `For(Type)`.
- **`Integration` entity, `IntegrationAppService` CRUD** — the entity and its CRUD are unchanged. Jira's credentials, however, **stop** flowing through `ConfigJson`/`MaskSecrets`: they move to RFC 0004's encrypted OAuth token store (§5). The masking machinery (`IntegrationExtensions.MaskSecrets`, `IntegrationExtensions.cs:48`) is untouched and still serves other integrations; it simply no longer has a Jira `ApiToken` to mask.
- **`Alert.ExternalId` / `Alert.SourceUrl`** (`Alert.cs:107,114`) and their `(Source, ExternalId)` dedup index — untouched. Those remain inbound-only; outbound links live in the new `ExternalReference` table, deliberately separate so the inbound dedup semantics aren't overloaded.
- **RFC 0004's OAuth framework, `IAlertEventDispatcher`, `ServiceIntegrationMapping`** — *consumed, not modified*. This RFC registers Jira as an OAuth provider within 0004's framework and (Phase 2) calls its dispatcher hook; it changes none of 0004's own code. If 0004 isn't implemented, this RFC can't ship (§4.10, §6).
- **`IntegrationType` enum values and ordinals** — unchanged; only Jira's manifest *capability flag* flips (a metadata edit, no migration).

### 4.10 Relationship to RFC 0004 (hard dependency), and the plugin door

**RFC 0004 is a hard prerequisite of this RFC — from Phase 1, not just Phase 2.** The two RFCs describe different *behavior axes* of the same `Integration`, but this RFC's axis is built *on top of* 0004's OAuth framework and cannot exist without it. Two things 0004 provides that Phase 1 here strictly requires:

1. **Secure credentials to call Jira** — `IOAuthClient` (connect/refresh) + the encrypted, refreshing token store (RFC 0004 §4.3). This RFC deliberately does not authenticate any other way (§7 rejects Basic auth). No token store ⇒ no way to call Jira ⇒ no action.
2. **The OAuth-provider registration shape** — the per-provider app-config precedent (`OidcProviderConfig`-style, RFC 0004 §4.3) that this RFC populates with Jira's authorize/token endpoints and scopes.

The two axes still compose cleanly on one `Integration`:

| | RFC 0004 — `IAlertEventDispatcher` | This RFC — `IIntegrationAction` |
|---|---|---|
| Trigger | automatic, on alert firing | user clicks a button |
| UI | none (headless) | a button + a dialog on a detail page |
| Input | none (derived from context) | human-supplied, schema-validated |
| Auth | 0004's OAuth token store | **same** 0004 OAuth token store |
| Direction | outbound event (trigger/resolve) | outbound create, linked back |
| Hangs off | `Integration` | `Integration` (+ 0004's OAuth connection) |

That shared OAuth foundation is also what makes this RFC's **Phase 2 (auto-create)** a thin addition: "auto-create a Jira ticket when a matching alert fires" reuses RFC 0004's firing hook (`AlertLifecycleService.RecordOccurrenceAsync`, RFC 0004 §4.6) as the *caller* that invokes the *same* `JiraCreateIssueAction.ExecuteAsync` — same handler, same token, no headless copy. The criterion ("which severities auto-create") is stored on the integration's OAuth mapping (Phase 2), and the reference table already prevents duplicates.

**The plugin door.** Phase 1 resolves actions from DI directly. To keep hot-loaded plugins a *seam-swap* rather than a rewrite, two cheap decisions are made now:

1. **Resolve actions through an `IActionRegistry`, not a raw injected `IEnumerable<IIntegrationAction>`.** In Phase 1 the registry just returns the DI-registered actions. A future plugin host makes the registry *also* return actions contributed by loaded plugins — and the discovery endpoint, the executor, the frontend, and `ExternalReference` never change, because they ask the registry, not the container.
2. **Keep the discriminator abstractable to a string.** The contract is designed so `Type` (`IntegrationType`) can later become an additional `ProviderKey` string (`"jira"`, `"acme-custom"`) for actions that don't exist in the compiled enum. Phase 1 does **not** widen it — the enum stays the key — but nothing in the wire format, the frontend, or the table assumes the enum, so admitting a string later touches only the registry and resolution.

This RFC builds neither the registry-plus-plugins nor the string key beyond the `IActionRegistry` indirection; it commits only to *not foreclosing* them. Runtime plugins remain a separate future RFC.

## 5. Data / schema scope

New (migration):

- **`ExternalReference`** table (§4.5): `Id (int, PK)`, `TargetType (int enum)`, `TargetId (int)`, `IntegrationId (Guid, FK → Integration, OnDelete Cascade)`, `ActionId (string)`, `ExternalId (string)`, `Url (string)`, `Label (string)`, `CreatedAt`/`UpdatedAt (DateTime)`. Non-unique index on `(TargetType, TargetId)`. One `IEntityTypeConfiguration`, one `DbSet`, one migration.

Code-level (no migration):

- New `IntegrationCapability.ProvidesActions = 1 << 6` (`IntegrationCapability.cs`; `1 << 5` is RFC 0004's `SendsAlertEvents`); flip Jira's manifest capability (`IntegrationType.cs:23`) from `None` to `ProvidesActions`.
- New DTOs: `IntegrationActionDescriptorDto`, `ExternalReferenceDto`, `IntegrationActionResultDto` (Application layer).
- New interfaces/records: `IIntegrationAction`, `IActionRegistry`, `ActionContext` enum, `ActionExecutionContext`, `ActionResult`.
- New input class `JiraCreateIssueInput`; new handler `JiraCreateIssueAction`; markdown→ADF helper.

**Jira OAuth (from RFC 0004, referenced not owned here):** Jira's authorize/token endpoints + scopes registered as an OAuth provider in 0004's framework; the encrypted token store (0004's entity) holds Jira's access/refresh tokens; the connect-time-discovered `cloudId`, `siteUrl`, chosen `projectKey`, and `issueType` are stored in 0004's per-integration OAuth mapping. **`JiraConfig`'s `Email`/`ApiToken` fields are removed** (superseded by the OAuth connection); `BaseUrl` may remain only if still needed for display, otherwise also dropped. This is the one place the RFC touches an existing config class, and it *removes* plaintext-secret fields rather than adding any.

Explicitly **no changes** to: `Alert`, `Incident`, `Maintenance`, `Integration` entity, `AlertConfig`, `Check` schemas; `IntegrationType` ordinals; `ConfigFieldType`; any existing migration.

## 6. Phased plan

**Prerequisite (blocking): RFC 0004's OAuth framework must be implemented first** — `IOAuthClient`, the encrypted refreshing token store, and per-provider OAuth-app registration (RFC 0004 §4.3). Phase 1 below cannot start until these exist; the suggested implementation order in `docs/rfcs/README.md` reflects this (`0004 → 0012`).

1. **Action contract + Jira create-issue over OAuth, manual, all three contexts (this RFC's core).**
   - Jira OAuth wiring: register Jira as an OAuth provider in 0004's framework (scopes `write:jira-work read:jira-work offline_access`); connect flow + `cloudId`/`siteUrl` discovery; project/issue-type picker; remove `JiraConfig`'s plaintext credential fields (§5).
   - Backend: `IIntegrationAction` (incl. `IsReadyAsync`), `ActionContext`, `ActionExecutionContext`, `ActionResult`, `IActionRegistry` (DI-only impl); the three endpoints on `IntegrationsController`; discovery (readiness-filtered) + execution in `IntegrationAppService`; `ExternalReference` entity + migration + read endpoint; `JiraCreateIssueAction` (token via 0004's provider, POST to `api.atlassian.com/ex/jira/{cloudId}`) + `JiraCreateIssueInput` + markdown→ADF; capability flag + manifest flip; manifest-honesty test (§4.8-style).
   - Frontend: "Connect Jira" on the integration page; `<ActionButtons>` + `<ActionDialog>` reusing `DynamicConfigForm`; placement on Alert, Incident, Maintenance detail pages; generated-type aliases. Buttons appear only for OAuth-connected Jira integrations (§4.4).
   - **End of Phase 1 = an operator connects Jira via OAuth, then creates a Jira ticket from an alert (and incident, and maintenance), sees it linked, and can't silently double-create.** Verified against a real/sandbox Jira Cloud site.

2. **Auto-create by integration settings (the second axis, reusing 0004's dispatch hook).** Add auto-rule fields (which severities/services auto-create) to the Jira OAuth mapping. Invoke the *same* `JiraCreateIssueAction.ExecuteAsync` from RFC 0004's alert-firing hook (`AlertLifecycleService.RecordOccurrenceAsync`), guarded by the `ExternalReference` dedup. No new handler, same OAuth token.

3. **(Direction only, not committed here)** A second `IIntegrationAction` (e.g. "add comment" / "transition") to validate the contract's generality; and/or the `IActionRegistry` + `providerKey` extension that admits hot-loaded plugins (own RFC).

## 7. Alternatives considered

- **Hardcode a "Create Jira ticket" button on the alert page.** Rejected — it's the exact coupling the user asked to avoid; a second integration (Linear, GitHub Issues) means editing every detail page again, and there's no generic place for the created-reference link. The action contract costs one interface + one endpoint trio and makes every future integration's button appear for free.
- **Columns per entity (`Alert.JiraIssueKey`, `Incident.JiraIssueKey`, …) instead of `ExternalReference`.** Rejected — doesn't scale to N integrations or to "two tickets on one object," and forces a schema migration for each new (entity × provider). The polymorphic table is the same trade RFC 0004 §7 makes for its mapping blob; type-safety is recovered at the read boundary, not the DB.
- **Model the action as another `INotificationDispatcher`.** Rejected — that interface is headless and per-person (`DispatchPersonalAsync(handle, …)`), has no notion of user-supplied input, a target object, or a UI descriptor, and returns `bool` not an external reference. Overloading it would distort both use cases.
- **Build the plugin runtime now (Option B from discussion).** Rejected for Phase 1 — contradicts the closed-enum architecture RFC 0003/0011 deliberately chose, and the wanted feature (Jira button) needs none of it. The `IActionRegistry` seam (§4.10) preserves the option at ~zero cost without paying for a loader/sandbox that has no consumer yet.
- **Put `projectKey`/`issueType` in the action dialog instead of the connection.** Rejected — they're connection-level settings an admin sets once, not per-ticket decisions; asking for them on every ticket is friction and they'd drift from the connection. The dialog asks only for the human decision (title + description).
- **Basic auth (email + API token) for Phase 1, OAuth later.** Rejected — this was the original draft's approach and the user explicitly chose OAuth for security. A static API token would live as plaintext in `ConfigJson` (`Integration.cs:12`, no encryption at rest), the exact anti-pattern RFC 0004 §3 calls out; and shipping Basic-auth-now-OAuth-later means writing the Jira HTTP path twice (different endpoint: `{baseUrl}/rest/api/3` vs. `api.atlassian.com/ex/jira/{cloudId}`) and a migration for existing integrations. Depending on 0004 from the start costs a sequencing constraint but no throwaway code, and every ticket is created with an encrypted, revocable, refreshable credential.
- **Per-user OAuth identity (each operator connects their own Jira).** Rejected for now — it would make the Jira author the real acting person (better audit), but requires a per-user token store and forces every operator to complete an OAuth connect before they can use the button; RFC 0004 models the token per `Integration`, not per user. The service-identity model (one connection per integration, operator recorded in the ticket body) matches how Piro models integrations today and is far less scope. Revisitable if per-user attribution becomes a hard requirement.
- **Draft as a client-side template instead of a `BuildDraftAsync` server call.** Rejected — the server already has the target object loaded and authoritative; templating the title/description client-side would duplicate alert/incident field access in the frontend and couple it to entity shapes. `SupportsDraft` keeps that logic in the handler, next to the entity repositories.

## 8. Risks

- **Duplicate tickets under double-click / retry.** The `ExternalReference` dedup is checked and shown, but Phase 1 doesn't *hard-block* a deliberate second create (multiple references are allowed by design). Mitigation: the UI surfaces existing references before offering "create another," and the execute path is idempotent only at the display level, not the DB level. A per-`(TargetType, TargetId, IntegrationId, ActionId)` uniqueness guard is an option if accidental duplicates prove real — deferred rather than assumed.
- **Orphaned references when a target is deleted.** `ExternalReference` has no cascade from the polymorphic target (there's no FK). If an Alert/Incident/Maintenance is deleted, its references must be cleaned in that entity's delete path, or they dangle. Mitigation: add reference cleanup to each target's delete service method; a periodic sweep is a fallback. (The `Integration` FK *does* cascade, so deleting the integration is safe.)
- **Markdown→ADF conversion is lossy / a maintenance surface.** Jira Cloud v3 rejects raw markdown, so a converter is unavoidable. Risk: an unsupported markdown construct produces a degraded or wrong ADF body. Mitigation: the converter targets the small markdown subset Piro's own draft generates and degrades unknown constructs to plain paragraphs (never throws); user-edited exotic markdown is best-effort. A Jira Server/DC deployment (wiki markup, not ADF) is out of scope — Phase 1 targets Jira Cloud REST v3 only, stated in the handler.
- **Action input schema drift across the network.** Mitigated by construction — the dialog schema and the execute-time validation are both `ConfigSchemaBuilder.For(InputType)` / DataAnnotations on the *same* class, so they cannot disagree. The only residual risk is a stale generated `api-types.ts`; the existing `pnpm run generate:api-types` discipline (AGENTS.md) covers it.
- **`IActionRegistry` abstraction with one caller.** A mild premature-abstraction bet, accepted because it is the single cheap seam that keeps the plugin door open (§4.10) and it collapses to a trivial DI passthrough in Phase 1 — the cost is one interface, the option it preserves is large.
- **An integration with many actions crowds a page header.** With one action (Jira create) this is moot; if action count grows, `<ActionButtons>` can collapse into a dropdown menu. Noted, not solved now.
- **Hard dependency on RFC 0004 gates delivery.** Because Phase 1 authenticates only via OAuth, this RFC cannot ship until 0004's framework lands — a real scheduling coupling (the `0004 → 0012` order in `docs/rfcs/README.md`). Accepted deliberately over Basic-auth-first (§7): the alternative trades a sequencing constraint for throwaway auth code and a plaintext-token security regression. Mitigation if 0004 slips: the action *contract, `ExternalReference`, and frontend* (everything except the Jira handler's auth) have no 0004 dependency and could be built and tested against a stub token provider, so most of Phase 1 isn't actually blocked — only the live Jira call is.
- **Revoked or expired Jira OAuth connection.** A refresh token can be revoked in Jira, or the grant expires; a stored connection can silently go dead. Mitigation: `IsReadyAsync` (§4.4) drops the button when no live token resolves, and a `401` after a refresh attempt (§4.6) surfaces "reconnect Jira" rather than a generic failure — the operator is pointed at the fix, and no `ExternalReference` is written for a failed create.
