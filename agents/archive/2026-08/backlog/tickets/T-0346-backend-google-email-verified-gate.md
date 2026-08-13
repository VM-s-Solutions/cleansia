---
id: T-0346
title: "Backend (security hardening): gate GoogleAuth provisioning on email_verified (parity with the new AppleAuth gate)"
status: done
size: S
owner: backend
created: 2026-06-28
updated: 2026-06-30
depends_on: [T-0343]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
priority: medium
manual_steps: []
sprint: 12
source: T-0343 / Q-IOS-04 security gate (sprint-12 §7.14) — security finding (medium)
---

> **MEDIUM security hardening on EXISTING code — deliberately NOT bundled into T-0343** (T-0343 is a clean
> Apple parity-mirror; this changes live Google behavior, so it's a separate, considered change). The new
> `AppleTokenVerifier`/`AppleAuth` provisions **only on `email_verified == true`**. The existing
> `GoogleTokenVerifier` (`GoogleTokenVerifier.cs:34-39`) does **not** read or check `email_verified` at all —
> `GoogleAuth` provisions on any verified-token email. Both providers should refuse to auto-provision / auto-link
> on an **unverified** email.

## The gap
A Google account with an **unverified** email (rare, but possible with certain federated Google identities)
could be auto-provisioned, and an unverified-email collision is conceivable. Apple's stricter gate is correct;
Google should match it so the takeover/collision guard rests on a **verified** email for both providers.

## Fix
- Extract `email_verified` in `GoogleTokenVerifier` (the Google ID token carries it) and surface it on the
  verified-claims result (mirror `AppleVerifiedClaims.EmailVerified`).
- In `GoogleAuth.Handler`, provision/auto-link **only when `email_verified == true`** (reject otherwise),
  matching the AppleAuth gate. The existing `AuthenticationType != Google` takeover guard stays.
- Add a unit test: an unverified-email Google token does **not** provision.

## Risk / rollout note
This is a behavior change on a live endpoint. Google-managed consumer accounts almost always report
`email_verified == true`, so impact should be near-zero, but it must be a **deliberate** change (hence its own
ticket): confirm no legitimate current sign-in path relies on an unverified Google email before merging.

## Done when
- [x] `GoogleTokenVerifier` extracts `email_verified`; `GoogleAuth` provisions/links only on a verified email.
- [x] An unverified-email Google token is rejected (no provision); the test proves it.
- [x] Both providers' auto-provision/link rest on a verified email (parity).

## Status log
- 2026-06-28 — filed from the Q-IOS-04 security gate (§7.14, medium finding). Kept separate from T-0343 to avoid
  changing live Google behavior inside the iOS-enabling parity work.
- 2026-06-30 — **proposed → done** (HARDENING-1, `64f6525` on `phase/hardening-1`, off master `3e7ce52`;
  bundled in the backend trio with T-0348 + T-0350). `IGoogleTokenVerifier`/`GoogleTokenVerifier` now surface
  `email_verified`; `GoogleAuth.Handler` provisions/auto-links **only when `email_verified == true`** (fail-
  closed, parity with the AppleAuth gate); the existing `AuthenticationType != Google` takeover guard stays.
  Added the unit test proving an unverified-email Google token does NOT provision (`GoogleAuthHandlerTests`).
  No contract/DTO change, no regen, no migration. **Security review CLEAN** (account-takeover NO; both
  providers' auto-provision now rests on a verified email). Build 0 errors; `Cleansia.Tests` 1685. Reviewer
  APPROVE. NOT committed by the PM — the owner commits the backlog edits with the phase PR.
