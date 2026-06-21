---
id: T-0268
title: "E2 — verify-and-close shared ActionState one-shot-effect coverage (already implemented by T-0252)"
status: done
size: S
owner: android
created: 2026-06-21
updated: 2026-06-21
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
source: audits/consistency-violations.md F14 (E2); knowledge/consistency.md §E2; T-0252 (Wave 5) close-out
---

## Context

Residual Android consistency rule **E2** (one-shot actions use the shared `sealed ActionState`
(`Idle`/`Submitting`/`Error`) + a `SharedFlow(replay=0)` success effect — never loose
`_submitting: Boolean` + `_error: String?` StateFlows). Rule: `knowledge/consistency.md` §E2; finding:
`audits/consistency-violations.md` F14.

**Reconciliation finding: E2 is already implemented by T-0252 (Wave 5) and merged to `master`.**
Verified on `master`:
- customer `CreateDisputeViewModel`, `MembershipViewModel`, `ProfileViewModel` all use
  `cz.cleansia.customer.ui.state.ActionState` (no loose `_submitting`/`_error`); `CreateDisputeViewModel`
  carries no `isLoading`/`_submitting` flags.
- partner's old `enum OrderAction inFlight` was replaced by `cz.cleansia.core.ui.state.ActionState`
  (`OrderDetailsViewModel.inFlightAction`, verified), and `LoginViewModel` uses `ActionState` +
  `SharedFlow`.
- T-0252's status log records the test-first VM tests (`CreateDisputeViewModelTest`,
  `ProfileViewModelTest`, `MembershipViewModelTest`) and a `check-consistency` run reporting **0 E1/E2
  violations** on the touched dirs.

So there is **no implementation work** for E2. This ticket exists to give the rule a tracked
disposition: **verify on current `master`, confirm zero E2 violations app-wide, and close** — so the
Wave-7 consistency-debt sweep is complete and auditable. It edits **no production files** (so it does
**not** collide with T-0266/T-0267/T-0269).

## Acceptance criteria

- [ ] **AC1 (no loose action booleans remain)** — A scan of both apps confirms no one-shot action path
  still uses a loose `_submitting: Boolean` + `_error: String?` pair (or an `enum …Action inFlight`)
  where the shared `ActionState` + `SharedFlow(replay=0)` is the canon. Any genuine remaining instance
  is listed with file:line (expected: none beyond the §E2 not-issues).
- [ ] **AC2 (consistency gate clean)** — `node agents/tools/check-consistency.mjs --paths=src/cleansia_android`
  reports **zero E2 violations** across both apps.
- [ ] **AC3 (E2 tests green)** — The T-0252 E2 VM tests (`CreateDisputeViewModelTest`,
  `ProfileViewModelTest`, `MembershipViewModelTest`) run green on plain JVM as the existing evidence;
  reviewer confirms the `ActionState` + `SharedFlow` shape is the implemented contract.
- [ ] **AC4 (audit closed)** — If AC1–AC3 are all green, the reviewer/PM records E2 as **done by
  T-0252, verified here**; the F14 entry in `audits/consistency-violations.md` is marked cleared.
  **If AC1 surfaces any genuine un-migrated one-shot action**, this ticket converts only that specific
  VM (test-first) — it does not re-touch the already-canonical ones.

## Out of scope

- Re-touching the customer VMs / partner `OrderDetails` already migrated by T-0252 (no churn).
- E1 (T-0267), E6 (T-0269), E7 (T-0266).
- Any behavior or API change.

## Implementation notes

- **Verification-first, no production edits expected.** This is a scan + gate-run + close. Because it
  does not edit production files, it can run **concurrently with anything** in Wave 7 (no file-lock
  conflict). If AC1 finds a real residual, the dev converts just that VM test-first and the ticket
  picks up a same-file lane only against whichever of T-0267/T-0269 touches that exact VM (serialize
  that single file if so — see sprint-9).
- **No `manual_steps`** — mobile-only; no nswag-regen, no ef-migration.

## Status log
- 2026-06-21 — ready (created by pm). Wave 7 (Android consistency debt). **Reconciliation:** E2 is
  already implemented + merged by T-0252; this ticket is the verify-and-close disposition so the rule
  has a tracked outcome (not a redo). DoR met: AC observable, sized S, no deps, mobile-only (no
  migration/regen), verification-only (no file collision; runs concurrently). **No new
  behavior/decision → no deliberation panel** (no-decision verify/close; converts a residual only if
  the scan surfaces a genuine one). Reviewer-per-developer.
