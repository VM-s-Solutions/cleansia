---
id: T-0268
title: "E2 — verify-and-close shared ActionState one-shot-effect coverage (already implemented by T-0252)"
status: ready
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

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
