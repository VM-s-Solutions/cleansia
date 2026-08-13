---
id: T-0406
title: "Android partner — no collector of SessionManager forced-signout events: a token-dead device stays on its current screen instead of routing to login"
status: done
size: S
owner: android
created: 2026-07-15
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: revoke-device-session-audit (wf_88de1ca0) — byproduct finding while investigating device revocation
---

> Found while auditing device-revocation latency. The shared `AuthAuthenticator` correctly clears
> tokens/caches and emits `ForcedSignOut(SessionExpired)` when a refresh fails — and the **customer**
> app collects it in `CleansiaNavHost` (`navigate(Routes.SignIn)` with `popUpTo` inclusive). The
> **partner** app has **no collector** of `SessionManager.events`: after its session dies (device
> revoked, refresh token expired/rotated away), every API call fails but the UI stays on the current
> screen until a cold start hits the splash gate. iOS handles this correctly in both apps
> (`forcedSignOutStream` → `route = .login` in both RootViews).

## Acceptance criteria
- [ ] **AC1** — partner app collects the forced-signout event at the nav-host level (mirror the
  customer implementation) and routes to login with a cleared back stack.
- [ ] **AC2** — a revoked/expired partner session lands on the login screen on the next failed call,
  parity with customer; manual check + a UI/unit test where the harness allows.
- [ ] **AC3** — existing partner tests green.

## Status log
- 2026-07-15 — filed `proposed` from the session-revocation audit byproduct.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped `26d2d6df`: SessionViewModel + nav-root collector; all sign-out navigations graph-clearing.
