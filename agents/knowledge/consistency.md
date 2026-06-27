# Consistency Rules — One Way To Do Each Thing

The single biggest threat to a codebase this size is **the same operation written five different
ways**. This catalog fixes the canonical form for each recurring archetype, derived from a
line-by-line variance analysis of the real code (the majority/best form wins; deviations are named).
Every developer follows these; the Reviewer enforces them; deviations are either fixed or recorded as
a canonicalization ticket.

**How to read a rule:** `C#` = the canonical form (do this). `✗` = a real deviation found in the
codebase (don't add new ones; existing ones are tracked in
[`../backlog/audits/consistency-violations.md`](../backlog/audits/consistency-violations.md)).
Where a rule encodes a genuine judgment call (not just majority), it says **why**.

---

## A. Backend — paged queries

Canonical shape (see `patterns-backend.md` for the full sample). **Every paged/list query MUST:**

- **A1.** Be a `public class GetXxx` with a nested **`class Request : DataRangeRequest, IRequest<PagedData<TItem>>`**
  and a nested `XxxFilter? Filter { get; init; }`. ✗ *Don't* use a `record Query` with inline
  `Offset`/`Limit` (found in `GetPagedPromoCodes`, `GetPagedReferrals`).
- **A2.** Use a **`internal class Handler : IRequestHandler<Request, PagedData<TItem>>`** that returns
  `PagedData<TItem>` **directly** (never `BusinessResult<PagedData<T>>`).
- **A3.** Filter via a **Specification**: `XxxSpecification.Create(...).SatisfiedBy()`. ✗ *Don't* call
  a bespoke `repo.GetPagedAdminAsync(...)` with inline params (`GetPagedPromoCodes`, `GetPagedReferrals`).
  Filter→spec via a `filter.MapToDomain()` extension is acceptable *only* when the spec is built from
  the same `XxxSpecification` underneath (as in `GetPagedDisputes`).
- **A4.** Page+sort via **`repository.GetPagedSort<XxxSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())`**,
  count via `repository.GetCountAsync(filter, ct)`.
- **A5.** Return via the **`items.MapToDto(totalItems, request)`** extension. ✗ *Don't* hand-build
  `new PagedData<T>(...)` or compute `PageNumber` manually (`GetPagedPromoCodes`, `GetPagedReferrals`).
- **A6.** Read path is **`.Include(...) → .AsNoTracking() → .Select(x => x.MapToDto()) → .ToListAsync(ct)`**
  in that order, `.AsSplitQuery()` when there are multiple collection includes. Project **in the query**
  (`.Select(... MapToDto())`) — materialize-then-`foreach`-map is allowed **only** when per-row logic
  needs pre-loaded context (the documented `GetPagedOrders` pay-estimation case). ✗ `AsNoTracking`
  before `Include` (`GetPagedServices`) — keep the canonical order.
- **A7.** `Filter` is **`{ get; init; }`**, never `{ get; set; }` (✗ `GetPagedPayConfigs`).
- **A8.** Role/ownership scoping (admin sees all, employee/customer sees own) is done by **mutating
  the filter/spec inputs in the handler before building the spec** (as in `GetPagedOrders`,
  `GetPagedDisputes`, `GetPagedInvoices`) — consistently, not ad hoc.

## B. Backend — commands (create / update / delete / status-change)

- **B1.** `public class <UseCase>` with `public record Command(...) : ICommand<Response>` and a
  `public record Response(...)`. **Every command returns `ICommand<Response>`** with a real `Response`
  record. ✗ *Don't* use `ICommand<string>` (`CreateDispute`) or bare `ICommand` with no response
  (`UpdateDisputeStatus`, `DeleteSavedAddress`) — wrap the id/flag in a `Response`.
- **B2.** Identity comes from **`IUserSessionProvider.GetUserId()` in the handler**, never a `UserId`
  field on the command wire shape (a defaulted `UserId = ""` enriched by the controller is the legacy
  fallback — prefer reading the session in the handler).
