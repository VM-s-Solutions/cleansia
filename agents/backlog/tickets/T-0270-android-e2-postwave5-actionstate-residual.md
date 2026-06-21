---
id: T-0270
title: "E2 — convert the 3 post-Wave-5 one-shot-action VMs off loose _submitting/_loading booleans onto the shared ActionState + SharedFlow pattern"
status: done
size: S
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 8
source: T-0268 (Wave-7 E2 verify-close) AC4 residual finding; audits/consistency-violations.md F14 note; knowledge/consistency.md §E2
---

## Context

Residual Android consistency rule **E2** (one-shot actions use the shared
`cz.cleansia.customer.ui.state.ActionState` (`Idle`/`Submitting`/`Error`) + a `SharedFlow(replay=0)`
success effect — never a loose `_submitting: Boolean` / `_loading: Boolean` + `_error`/outcome
`StateFlow` pair as the action machine). Rule: `knowledge/consistency.md` §E2; canonical pattern
established by **T-0252 (Wave 5)**; finding: `audits/consistency-violations.md` F14 NOTE.

T-0268 (the Wave-7 E2 **verify-and-close** disposition) confirmed the **audit-named** F14 set
(customer `CreateDispute`/`Membership`/`Profile` + partner ex-`OrderAction inFlight`) is fully on the
shared `ActionState` and cleared F14. **In doing so its AC1 scan surfaced 3 GENUINE residual E2
violations that postdate the 2026-06-01 audit and T-0252** — one-shot ACTION paths shipped after the
F14 work that still use a loose boolean as the action machine. They are out of F14's named set, so
T-0268 correctly did **not** fix them; they are carried here as a scoped, behavior-preserving
follow-up.

### In scope — convert (3 customer-app VMs, test-first)

1. **`customer/features/recurring/CreateRecurringViewModel.kt`** (≈line 60) —
   `_submitting = MutableStateFlow(false)` drives `submit()`; the outcome is surfaced via
   `_submitOutcome: StateFlow<SubmitOutcome?>` (a held StateFlow, not a one-shot `SharedFlow(replay=0)`).
   Convert the action machine to `ActionState` (`Idle`/`Submitting`/`Error`) and the outcome to a
   one-shot effect (`SharedFlow(replay=0)` / callback), matching T-0252.
2. **`customer/features/disputes/DisputeDetailViewModel.kt`** (≈lines 57, 66) — two loose `Boolean`
   StateFlows for two distinct one-shot actions: `_sending` (`sendMessage`) and `_uploadingEvidence`
   (`uploadEvidence`). Each gets its own `ActionState` action machine + one-shot success effect.
3. **`customer/features/profile/DeleteAccountViewModel.kt`** (≈line 22) —
   `_loading = MutableStateFlow(false)` gates the one-shot `deleteAccount()` action (milder: a single
   boolean, no `_error`). Convert to `ActionState` + a one-shot completion effect.

All three are **behavior-correct today** — this is a pure canonicalization onto the established
pattern; impossible/loose states become unrepresentable and the success path becomes a one-shot effect
(no replay on rotation), exactly as the T-0252 VMs.

### Explicitly NOT in scope — recorded judgment-call NON-violations (do NOT convert)

These are **per-row / per-button in-flight discriminators** that a single `ActionState` (one
`Idle`/`Submitting`/`Error` for the whole VM) **structurally cannot express** — they must remain a
keyed/enum discriminator. They are recorded NON-violations, not E2 defects:

- partner `features/orders/OrderDetailsViewModel._inFlightAction` — **per-button** (which action's
  spinner), layered on top of the VM's `actionState`.
- partner `features/orders/OrdersListViewModel.inFlightActionOrderId` — **per-row** (which order is
  mutating).
- customer `features/recurring/RecurringBookingsViewModel._mutating` — **per-row** (which booking is
  mutating).

## Acceptance criteria

- [x] **AC1 (characterization-first, per `testing.md`)** — For each of the 3 VMs, a ViewModel test pins
  the current action behavior (Idle→Submitting→success-effect, Submitting→Error on failure, button
  disabled while in-flight) **before** the refactor; status log shows test-first ordering (red→green).
- [x] **AC2 (E2 canonical form)** — Each of `CreateRecurringViewModel`, `DisputeDetailViewModel` (both
  its `sendMessage` and `uploadEvidence` actions), and `DeleteAccountViewModel` exposes its one-shot
  action(s) as `ActionState` (`cz.cleansia.customer.ui.state.ActionState`) + a `SharedFlow(replay=0)`
  (or equivalent one-shot callback) success/completion effect; the loose `_submitting`/`_loading` +
  `_submitOutcome`/`_error` action booleans are removed.
