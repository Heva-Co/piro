---
rfc: 18
title: "Multi-session refresh tokens (per-device sessions)"
status: proposal
created: 2026-07-26
depends-on: []
---

# RFC 0018 — Multi-session refresh tokens (per-device sessions)

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-26

## 1. Problem

A user can only have **one** live session at a time. Signing in on a second
device silently logs the first one out.

The refresh token is stored in a single per-user slot via ASP.NET Identity's
token store — `GenerateRefreshTokenAsync` writes
`SetAuthenticationTokenAsync(user, "Piro", "RefreshToken", token)`
(`src/Piro.Infrastructure/Auth/TokenService.cs:51`), and every new sign-in
overwrites that same slot. `ValidateRefreshTokenAsync`
(`TokenService.cs:56`) reads the one slot back
(`GetAuthenticationTokenAsync(user, "Piro", "RefreshToken")`, line 63). There is
exactly one refresh token per user, so:

- Device A signs in → slot holds token A.
- Device B signs in (same user) → slot now holds token B; token A is dead.
- Device A's access token expires (~60 min, `Auth:AccessTokenExpiryMinutes`).
  Its refresh-on-401 sends token A → not found → **401 "Session expired"**.

This surfaced directly in the mobile apps: logging into the Android app evicts
the iOS app's session (and vice-versa), even though the whole point of the
on-call app is that an engineer keeps it signed in on their phone. The mobile
`PiroApiClient` already does refresh-on-401 correctly — the failure is entirely
server-side: there is nothing to refresh against once the slot is overwritten.

A second, pre-existing issue lives in the same method: validation **scans every
active user** and compares the stored token one by one
(`TokenService.cs:58-65`), which the code itself flags as MVP-only
(*"For scale, store a hashed index — fine for MVP."*, line 59). This RFC fixes
that too, since it has to touch storage anyway.

Non-goals: changing the access-token format or lifetime; SSO/OIDC token handling
(those already end at the same `SignInResponse`); "log out everywhere" UI polish
beyond the endpoint needed to support it.

## 2. Proposal

Replace the single Identity token slot with a dedicated **`RefreshToken`** table
holding **many rows per user — one per active device/session**. Each sign-in
inserts a new row instead of overwriting; each device refreshes and rotates only
its own row; signing out revokes only that device's row.

### 2.1 Entity

New `Piro.Domain.Entities.RefreshToken`:

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `int` | FK → `AppUser`, cascade delete |
| `TokenHash` | `string` | SHA-256 of the raw token (never store the raw value) |
| `DeviceLabel` | `string?` | optional, for a future "your sessions" screen (e.g. "Pixel 8") |
| `ExpiresAt` | `DateTimeOffset` | absolute expiry (see §2.4) |
| `CreatedAt` | `DateTimeOffset` | |
| `RevokedAt` | `DateTimeOffset?` | set on sign-out / rotation; a non-null value means dead |

Unique index on `TokenHash`; index on `UserId`. Storing only the **hash** means a
DB leak doesn't expose usable tokens, and lookup is a single indexed query — this
is the "hashed index" the current code says it wants.

### 2.2 Issue / validate / rotate

In `TokenService`:

- **Generate** (on sign-in and OIDC callback): create a random 64-byte token as
  today, insert a `RefreshToken` row with its hash + expiry, return the raw token.
  Do **not** touch other rows — other devices stay valid.
- **Validate + rotate** (on `POST /auth/refresh`): hash the incoming token, look
  it up by `TokenHash` (single indexed query — no user scan), reject if missing,
  revoked, or past `ExpiresAt`; otherwise mark it revoked and insert a fresh row
  (rotation), returning the new pair. Rotation detection: if a **revoked** token
  is presented, that's a replay — revoke the whole user's chain and force
  re-login (standard refresh-token-reuse defense).
- **Sign-out** (`POST /auth/sign-out`): revoke only the row matching the caller's
  current refresh token, so other devices stay signed in. (Optional
  `POST /auth/sign-out-all` revokes every row for the user — "log out everywhere".)

The `AuthController` surface is unchanged (`sign-in`, `refresh`, `sign-out` keep
their shapes); `AuthService` swaps how it calls `TokenService`. No client change
is required — the mobile refresh-on-401 flow starts working across devices
automatically.

### 2.3 Migration

Add the `RefreshToken` table. Backfill is unnecessary: existing single-slot tokens
simply stop being honored, so all current sessions must sign in once after
deploy (acceptable, one-time). The old Identity token slot can be left in place
(ignored) or cleaned up in the same migration.

### 2.4 Expiry & cleanup

Refresh tokens today carry **no stored expiry** (validity ends only when
overwritten or on sign-out). This RFC gives them an absolute `ExpiresAt` (e.g. 30
days, configurable via `Auth:RefreshTokenExpiryDays`), refreshed by rotation. A
periodic Quartz job (Piro already uses Quartz for background jobs) prunes rows
that are revoked or past expiry, so the table doesn't grow unbounded.

## 3. Alternatives considered

- **Keep single-session (status quo).** Zero work, but the on-call app is unusable
  as designed — a person with a work phone + tablet, or who also opens the admin
  web, gets logged out constantly. Rejected for the mobile use case.
- **Bump refresh-token count to N slots in Identity's token store.** Hacky
  (encode a device id into the token name), still scans, still no expiry, no
  reuse-detection. Rejected.
- **Stateless refresh (JWT refresh tokens).** No DB row, but then you can't revoke
  a single device or detect reuse without a denylist — which reintroduces state.
  Rejected: on-call security wants explicit per-device revocation.

## 4. Impact

- **Backend, medium/contained:** one entity + migration, rewrite two
  `TokenService` methods, adjust `AuthService` sign-in/refresh/sign-out, add a
  prune job. No controller/API-shape change.
- **Clients, none required:** Android and iOS already refresh-on-401. They gain
  persistent per-device sessions for free. A future "active sessions" screen and
  "log out everywhere" become possible (the `DeviceLabel` + sign-out-all endpoint).
- **Security, net positive:** hashed-at-rest tokens, absolute expiry,
  refresh-reuse detection, and per-device revocation — all absent today.
- **One-time cost:** every existing session re-authenticates once after deploy.

## 5. Open questions

- Refresh-token TTL default (30 days? sliding vs absolute?).
- Cap on concurrent sessions per user (unlimited, or evict the oldest past N)?
- Should `DeviceToken` (RFC push devices) and `RefreshToken` be correlated by a
  shared device id so "log out this device" also unregisters its push token?
