# Audit — Consistency Violations (cross-stack)

- **Auditor:** architect + reviewers (variance analysis)
- **Date:** 2026-06-01
- **Scope:** recurring archetypes across backend (paged queries, commands), frontend (list/form
  features), Android (ViewModels/Screens/Repositories).
- **Method:** line-by-line comparison of many real instances of each archetype; canonical form fixed
  in [`../../knowledge/consistency.md`](../../knowledge/consistency.md); deviations recorded here.

## Summary
The architecture is sound and most features conform. The divergences below are where "the same
operation" was written differently. Each maps to a `consistency.md` rule and is directly
convertible to a canonicalization ticket. Severity reflects user/maintenance/correctness impact,
not effort. **None of these block launch on their own**, but they are the spaghetti you asked to
remove before PROD.

---

## Backend — paged queries

### F1 — `record Query` instead of `Request : DataRangeRequest` [major] [type: spaghetti]
- **Where:** `Features/PromoCodes/GetPagedPromoCodes.cs`, `Features/Referrals/GetPagedReferrals.cs`
- **What:** Use a `record Query` with inline `Offset`/`Limit`, a bespoke `repo.GetPagedAdminAsync(...)`
  (no Specification), manual `PageNumber` math, and hand-built `PagedData<T>`.
- **Rule:** A1, A2, A3, A5. **Fix:** convert to `class Request : DataRangeRequest, IRequest<PagedData<T>>`
  + `XxxSpecification` + `GetPagedSort<XxxSort>` + `MapToDto(total, request)`.
- **Proposed ticket:** `Canonicalize GetPagedPromoCodes + GetPagedReferrals to the paged-query pattern` · M · [backend]

### F2 — `Filter { get; set; }` instead of `init` [minor] [type: spaghetti]
- **Where:** `Features/PayConfig/GetPagedPayConfigs.cs:19`
- **Rule:** A7. **Fix:** `{ get; init; }`.
- **Proposed ticket:** `Make GetPagedPayConfigs.Filter init-only` · S · [backend]

### F3 — projection/Include ordering drift [minor] [type: spaghetti/perf]
- **Where:** `Features/Services/GetPagedServices.cs` (`AsNoTracking` before `Include`; projects after
  materialization).
- **Rule:** A6. **Fix:** `Include → AsNoTracking → Select(MapToDto) → ToListAsync`.
- **Proposed ticket:** `Align GetPagedServices to canonical read-path order` · S · [backend]

## Backend — commands

### F4 — commands not returning `ICommand<Response>` [major] [type: spaghetti]
- **Where:** `Features/Disputes/CreateDispute.cs` (`ICommand<string>`),
  `Features/Disputes/UpdateDisputeStatus.cs` (`ICommand`), `Features/SavedAddresses/DeleteSavedAddress.cs` (`ICommand`).
- **Rule:** B1. **Fix:** wrap output in a `record Response(...)`.
- **Proposed ticket:** `Give CreateDispute/UpdateDisputeStatus/DeleteSavedAddress a Response record` · S · [backend]

### F5 — ownership/session checks in the Validator [major] [type: security/spaghetti]
- **Where:** `Features/Employees/UpdateEmployee.cs`, `Features/Users/UpdateCurrentUser.cs`,
  `Features/SavedAddresses/UpdateSavedAddress.cs` + `DeleteSavedAddress.cs` (each calls
  `IUserSessionProvider` in the validator to check ownership).
- **Rule:** B4 (+ S3). **Fix:** move ownership to the handler; validator validates shape only.
- **Proposed ticket:** `Move ownership checks from validators to handlers (4 features)` · M · [backend, security]

### F6 — custom validator base classes [minor] [type: spaghetti]
- **Where:** `Features/PayConfig/Create+UpdatePayConfig.cs`, `Features/PayPeriods/Create+UpdatePayPeriod.cs`
  (`UserEmailValidator<Command>`), `UpdateEmployee.cs`/`UpdateCurrentUser.cs` (`BaseUserValidator<Command>`).
- **Rule:** B3. **Fix:** inherit `AbstractValidator<Command>`; compose shared rules via `.SetValidator(...)`.
- **Proposed ticket:** `Refactor validators to AbstractValidator + composed shared rules` · M · [backend]

