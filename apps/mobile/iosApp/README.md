# Piro on-call — iOS app

Native **SwiftUI** app that consumes the shared **Kotlin Multiplatform** module (`../shared`) for all
business logic: the API client, wire models, auth token storage (Keychain), and refresh-on-401. The UI
is 100% native SwiftUI — the same "shared logic, native UI per platform" split the Android app uses
(native Jetpack Compose there, native SwiftUI here).

## Architecture

```
SwiftUI views + ObservableObject view models   (this module, apps/mobile/iosApp)
        │  import Shared
        ▼
Shared.framework (KMP)                          (apps/mobile/shared, iosArm64 / iosSimulatorArm64)
  ├─ PiroApiClient        — Ktor Darwin engine, bearer + refresh-on-401
  ├─ model/*              — @Serializable wire models mirroring the API DTOs
  └─ KeychainTokenStorage — Security-framework token storage
        │  HTTP
        ▼
Piro API (ASP.NET Core)
```

Feature parity with the Android app: **Login** (email/password + SSO), **On-call** home, **Alerts**
list, **Alert detail + Acknowledge**, **Settings** (sign out), APNs device registration, and
`piro://alert/{id}` deep links.

## Requirements

- **Xcode 16.4+** (built against the iOS 18.5 SDK).
- **XcodeGen** (`brew install xcodegen`) — only to regenerate the project from `project.yml`.
- A JDK for the Gradle build phase. If `JAVA_HOME` isn't set, the pre-build script falls back to Android
  Studio's bundled JBR (`/Applications/Android Studio.app/Contents/jbr/Contents/Home`).

## Building & running

The `.xcodeproj` is generated from `project.yml` but committed, so you can just open it:

```bash
open apps/mobile/iosApp/Piro.xcodeproj    # then ⌘R on an iPhone simulator
```

A pre-build script phase runs `./gradlew :shared:embedAndSignAppleFrameworkForXcode`, which builds and
embeds `Shared.framework` for the SDK/arch/configuration Xcode is compiling — no manual framework step.

From the command line:

```bash
cd apps/mobile/iosApp
xcodegen generate      # only if you changed project.yml or added/removed files
xcodebuild -project Piro.xcodeproj -scheme Piro -configuration Debug \
  -destination 'platform=iOS Simulator,name=iPhone 17,OS=26.5' \
  -derivedDataPath build CODE_SIGN_IDENTITY="-" build
```

> **Sign the build** (Xcode does this automatically; the CLI needs `CODE_SIGN_IDENTITY="-"` for an
> ad-hoc simulator signature). Do **not** pass `CODE_SIGNING_ALLOWED=NO` — an unsigned app has no
> keychain access group, so `SecItemAdd` fails and the login session never persists.

### Pointing at a server (self-hosted)

Piro is self-hosted, so the app has no built-in server. The user enters their **Piro server URL** on the
login screen; it's normalized (scheme defaulted to `https://`, trailing slash stripped) and persisted in
`UserDefaults` (`ServerStore`), and the shared `PiroApiClient` is rebuilt to target it. Debug builds
prefill the field with `http://localhost:5117` for local development (`AppConfig.defaultServerURL`);
release builds start empty. On the iOS Simulator `localhost` resolves to the host Mac, so a locally-run
Piro API is reachable directly — but it must actually be running (a connection-refused error means
nothing is listening on that port, not a networking problem).

## Liquid Glass

Liquid Glass (`.glassEffect(...)`) requires the **iOS 26 SDK (Xcode 26)**. This project builds against
the iOS 18 SDK, so the app approximates the look with `Material` (`.ultraThinMaterial`). The effect is
isolated in **`Piro/Support/GlassCard.swift`** — adopting real Liquid Glass later is a one-file change
(the `if #available(iOS 26, *)` branch is documented inline). To migrate: install Xcode 26, bump the
deployment target in `project.yml`, and switch `GlassBackground` to `.glassEffect`.

## Push notifications

`PushManager` requests notification authorization, registers with APNs, and associates the device token
with the signed-in user via `POST /api/v1/devices` (platform `Ios`). Live delivery needs an Apple
Developer team + an APNs key configured server-side (the analogue of Android's `google-services.json`);
without it, login and the rest of the app work unchanged, and the Simulator simply won't receive pages.

## Project layout

| Path | Purpose |
|---|---|
| `Piro/PiroApp.swift` | `@main` app + `AppDelegate` (APNs token) + login/shell switch + deep links |
| `Piro/ServiceLocator.swift` | Single `PiroApiClient` + `KeychainTokenStorage` |
| `Piro/Auth/` | `SessionViewModel`, `LoginView`, `SSOAuthenticator` |
| `Piro/Main/` | `RootView` (tab shell), `MainTab` |
| `Piro/OnCall/` | `OnCallView` |
| `Piro/Alerts/` | Alerts list + detail, their view models, `AlertCardView` |
| `Piro/Settings/` | `SettingsView`, `PlaceholderView` |
| `Piro/Push/` | `PushManager` (APNs + device registration) |
| `Piro/Theme/` | `PiroColors`, `PiroFlame` (SVG-path brand mark) |
| `Piro/Support/` | `GlassCard`, `DeepLinkRouter`, date & error helpers |