- **B3.** **Validator** inherits **`AbstractValidator<Command>`** (✗ not custom bases like
  `UserEmailValidator`/`BaseUserValidator` — compose shared rules with `.SetValidator(...)` or a rule
  extension instead of inheriting). First rule of a field chain uses `.Cascade(CascadeMode.Stop)`.
  Every rule maps to `.WithMessage(BusinessErrorMessage.X)`.
- **B4.** **Validator validates the *shape and existence of inputs*; the Handler enforces *business
  rules and ownership*.** Put `MustAsync(repo.ExistsAsync)` in the validator only when the handler does
  **not** otherwise load the entity. When the handler *does* load the entity to operate on it (every
  Update/Delete that mutates a fetched row), the **fetch-and-guard lives in the handler** —
  `var x = await repo.GetByIdAsync(...); if (x is null) return Failure(...)` is the canonical guard,
  **not** redundant. Do **not** put ownership/session checks in the validator (✗ `UpdateEmployee`,
  `UpdateCurrentUser`, `UpdateSavedAddress`, `DeleteSavedAddress` all check ownership in the validator
  — ownership belongs in the handler, S3).
- **B5.** **Failure construction is `BusinessResult.Failure<Response>(new Error(nameof(command.Field), BusinessErrorMessage.X))`** —
  the first `Error` arg is **`nameof` of the offending field**, never `nameof(Command)`/`nameof(request)`
  (✗ `CreateMembershipSubscription`).
- **B6.** **Delete semantics:** prefer **soft-delete via `repo.Deactivate(entity)`** (sets `IsActive=false`,
  preserves history/audit) for any user- or business-facing entity. Use `repo.Remove(entity)` (hard
  delete) **only** for true join/scratch rows that carry no history and are never referenced. *(Judgment
  call: the codebase currently hard-deletes widely via `repo.Remove`; soft-delete is the long-term-correct
  default for a platform that needs audit trails and GDPR-traceable deletion. New deletes use
  `Deactivate`; existing hard-deletes are reviewed case-by-case — tracked as a violation.)*
- **B7.** Handlers call **rich domain methods** (`order.Cancel(...)`, `entity.Update(...)`,
  `repo.Deactivate(...)`) — never set entity properties directly from the handler.
- **B8.** **Side-effecting commands are idempotent** (S7) and wrap each external call (Stripe/email/queue)
  in a **narrow** `try/catch` for *that provider's* exception, mapping to a `BusinessResult.Failure`
  or logging a non-blocking follow-up — never a broad `catch (Exception)` for control flow. ✗
  `CreateMembershipSubscription` calls Stripe with no try/catch; ✗ `CreateOrder` has a Stripe
  try/catch but no idempotency guard.
- **B9.** Map outputs with the **`entity.MapToDto()` extension**; never inline-project a DTO in a handler.
- **B10a.** **A hard-delete guarded by an in-use check must let the DB be the final arbiter (S7a/S7b).**
  An `if (await repo.IsInUseAsync(id)) return InUse;` followed by `repo.Remove(...)` is check-then-act:
  a reference inserted between the check and the commit is still orphaned/cascaded. The durable shape is
  (1) the catalog-reference FKs are **`OnDelete(DeleteBehavior.Restrict)`** (the owning-aggregate side
  stays Cascade), and (2) the handler **flushes the `Remove` itself** —
  `try { await repo.CommitAsync(ct); } catch (DbUpdateException ex) when (DbConstraintViolation.IsForeignKeyViolation(ex)) { return Failure(...InUse); }`
  — because the `UnitOfWorkPipelineBehavior` commit runs AFTER the handler returns (S7b), so a try/catch
  around a tracked delete only works if YOU flush. The `IsInUseAsync` pre-check stays for honest UX.
  **Gotcha (real, T-0237):** an explicit `ON DELETE RESTRICT` raises SQLSTATE **`23001` (restrict_violation)**,
  NOT `23503` — `DbConstraintViolation.IsForeignKeyViolation` maps BOTH. A reference class with **no FK**
  (ids inside a JSON column, e.g. `RecurringBookingTemplate.SelectedServiceIds`) cannot be made
  DB-arbitrated; cover it in `IsInUseAsync` (materialize + check in memory, `IgnoreQueryFilters` when the
  referenced row is tenantless platform config) and accept the documented window.