### F7 — `Error` first-arg is `nameof(Command)` not a field [minor] [type: spaghetti]
- **Where:** `Features/Memberships/CreateMembershipSubscription.cs:46`
- **Rule:** B5. **Fix:** `new Error(nameof(command.<Field>), ...)`.
- **Proposed ticket:** `Fix Error field name in CreateMembershipSubscription` · S · [backend]

### F8 — missing idempotency / provider try-catch on side-effecting commands [major] [type: bug-risk]
- **Where:** `Features/Memberships/CreateMembershipSubscription.cs` (Stripe call, no try/catch),
  `Features/Orders/CreateOrder.cs` (Stripe try/catch but no idempotency guard).
- **Rule:** B8 (+ S7). **Fix:** narrow provider try/catch + idempotency check.
- **Proposed ticket:** `Add idempotency + provider error handling to membership/order create` · M · [backend, security]

### F9 — hard-delete where soft-delete is correct [major] [type: data-loss-risk]
- **Where:** all `Delete*` commands currently call `repo.Remove(entity)`; review each for whether the
  entity carries history/audit/GDPR significance and should `repo.Deactivate(entity)` instead.
- **Rule:** B6. **Fix:** convert business/user-facing deletes to soft-delete; keep hard-delete only for
  true join/scratch rows. **(Architect-owned decision — needs an ADR before sweeping.)**
- **Proposed ticket:** `ADR + sweep: soft-delete for business entities` · L (split) · [architect, backend, db]

## Frontend

### F10 — non-`UnsubscribeControlDirective` cleanup [major] [type: spaghetti/leak-risk]
- **Where:** `cleansia-customer-features/order-wizard` (`DestroyRef`/`takeUntilDestroyed`),
  `…/recurring-bookings` (`firstValueFrom`, no cleanup), `…/rewards`.
- **Rule:** C1. **Fix:** extend `UnsubscribeControlDirective` + `takeUntil(this.destroyed$)`.
- **Proposed ticket:** `Unify customer-feature facades on UnsubscribeControlDirective` · M · [frontend]

### F11 — list facade signal/pipe drift [minor] [type: spaghetti]
- **Where:** `fiscal-failures-list.facade` (no `totalRecords`), `invoices` partner (inline loading
  reset instead of `finalize`), partner `orders` + customer `disputes` (NgRx mixed into a feature facade).
- **Rule:** C2, C3, C8. **Fix:** add missing signals; use `finalize`; keep feature state in signals.
- **Proposed ticket:** `Normalize list facades (signals, finalize, no stray NgRx)` · M · [frontend]

### F12 — split table definition + form-builder mixing [minor] [type: spaghetti]
- **Where:** `fiscal-failures-list.models` (split `getColumns`+`getActions`), `package-form` (mixes
  `fb.group` + `fb.nonNullable.group`).
- **Rule:** C6, D2. **Fix:** unify into `getXxxTableDefinition()`; isolate/justify any nullable subgroup.
- **Proposed ticket:** `Unify fiscal-failures table def + package-form builder` · S · [frontend]

## Android

### F13 — flag-bag UiState instead of sealed state [major] [type: bug-risk]
- **Where:** partner `LoginViewModel`, `OrderDetailsViewModel`, `EarningsSummaryViewModel`,
  `DashboardViewModel` (single `data class` with multiple booleans → impossible states possible).
- **Rule:** E1. **Fix:** sealed `*UiState` (Loading/Error/Loaded).
- **Proposed ticket:** `Convert partner-app flag-bag UiStates to sealed states` · M · [android]

### F14 — loose action booleans instead of `ActionState` [minor] [type: spaghetti]
- **Where:** customer `CreateDisputeViewModel`, `MembershipViewModel`, `ProfileViewModel` (loose
  `_submitting`/`_error`); partner uses `enum OrderAction inFlight`.
