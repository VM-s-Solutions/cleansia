---
id: T-0269
title: "E6 — collectAsStateWithLifecycle() sweep over screen/VM-flow collections (both apps; filtered real violations)"
status: ready
size: M
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: [T-0266, T-0267]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
source: audits/consistency-violations.md F15 (E6); knowledge/consistency.md §E6
---

## Context

Residual Android consistency rule **E6**: a screen that collects a **ViewModel-owned, lifecycle-bound
`StateFlow` inside a composable** must use **`collectAsStateWithLifecycle()`**, not `collectAsState()`
(plain `collectAsState` keeps collecting while the screen is in the background → wasted work / stale
recomposition / the F15 lifecycle bug). Rule: `knowledge/consistency.md` §E6; finding:
`audits/consistency-violations.md` F15 (which named only `RecurringBookingsScreen`).

### FILTERED real-violation count (the headline finding — raw 85 is misleading)

A raw `grep collectAsState()` finds **85 occurrences across 36 files** (matches the audit's raw
number). The audit *scoped* "~22 across the mobile screens" — **both are wrong for current `master`**:
the audit predates the Wave-3 Android build-out (T-0188 devices, T-0192, the partner profile-section
screens), so 22 **undercounts**. Filtering the raw 85 to **only screen/composable collections of a
VM-owned, lifecycle-bound flow** gives the real number:

- **TRUE E6 violations ≈ 56 occurrences across ~30 files** (the in-scope set below).
- **NON-violations excluded ≈ 29:**
  - **`@Singleton` repository StateFlows** collected in screens (`loyaltyRepo`/`referralRepo` in
    `RewardsTab`, `orderRepo` in `OrdersTab`, `catalogRepo` in `ServicesStep`/`ConfirmStep`,
    `membership.current`/`membershipRepository.current` where the receiver is the app-scoped repo) —
    these flows live for the **app** lifetime, not the screen's, so plain `collectAsState()` is
    **correct** (verified the repos are `@Singleton`). **Not violations.**
  - **NavHost-level collections** (`CleansiaNavHost` ×9, `PartnerNavHost` ×1) — collected at the
    nav-graph composable, a separate pattern; **excluded** (not a screen-body VM collection).
  - **`:core` infra** (`GlobalSnackbarHost` `SnackbarInsetState.insetDp`) — non-screen platform
    plumbing; **excluded.**

**Note on the mechanical tool:** `check-consistency.mjs`'s E6 regex only matches when the receiver is
literally named `viewModel` or `vm` (`\b(viewModel|vm)\.…collectAsState()`). It therefore **misses**
the many real violations whose receiver is `bookingVm`, `chainViewModel`, `settingsViewModel`,
`checklistViewModel`, `profileVm`, `viewModel.uiState` chained differently, etc. So "tool-clean" is
**necessary but not sufficient** — this ticket fixes the full *conceptual* screen/VM-flow set, not
only what the tool's narrow regex catches. (A follow-up to widen the tool's E6 receiver match may be
filed; not in scope here.)

### In-scope violation file list (screen/sheet/feature-component VM-flow collections → convert)

**customer-app (22 occ / 9 files):**
- `features/recurring/RecurringBookingsScreen.kt` (4 — the original F15: templates/loading/loaded/mutating)
- `features/recurring/CreateRecurringScreen.kt` (3 — state/submitting/submitOutcome)
- `features/booking/BookingBottomSheet.kt` (4 — bookingVm: state/submitState/quoteState/promoCodeState)
- `features/booking/ConfirmStep.kt` (2 — bookingVm: quoteState/promoState; the catalogRepo ones are repo-flow, excluded)
- `features/membership/MembershipManagementCard.kt` (3 — current/plans/submitState)
- `features/membership/SubscribePlusScreen.kt` (3 — submitState/current/plans)
- `features/main/MainShell.kt` (1 — profileVm.currentUser)
- `features/profile/PlusRecurringEntryRow.kt` (1 — membership VM flow)  ← if its receiver is the @Singleton membership repo, RE-CLASSIFY as non-violation at dispatch
- `features/booking/PreferredCleanerPicker.kt` (1 — membership VM flow)  ← same re-classify check

**partner-app (≈34 occ / ≈21 files) — paths AFTER the T-0266 E7 move (files collapse to `features/<name>/`):**
- `features/settings/…/LanguagePickerScreen.kt` (2), `features/earnings/…/EarningsSummaryScreen.kt` (1),
  `features/dashboard/…/DashboardScreen.kt` (3), `features/auth/…/RegisterScreen.kt` (1),
  `features/auth/…/LoginScreen.kt` (2), `features/invoices/…/InvoicesListScreen.kt` (1),
  `features/invoices/…/InvoiceDetailsScreen.kt` (1), `features/auth/…/ForgotPasswordScreen.kt` (1),
  `features/auth/…/ConfirmEmailScreen.kt` (1), `features/orders/…/RegistrationLockScreen.kt` (1),
  `features/orders/…/OrdersListScreen.kt` (1), `features/orders/…/OrderDetailsScreen.kt` (3),
  `features/profile/…/ProfileScreen.kt` (3), `features/profile/…/IdentificationSectionScreen.kt` (2),
  `features/profile/…/BankSectionScreen.kt` (2), `features/profile/…/EmergencySectionScreen.kt` (1),
  `features/profile/…/DocumentsSectionScreen.kt` (1), `features/profile/…/PersonalSectionScreen.kt` (2),
  `features/profile/…/AddressSectionScreen.kt` (2),
  `features/orders/…/NotesAndIssuesSection.kt` (2), `features/orders/…/PhotosSection.kt` (1).

