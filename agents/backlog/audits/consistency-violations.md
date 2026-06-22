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

> **STATUS (2026-06-21) — the consistency sweep is essentially COMPLETE.** Backend F1–F8 + frontend
> F10–F12 were canonicalized across Waves 1–5 (T-0196 epic and its children + the long tail). The
> Android §E rules are now all resolved: **E5/ApiResult (F16) — T-0197**; **E1 (F13) — T-0252 + T-0267**;
> **E2 (F14) — T-0252, verified by T-0268**; **E6 (F15) — T-0269**; **E7 (F16) — T-0266**. The only
> carried item is a **SMALL E2 residual — T-0270** (3 one-shot-action VMs that postdate this audit and
> T-0252; behavior-correct today, queued for a canonicalization follow-up). F9 (soft-delete) shipped as
> its own architect-owned ADR+sweep in Wave 1. See each finding below for the per-rule disposition.

---

## Backend — paged queries

### F1 — `record Query` instead of `Request : DataRangeRequest` [major] [type: spaghetti]
- **Where:** `Features/PromoCodes/GetPagedPromoCodes.cs`, `Features/Referrals/GetPagedReferrals.cs`
- **What:** Use a `record Query` with inline `Offset`/`Limit`, a bespoke `repo.GetPagedAdminAsync(...)`
  (no Specification), manual `PageNumber` math, and hand-built `PagedData<T>`.
- **Rule:** A1, A2, A3, A5. **Fix:** convert to `class Request : DataRangeRequest, IRequest<PagedData<T>>`
  + `XxxSpecification` + `GetPagedSort<XxxSort>` + `MapToDto(total, request)`.
- **Proposed ticket:** `Canonicalize GetPagedPromoCodes + GetPagedReferrals to the paged-query pattern` · M · [backend]
- **STATUS:** GetPagedPromoCodes + GetPagedReferrals **canonicalized** in Wave 5 (T-0248). **F1 reopened
  at Wave-8 intake (2026-06-22):** see F1b below — the F1-shape (`record Query` / hand-built `PagedData`)
  recurs in a **further 6 paged offenders** that `check-consistency.mjs` flags (A1/A5) but were never
  ticketed. The "sweep complete" claim in the Summary banner was **stale** for these.

### F1b — RESIDUAL F1-shape paged offenders the tool flags but were never ticketed [major] [type: spaghetti] — **Wave-8 T-0273**
- **Meta-finding (the lesson):** `check-consistency.mjs` already catches every one of these via the
  **existing** A1/A5 rules — the gap was never a missing rule, it was that the findings were never
  converted to tickets, and this doc claimed the backend paged sweep complete. No new rule is needed
  (adding one would duplicate A1/A5); the fix is to **ticket the offenders + de-stale this doc**.
- **Where (verified on master 2026-06-22 via `check-consistency.mjs --paths=src/Cleansia.Core.AppServices/Features`):**
  - `Features/Referrals/GetMyReferrals.cs:11,32` — A1, A5 (admin twin `Admin/GetPagedReferrals.cs` is canonical)
  - `Features/Loyalty/GetLoyaltyActivity.cs:12,36` — A1, A5 (no Spec/Sort yet)
  - `Features/Loyalty/Admin/GetUserLoyaltyActivity.cs:17,40` — A1, A5 (shares the loyalty repo method)
  - `Features/PromoCodes/Admin/GetPromoCodeRedemptions.cs:15,26` — A1, A5 (no Spec/Sort yet)
  - `Features/Memberships/Admin/GetPagedMembershipPlans.cs:20,38` — A1, A5 **(missed by AUDIT-2026-06-22; caught by the tool)**
  - `Features/EmployeeDocuments/GetEmployeeDocuments.cs:59` — A5 only (canonical spec path, hand-built `PagedData`)
- **Not an offender (reconciled):** `Features/Disputes/GetPagedDisputes.cs` is **canonical A1–A8** —
  do NOT touch it (the earlier quick-classify that flagged it is refuted; tool + re-read confirm clean).
- **Rule:** A1, A5 (A2 public-Handler on some). **Fix:** convert each to
  `class Request : DataRangeRequest` + `XxxSpecification` + `GetPagedSort<XxxSort>` + `MapToDto(total, request)`.