- **Rule:** E2. **Fix:** shared `ActionState` + `SharedFlow` effect.
- **Proposed ticket:** `Standardize one-shot actions on ActionState` · M · [android]
- **CLEARED — done by T-0252 (Wave 5), verified by T-0268 (Wave 7, 2026-06-21).** The three named
  customer VMs use `cz.cleansia.customer.ui.state.ActionState` + `SharedFlow`/callback effects (no
  loose `_submitting`/`_error`); partner `OrderDetailsViewModel` is on `cz.cleansia.core.ui.state.ActionState`
  (`actionState`), with the retained `enum OrderAction`/`inFlightAction` now a per-button spinner
  discriminator layered on top (§E2 judgment-call non-issue). Gate: `check-consistency.mjs` mobile →
  **0 E2 violations**; the three T-0252 E2 VM tests green (`CreateDisputeViewModelTest` 8/8,
  `MembershipViewModelTest` 10/10, `ProfileViewModelTest` 9/9). **F14 closed.**
- **NOTE (separate follow-up, NOT F14):** three one-shot ACTION paths shipped AFTER this audit still
  use a loose `_submitting`-style boolean (genuine but out of F14's named set, surfaced by T-0268):
  `recurring/CreateRecurringViewModel` (`_submitting` + `_submitOutcome` StateFlow),
  `disputes/DisputeDetailViewModel` (`_sending`, `_uploadingEvidence`),
  `profile/DeleteAccountViewModel` (`_loading` gating `deleteAccount()`). Recommend a scoped
  `Standardize post-Wave-5 one-shot actions on ActionState` follow-up — not folded into F14.

### F15 — `collectAsState()` instead of lifecycle-aware [major] [type: bug/leak]
- **Where:** `customer/features/recurring/RecurringBookingsScreen.kt:77`
- **Rule:** E6. **Fix:** `collectAsStateWithLifecycle()`.
- **Proposed ticket:** `Fix RecurringBookingsScreen state collection (lifecycle)` · S · [android]

### F16 — divergent repo contract (`T?` vs `ApiResult<T>`) + dir/naming split [major] [type: spaghetti]
- **Where:** customer-app repos returned `T?` (snackbar in repo); partner-app returns `ApiResult<T>`
  (snackbar in VM). Customer features inline; partner splits `screens/`+`viewmodels/` with `Details` drift.
- **Rule:** E5, E7. **Fix:** canonicalize on `ApiResult<T>` + inline singular structure.
  **(Cross-cutting — Architect-owned; ADR-0011 accepted.)**
- **E5 — RESOLVED for customer-app (T-0197, closed 2026-06-17, commits dca897e1 + 7f391fdb).** ADR-0011
  accepted; `ApiResult`/`ApiError`/`safeApiCall` hoisted into `:core` (`cz.cleansia.core.network`);
  **all 15 customer-app repos migrated to `ApiResult<T>` with the snackbar moved repo → VM**
  (catalog, data/address, devices, notifications, payments, auth, orders, disputes, memberships, loyalty,
  referral, recurring, user, settings, + the remaining repo). Orchestrator-verified: `:core` + partner-app
  + customer-app all compile, customer-app 201/201 unit tests pass, **`check-consistency mobile` reports
  ZERO E5 violations for customer-app**, all 64 changed files encoding-clean. **E5 closed.**
- **STILL OPEN — separate rules, NOT resolved by T-0197 (their own future tickets):**
  - **E1/E2 (sealed `*UiState` + shared `ActionState`)** — see F13/F14 below; T-0197 deliberately did not
    touch UiState shape (its scope was only the repo → VM error channel).
  - **E6 (`collectAsStateWithLifecycle()`)** — 22 instances of `collectAsState()` remain across the mobile
    screens; see F15.
  - **E7 (dir/naming — inline-singular `features/<name>/` convention)** — still its own ticket.
- **Proposed ticket:** ~~`ADR + migrate customer-app repos to ApiResult<T> and unify mobile structure`~~
  **DONE for the E5/ApiResult half (T-0197); E7 structure unification remains a separate ticket.**

---

## Not-issues (intentional — do not re-flag)
- `GetPagedOrders` materialize-then-map loop: required for per-row pay estimation with pre-loaded
  configs (A6 documented exception).
- Handler fetch-and-guard (`if (x is null) return Failure`) on Update/Delete: the canonical guard at
  point-of-use, **not** redundant validation (B4).
- Customer-app vs partner-app notification storage difference (Room in partner only): intentional.