- **B10.** **Dispute terminal-state writes go through the guard** (ADR-0006 D4 / the T-0172 transition
  table). A direct `dispute.Close(...)` / `dispute.Escalate(...)` / `dispute.Resolve(...)` is allowed
  **only** from the sanctioned writers: `Dispute.UpdateStatus` (the guarded in-app router),
  `ResolveDispute.Handle` (owns the `Resolve` money-path; gates on `IsTerminal` at the seam),
  `HandlePaymentNotification.ReflectChargebackStatus` (webhook reflector; gates on
  `CanTransitionTo`/`IsTerminal` itself), and `HandlePaymentNotification.HandleChargeback` (webhook
  creator; escalates a freshly-built `Pending` dispute on the legal `Pending → Escalated` edge before
  persisting). Any new caller must be added to the allowlist with a reviewable justification or
  refactored to route through `CanTransitionTo`/`UpdateStatus` — a direct write elsewhere can force an
  illegal terminal overwrite (e.g. `Closed → Resolved` on a late Stripe event). Mechanically checked
  by `check-consistency.mjs` (rule B10).

## C. Frontend — list features

- **C1.** The facade **extends `UnsubscribeControlDirective`**, is `@Injectable()`, and is provided on
  the component (`providers: [XxxFacade]`). ✗ *Don't* use `DestroyRef`/`takeUntilDestroyed` or bare
  `firstValueFrom` (`OrderWizardFacade`, `RecurringBookingsFacade`, `RewardsFacade` — customer-features).
  **One cleanup paradigm codebase-wide: `UnsubscribeControlDirective` + `takeUntil(this.destroyed$)`.**
- **C2.** State is **`signal<T>()`**. A paged list exposes exactly **`loading`, `initialLoading`,
  `totalRecords`** signals (plus the data signal). ✗ `fiscal-failures-list` omits `totalRecords`.
  Non-paginated master lists may omit `initialLoading`/`totalRecords` but **must carry a one-line
  comment** saying so.
- **C3.** **Every client call uses the exact pipe `takeUntil(this.destroyed$) → catchError(() => of(null)) → finalize(() => this.loading.set(false))`.**
  ✗ *Don't* reset `loading` inline inside `catchError` (`invoices` partner); use `finalize`.
- **C4.** Errors surface via **`SnackbarService`** (`showError`/`showApiError`); never inline strings.
- **C5.** Server-side paging only: `offset`/`limit`/`SortDefinition` through the generated client. No
  client-side slicing of a full list for a paged table.
- **C6.** Tables use **`cleansia-table`** fed by a **single `getXxxTableDefinition(...)` returning
  `{ columns, actions }`** in `*.models.ts`. ✗ *Don't* split into `getXxxColumns()` + `getXxxActions()`
  (`fiscal-failures-list`) and *don't* inline columns in the component or use `p-table` directly.
- **C7.** Component is **`standalone: true` + `ChangeDetectionStrategy.OnPush`**, exposes
  `protected readonly Policy = Policy` and gates actions with `*cleansiaPermission="Policy.CanXxx"`,
  and uses `ConfirmationService` for destructive actions.
- **C8.** **NgRx is for genuinely cross-feature state only** (auth, user, shared catalogs). A single
  feature's list state lives in its facade's signals — **don't** mix `store.dispatch`/`store.select`
  into a feature facade that could be plain signals (✗ partner `orders`, customer `disputes` mix both).

> **Reference archetype.** For a new admin paged list, **mirror the `disputes-management` list
> feature** — it is the canonical C1–C8 implementation (facade + signals + `cleansia-table` +
> server-side paging + `*cleansiaPermission` gating). The Wave-9 audit-log lib (ADR-0012) was built
> by copying it; copy it for the next one too rather than re-deriving the shape.

## D. Frontend — form features

- **D1.** Facade extends `UnsubscribeControlDirective`, exposes `loading` + `saving` signals, has
  **separate `createXxx(data)` and `updateXxx(id, data)`** methods, each building the generated
  `Create*Command`/`Update*Command`, using the C3 pipe, and **navigating via a route enum on success**.
  *(This archetype is already consistent — keep it that way.)*
