# RFC 0017 — Push Relay Server

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-25

## 1. Problem

Piro ships a native on-call mobile app (KMP + Android + iOS). The MobilePush channel already sends real pushes: `FcmPushTransport` (`src/Piro.Integrations.MobilePush/Transport/FcmPushTransport.cs`) sends to Android through FCM v1 with a service-account key, and `ApnsPushTransport` (`src/Piro.Integrations.MobilePush/Transport/ApnsPushTransport.cs`) sends to iOS through APNs with a `.p8` signing key. Both read their credentials from `MobilePushConfig` (`src/Piro.Integrations.MobilePush/MobilePushConfig.cs`), encrypted at rest in the integration's `ConfigJson`.

This works only for a self-hoster who owns both the mobile-provider credentials **and** the app build those credentials were minted for. It breaks for the case Piro actually wants to support: a public app on the App Store / Play Store, installed by anyone, pointed at their own self-hosted Piro server.

The reason is structural, not a config gap:

- An FCM registration token minted by an app is only pushable by the Firebase project whose `sender_id` that app was compiled with. The published Android app is built with the publisher's `apps/mobile/androidApp/google-services.json` (its Firebase project and app package). A self-hoster's own Firebase service account **cannot** push to a token that app produced.
- APNs is the same shape with a softer wall: to push to a device token from the published iOS app you need the `.p8`, Team ID, and Bundle ID of the Apple Developer account that signed the published app. A self-hoster does not have those either.

So the published app produces tokens that only the app publisher's provider credentials can reach. A self-hosted server that receives such a token has no way to deliver to it. Handing every self-hoster the publisher's private FCM key and APNs `.p8` is a non-starter — it is the whole account's send authority, revocable only by rotating and breaking every deployment at once.

This is the same wall Bitwarden hit, and the resolution is the same shape: a **push relay** operated by the app publisher, which holds the provider credentials and forwards on behalf of self-hosted servers. Self-hosters keep pointing their app at their own server URL; only the final hop to FCM/APNs goes through the relay.

## 2. Non-goals

- **Not replacing the direct path.** A self-hoster who compiles their own app with their own Firebase/APNs credentials must keep sending straight to FCM/APNs with zero relay involvement. The relay is an *added* mode, not a replacement — the direct transports stay the default.
- **Not making the relay a message broker or store-and-forward queue.** It forwards one push, gets a provider result, returns it. It does not persist payloads, retry on the server's behalf, or hold state per notification. Delivery semantics (prune vs. retry) stay owned by `MobilePushNotificationDispatcher`.
- **Not a general BaaS.** The relay serves exactly one operation — "push this opaque blob to this token on this platform" — for Piro instances only. It is not a public push API.
- **Not re-architecting device registration or escalation.** `DeviceToken`, `DeviceRegistrationService`, and the single-`MobilePush`-preference-per-user model (`src/Piro.Application/Services/DeviceRegistrationService.cs`) are untouched except for one additive column.
- **Not defining relay operational hosting** (region, autoscaling, billing) — that is a deployment concern for whoever runs `push.piro.io`, out of scope for the design.

## 3. Design principle

The relay is a dumb, blind forwarder. It never learns what a notification says, and it plugs in at exactly one seam Piro already has — `IPushTransport` — so that nothing above that seam (the dispatcher, fan-out, severity mapping, failure handling) knows the relay exists. The choice between sending direct or via the relay is a **server-side config decision**, invisible to the mobile app: the app registers its platform token the same way in both modes.

## 4. Design

### 4.0 Where the relay plugs in

Today the dispatcher selects a transport purely by platform. `MobilePushNotificationDispatcher.HandleAsync` (`src/Piro.Integrations.MobilePush/MobilePushNotificationDispatcher.cs`) resolves the user's devices via `IDeviceTokenReader`, renders one neutral `PushMessage`, and fans out to each device by matching `IPushTransport.Platform` (`src/Piro.Integrations.MobilePush/Transport/IPushTransport.cs:14`). The transport is the only thing that touches a provider SDK; the dispatcher is pure orchestration.

That seam is exactly where the relay belongs. The relay is a **new `IPushTransport` implementation** — it is not a new dispatcher, not a parallel pipeline, and not a change to fan-out. Selection changes from "by platform" to "by (mode, platform)":

