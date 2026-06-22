---
id: T-0278
title: Hoist the duplicated push-token cluster into :core behind a DeviceRegistrationClient interface
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** mechanical hoist of mirror-image code into the existing `:core`
> shared module behind a small bind-per-app interface — the canonical factoring `:core` already uses for
> `ApiResult`/`TokenStore`/`DeviceIdProvider`. No new behavior; the device-registration flow is unchanged.

## Context

Audit finding #3 (HIGH). The push-token cluster —
`{PushTokenRepository, PushTokenSessionObserver, DeviceApi, DeviceApiDtos}.kt` — is **duplicated across
both Android apps** under `.../core/notifications/`. `partner PushTokenRepository.kt:30` itself admits
the mirror. `:core` (`cz.cleansia.core`) is the ratified home for shared cross-app code
(`DeviceIdProvider`, `ApiResult`, `SessionScopedCache`, `TokenStore` already live there). A secondary
defect: the **migration comments disagree on the Firebase project** — the migration constant must be
reconciled to one truth.

## Acceptance criteria

- [ ] **AC1 — Characterization first.** Before the move, the existing push-token behavior is pinned by
  the apps' unit tests (register-on-session, unregister/revoke, the session-observer wiring); these stay
  **green unchanged** through the hoist (proves the device-registration flow is preserved).
- [ ] **AC2 — Four files hoisted to `:core`.** `PushTokenRepository`, `PushTokenSessionObserver`,
  `DeviceApi`, `DeviceApiDtos` live in `cz.cleansia.core.notifications`; both apps consume them. The
  per-app duplicate copies are deleted.
- [ ] **AC3 — Per-app binding seam.** The app-specific concerns (the API binding, the DataStore name)
  are injected: a `DeviceRegistrationClient` interface (or equivalent) is defined in `:core` and **each
  app binds its own** implementation; the DataStore name is **parameterized** (not hardcoded in `:core`).
  No `:core` code hardcodes a partner-vs-customer choice.
- [ ] **AC4 — Migration constant reconciled.** The disagreeing Firebase-project migration comments/
  constant are reconciled to a single documented value; a grep shows no remaining contradictory constant.
- [ ] **AC5 — Mechanical checks green + encoding-clean.** `:core` + `partner-app` + `customer-app`
  `compileDebugKotlin` + `testDebugUnitTest` pass; the diff is **byte-clean ASCII/UTF-8** (no BOM/
  mojibake); `check-consistency.mjs mobile` no new violation.

## Out of scope
- **No change to the device-registration *protocol*** (endpoints, DTO shapes on the wire) — this is an
  internal Android factoring; the backend device endpoints are untouched.
- **No FCM/Firebase config change** beyond reconciling the contradictory migration constant to one value
  (if the *correct* value is genuinely unknown, **stop and raise a question** — do not guess a project id).
- **The formatter hoist** is T-0277 — separate ticket.

## Implementation notes

Mirror the `:core` factoring already used for `ApiResult`/`TokenStore` (ADR-0011): shared logic in
`:core`, app-specific bindings via Hilt in each app module. **Single android dev + one reviewer** (M:
4 files × 2 apps + an interface + DataStore parameterization + the constant reconcile).

**Serialization:** this ticket and **T-0277 both edit `:core`** — do **not** run concurrently on
`:core`; serialize. If the Firebase-project value is ambiguous (AC4), the dev stops and the PM raises a
question to the owner rather than picking one.

**Routing:** `[android]`. `reviewer`. `qa` = compile + JVM unit tests green + AC1↔test mapping. No
`security` (no wire/authz change — the device endpoints' auth is server-side and unchanged), no
`optimizer`.

## Status log
- 2026-06-22 — draft → ready (created by pm). Finding #3 (traced evidence in the audit:
  `partner PushTokenRepository.kt:30` admits the mirror; `:core` is the shared home). No-decision (hoist
  onto `:core`). `manual_steps: []` (Android-internal; no nswag/ef). Sized **M**. **Serialize with T-0277
  on `:core`.** Note: if the Firebase migration constant is genuinely unknown → stop + ask owner.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
