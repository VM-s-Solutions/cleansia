---
id: T-0344
title: "Owner: Apple Sign in with Apple provisioning (capability on cz.cleansia.customer App ID + Xcode entitlement + Apple:BundleId config)"
status: proposed
size: S
owner: owner
created: 2026-06-28
updated: 2026-06-28
depends_on: []
blocks: []
stories: []
adrs: [0013, 0016]
layers: [ios, backend]
security_touching: false
manual_steps: [apple-siwa-capability, xcode-siwa-entitlement, apple-bundleid-config]
sprint: 12
source: Q-IOS-04 ruling (sprint-12 §7.14 §"MANUAL_STEPs"); gates LIVE Apple sign-in for T-0312 (the code ships in T-0343/T-0312)
---

> **OWNER TASK — gates LIVE Apple sign-in, not the code.** T-0343 (backend) + T-0312 (iOS) ship the full SIWA
> code behind an empty `Apple:BundleId` (fails closed). Native Sign in with Apple needs **only** the three
> steps below — **no `.p8` key, no Services ID, no domain verification** (those are web/Services-ID concerns;
> the native identity-token flow verifies `aud == bundle id`). Same gate shape as T-0342-gates-T-0311.

## Steps (minimal native path)
1. **Apple Developer → Identifiers → the `cz.cleansia.customer` App ID:** enable the **Sign in with Apple**
   capability, choose **"Enable as a primary App ID"**, Save. *(A paid Apple Developer Program membership is
   required.)*
2. **Xcode → the `cz.cleansia.customer` target → Signing & Capabilities:** add the **Sign in with Apple**
   capability (same Team) — this writes the entitlement the App ID authorizes. *(Claude adds the
   `com.apple.developer.applesignin` entitlement to `project.yml`/the entitlements file as part of T-0312; the
   owner just needs the capability enabled on the App ID + the Team selected so signing succeeds.)*
3. **Customer Mobile API config:** set `Apple:BundleId = cz.cleansia.customer` (bound by `AppleConfig`).
   Until set, `AppleTokenVerifier` fails closed exactly like an empty `Google:ClientId`.

## Explicitly NOT needed (identity-token-only design — §7.14 D4)
- **No Sign in with Apple key (`.p8`), no Key ID, no Team ID** — required only if the backend calls Apple's
  token-exchange/refresh/`/auth/revoke` endpoints. Our design never exchanges the `authorizationCode` and holds
  no Apple token, so 5.1.1 account-deletion has nothing to revoke. (Re-open only if a future feature needs it.)
- **No Services ID, no Return URL / domain verification** (web-only).

## Done when
- [ ] Sign in with Apple is enabled on the `cz.cleansia.customer` App ID (primary).
- [ ] The Xcode target signs with the Sign in with Apple entitlement on the Team.
- [ ] `Apple:BundleId = cz.cleansia.customer` is set in the Customer Mobile API config.
- [ ] A real-device Apple sign-in issues a platform JWT end-to-end.

## Status log
- 2026-06-28 — filed from the Q-IOS-04 ruling. The buildable code is T-0343 (backend) + T-0312 (iOS); this is
  the Apple-account/config gate for live sign-in.