- **Direct mode** (default, today's behavior): Android → `FcmPushTransport`, iOS → `ApnsPushTransport`.
- **Relay mode**: Android **and** iOS → `RelayPushTransport`, which POSTs an opaque blob to `push.piro.io` and lets the relay pick FCM vs. APNs.

```mermaid
flowchart TD
  D["MobilePushNotificationDispatcher<br/>(unchanged: fan-out + failure handling)"]
  D -->|per device, by mode+platform| SEL{"Mode?"}

  subgraph direct["Direct mode (self-compiled app)"]
    FCM["FcmPushTransport"] --> F["FCM v1"]
    APNS["ApnsPushTransport"] --> A["APNs"]
  end

  subgraph relay["Relay mode (store app)"]
    R["RelayPushTransport"] -->|POST /v1/push<br/>token + ciphertext| PR["push.piro.io"]
    PR --> PRF["FCM v1 (publisher creds)"]
    PR --> PRA["APNs (publisher creds)"]
  end

  SEL -->|Direct, Android| FCM
  SEL -->|Direct, iOS| APNS
  SEL -->|Relay, any| R
```

Because `RelayPushTransport` returns the same `PushSendResult` enum (`Sent` / `Unregistered` / `TransientFailure` / `NotConfigured`, `IPushTransport.cs:43-56`), the dispatcher's existing prune-and-retry logic works verbatim — the relay just has to translate the provider's verdict into that enum in its response.

### 4.1 `MobilePushConfig` — add a push mode

`MobilePushConfig` today holds only the direct-send credentials. Add a mode selector plus the relay's connection fields:

```csharp
public enum MobilePushMode { Direct, Relay }

// new fields on MobilePushConfig
[ConfigField("Push delivery mode",
    HelpText = "Direct: this server sends to FCM/APNs itself (requires a self-compiled app with your own credentials). Relay: forward to a Piro push relay so the published app works without provider credentials.")]
public MobilePushMode Mode { get; set; } = MobilePushMode.Direct;

[ConfigField("Relay URL", Placeholder = "https://push.piro.io")]
public string? RelayUrl { get; set; }

[SecretField]
[ConfigField("Relay API key",
    HelpText = "Issued when this instance registers with the relay. Identifies and authorizes this server.")]
public string? RelayApiKey { get; set; }
```

Field visibility is conditional on `Mode` in the admin form (§4.5). The existing FCM/APNs fields stay and are only relevant in `Direct` mode; the relay fields are only relevant in `Relay` mode. `IsConfigured` on each transport already gates on its own credentials, so `RelayPushTransport.IsConfigured` returns true only when `Mode == Relay` and `RelayUrl`/`RelayApiKey` are present, while `FcmPushTransport`/`ApnsPushTransport.IsConfigured` additionally return false in `Relay` mode. A device whose platform transport is `NotConfigured` is left untouched, matching current behavior.

### 4.2 `RelayPushTransport` — the client seam

New file `src/Piro.Integrations.MobilePush/Transport/RelayPushTransport.cs`, registered in `src/Piro.Infrastructure/Integrations/IntegrationServiceExtensions.cs` alongside `FcmPushTransport`/`ApnsPushTransport` (via `AddHttpClient`, like the APNs transport). It implements `IPushTransport` for **both** platforms — unlike the direct transports it is not bound to one `DevicePushPlatform`, so the dispatcher's per-device platform match is satisfied by registering it for each platform value in relay mode, or by relaxing the selection to "relay transport wins when mode is Relay" (chosen in phasing, §6).

`SendAsync` builds the request body:

```json
{
  "platform": "Android" | "Ios",
  "token": "<opaque provider token>",
  "critical": true,
  "ciphertext": "<base64 sealed PushMessage>"
}
```

`ciphertext` is the encrypted `PushMessage` (§4.3). `critical` stays in the clear because the relay must know whether to send a high-priority/`interruption-level` push and, on iOS, a visible `alert` (§4.4) — but criticality is not sensitive, the alert text is. The relay's HTTP status and body map to `PushSendResult`: `200` → `Sent`, `410`/unregistered verdict → `Unregistered`, `5xx`/timeout → `TransientFailure`, misconfigured/unauthorized → surfaced as `TransientFailure` (so a revoked key doesn't silently prune every token).

### 4.3 End-to-end encryption — key lives on the device

The relay must never see notification content. That means the `PushMessage` (title, body, deep-link URL) is encrypted by the self-hosted Piro server and decrypted only on the device — the relay and even the relay operator (the app publisher) only ever handle `{platform, token, ciphertext}`.

For that to be true, the encryption key cannot come from the relay or from a publisher-held secret. It comes from the device itself. Each device generates an asymmetric keypair on first registration, keeps the private key in the platform keystore (Android Keystore / iOS Keychain), and sends the **public** key to its own Piro server as part of registration. The server seals each `PushMessage` to that device's public key before handing the ciphertext to `RelayPushTransport`.

