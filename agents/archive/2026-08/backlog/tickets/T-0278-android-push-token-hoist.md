---
id: T-0278
title: Hoist the duplicated push-token cluster into :core behind a DeviceRegistrationClient interface
status: done
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
- 2026-06-22 — ready → review (android). Behavior-preserving hoist complete.
  - **AC1 (characterization-first):** customer `PushTokenRepositoryTest` was green at baseline
    (`:customer-app:testDebugUnitTest --tests …PushTokenRepositoryTest`). The same observable contract
    (cache last-registered token only on success → repeat call short-circuits; failed call leaves cache
    untouched → retries; `unregisterDevice` always clears cache) is re-pinned in the hoisted
    `core/src/test/.../notifications/PushTokenRepositoryTest.kt` against the new
    `DeviceRegistrationClient` seam.
  - **AC2 (four files hoisted):** `PushTokenRepository`, `PushTokenSessionObserver`, `DeviceApiDtos`
    (`RegisterDeviceRequest`/`RegisterDeviceResponse`/`UnregisterDeviceResponse`) moved verbatim-by-
    behavior into `cz.cleansia.core.notifications`. The 6 per-app duplicate copies are deleted; both
    apps now consume the `:core` types.
  - **AC3 (per-app binding seam):** `DeviceApi` (the generated-client adapter, which references the
    app-specific `cz.cleansia.{customer,partner}.api.*` clients and therefore CANNOT live in `:core`)
    becomes each app's `DeviceApiClient @Inject` impl of the new `:core`
    `DeviceRegistrationClient` interface, bound via `@Binds` in each app's `NotificationsBindingsModule`.
    The DataStore name is parameterized: `:core` declares `@PushTokenDataStore` qualifier; each app
    `@Provides` its own `DataStore<Preferences>` (`push_token_state` customer / `partner_push_token_state`
    partner). No partner-vs-customer choice is hardcoded in `:core`. The repo no longer takes
    `@ApplicationContext` or `Json` (the `safeApiCall`/`ApiResult` boundary lives in each app's
    `DeviceApiClient`; the repo only needs the injected DataStore + the interface).
  - **AC4 (Firebase constant reconciled — NOT a guess):** the two stale comments named DIFFERENT
    projects (partner: dest `cleansia`/old `cleansia-28fbc`; customer: dest `cleansia-28fbc`/old
    `cleansia`). Ground truth resolved without guessing: BOTH apps' `google-services.json` are on
    `project_id: cleansia-cz` (project_number 834394881207), and the backend documents the same
    (`Cleansia.Infra.Common/Configuration/Interfaces/IFcmConfig.cs:15` → e.g. "cleansia-cz"). The
    one-shot v1 token-reset migration's behavior is project-agnostic (deleteToken + clear cache, gated
    once per install) — only the documentary comment disagreed. Reconciled the single `:core`
    `MIGRATION_VERSION = 1` comment to the verifiable current project `cleansia-cz`; grep shows no
    remaining `cleansia-28fbc` / contradictory `cleansia` constant. (No `STOP/owner-question` was
    needed because the correct value was independently verifiable from committed config, not guessed.)
  - **AC5 (mechanical + encoding):** `:core` + `customer-app` + `partner-app` `compileDebugKotlin` and
    `testDebugUnitTest` all green (run individually and combined, EXIT=0). All touched files are
    BOM-free, valid UTF-8 (only intentional `—`/`→` glyphs, matching the original source style).
    `check-consistency.mjs mobile` = 27 violations, ALL pre-existing in unrelated files (auth/nav/
    profile VMs); zero in any file this ticket touched (no new violation).
  - **Deviations:** (1) the customer `PushTokenRepositoryTest` necessarily MOVED to `:core` (the class
    moved + its constructor changed to the new seam) rather than staying byte-identical in customer-app
    — the SAME behavior is pinned, so AC1's intent (flow preserved, suite green) holds. (2) the
    interface returns `Boolean` (success) instead of leaking `ApiResult` into `:core`, keeping the
    repo's only observable behavior — cache on success — identical. (3) Harvested the per-app
    binding-seam idiom into `patterns-mobile.md` (Modules section) per the conventions "harvest patterns
    back" rule.
  - **Manual steps:** none. No wire/DTO/endpoint shape changed (the generated clients and the
    `/api/Device/*` protocol are untouched), so no nswag-regen and no ef migration.
- 2026-06-22 — review fix (android, blocking finding #1: dead-code trim). The two app-DTO response
  types orphaned by the seam change — `RegisterDeviceResponse` / `UnregisterDeviceResponse` (old
  return types of the deleted per-app `DeviceApi` adapter; the `DeviceRegistrationClient` seam now
  returns `Boolean`) — were dead code hoisted verbatim into `:core` `DeviceApiDtos.kt`. Removed:
  `DeviceApiDtos.kt` now holds only `RegisterDeviceRequest`. Zero-consumer proven by grep across the
  whole repo (`*.kt`, production + tests + generated) — no matches for either response type.
  `RegisterDeviceRequest` retained (consumed by `PushTokenRepository.ensureRegistered`,
  `DeviceRegistrationClient.register`, both apps' `DeviceApiClient`, and `PushTokenRepositoryTest`).
  `:core:compileDebugKotlin` + `:core:testDebugUnitTest` re-run `--offline`, BUILD SUCCESSFUL EXIT=0,
  still green. Behavior-preserving (pure dead-code delete). No manual steps.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