- **D2.** Component is `standalone` + `OnPush`, builds the form with **`fb.nonNullable.group(...)`**
  and detects mode via **`route.snapshot.data['mode']`**. ✗ *Don't* mix `fb.group({})` with
  `fb.nonNullable.group({})` in one component (`package-form`) — if a nested dynamic group genuinely
  needs nullable controls, isolate it and comment why.
- **D3.** Inputs are **`cleansia-*` bound by `formControlName`**; field errors via **`ErrorPipe`**; API
  errors via `SnackbarService.showApiError`. No raw PrimeNG/`ngModel` for form fields.
- **D4 — audit drill-in.** An admin detail/edit page for an **audited** resource exposes a gated
  *"View audit history"* `<cleansia-button>` (`*cleansiaPermission="Policy.CanViewAuditLog"`) that
  navigates via the shared helper **`buildAuditResourceHistoryRoute(AuditResourceType.X, id)`** from
  `@cleansia/services` — **never** a hand-built URL or a duplicated resource-type string literal. The
  `AuditResourceType.X` constant **must equal the backend `[AuditAction(ResourceType="…")]` literal**
  for that entity (Order/Dispute/AdminUser/EmployeePayConfig), and the `id` you pass must be the **same
  id the backend records** — a page whose DTO only exposes a *different* id (e.g. employee-detail
  exposes the employee id, not the audited `User` id) must **not** wire a drill-in until the DTO carries
  the recorded id, otherwise the history view filters to nothing.

## E. Mobile — ViewModels, Screens, Repositories

- **E1.** **UiState is a `sealed interface` with `Loading` / `Error(...)` / `Loaded(...)`** — **never a
  single flag-bag `data class`** with `isLoading`/`error`/`isXSuccessful` booleans (which permits
  impossible states). ✗ partner `LoginUiState`, `OrderDetailsUiState`, `EarningsSummaryUiState`,
  `DashboardUiState` are flag-bags → migrate to sealed states. ✗ **Also** partner `ProfileUiState`
  (`ProfileViewModel.kt:26-36`) + the profile section `*UiState` (`PersonalSectionViewModel.kt:17-30` &c. —
  `isLoading`/`isSaving`/`error?`/`isSaved` mixing a load + a save lifecycle in one bag) are flag-bags,
  **and** those section VMs hardcode English validation/error strings (`PersonalSectionViewModel.kt:82,91`;
  `AddressSectionViewModel.kt:201,205,220` — the same F1/E8 class as Register/Forgot). **The iOS port is born
  right** (sealed `UiState<T>` load + `ActionState` save + `.xcstrings` ×5; sprint-12 §7.7 D5 — Android E1 NOT
  replicated); the android profile-VM fix (sealed states + move the literals to `R.string.*`) is the PM-filed
  follow-up **T-0337** (mechanical; independent of the iOS wave — same shape as F1/T-0333).
- **E2.** **One-shot actions use the shared `sealed ActionState` (Idle/Submitting/Error)** + a
  `SharedFlow(replay=0)` for the success effect — **not** loose `_submitting: Boolean` + `_error: String?`
  StateFlows. ✗ customer `CreateDisputeViewModel`, `MembershipViewModel`, `ProfileViewModel` use loose
  booleans; partner uses an `enum OrderAction` `inFlight` field → standardize on `ActionState`.
- **E3.** ViewModel is **`@HiltViewModel`** injecting the repository, **`SnackbarController`**, and
  **`@ApplicationContext Context`** (for `getString`). *(Judgment call on error localization: localize
  at the layer that surfaces the snackbar — repo for `T?`/`ApiResult` repos — and keep VM/​repo split
  consistent within an app.)*
- **E4.** Repositories are **`@Singleton`**, implement **`SessionScopedCache`** (`clear()` on sign-out),
  cache via `StateFlow`, wrap calls in **`networkCall { }`**, and parse errors with
  **`ApiErrorParser.parseToUserMessage(...)`**.