`DeviceToken` (`src/Piro.Domain/Entities/DeviceToken.cs:18`) gains one nullable column:

```csharp
/// <summary>
/// The device's push-encryption public key (base64), generated on the device and uploaded at
/// registration. In relay mode the server seals each PushMessage to this key so the relay only
/// ever forwards opaque ciphertext. Null for devices registered before E2E or in direct mode.
/// </summary>
public string? PushPublicKey { get; set; }
```

Nullable is deliberate: direct-mode devices and pre-E2E rows don't need it, and the dispatcher falls back to sending an unencrypted `PushMessage` (direct mode only) or skips relay send with a `NotConfigured` result when relay mode is active but the device has no key yet (it will re-register and get one). This avoids a non-nullable column that would assume every existing device already did E2E enrollment.

`DeviceTokenInfo` (the neutral projection in `src/Piro.Integrations.Abstractions/IDeviceTokenReader.cs`, produced by `DeviceTokenReader`) gains the same field so the dispatcher can seal per device without reaching into `Piro.Domain`.

Sealing uses libsodium sealed boxes (X25519 + XSalsa20-Poly1305): the server needs only the public key to encrypt, and only the device's private key can open it — no shared secret, no key exchange round-trip, no publisher-held master key. The .NET side uses the same crypto primitive already available to the platform; the exact library is an implementation detail for the phase that builds it.

### 4.4 iOS: the data-only vs. visible-alert conflict, and the Notification Service Extension

iOS is where E2E collides with how APNs actually behaves, and this RFC resolves it explicitly rather than assuming a data-only push suffices.