- 2026-06-21 — verified (android). **Verify-only; NO production files edited.** Evidence:
  - **AC1 (named F14 set canonical):** the three F14-named customer VMs + the partner `OrderAction`
    case are all on the shared `ActionState`:
    - `features/disputes/CreateDisputeViewModel` — `submitState: StateFlow<ActionState>` +
      `createdDisputeId: SharedFlow` (extraBufferCapacity=1); no `_submitting`/`_error`.
    - `features/membership/MembershipViewModel` — `submitState: StateFlow<ActionState>`; subscribe
      success flows through `SubscribeOutcome`; no loose booleans.
    - `features/profile/ProfileViewModel` — `refreshState` + `saveState` (both `ActionState`); save
      effect via `onSaved`/`onCompleted` callbacks; no loose booleans.
    - partner `features/orders/OrderDetailsViewModel` — `actionState: StateFlow<ActionState>`
      (`cz.cleansia.core.ui.state.ActionState`) is the one-shot machine; the retained
      `enum OrderAction` + `inFlightAction` is now a *per-button which-spinner* discriminator layered
      ON TOP of `ActionState`, NOT the old enum-as-inFlight substitute → §E2 judgment-call non-issue.
  - **AC2 (gate clean):** `node agents/tools/check-consistency.mjs --paths=src/cleansia_android mobile`
    → **zero E2 violations** (the 69 reported are E1×18 [T-0267], E6×49 [T-0269], E5×1 [partner
    DashboardRepository legacy nullable], conv×1 [RewardsTab hardcoded string] — none E2). The tool has
    no dedicated E2 line-scan; AC2 is satisfied by the absence of any E2-tagged output, with the manual
    scan (AC1) as the substantive E2 check.
  - **AC3 (E2 tests green):** `:customer-app:testDebugUnitTest --offline` → **201/201**, incl.
    `CreateDisputeViewModelTest` 8/8, `MembershipViewModelTest` 10/10, `ProfileViewModelTest` 9/9 — all
    assert the `ActionState` + `SharedFlow`/callback contract. Baseline also re-confirmed:
    `:partner-app` 26/26, `:core` 13/13; all three modules `compileDebugKotlin` EXIT 0. All touched
    files byte-clean UTF-8.
  - **Judgment-call non-violations (recorded, NOT fixed):** per-item in-flight discriminators that
    `ActionState` (a single Idle/Submitting/Error) structurally cannot express — partner
    `OrderDetailsViewModel._inFlightAction` (per-button), `OrdersListViewModel.inFlightActionOrderId`
    (per-row), customer `RecurringBookingsViewModel._mutating` (per-row). Partner profile-section VMs
    (Bank/Address/Personal/…) carry `isSaving`/`error` INSIDE a flag-bag `*UiState` data class → that
    is an **E1** concern (T-0267), not loose standalone E2 booleans.
  - **AC4 (audit close):** AC1–AC3 green for the F14-named set → **F14 (E2) cleared**; entry marked in
    `audits/consistency-violations.md`.
  - **GENUINE RESIDUAL E2 (report-only, NOT in F14's named set; needs its own scoped ticket — per
    AC4 + Wave-7 instruction, NOT fixed here):** three one-shot ACTION paths shipped AFTER the
    2026-06-01 audit / T-0252 (Wave 5) still use a loose `_submitting`-style `Boolean` StateFlow as the
    action machine instead of the shared `ActionState`:
    1. `customer/features/recurring/CreateRecurringViewModel.kt:60` — `_submitting = MutableStateFlow(false)`
       drives `submit()`; outcome via `_submitOutcome: StateFlow<SubmitOutcome?>` (not a `SharedFlow(replay=0)`).
    2. `customer/features/disputes/DisputeDetailViewModel.kt:57,66` — `_sending` (sendMessage) +
       `_uploadingEvidence` (uploadEvidence) loose `Boolean` StateFlows for two one-shot actions.
    3. `customer/features/profile/DeleteAccountViewModel.kt:22` — `_loading = MutableStateFlow(false)`
       gates the one-shot `deleteAccount()` action (milder: single boolean, no `_error`).
    These are behavior-correct today; converting them to `ActionState` is a scoped (test-first)
    refactor like T-0252, recommended as a follow-up consistency ticket. Not touched here.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