(`NotesAndIssuesSection`/`PhotosSection` live under `components/` but collect a VM flow → in scope.
The exact occurrence count is the dev's to confirm at execution against the post-E7 tree; the rule is
"VM-owned lifecycle flow in a composable → `collectAsStateWithLifecycle()`".)

## Acceptance criteria

- [ ] **AC1 (filter applied, list confirmed)** — The dev re-runs the raw `collectAsState()` scan on the
  post-T-0266 tree, applies the filter (VM-owned lifecycle flow in a composable IN; `@Singleton`
  repo flows, NavHost collections, `:core` infra OUT), and records the confirmed final violation list
  + count in the status log. The two `PlusRecurringEntryRow`/`PreferredCleanerPicker` receivers are
  re-classified explicitly (VM-flow → convert; @Singleton repo → exclude).
- [ ] **AC2 (sweep)** — Every confirmed in-scope `collectAsState()` is changed to
  `collectAsStateWithLifecycle()` (with the `androidx.lifecycle.compose.collectAsStateWithLifecycle`
  import added where missing).
- [ ] **AC3 (excluded set left alone, with rationale)** — The repo-`@Singleton`-flow, NavHost, and
  `:core` collections are **not** changed; the status log records why (app-scoped flow / nav-graph
  pattern / non-screen infra), so the reviewer confirms the boundary.
- [ ] **AC4 (behavior preserved)** — No state shape changes; the screens render the same data. This is a
  mechanical collection-API swap. Where a VM has a stateful `XxxScreen`/stateless `XxxScreenContent`
  split (§E6 second clause), the dev notes whether the split already holds; this ticket does **not**
  introduce new screen splits (that would be its own refactor) — it swaps the collection API only.
- [ ] **AC5 (consistency gate)** — `node agents/tools/check-consistency.mjs --paths=src/cleansia_android`
  reports **zero** `viewModel.`/`vm.`-receiver E6 hits; the dev additionally confirms by re-grep that
  no in-scope screen-body VM collection still uses plain `collectAsState()` (covering the
  receiver-name cases the tool's regex misses — see Context).
- [ ] **AC6 (both apps compile + suites green)** — customer-app and partner-app both build and their
  unit suites are green on plain JVM; reviewer confirms swap-only diff.

## Out of scope

- E1 (T-0267), E2 (T-0268), E7 (T-0266).
- Introducing new `XxxScreen`/`XxxScreenContent` stateful/stateless splits (the §E6 second clause) —
  collection-API swap only this ticket.
- Changing the `@Singleton` repo-flow / NavHost / `:core` collections (deliberately correct as plain
  `collectAsState()`).
- Widening the `check-consistency.mjs` E6 receiver regex (possible separate follow-up).

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E6; `import androidx.lifecycle.compose.collectAsStateWithLifecycle`.
- **Runs LAST in the partner-files lane** (`depends_on: [T-0266, T-0267]`): E6 edits the SAME partner
  screens that E7 (T-0266) moves and E1 (T-0267) refactors — so it sweeps over the **settled** files
  after both, avoiding same-file collisions (see sprint-9 execution order). The customer-app E6 files
  do **not** overlap T-0266/T-0267 (those are partner-only), so the customer half of this sweep could
  run earlier — but kept as one ticket for a single clean gate; the dev may do the customer files
  first while the partner lane settles.
- **Characterization-first (`testing.md`):** the swap is behavior-preserving; where a VM test exists,
  it stays green across the swap (the regression net). No new logic to TDD — the evidence is "suite
  green before and after + grep-clean".
- **No `manual_steps`** — mobile-only; no nswag-regen, no ef-migration.

## Status log
- 2026-06-21 — ready (created by pm). Wave 7 (Android consistency debt). DoR met: AC observable,
  sized M (mechanical sweep with an explicit filtered file list — if the confirmed count regrows past
  M at dispatch, split customer/partner), deps = T-0266 + T-0267 (partner same-files lane order),
  mobile-only (no migration/regen), behavior-preserving. **FILTERED real count ≈ 56 occ / ~30 files**
  (raw 85 / 36; excluded ≈29 = @Singleton repo flows + NavHost + :core infra). **No new
  behavior/decision → no deliberation panel** (mechanical canonicalization against the ratified §E6
  rule; one-line no-decision note). Reviewer-per-developer.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
