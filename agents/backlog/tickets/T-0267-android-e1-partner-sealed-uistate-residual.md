---
id: T-0267
title: "E1 — convert residual partner-app flag-bag *UiState to sealed states (the ones T-0252 did not name)"
status: done
size: M
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: [T-0266]
blocks: [T-0269]
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
source: audits/consistency-violations.md F13 (E1); knowledge/consistency.md §E1; reconciled against T-0252 (Wave 5)
---

## Context

Residual Android consistency rule **E1** (sealed `*UiState` = `Loading`/`Error`/`Loaded`, never a
flag-bag `data class` with `isLoading`/`error` booleans that permits impossible states). Rule:
`knowledge/consistency.md` §E1; finding: `audits/consistency-violations.md` F13.

**Reconciliation with T-0252 (Wave 5) — done first, this is NOT a redo.** T-0252 already converted the
**four audit-named** partner VMs — `DashboardViewModel`, `EarningsSummaryViewModel`,
`OrderDetailsViewModel` (sealed `*UiState`, verified present on `master`) — and `LoginViewModel` is
already canonical (`LoginFormState` form-holder + shared `ActionState` + `SharedFlow`, verified — the
audit's "Login is a flag-bag" line is **stale**). **E2 (customer `CreateDispute`/`Membership`/`Profile`
+ partner `OrderAction inFlight`) is fully done by T-0252 and verified on `master` — there is NO E2
work left; this ticket is E1-only.**

What **remains** are the partner `data class *UiState` flag-bags T-0252 did not name. The mechanical
tool (`check-consistency.mjs`) flags **every** `data class *UiState`, but **not all are violations** —
the §E1 judgment call is that a legitimate *form-field holder* data class is fine; only a *page-state*
flag-bag (load/error/loaded modelled as booleans, permitting impossible combos) is the defect. The
dev applies that filter:

**Convert (genuine page-state flag-bags):**
- `invoices/.../InvoiceDetailsViewModel` `InvoiceDetailsUiState` (`isLoading`/`invoice`/`error` — the
  classic 3-state flag-bag) → sealed Loading/Error/Loaded (download-effect state moves to an
  `ActionState`/effect, matching §E2, not a new page-state boolean).
- `orders/.../OrderPhotosViewModel` `OrderPhotosUiState` (`isLoading`/`photos`/`error`) → sealed
  page-state; the `isUploading`/`deletingId`/`mutationVersion` action-effect fields stay as an
  action channel (not folded into the sealed page-state).

**Do NOT convert (documented design / judgment-call NON-violations — record the rationale, do not
churn):**
- The dual-spinner list VMs `OrdersListViewModel`, `InvoicesListViewModel`, `RegistrationLockViewModel`
  — their `isUserRefreshing`/`isBackgroundRefreshing`/`hasLoadedOnce` split is **intentional,
  heavily-documented pull-to-refresh design** that does NOT cleanly fold into Loading/Error/Loaded
  (the comments in-file explain why the pull indicator must not subscribe to a generic `isLoading`).
- The partner **form-section** VMs (`PersonalSection`, `BankSection`, `Emergency`, `Identification`,
  `Documents`, `AddressSection`, `Profile`, `Register`, `ForgotPassword`, `ConfirmEmail`,
  `Settings`) — these `*UiState` are **form-field/value holders** (the §E1 form-state exception), not
  page-state flag-bags.
- `OrderNotesViewModel` — its `UiState` is an **action-effect** holder (`isSavingNote`,
  `noteSaved`, `mutationVersion`), an §E2 concern, not §E1 page-state.

This is a **refactor, not a behavior change** — same Loading→Loaded→Error rendering; impossible flag
combinations become unrepresentable.

## Acceptance criteria

- [ ] **AC1 (characterization-first, per `testing.md`)** — For each VM converted, a ViewModel test
  (or, if no harness exists for that VM, a recorded state-sequence reviewed against the screen) pins
  the current Loading→Loaded→Error sequence **before** the refactor (status log shows red→green /
  test-first ordering).
- [ ] **AC2 (E1 canonical form on the in-scope VMs)** — `InvoiceDetailsViewModel` and
  `OrderPhotosViewModel` convert their page-state flag-bag `*UiState` to a sealed
  `*UiState` (`Loading`/`Error`/`Loaded`); their consuming screens derive what they render from the
  sealed state.
- [ ] **AC3 (judgment-call list recorded)** — The dev records, in the status log, the explicit
  not-converted list above with the one-line rationale per VM (documented design / form-state
  exception / §E2-not-§E1), so the reviewer can confirm the scope boundary rather than re-litigate it.
- [ ] **AC4 (behavior identical)** — Rendered sequence unchanged for the same inputs; impossible flag
  combinations (e.g. `isLoading && error != null && invoice != null`) are no longer representable on
  the converted VMs.
- [ ] **AC5 (consistency gate)** — `node agents/tools/check-consistency.mjs --paths=<each touched dir>`
  shows the converted files no longer emit an E1 hit; for any `data class *UiState` deliberately kept
  (the judgment-call NON-violations), the reviewer notes it is a known/accepted form-state or
  action-effect case, not a new violation (the tool flags them mechanically; AC3's recorded rationale
  is the disposition).
- [ ] **AC6 (suite green / compiles)** — partner-app builds and `:partner-app:testDebugUnitTest` is
  green on plain JVM; reviewer confirms refactor-only.

## Out of scope

- E2 (`ActionState`/effect) — **already done by T-0252; no E2 work here.** (If converting
  `InvoiceDetails` download state cleanly lands on `ActionState`, that is incidental reuse of the
  existing shared type, not new E2 scope.)
- E7 dir/naming (T-0266, runs first) and E6 collectAsState (T-0269, runs after).
- The dual-spinner list VMs and form-section VMs (judgment-call NON-violations above) — explicitly NOT
  converted.
- Any feature behavior, new screens, or API change.

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E1; samples in `knowledge/patterns-mobile.md`.
- **Reuse, don't recreate:** the shared `cz.cleansia.core.ui.state.ActionState` already exists (used by
  the T-0252 work) — if the InvoiceDetails download-effect needs an action channel, reuse it; do not
  introduce a parallel type.
- **Depends on T-0266 (E7):** this ticket edits the SAME partner screen/VM files E7 moves. It runs
  **after** E7 in the partner-files lane so the file paths/packages are settled (see sprint-9
  execution order). It must complete **before** T-0269 (E6) touches the same screens.
- **No `manual_steps`** — mobile-only, no API/DTO/migration/i18n change (reuses existing string keys).

## Status log
- 2026-06-21 — ready (created by pm). Wave 7 (Android consistency debt). DoR met: AC observable +
  characterization-first, sized M, deps = T-0266 (same-files lane order), mobile-only (no
  migration/regen), refactor-only, reuses existing `ActionState`. **Reconciled against T-0252** — the
  four audit-named VMs + all E2 are already done; this is the un-named residual only, scoped by the
  §E1 form-state/page-state judgment call. **No new behavior/decision → no deliberation panel**
  (canonicalization against the ratified §E1 rule; the only judgment is which existing data classes
  are genuine flag-bags, which the dev records per AC3). Reviewer-per-developer.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