A pure data-only APNs push (`content-available: 1`, no `alert`) is throttled and unreliable in the background — iOS does not guarantee it wakes the app, so it cannot be the transport for a critical on-call page. A reliable, DND-bypassing page needs a **visible `alert` payload** and, for full bypass, `interruption-level: critical` (which requires Apple's Critical Alerts entitlement on the published app). But a visible `alert` carries its title and body in the APNs payload — which, in relay mode, would pass through `push.piro.io` in the clear, defeating E2E.

The resolution is a **Notification Service Extension (NSE)** in the iOS app (`apps/mobile/iosApp`). The push is sent with:

- A visible `alert` block containing only **non-sensitive placeholder** text (e.g. "New alert") so iOS treats it as a real, wake-worthy notification and honors `interruption-level`.
- `mutable-content: 1`, which hands the notification to the NSE before display.
- The sealed `ciphertext` in a custom data key.

The NSE runs on-device, decrypts the ciphertext with the device private key from the Keychain, rewrites the notification's title/body/deep-link with the real content, and only then does iOS display it. The relay and its operator only ever saw the placeholder and the blob.

```mermaid
sequenceDiagram
  participant S as Self-hosted Piro
  participant R as push.piro.io
  participant P as APNs
  participant N as iOS NSE (on device)
  participant U as User
  S->>S: seal PushMessage to device pubkey
  S->>R: POST /v1/push {token, ciphertext, critical}
  R->>P: alert:"New alert" + mutable-content + ciphertext
  P->>N: deliver
  N->>N: decrypt ciphertext with Keychain key
  N->>U: display real title/body, honor critical
```

Android has no equivalent constraint: `PiroMessagingService` (`apps/mobile/androidApp/.../push/PiroMessagingService.kt`) already handles data-only messages and builds the notification (including the full-screen critical alarm) itself, so it decrypts the ciphertext in `onMessageReceived` and constructs the notification directly — no extension needed. `FcmPushTransport` already sends data-only on purpose, which is exactly the shape E2E wants.

### 4.5 Admin UI — mode selector on the MobilePush integration form

The MobilePush integration is configured in the admin panel (`apps/admin`) through the standard integration config form driven by `[ConfigField]`/`[SecretField]` metadata (RFC 0016). This RFC adds one control and a conditional section to that existing form — no new page:

- **Push delivery mode** — a select (`Direct` / `Relay`), bound to `MobilePushConfig.Mode`, default `Direct`.
- When **Direct** is selected: show the existing FCM service-account and APNs fields (today's form, unchanged). Hide the relay fields.
- When **Relay** is selected: show **Relay URL** (text, validated as an absolute `https://` URL) and **Relay API key** (secret input, masked like other `[SecretField]`s). Hide the FCM/APNs credential fields, since they are unused in relay mode.

The conditional show/hide keys off the `Mode` value and reuses the form's existing field-rendering; the only new capability the config form needs is field visibility conditioned on another field's value. If the current `[ConfigField]` renderer has no conditional-visibility mechanism, adding a lightweight `VisibleWhen` hint to the metadata is in scope for the UI phase (§6, Phase 4) and is defined here as: a field annotated `VisibleWhen(nameof(Mode), "Relay")` renders only when that sibling field holds that value.

No public status-page (`apps/web`) surface is involved.

### 4.6 Mobile app — close the Android/iOS server-URL asymmetry

Relay mode assumes the published app points at a self-hosted server. iOS already supports this: the user enters their server at login and it is persisted (`apps/mobile/iosApp/Piro/ServerStore.swift`, applied via `ServiceLocator.swift`). Android does **not** — its base URL is hardcoded in `BuildConfig.PIRO_API_BASE_URL` (`apps/mobile/androidApp/.../ServiceLocator.kt`, set in `build.gradle.kts`), so a published Android build can only ever reach one server.

This RFC closes that asymmetry: Android gains the same login-time server-URL entry and persistence as iOS. `PiroApiClient` (`apps/mobile/shared/.../api/PiroApiClient.kt`) already takes `baseUrl` as a constructor argument, so this is Android UI + persistence (mirroring `ServerStore`/`ServiceLocator`), not a networking change. Device registration (`POST /api/v1/devices`) is unchanged except that the request now also carries the device's `PushPublicKey`.

### 4.7 Relay registration — identity by API key

An instance authenticates to the relay with a per-instance API key. Onboarding is a one-time `POST /v1/register` to the relay that returns a `RelayApiKey`, which the admin stores in `MobilePushConfig.RelayApiKey`. The relay keeps a small store of instances — id, hashed key, rate-limit counters, revocation flag — and rejects unknown or revoked keys. Every `POST /v1/push` presents the key; the relay rate-limits per instance so one deployment can't exhaust the publisher's FCM/APNs quota. Anonymous/IP-only auth was rejected (§7) because it makes quota abuse trivial and gives no revocation handle.

### 4.8 What does NOT change

- **`MobilePushNotificationDispatcher`** — fan-out, severity/critical mapping, `Unregistered` pruning, and `FailureCount` handling are untouched. The relay is a transport behind the same `IPushTransport` seam it already uses.
- **`IPushTransport` / `PushMessage` / `PushSendResult`** contracts — the relay is a new implementation of the existing interface returning the existing result enum. `PushMessage` is unchanged; it is what gets sealed.
- **`FcmPushTransport` / `ApnsPushTransport`** — the direct path is byte-for-byte the same in `Direct` mode.
- **Device registration model** — `DeviceRegistrationService`, the single-`MobilePush`-preference-per-user rule, `IDeviceTokenReader`, and the `DevicesController` CRUD endpoints stay as-is aside from the additive `PushPublicKey` field.
- **Escalation, alert lifecycle, notification preferences** — none are aware of push mode; MobilePush is still one destination that fans out.

## 5. Data / schema scope

- **New enum**: `MobilePushMode { Direct, Relay }` in `Piro.Integrations.MobilePush` (not a domain enum — it is integration config, stored inside `ConfigJson`, so **no migration** for the mode itself).
- **New columns on `DeviceTokens`**: `PushPublicKey text NULL` — one EF migration under `src/Piro.Infrastructure/Migrations`. Nullable, no backfill; existing rows re-enroll on next registration.
- **`MobilePushConfig`**: three new properties (`Mode`, `RelayUrl`, `RelayApiKey`) serialized into the existing encrypted `ConfigJson`. **No schema change** — the integration config is a JSON blob.
- **Relay service store** (separate service, `push.piro.io`): its own instances table (id, hashed API key, rate-limit, revoked). Not part of the Piro application database.
- **No changes** to `Alert`, `AlertConfig`, `Incident`, `UserNotificationPreference`, `Integration`, or any check/worker table.

## 6. Phased plan

1. **Push-mode plumbing (server, no relay yet).** Add `MobilePushMode`, the three `MobilePushConfig` fields, and `RelayPushTransport` that POSTs to a configurable URL sending the **unencrypted** `PushMessage` as JSON. Update transport selection to be `(mode, platform)`-aware. Ships behind config; lets a self-hoster point at any HTTP forwarder for testing. No E2E yet.
2. **The relay service (`src/Piro.PushRelay`).** Minimal ASP.NET Core service: `POST /v1/register`, `POST /v1/push`, instance store, per-instance rate-limit. Reuses `FcmPushTransport`/`ApnsPushTransport` (it references `Piro.Integrations.MobilePush`) to do the actual FCM/APNs send with the publisher's credentials. Still forwarding cleartext at this point.
3. **End-to-end encryption.** Add `PushPublicKey` (migration + `DeviceTokenInfo`), device keypair generation + public-key upload (both apps), server-side sealing before `RelayPushTransport`, Android decrypt in `onMessageReceived`, and the iOS **Notification Service Extension** for decrypt-before-display. This is the phase that makes the relay blind; it is last because it needs both apps and the relay to already exist to test end-to-end.
4. **Admin UI + Android server-URL parity.** The `Mode` selector with conditional field visibility on the integration form (§4.5, incl. the `VisibleWhen` metadata hint if needed), and the Android login-time server-URL entry (§4.6) to match iOS.

Phases 1–2 are independently shippable and useful (a trusted-relay setup for a self-hoster who accepts the relay seeing content). Phase 3 is the one that delivers the privacy guarantee; Phase 4 is polish that makes the published-app story complete.

## 7. Alternatives considered

- **Hand each self-hoster the publisher's FCM/APNs credentials.** Rejected — it is the entire account's send authority, cannot be scoped per instance, and can only be revoked by rotating and breaking every deployment simultaneously.
- **Bring-your-own-Firebase, self-compiled app only (no relay at all).** This is exactly `Direct` mode, and it stays fully supported — but as the *only* option it forces every self-hoster to run their own Firebase project, Apple Developer account, and CI to rebuild and distribute the app. Too high a barrier to be the default; the relay exists so the published store app works out of the box.
- **Anonymous relay with per-IP rate-limiting.** Rejected — no way to revoke a bad actor, trivial to rotate IPs and exhaust the publisher's provider quota, and no per-instance accounting. Registration + API key (§4.7) gives identity, revocation, and per-instance limits for little extra onboarding cost.
- **Relay sees plaintext (no E2E).** Simpler — the relay just forwards title/body — but it makes the publisher a data processor for every self-hoster's alert content and a single point of leakage. Rejected as the end state; it exists only as the intermediate state of Phases 1–2 before E2E lands.
- **Data-only push on iOS instead of an NSE.** Rejected — background data-only pushes are throttled and not guaranteed to wake the app, so a critical page could silently not appear. The NSE is the only way to get a reliable, DND-bypassing visible alert whose real text is still decrypted on-device.
- **Server-held (not device-held) decryption key.** Rejected — if the self-hosted server could decrypt on the device's behalf there'd be a key the relay operator could in principle be compelled to reconstruct; a device-generated keypair with the private half never leaving the Keychain/Keystore is what makes "the relay cannot read notifications" a real guarantee rather than a policy.

## 8. Risks

- **iOS Critical Alerts entitlement.** `interruption-level: critical` requires an Apple-granted entitlement on the published app; without it, critical pages bypass DND less aggressively. This gates the published iOS build, not the design, but the NSE + entitlement work is real and Apple approval is not instant.
- **NSE payload-size and time budget.** APNs caps payload at 4 KB and the NSE has a short wall-clock budget to decrypt and rewrite. The sealed `PushMessage` is small (title/body/URL/ids), so this is comfortable — but a future richer payload must stay within 4 KB including the ciphertext overhead.
- **Lost device key = undecryptable pushes.** If a device's private key is lost (Keychain/Keystore wipe, restore-to-new-device without key migration) the server keeps sealing to a stale public key and the device can't open the result. Mitigation: treat key rotation as re-registration — the app regenerates and re-uploads on key-unavailable, and the old `DeviceToken` row is pruned like any dead token.
- **Relay as availability dependency.** In relay mode, `push.piro.io` being down means no mobile pages for every relay-mode instance at once — a shared failure domain the direct path doesn't have. Mitigation: the relay is deliberately minimal and stateless-per-push so it can be run redundantly; self-hosters who can't accept the dependency use `Direct` mode.
- **Placeholder-text leakage on iOS lock screen.** Between APNs delivery and NSE rewrite, the placeholder ("New alert") is what's technically in transit — acceptable because it carries no incident detail, but it means the relay-mode iOS notification can never encode anything sensitive in the pre-decrypt alert.
- **Quota exhaustion despite rate-limiting.** A large fleet of legitimate relay-mode instances still all draw on the publisher's single FCM/APNs quota. Per-instance limits bound abuse but not aggregate legitimate load; the publisher must size (and possibly tier) the relay's provider quota as adoption grows.
