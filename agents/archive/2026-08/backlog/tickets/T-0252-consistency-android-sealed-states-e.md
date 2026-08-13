---
id: T-0252
title: "Consistency sweep E1/E2 — sealed Android UiState + shared ActionState (partner + customer ViewModels)"
status: done
size: M
owner: —
created: 2026-06-13
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 5
source: T-0196 split (Batch 5C sub-stream E1/E2); audits/consistency-violations.md (T-0013/E1, T-0014/E2)
---

## Context
Child of the **T-0196** mechanical consistency sweep (Batch **5C**, sub-stream **5C.E**). Android ViewModels use
flag-bag UI state and loose one-shot action booleans instead of the §E canon in `agents/knowledge/consistency.md`:

- **E1 (sealed UiState):** partner `features/auth/viewmodels/LoginViewModel.kt`,
  `features/orders/viewmodels/OrderDetailsViewModel.kt`, `features/earnings/viewmodels/EarningsSummaryViewModel.kt`,
  `features/dashboard/viewmodels/DashboardViewModel.kt` use flag-bag `data class …UiState` → sealed interface
  `Loading`/`Error`/`Loaded`.
- **E2 (shared ActionState + effect):** customer `features/disputes/CreateDisputeViewModel.kt`,
  `features/membership/MembershipViewModel.kt`, `features/profile/ProfileViewModel.kt` use loose
  `_submitting`/`_error`; partner uses an `enum OrderAction inFlight` → standardize on the **existing** shared
  `ActionState` (`customer-app/.../ui/state/ActionState.kt`) + a `SharedFlow(replay=0)` success effect.

**This is a refactor, NOT a behavior change** — same Loading→Loaded→Error sequence and one-shot submit
success/error path; impossible flag combinations become unrepresentable.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST)** — A ViewModel test (or, if no Android harness exists for a given VM, a recorded
  state-sequence reviewed against the screen) pins the current Loading→Loaded→Error sequence and the one-shot
  submit success/error path, **before** the refactor (per `testing.md`; status log shows it first).
- [ ] **AC2 (E1 canonical form)** — The four partner ViewModels convert their flag-bag `…UiState` data classes
  to a sealed `…UiState` (`Loading`/`Error`/`Loaded`).
- [ ] **AC3 (E2 canonical form)** — The three customer ViewModels + the partner `OrderAction inFlight` use the
  **existing** shared `ActionState` + a `SharedFlow(replay=0)` success effect (no parallel/duplicate type
  introduced).
- [ ] **AC4 (behavior identical)** — The rendered sequence is unchanged for the same inputs; impossible flag
  combinations are no longer representable.
- [ ] **AC5 (consistency gate)** — `node agents/tools/check-consistency.mjs --paths=<each touched dir>` reports
  zero E1/E2 violations for the touched files; global baseline drops by the count cleared.
- [ ] **AC6** — The touched Android module(s) build + tests green; Reviewer confirms refactor-only.

## Out of scope
- A* paged-query, B1 Response-wrap, B3 validator-base, C* facades (sibling 5C children).
- E6 `RecurringBookingsScreen` `collectAsState` (separate small ticket).
- Any feature behavior, new screens, or API change.

## Implementation notes
- **Canonical forms:** `knowledge/consistency.md` §E (E1, E2); samples in `knowledge/patterns-mobile.md`.
- **Reuse, don't recreate:** the shared `ActionState` already exists at `customer-app/.../ui/state/ActionState.kt`
  — do not introduce a parallel type. For partner, replace `enum OrderAction inFlight` with `ActionState` +
  `SharedFlow`.
- **No backend/NSwag/migration** → none flagged. Mobile-only, no API surface change.
- **Shared-file lane:** disjoint ViewModel files across partner/customer modules — no overlap with the other 5C
  children. Run concurrently. (If both the partner and customer apps share the `:core` `ActionState` file and a
  change is needed there, serialize that single file edit.)

## Status log
- 2026-06-13 — ready (created by pm — split of T-0196, Batch 5C sub-stream E1/E2). DoR met: AC observable,
  sized M, no deps, no migration/regen, refactor-only, reuses existing `ActionState`. Reviewer-per-developer.
- 2026-06-13 — review (android). **E2 customer refactor (AC1/AC3/AC4/AC5) done test-first.**
  - **Test-first (AC1):** wrote `CreateDisputeViewModelTest`, `ProfileViewModelTest`,
    `MembershipViewModelTest` against the target `ActionState` API first; pin Loading→submit
    success/error sequence, re-entry guard, and the one-shot `createdDisputeId`/callback effects.
  - **E2 (AC3):** customer `CreateDisputeViewModel` (`_submitting`+`_error` → `submitState: StateFlow<ActionState>`
    + existing `createdDisputeId` SharedFlow), `ProfileViewModel` (`_loading`→`refreshState`,
    `_savingProfile`→`saveState`), `MembershipViewModel` (`_submitting`→`submitState`). All use the
    **existing** `cz.cleansia.customer.ui.state.ActionState` — no parallel type introduced. Consuming
    screens derive `submitting`/`error` from the sealed state (`CreateDisputeScreen`, `SubscribePlusScreen`,
    `MembershipManagementCard`, `CleansiaNavHost` edit/onboarding wiring).
  - **AC4:** behavior preserved — same Submitting→Idle/Error sequence, same snackbar + effect/callback
    paths; the only added re-entry guard fires in a race the screens already gate (button disabled),
    so the rendered sequence is unchanged. Impossible flag combos (submitting+error) now unrepresentable.
  - **AC5 gate:** `check-consistency.mjs` on the three touched dirs → **0 E1/E2 violations**. The 6
    remaining hits are all **E6** (`collectAsState`) in the membership screens — explicitly out of scope
    (separate ticket); these `collectAsState` calls pre-existed and were left untouched to stay surgical.
  - **Test evidence:** `:customer-app:testDebugUnitTest` for the three new VM test classes → **BUILD
    SUCCESSFUL** (module compiles incl. modified screens/nav). Verified `mockDebug` n/a — customer-app has
    no flavor dimension; ran `testDebugUnitTest`.
  - **AC2 (partner E1) + partner half of AC3:** the four partner VMs (`LoginViewModel`,
    `OrderDetailsViewModel`, `EarningsSummaryViewModel`, `DashboardViewModel`) were already converted to
    sealed `*UiState` + shared `cz.cleansia.core.ui.state.ActionState` on this branch (uncommitted), with
    partner test files present. I did **not** re-touch them (out of my authored scope this pass).
  - **DEVIATION / FINDING (for the orchestrator):** `:partner-app:testDebugUnitTest` is **RED** on the
    pre-existing partner WIP — 5 failures NOT introduced here (`:partner-app` does not depend on
    `:customer-app`, so my customer-only edits cannot affect it):
    (a) `LoginViewModelTest` ×4 NPE — `LoginViewModel.login()` calls `android.util.Patterns.EMAIL_ADDRESS`
    which is `null` under plain JVM unit tests (needs Robolectric or a non-`android.util` email check);
    (b) `DashboardViewModelTest` "user pull surfaces isUserRefreshing" assertion at line 113.
    These are partner-test-harness/assertion issues in already-present uncommitted files; flagging for a
    follow-up rather than fixing here per "touch only files your ticket owns / never revert unrelated
    uncommitted changes."
  - **MANUAL_STEPs:** none (mobile-only, no API/DTO/migration/i18n change — reused existing string keys).

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