- **E5.** **Repository contract: return `ApiResult<T>`** (the sealed `Success`/`Error(ApiError)` type with
  `map`/`onSuccess`/`onError`) and surface the snackbar in the ViewModel. *(Judgment call: `ApiResult<T>` is
  the target over customer-app's `T?`-with-snackbar-in-repo because it carries the error explicitly, enables
  retry, and doesn't bury UI concerns in the data layer. customer-app's `T?` repos are the legacy form to
  migrate — this is a cross-cutting change, so it's a tracked refactor, not a same-day edit.)* **Ratified by
  ADR-0011** (`../backlog/adr/0011-mobile-apiresult-contract.md`): `ApiResult<T>` is THE mobile repository
  contract; the type **lives in the shared `:core` module** (`cz.cleansia.core.network`) so partner-app,
  customer-app, and the incoming iOS app consume **one** contract (fire-and-forget returns `ApiResult<Unit>`);
  the app-local localizers (`ApiErrorTranslator`/`ApiErrorParser`) stay per-app (E3); and the iOS Swift
  equivalent (`Result<T, ApiError>` / `ApiResult<Void>`, repo returns it, ViewModel surfaces the message) is
  fixed there so iOS is born canonical. Changing E5 is an ADR superseding ADR-0011, not an ad-hoc edit.
- **E6.** Screens inject via **`hiltViewModel()`**, collect **every** flow with
  **`collectAsStateWithLifecycle()`** (✗ `RecurringBookingsScreen` uses `collectAsState()` — a real
  lifecycle bug), and split a stateful `XxxScreen` (collects state, wires effects) from a stateless
  `XxxScreenContent` (pure, previewable).
- **E7.** **Directory & naming unified across both apps:** `features/<name>/<Name>ViewModel.kt` +
  `<Name>Screen.kt` **inline** (the customer-app convention), singular naming. ✗ partner-app's
  `features/<name>/{screens,viewmodels}/` split and `Details` plural drift → align to the inline
  singular convention for new code; existing is a tracked move.
- **E8.** All user-facing text via `stringResource(R.string.x)` / `appContext.getString(...)` — mostly
  consistent; keep it. ✗ **Parity deviation (F1, sprint-12 §7.5 Decision 5):** the partner
  `features/auth/RegisterViewModel.kt:64-84` + `ForgotPasswordViewModel.kt:45-52` set their **validation**
  error strings as **hardcoded English literals** (these VMs don't inject `@ApplicationContext Context`), so
  the register/forgot field errors render English in all 5 locales. **The iOS port does this correctly**
  (`Localizable.xcstrings` keys ×5, ADR-0013 D11) — iOS is the right reference; **do NOT replicate the Android
  literals on iOS.** Android fix = inject `@ApplicationContext Context` + move the strings to `R.string.*`
  (mirror `OrderDetailViewModel.kt:80`); a PM-filed **android follow-up ticket** (small, mechanical i18n),
  not part of the iOS wave. (This is the canonical case for the `patterns-mobile.md` Parity rule:
  Android-wrong → diverge correctly on iOS + raise an Android finding, don't silently copy.)

---

## Judgment calls (where we did NOT just follow the majority)

- **B6 soft-delete:** majority hard-deletes; we canonicalize on **soft-delete** because audit/GDPR/
  history demand it long-term.
- **E5 `ApiResult<T>`:** the two apps disagree; we canonicalize on the **more explicit** contract — ratified
  by **ADR-0011** (the type moves to shared `:core`/`cz.cleansia.core.network`; iOS born on the Swift equivalent).
- **E1/E2 sealed states:** customer-app is mostly right, partner-app mostly wrong; we canonicalize on
  **sealed states** because flag-bags permit impossible states (the actual defect).
- **B4 fetch-and-guard:** the "redundant null-check after validator" flagged by analysis is **not**
  redundant when the handler must load the entity to act on it — that's the canonical guard. We only
  forbid duplicating an existence check that the handler's own fetch already covers.

These judgment calls are **Architect-owned**; changing one is an ADR, not an ad-hoc reversal.