- [x] **AC3 (behavior identical)** — Same screen behavior for the same inputs: the action button
  disables while submitting, the success effect fires exactly once (does **not** replay on config
  change / rotation), and error surfaces as before. No new product behavior.
- [x] **AC4 (consistency gate + scan clean)** — `node agents/tools/check-consistency.mjs --paths=src/cleansia_android`
  shows no E2 regression, and a manual re-scan confirms these 3 VMs no longer use a loose
  `_submitting`/`_loading` boolean as the action machine. The 3 recorded per-row/per-button
  discriminators (above) are left untouched and noted as accepted NON-violations.
- [x] **AC5 (suite green / compiles)** — customer-app builds and `:customer-app:testDebugUnitTest` is
  green on plain JVM (the existing 201 plus the new characterization tests; now 222 total); reviewer confirms
  refactor-only (no behavior change).

## Out of scope

- The per-row/per-button in-flight discriminators (`OrderDetailsViewModel._inFlightAction`,
  `OrdersListViewModel.inFlightActionOrderId`, `RecurringBookingsViewModel._mutating`) — **recorded
  judgment-call NON-violations**; `ActionState`'s single Idle/Submitting/Error cannot express
  which-row/which-button, so they correctly stay as keyed discriminators.
- The already-canonical F14-named VMs (customer `CreateDispute`/`Membership`/`Profile`, partner
  ex-`OrderAction inFlight`) — done by T-0252, verified by T-0268. No churn.
- E1 (sealed `*UiState`), E6 (`collectAsStateWithLifecycle`), E7 (dir/naming) — separate rules
  (T-0267/T-0269/T-0266, all done in Wave 7).
- Any new screen, feature behavior, API/DTO change, or i18n change.

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E2; reuse the exact shape T-0252 established on
  `CreateDisputeViewModel`/`MembershipViewModel`/`ProfileViewModel` (shared
  `cz.cleansia.customer.ui.state.ActionState` + a `SharedFlow(replay=0)` / callback effect).
- **Reuse, don't recreate:** the shared `ActionState` type already exists — do not introduce a parallel
  type. The one-shot effect must be `replay=0` so success does not re-fire on rotation.
- **No new behavior/decision → no deliberation panel.** Pure mechanical canonicalization of 3 VMs
  against the already-ratified §E2 rule and the T-0252 reference implementation; the only judgment (the
  per-row/per-button exclusions) is already recorded above.
- **No `manual_steps`** — mobile-only, customer-app only: no nswag-regen, no ef-migration, no i18n
  change (reuses existing string keys / effects).
- **Files are customer-app only** — no overlap with the partner-files lane (T-0266/T-0267/T-0269, all
  done). When promoted, runs as a single S ticket, reviewer-per-developer, no security/optimizer gate.

## Status log
- 2026-06-21 — draft (created by pm). Sprint 8 (next-wave candidate). **Source: T-0268 (Wave-7 E2
  verify-close) AC4 residual** — the 3 one-shot-action VMs that postdate T-0252's F14 work and still use
  a loose `_submitting`/`_loading` boolean as the action machine. DoR-aligned for promotion: AC
  observable + characterization-first, sized **S**, no deps (customer-app-only, partner lane done),
  mobile-only (no migration/regen/i18n), behavior-preserving. **No deliberation panel** (no-decision
  canonicalization against §E2 + the T-0252 reference). Per-row/per-button discriminators explicitly
  excluded as recorded NON-violations.