- **Ticket:** **T-0273** (Wave 8) — 7 live query files (the 6 lines above = 7 queries since the loyalty
  pair shares one new Spec/Sort). **DISPOSITION: ticketed; this doc will be marked resolved when T-0273
  lands and the tool reports zero A1/A5 for these files.**

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
- **RESOLVED (E1) — T-0252 (Wave 5) + T-0267 (Wave 7, 2026-06-21).** The four audit-named partner VMs
  (`Dashboard`/`Earnings`/`OrderDetails` sealed `*UiState`; `Login` already canonical via
  `LoginFormState` + shared `ActionState` — the "Login is a flag-bag" line was stale) were done by
  T-0252. The residual partner page-state flag-bags T-0252 did not name — `InvoiceDetailsViewModel`
  and `OrderPhotosViewModel` — were converted to sealed `*UiState` by **T-0267**. Recorded
  judgment-call NON-violations (NOT converted, by design): the dual-spinner pull-to-refresh list VMs
  (`OrdersList`/`InvoicesList`/`RegistrationLock`), the partner form-section value-holder `*UiState`
  (`PersonalSection`/`BankSection`/`Emergency`/`Identification`/`Documents`/`AddressSection`/`Profile`/
  `Register`/`ForgotPassword`/`ConfirmEmail`/`Settings`), and `OrderNotesViewModel` (an §E2
  action-effect holder, not §E1 page-state). **F13 closed.**

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
- **SMALL RESIDUAL — carried as T-0270 (NOT F14, postdates this audit + T-0252):** three one-shot
  ACTION paths shipped AFTER this audit still use a loose `_submitting`/`_loading`-style boolean as the
  action machine (genuine but out of F14's named set, surfaced by T-0268's AC1 scan):
  `recurring/CreateRecurringViewModel` (`_submitting` + `_submitOutcome` StateFlow),
  `disputes/DisputeDetailViewModel` (`_sending`, `_uploadingEvidence`),
  `profile/DeleteAccountViewModel` (`_loading` gating `deleteAccount()`). Filed as **T-0270**
  (`Standardize post-Wave-5 one-shot actions on ActionState`, S, `[android]`, draft, sprint 8) —
  behavior-preserving conversion to the canonical T-0252 `ActionState` + `SharedFlow` pattern; NOT
  folded into F14. The per-row/per-button in-flight discriminators
  (`OrderDetailsViewModel._inFlightAction`, `OrdersListViewModel.inFlightActionOrderId`,
  `RecurringBookingsViewModel._mutating`) are **recorded NON-violations** (a single `ActionState` can't
  express which-row/which-button) and are out of T-0270's scope.

### F15 — `collectAsState()` instead of lifecycle-aware [major] [type: bug/leak]
- **Where:** `customer/features/recurring/RecurringBookingsScreen.kt:77`
- **Rule:** E6. **Fix:** `collectAsStateWithLifecycle()`.
- **Proposed ticket:** `Fix RecurringBookingsScreen state collection (lifecycle)` · S · [android]
- **RESOLVED (E6) — T-0269 (Wave 7, 2026-06-21).** The audit named only `RecurringBookingsScreen` and
  scoped "~22"; the real **filtered** count (screen/composable collections of a VM-owned lifecycle
  flow) was **≈56 occ / ~30 files** across both apps (raw `grep collectAsState()` = 85/36). T-0269 swept
  every in-scope screen-body VM-flow collection to `collectAsStateWithLifecycle()` across customer-app
  and partner-app (the latter on the post-T-0266 settled paths). **Correctly excluded NON-violations
  (left as plain `collectAsState()`):** `@Singleton` app-scoped repository flows collected in screens
  (`loyaltyRepo`/`referralRepo`/`orderRepo`/`catalogRepo`/`membership` — the flow outlives the screen),
  the two NavHost-level collections (`CleansiaNavHost`, `PartnerNavHost`), and the `:core`
  `GlobalSnackbarHost` infra. The dev confirmed by re-grep (not only the tool's narrow
  `viewModel`/`vm`-receiver regex). **F15 closed.**

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
- **E7 — RESOLVED (T-0266, Wave 7, 2026-06-21).** The partner-app dir/naming divergence
  (`features/<name>/{screens,viewmodels,components}/` split across `auth`/`dashboard`/`earnings`/
  `invoices`/`notifications`/`onboarding`/`orders`/`profile`/`settings`) was collapsed to the
  customer-app inline-singular `features/<name>/` convention — a pure structural move + package/import
  rewrite (proven move/rename/package-only, 0 function-body diffs by blob-sha comparison), plus
  renaming the surviving `Details` plural-drift to singular (`OrderDetail*`/`InvoiceDetail*`). Deliberate
  inline features (`devices`/`main`/`payroll`) and sub-namespaces left as-is. **E7 closed.**
- **The other deferred §E rules, now ALSO resolved in Wave 7 (see F13/F14/F15):** **E1 — RESOLVED**
  (T-0252 + T-0267, F13). **E6 — RESOLVED** (T-0269, F15). **E2 — RESOLVED** (T-0252, verified +
  closed by T-0268, F14) **with a SMALL residual carried as T-0270** (3 post-Wave-5 one-shot-action VMs
  on loose `_submitting`/`_loading` booleans — see the F14 note).
- **Proposed ticket:** ~~`ADR + migrate customer-app repos to ApiResult<T> and unify mobile structure`~~
  **DONE — E5/ApiResult (T-0197) + E7 structure (T-0266). All §E mobile-consistency rules resolved.**

---

## Not-issues (intentional — do not re-flag)
- `GetPagedOrders` materialize-then-map loop: required for per-row pay estimation with pre-loaded
  configs (A6 documented exception).
- Handler fetch-and-guard (`if (x is null) return Failure`) on Update/Delete: the canonical guard at
  point-of-use, **not** redundant validation (B4).
- Customer-app vs partner-app notification storage difference (Room in partner only): intentional.
