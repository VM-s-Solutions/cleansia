---
id: ADR-0030
title: "Web.Admin access-token TTL is 15 minutes — the web revocation bound (supersedes-in-part ADR-0024 for the admin host)"
status: accepted (panel verdict 2026-07-18; supersedes-in-part ADR-0024 D4.3 for the Web.Admin host only)
supersedes_in_part: ADR-0024
date: 2026-07-18
source: T-0409 (ADR-0024 amendment A3 follow-up); full author→challenger→lead trail in the ticket
---

## Context

ADR-0024 set the mobile access-token TTL as the device-revocation latency bound (short TTL, the
ADR-0026 device directory + ADR-0027 user directory cap the window) and deferred the web hosts as
a follow-up (D4.3), naming the **admin host as the priority, separable case**. On web the TTL is
the *entire* revocation machinery: no web client sends `X-Device-Id`, so the device directory can
never reach a cookie session (`RefreshTokenService.cs:154-167` null-guard), and no
password-reset/-change directory fires for a plain admin logout either. Every rotation still
re-checks `IsActive` and re-pins the `Administrator` profile/audience
(`RefreshToken.cs:75-95`, `AdminAuthController.cs:47-52`), and the admin chain even has a 429
back-off retry the mobile chain lacked.

## Decision

**`Cleansia.Web.Admin` issues 15-minute access tokens** (was 1440). This supersedes-in-part
ADR-0024's blanket web-1440 pin **for the admin host only**; `Web.Partner` and `Web.Customer`
stay 1440 and stay pinned (their flips are separately gated — see ADR-0024 D4.3 and the T-0409
ticket's customer-SSR prerequisite chain).

- **15, not 30:** ADR-0024's "not 15" arguments were mobile-shaped (a NAT herd needs ~10
  co-located clients to wake together; radio flakiness). Neither transfers to a single-digit
  desktop-admin population on a wired network, and the admin refresh chain has the 429 back-off
  the mobile chain lacked when "not 15" was decided.
- **Session UX untouched:** the access-cookie `Expires` is pinned to the *refresh* expiry
  (`AuthCookieWriter.cs:51`) and the SPA keys all state off refresh expiry, never access expiry —
  a 15-min access token rotates invisibly behind the cookie.

**Blocking predecessor (T-0409 found it live in prod today):** the admin 401→refresh→replay
carries the **pre-refresh** `X-CSRF-Token` with the **post-refresh** cookie. The server derives the
double-submit key from the token's per-token `jti` (`AuthExtensions.cs`), so every refresh rotates
the CSRF; the replay 403s on `csrf.header_mismatch`. This fires at the current 24 h boundary in
prod (`Csrf:Enabled=true` only in Production) and a 15-min TTL multiplies it ~96×. The one-file
interceptor fix (restamp CSRF from the post-refresh value on both replay branches) is a hard
predecessor and ships with this flip (`error.interceptor.ts`, TC-ADMIN-TTL-3).

## Consequences

- Config: two files (`Cleansia.Web.Admin/appsettings.json` + `appsettings.Production.json`
  `AccessTokenExpMinutes: 1440 → 15`) and the raw-file pin `AccessTokenTtlConfigPinTests`
  (admin → `15d`; other four hosts unchanged). No Bicep, no NSwag, no `TokenService`/`RefreshToken`
  change, `ClockSkew` stays `Zero`, mobile hosts untouched.
- **Effectiveness pre-flight (OPEN, owner/T-0400):** `environment.prod.ts` `authApiBaseUrl` points
  admin auth at `api.cleansia.cz` (the **partner** API host). Until that pairing is corrected under
  T-0400, the prod flip is **inert for the deployed admin app** (dev/devremote proxies already
  pair correctly with :5001). This ADR does not fix the pairing; it records the gate.
- Accepted residues (not this ADR): the admin multi-tab boundary race (shared cookie jar +
  localStorage, no reuse grace window → Web Locks single-flight is the named fix, telemetry-
  triggered); refresh transport/429 conflated as terminal on web (the ADR-0024 D4.5 mirror, same
  trigger). Both documented in the T-0409 ticket.

## Verification

- **TC-ADMIN-TTL-1** — raw-file pin: Web.Admin `15` in both settings files; the other four hosts
  unchanged (existing `AccessTokenTtlConfigPinTests`).
- **TC-ADMIN-TTL-2** — HostTests fractional-TTL end-to-end on the admin host: login → authed 200 →
  past-expiry 401 → cookie refresh → replay 200 (pins ClockSkew=Zero + the cookie refresh path).
- **TC-ADMIN-TTL-3** — Jest: the replayed mutation carries the post-refresh CSRF on both the
  initiator and the queued-waiter branch (the predecessor regression pin) — SHIPPED with this
  change.
- **TC-ADMIN-TTL-4** — every existing revocation/TTL suite stays green unedited.

This ADR is **immutable** — supersede, never edit.