- 2026-06-21 — **android → review**. Behavior-preserving E2 conversion of the 3 customer-app VMs onto
  the shared `cz.cleansia.customer.ui.state.ActionState` + `SharedFlow(replay=0)` one-shot effect,
  reusing the T-0252 `CreateDisputeViewModel` shape (`MutableSharedFlow(extraBufferCapacity = 1)` →
  `asSharedFlow()`).

  **Test-first (AC1) — red→green per VM.** Wrote one characterization test per VM against the NEW
  shape **first**; confirmed RED via `:customer-app:compileDebugUnitTestKotlin` (8 distinct
  `Unresolved reference` errors: `submitState`/`submitted`, `sendState`/`uploadState`/`messageSent`/
  `evidenceUploaded`, `deleteState`/`accountDeleted` — the accessors did not yet exist). Then
  refactored each VM under the test; re-ran → GREEN. Tests pin: Idle start, Idle→Submitting in-flight
  gating, Submitting→Idle + one-shot success effect on success, Submitting→Error on failure
  (http-snackbar vs network-silent), and validation/no-op + re-entry-guard where the original had one.

  **Files changed.**
  - VMs: `features/recurring/CreateRecurringViewModel.kt` (`_submitting` Boolean + held
    `_submitOutcome: StateFlow<SubmitOutcome?>` → `submitState: ActionState` + `submitted:
    SharedFlow<Unit>`; removed the `SubmitOutcome` sealed type + `consumeOutcome()`; success/failure
    snackbars moved into the VM via `showSuccessKey`/`showErrorKey` so they fire once and never
    replay), `features/disputes/DisputeDetailViewModel.kt` (TWO machines: `_sending`→`sendState` +
    `messageSent`; `_uploadingEvidence`→`uploadState` + `evidenceUploaded`),
    `features/profile/DeleteAccountViewModel.kt` (`_loading` Boolean → `deleteState: ActionState` +
    `accountDeleted: SharedFlow<Unit>`; kept the success-confirmation snackbar).
  - Screens/wiring: `features/recurring/CreateRecurringScreen.kt` (collects `submitState` for the
    disabled/spinner state; `submitted` one-shot effect drives `onCreated()`; dropped the now-unused
    `RecurringSnackbarEntryPoint`), `features/disputes/DisputeDetailScreen.kt` (derives
    `sending`/`uploading` from the two ActionStates), `navigation/CleansiaNavHost.kt` (DeleteAccount
    route reads `deleteState` for the in-flight gate).
  - New tests: `CreateRecurringViewModelTest` (5), `DisputeDetailViewModelTest` (11),
    `DeleteAccountViewModelTest` (5) = **21 new**.

  **Behavior-preserving notes (AC3).** `sendMessage`/`uploadEvidence` deliberately keep **no**
  re-entry guard — the evidence path is a sequential multi-file batch (the screen loops
  `uploadEvidence` per URI in one coroutine), so a guard would silently drop files 2..N; the in-flight
  `Submitting` state still gates the UI button exactly as the old boolean did. Success effects use
  `SharedFlow(replay=0)` so they fire **exactly once** and do not re-fire on rotation (replacing the
  recurring screen's held-StateFlow + `consumeOutcome()` dance).

  **Verify.**
  - `:customer-app:compileDebugKotlin --offline -q` → **EXIT 0**.
  - `:customer-app:testDebugUnitTest --offline -q` → **EXIT 0**; 23 suites, **0 failures / 0 errors**,
    **222 tests total** (201 baseline + 21 new). New suites: DisputeDetail 11, DeleteAccount 5,
    CreateRecurring 5 — all green.
  - `node agents/tools/check-consistency.mjs --paths=src/cleansia_android` → **0 E2 violations** (the
    27 reported are pre-existing E1/E5/E6/conv in other files, out-of-scope per this ticket — incl.
    NavHost:449 E6 which was already `collectAsState` before this change). Manual re-grep of the 3 VMs
    for `_submitting`/`_loading`/`_sending`/`_uploadingEvidence`/`_submitOutcome`/`SubmitOutcome` → **no
    matches**.
  - Recorded NON-violations left untouched: partner `OrderDetailViewModel._inFlightAction`, partner
    `OrdersListViewModel.inFlightActionOrderId`, customer `RecurringBookingsViewModel._mutating` still
    present as keyed discriminators.
  - Encoding: all 9 changed `.kt` confirmed byte-clean UTF-8, no BOM, no mojibake.

  **Parity (iOS port).** Surface for 1:1 mirroring: each one-shot action exposes
  `state: <Result<T,ApiError> equiv>`-driven `ActionState` (`Idle`/`Submitting`/`Error(message)`) +
  a `replay=0` success effect the screen collects once. CreateRecurring: `submitState` + `submitted`
  (→ navigate, success/failure snackbar in VM). DisputeDetail: `sendState`+`messageSent` and
  `uploadState`+`evidenceUploaded` (both → reload on success; upload has no re-entry guard for batch).
  DeleteAccount: `deleteState` + `accountDeleted` (success snackbar in VM, forced-sign-out navigates).
  No API/DTO/i18n/navigation change.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
