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
  **not** redundant. Do **not** put ownership/session checks in the validator (✗ `UpdateSavedAddress`,
  `DeleteSavedAddress` still check ownership in the validator — ownership belongs in the handler, S3).
  **A SELF-write has no ownership check at all**: it resolves the subject from `IUserSessionProvider`
  in **both** arms (the validator's precondition and the handler's fetch) and leaves the wire id inert
  — `UpdateCurrentUser` and the seven `Features/Employees/Update*` commands are the reference shape.
  Comparing the session-resolved subject to a *client-supplied* id is not authorization (S1); the wire
  field must then be **`string?`**, or MVC's implicit-required rejects an absent id before MediatR and
  only a host test can see it.
- **B5.** **Failure construction is `BusinessResult.Failure<Response>(new Error(nameof(command.Field), BusinessErrorMessage.X))`** —
  the first `Error` arg is **`nameof` of the offending field**, never `nameof(Command)`/`nameof(request)`
  (✗ `CreateMembershipSubscription`).
- **B6.** **Delete semantics:** prefer **soft-delete via `repo.Deactivate(entity)`** (sets `IsActive=false`,
  preserves history/audit) for any user- or business-facing entity. Use `repo.Remove(entity)` (hard
  delete) **only** for true join/scratch rows that carry no history and are never referenced. *(Judgment
  call: the codebase currently hard-deletes widely via `repo.Remove`; soft-delete is the long-term-correct
  default for a platform that needs audit trails and GDPR-traceable deletion. New deletes use
  `Deactivate`; existing hard-deletes are reviewed case-by-case — tracked as a violation.)*
  **There is NO global `IsActive` query filter in this codebase** (`ApplyTenantQueryFilters` filters on
  `TenantId` alone; `grep HasQueryFilter` finds only the tenant filter), so "soft delete" is a convention
  every read implements by hand. **Every read of a soft-deletable entity filters `IsActive` explicitly**
  — a remaining-count or capacity query without `.Where(x => x.IsActive)` silently counts released rows
  (ADR-0035 §verify #24). Deviating form: a `CountAsync`/`AnyAsync` over an entity whose ordinals or
  slots are released by deactivation, with no `IsActive` term.
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
- **D1a — the command is built by construct-then-assign, never `new X({ … })`** (ADR-0031; full
  reasoning and the ESLint ratchet in `patterns-frontend.md` §"Building a generated DTO"). The
  command lives in the **facade or the feature's `*.models.ts`**, never in the component: a component
  that builds one is holding logic and is untestable without a TestBed. Where several call sites build
  the same shape, export one `buildXxxCommand(data)` beside the models (`profile.models.ts`
  `buildChangePasswordCommand` / `buildAddSavedAddressCommand`, marketing
  `buildSendSitewidePromoCommand`). Every member is declared `field!: T`, so **a dropped assignment
  type-checks and no build catches it** — the facade spec therefore asserts the whole serialized body,
  `expect(command.toJSON()).toEqual({ … })`, not field by field. ✗ A per-field
  `expect(command.orderId).toBe(…)` reads like coverage and passes when a *different* field is
  dropped.
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
  follow-up **T-0337** (mechanical; independent of the iOS wave — same shape as F1/T-0333). **RESOLVED on Android
  (T-0337):** the partner `ProfileViewModel` + every profile `*SectionViewModel` now expose a sealed
  `*UiState` (`Loading`/`Error`/`Loaded`). **The canonical shape for an EDITABLE-form section VM** (where
  `Loaded` must hold mutable fields + per-field errors): `Loaded(val form: XxxForm)` — a `XxxForm` data class
  carrying the editable fields + field-error strings — edited via a private `inline fun updateForm { … }`
  guard that only mutates when `is Loaded`; the **save** is a separate `StateFlow<ActionState>` + a
  `SharedFlow<Unit>(replay=0)` `saved` effect the screen collects to fire `onSaved()` (mirrors
  `DevicesViewModel`). Validation/error literals localized via injected `@ApplicationContext Context`.
  **E1a — an `Error` case no screen branches on is a flag-bag with extra steps (T-0353 on Android, the
  same gap on iOS).** Four iOS partner section views set `.error` in the VM and then rendered the empty
  form anyway: a cleaner whose read failed saw a blank profile, could type into it, and could save over
  it. Two obligations travel with the case, and a VM suite asserting `state == .error` proves neither:
  the screen passes it to its scaffold (`isError` + `onRetry` re-running the whole load), **and** the
  `save()` refuses a non-`Loaded` state — the id survives a failed *reload*, so a stale form otherwise
  goes out over a profile we could not read. Pin the screen half at its call site (the source-scoped
  binding test of `patterns-mobile.md`'s "a resolver test does not cover the call site"); pin the save
  half with a load-succeeds-then-reload-fails test, which is the only ordering that goes red.
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
  ADR-0011** (`/decisions/adr-0011`): `ApiResult<T>` is THE mobile repository
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
  Android-wrong → diverge correctly on iOS + raise an Android finding, don't silently copy.) **RESOLVED
  (T-0333):** both partner auth VMs now inject `@ApplicationContext Context` and source every validation
  message from `R.string.*` (reusing the existing `error_*_required`/`error_email_invalid` keys + one new
  `error_password_rules`), ×5 locales. **Also RESOLVED for the profile section VMs (T-0337):** the literals
  in `Personal/Address/Identification/Bank/Emergency` section VMs moved to `R.string.*` ×5.
  **E8 is now machine-enforced on iOS:** `StringCatalogCompletenessTests` (CleansiaCore, runs in the standalone
  package CI builds) reads all three `.xcstrings` off disk and fails on a key missing any of en/cs/sk/uk/ru, on
  an auto-extracted junk key (`-%@`, `#%@`, `%@%@%%` — the fingerprint of Xcode deleting real keys), and on a
  non-English value still equal to the English source unless it is on an asserted allow-list (see
  `patterns-mobile.md`). "It compiles" is no longer evidence a screen is translated.
- **E9.** **Every per-user cache is in the session-wipe set** (the security law is `security-rules.md`
  **S11** — this is its mobile mechanism + allowlist). A `@Singleton` (Android) / long-lived injected
  class (iOS) that holds **per-user state** (a cached `StateFlow`/`@Published`, a DataStore/`UserDefaults`
  row, a `Staleness` watermark, or a `Map` of these) **MUST** join the wipe set: Android — implement
  `SessionScopedCache` + `@Binds @IntoSet … : SessionScopedCache`; iOS — conform + `register(self)` with
  the `SessionScopedCacheRegistry`. The set is iterated on **all three** wipe-triggers (sign-out,
  authenticator forced-401, account-deletion) — never a hand-maintained partial clear-list (§`patterns-mobile.md`
  "three non-obvious rules"). ✗ *Don't* leave a per-user holder out — it leaks the prior user's data to
  the next account on a shared device.

  **The allowlist (the ONE sanctioned exclusion — device-level / public caches).** A `@Singleton` that
  caches state whose value is **the same for every user** (public/anonymous-fetchable, or device-scoped)
  is legitimately out of the wipe set, but **only** with a named, reason-annotated entry here. A stateless
  pass-through (no cache field) is trivially out and needs no entry — carry a `// Stateless — nothing
  cached, so no SessionScopedCache` comment instead (as `DeviceManagementRepository`/`PaymentRepository`
  do). Current allowlist (verified 2026-07-15):

  | Class | Platform | Holds cached state? | Why it is NOT per-user (reason required) |
  |---|---|---|---|
  | `CatalogRepository` (customer) | Android | **Yes** (`_services`/`_packages`/`_extras` `StateFlow`) | The **public** services/packages/extras catalog — identical for every user, anonymous-fetchable (guest booking). No account data. Re-fetched on booking entry; a stale catalog is not a cross-account leak. |
  | `CustomerServiceAreaDataSource` / `PartnerServiceAreaDataSource` | Android | No (caches in the shared `ServiceAreaProvider`) | Public serviced-countries/cities list — device-level, not per-user. |
  | `AppSettingsStore` / `AppSettingsRepository` | Android/iOS | **Yes** (language/theme/onboarding) | **Device**-level UI prefs (survive uninstall parity is DataStore/`UserDefaults`). Per-user onboarding is keyed **by userId** (`hasSeenOnboarding(userId:)`) so it is already user-partitioned, not a shared bucket. No account content. |
  | `OrderEventBus` / `SnackbarController` / `PushTokenSessionObserver` | Android | No (`SharedFlow(replay=0)` / delegates) | Transient event buses hold nothing after emit; the observer delegates to `PushTokenRepository` (which **is** in the set). |

  **Enforcement (see §`enforcement.md` — why a full static check is infeasible):** a cross-file "is this
  `@Singleton` per-user AND not in the multibinding" check needs Kotlin/Swift type-graph resolution the
  line-based `check-consistency.mjs` cannot do. Two layers, one **live today** and one **specified**:
  - **Live (warn-only advisory):** `check-consistency.mjs` rule **E9** flags a `@Singleton` that declares
    a cache field (`MutableStateFlow<`/`DataStore<`/`Staleness()`) but does **not** list `SessionScopedCache`
    on its class declaration and is **not** on the allowlist above. It is **non-blocking** (a Room-DAO- or
    other-backed per-user cache the field-regex can't see would slip past it), so it prompts the Reviewer,
    it does not gate. The allowlist above is duplicated as `SESSION_WIPE_ALLOW` in the tool — **keep the two
    in sync** when adding an entry.
  - **Specified, NOT yet built (the hard gate):** a **roster-equality assertion test** —
    `SessionScopedModuleTest` (Android, per app) / `SessionScopedCacheRegistryTest` (iOS) — that asserts the
    production wipe set **equals** a hardcoded expected roster, so a new per-user repo that neither joins the
    set nor lands on the allowlist fails a real test (the existing `AuthRepositoryTest`/`PushLogoutClearsTests`
    only exercise `clearAll()` with an *injected* set — they do not check the real multibinding's membership).
    Filed as a small follow-up ticket (§`enforcement.md`).
    **Retires when:** `SessionScopedModuleTest.kt` and `SessionScopedCacheRegistryTest.swift` exist.
- **E10.** **Every `HttpLoggingInterceptor` redacts the Authorization header.** A provider that builds
  `HttpLoggingInterceptor()` MUST call `redactHeader("Authorization")` in the same `.apply` block —
  a DEBUG build at `Level.HEADERS` otherwise prints live bearer tokens to logcat, where any
  on-device log collector (or a copied bug report) picks them up. Both existing providers
  (customer `AuthModule`, partner `NetworkModule`) comply; the rule guards the NEXT provider.
  **Live (blocking):** `check-consistency.mjs` rule **E10** flags any `.kt` file constructing the
  interceptor without the redact call.

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
- **Order offerability (ADR-0037, `accepted` 2026-08-03 — binding):** ten surfaces answered "which
  orders may a cleaner be offered / take" with six different status sets. We canonicalize on **none
  of them** — the majority set (`{New, Pending, Confirmed}`, held by the push and the web pane)
  contains a status with **no production writer**, and the dashboard's `{Pending, Confirmed}` reduces
  to `{Confirmed}`, which is structurally **zero** for cash orders. The rule is not a status list at
  all and it is **not** the draft's `Confirmed ∨ (New ∧ Cash)` either — the panel falsified that in
  both directions against two live sweeps. It is **status × money**, owned by
  `Cleansia.Core.Domain.Orders.OrderAvailability`:

  ```
  Offerable(o) ⟺ ( o.CurrentStatus == Confirmed ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash) )
               ∧ ( o.PaymentStatus == Paid ∨ (o.PaymentType == Cash ∧ o.RecurringTemplateId == null) )
  ```

  The second conjunct is the union of the negations of the only two scheduled retractors'
  `WHERE` clauses — **derive it from them, never paraphrase it.** Two evaluation forms (queryable +
  in-memory), pinned by an equivalence test against real Postgres, never by a shared `.Compile()`d
  lambda; the read surfaces fail CLOSED on a NULL `CurrentStatus`, the take gate deliberately does
  not. Deviating forms: **any availability status literal outside `OrderAvailability`**; **any set
  containing `OrderStatus.Pending`** (dead, no writer); **a second `RuleFor` chain in
  `TakeOrder.Validator`** (rule-level `Cascade.Stop` does not span chains, so a second one returns a
  multi-error composite and makes a held order distinguishable from a missing one).

- **Post-commit ordering + fail-soft admissibility (ADR-0038, `accepted` 2026-08-03 — amendments
  AM-1 … AM-11):** three rules from one outage.
  **Enforced by:** `quality-gates.md` **Gate 4 (Architecture)** + ADR-0038 reviewer checks #1/#3/#4/#14
  + the deviating forms below — **T3-HUMAN**. *(The seam's own five laws are separately tiered in
  `roles/post-commit-effects.md`; laws 1/3/5 are `(gate pending: T-0532)` → T1-CI.)*
  (a) *"Post-persist" in a handler means **tracked**, not durable* — the commit is in
  `UnitOfWorkPipelineBehavior:27-30`, after the handler returns. A self-committing write, or any write
  referencing a not-yet-committed row under an FK, must ride the pipeline's `SaveChangesAsync` or run
  **strictly after** the commit. Deviating form: a raw `SqlQueryRaw`/`ExecuteUpdate` write inside a
  command handler that references `Order.Id` (or any sibling aggregate id created in the same request),
  **or a self-committing write inside a handler with no sanctioned-exception doc-comment**. Each
  exception is an exception **because it says so, not because it exists**.

  > **This used to say "there are exactly *two*".** It has now been wrong twice — two while four were
  > shipped, and four while the S7a family was also in scope. **A count is the wrong shape for this**:
  > nothing fails when the tree gains a fifth, and a reviewer greps this entry to decide whether a new
  > self-committing write is sanctioned, so an understated list makes them either re-litigate settled
  > design or reject a legitimate next one. What follows is a **membership test** (normative — it
  > decides the next case) and a **roster** (descriptive — read from the tree 2026-08-09; it decides
  > nothing on its own). If a write passes the test and is not on the roster, the **roster** is stale;
  > add it. If it is on the roster and fails the test, the **write** is the defect.

  **The membership test — four conjuncts, all required.** A self-committing write is sanctioned iff:
  **(i)** it bypasses the change tracker (`SqlQueryRaw`, `ExecuteSqlRaw`, `ExecuteUpdateAsync`,
  `ExecuteDeleteAsync`) so it lands on its own, immediately; **(ii)** it is reached from **inside**
  `UnitOfWorkPipelineBehavior`'s `next(…)` — i.e. from a command **handler** or anything it calls (see
  the scope note below); **(iii)** it carries a doc-comment, at the method or on its interface member,
  stating all three of *that it self-commits outside the pipeline*, *why it must land independently of
  the caller's commit*, and *what it does **not** roll back*; and **(iv)** it references **no row
  created in the same request** under an FK — that hazard is what ADR-0038 §D3 closed and **no comment
  waives it** (`PromoCodeRedemptionRepository.cs:31-42` is the write that had to be converted **back**
  to change-tracked for exactly this reason, and it is therefore *not* on this roster).

  **Counting rule: one entry per method that issues the write** — not per decision, and not per call
  site. **A compensator is its own entry.** *Adjudicated 2026-08-09; the contrary reading is that
  `DecrementGlobalRedemptionsAsync` is merely the increment's undo and shares its sanction.* It does
  not, for three reasons: the roster is **grepped by method name**, so an unlisted method reads as
  unsanctioned; the compensator can be wrong **independently** of what it compensates (its floor guard,
  its trigger, and ADR-0038 AM-10's *catch-the-compensation-never-the-operation* obligation are all
  properties of the decrement alone — `PromoCodeService.cs:163-180` and `:194-210`); and "it is a
  compensator, so it does not count" is a counting rule that only works for a reader who **already
  knows the pairing**, which is the knowledge this roster exists to supply. One ADR, two methods, two
  entries.

  **Scope — a validator is out.** Registration order is Validation-**outer**, UnitOfWork-inner
  (`Cleansia.Config/Validation/FluentValidationExtensions.cs:35-36`, named in
  `UnitOfWorkPipelineBehavior.cs:22-26`), so a write from a **validator** never sits inside the unit of
  work at all and this rule does not reach it. The S7a attempt-budget charges are the live instances —
  `UserRepository.RecordFailedLoginAsync` (`LoginValidator.cs:156`),
  `TryChargeConfirmationCodeAttemptAsync` (`ConfirmUserEmail.cs:67`),
  `TryChargeResetPasswordCodeAttemptAsync` (`ChangePassword.cs:100`). **They are governed by
  `security-rules.md` S7a, not by this entry**; do not add them here, and do not read their absence as
  a finding.

  **Roster — family A, claim-before-commit** (a number or slot claimed before anything may carry it):
  1. `PromoCodeRepository.TryIncrementGlobalRedemptionsAsync` (`:24-48`, comment `:28-38`; call site
     `PromoCodeService.cs:152`) — the global promo cap must be claimed or rejected on its own,
     independently of the order commit.
  2. `PromoCodeRepository.DecrementGlobalRedemptionsAsync` (`:50-64`, comment `:54-58`; call site
     `PromoCodeService.cs:200`) — **entry 1's compensator, and its own entry.** It runs from a
     `finally` on *any* non-success, on `CancellationToken.None` because the increment is already
     durable, and its own `catch` never rethrows.
  3. `MembershipBenefitUsageRepository.TryReserveSlotAsync` (`:65-119`, statement `:105-107`;
     declaration on the interface, `IMembershipBenefitUsageRepository.cs:25-29`; reached from
     `CreateOrder.cs:409` via `ExpressWaiverConsumer.cs:33`) — ADR-0035 Mode A: a price may never be
     waived without a committed slot. Passes (iv) because `OrderId` is deliberately **nullable** and
     stamped later by a change-tracked update.
  4. `PayoutReferenceCounterRepository.AllocateNextAsync` (`:18-74`, comment `:32-41`, statement
     `:69-71`; contract `IPayoutReferenceCounterRepository.cs:12-17`; call sites `GenerateInvoice.cs:88`,
     `AssignInvoiceVariableSymbol.cs:103`, `PayPeriodBackgroundService.cs:332`) — ADR-0046 §D2.1: a
     payout invoice's *variabilní symbol* must be claimed **before** any row or document can carry it,
     so the allocation deliberately does **not** roll back with the caller: an invoice that fails to
     commit leaves a **gap**, which is correct for a payment reference (it is not a fiscal document
     number, and only `FiscalCounter` owes gaplessness). Its self-commit is a **caller** property, not
     an API one — `SqlQueryRaw` joins an ambient transaction if one is open — so the invariant travels
     with it: **no allocator call site may sit inside a `BeginTransactionAsync` scope**, or both the gap
     semantics and the single-counter-row lock duration break. *(The `CommitAsync` calls at
     `GenerateInvoice.cs:114-118` / `AssignInvoiceVariableSymbol.cs:115-119` are flushes that run
     **after** the allocation, not transactions around it — not a violation.)*

  **Roster — family B, must land while the command FAILS** (the write is the point *because* the
  request is refused):
  5. `UserRepository.RecordFailedCurrentPasswordAttemptAsync` (`:180-194`, comment `:174-179` +
     `:146-156`) — charged from **inside the handler**, `ChangeOwnPassword.cs:69`, whose own comment
     (`:57-60`) states the deviation: *"this failure never reaches the unit-of-work commit yet the
     counter still lands"*. S7a's lockout budget is worthless if it rolls back with the refusal.
  6. `DeactivateAdminUser.Handler`'s conditional `ExecuteUpdateAsync` (`DeactivateAdminUser.cs:63-76`,
     comment `:57-61`) — the only roster entry written **in a handler** rather than behind a repository
     method: an atomic last-active-admin guard where `0 rows ⇒ CannotDeactivateLastAdmin`.
     Its comment carried S7a's *why it is one statement* but not conjunct (iii) until 2026-08-10
     (T-0575), when the missing half was appended: **self-commits outside the pipeline → why it must
     land independently → what it does not roll back**. Recorded because the gap was listed rather
     than flagged for a reason worth keeping — the *design* was right, and unlisting a correct write
     would have made it read as a violation. The roster now has no incomplete entry.
  (a2) *A change-tracked write is invisible to every **DB-read** guard over it for the rest of the unit
  of work* (AM-4/AM-5) — the mirror of seam law 3. Converting a self-committing write to a tracked one
  disarms its idempotency/uniqueness pre-reads until the commit; the duplicate then surfaces as a
  `DbUpdateException` that rolls back the whole unit of work, or (nulls-distinct index, NULL tenant)
  does not surface at all. Deviating form: **a repository method that stages an entity while its
  caller's idempotency guard is a plain `DbSet` query.** Fix by making the guard `.Local`-first, or by
  *pinning* single-invocation with a test — a call-graph accident is not a safety property.
  (b) *A `catch` that logs and continues is admissible only over an operation that **normally
  succeeds*** — post-commit, proven happy-path by a **real-PostgreSQL** integration test, and detectable
  by a named reconciliation predicate **keyed on a column the anonymizer preserves** (AM-9: gate on the
  applied *amount*, never on an FK `AnonymizeCustomerData` nulls). Fail-soft over a deterministic
  failure converts a loud 500 into silent, permanent data loss. Deviating form: a `try`/`catch` added
  around a call that is *currently failing*; and a recon query gated on `PromoCodeId`/
  `MembershipPlanIdAtPurchase`/`UserId`. Prefer **`try`/`finally`** for "release on any non-success";
  the one mandated `catch` wraps **the compensation itself**, logs at Error and never rethrows (AM-10)
  — catch the compensation, never the operation. All three rules in `patterns-backend.md`; seam contract
  in `roles/post-commit-effects.md`.

- **Tenant-scoped unique indexes: `NULLS NOT DISTINCT` is decided by the index's JOB, not by a
  majority (ADR-0035 AM-6, ADR-0034 D1.3, ADR-0038 §D5.2 — all `accepted`).** Single-tenant mode *is*
  `TenantId == null` and PostgreSQL treats NULLs in a UNIQUE index as distinct, so on the platform's
  default deployment a tenant-scoped unique index either fires or it does not, and which one is not a
  style question:
  - **Sole arbiter of a concurrent claim ⇒ `.AreNullsDistinct(false)` is mandatory.** No read can
    arbitrate a race, so the index is the only thing between two simultaneous claims and it has to
    actually fire. Live instances: `FiscalCounters`, `MembershipBenefitUsages`,
    `PromoCodeRedemptions`, `EmployeePayoutDetails`, `LiveActivityTokens`, `Users` (the account email —
    see the arming note below, whose DDL half is still owed).
  - **Backstop behind an authoritative app-level assert ⇒ nulls-distinct is fine.** The invariant is a
    state you can read and assert on before writing. Live instances: `UserMemberships` (at most one
    active row per user), `LoyaltyTransactions` (the serial-replay fast-path read).

  **Which bullet you are on is decided by one question, not by how the pre-check reads
  (ADR-0050 §D1/§CH-3):** *is there a lock, an `ON CONFLICT`, or a serializable boundary between the
  read and the write?* If there is none, the read and the insert cross a snapshot boundary with nothing
  in between, the pre-check is a **courtesy**, and the index is the sole arbiter however carefully the
  pre-check is written. "We read it first" is not an authoritative assert. *(This test also puts the
  second bullet's own instances back in play — `UserMemberships` is rostered as a backstop behind
  `GetActiveForUserAsync`, which is itself a read-then-write; that is a known soft spot in this entry,
  recorded rather than relied on, and re-examining it is out of ADR-0050's scope.)*

  The **reviewer checks the emitted DDL, not the C# builder call.** Deviating form: a comment declining
  the option on consistency grounds — `AreNullsDistinct(false)` has shipped in the committed `Initial`
  migration since day one, so "we don't do that here" is a false invariant, and a confidently-wrong
  comment is worse than none because it stops the next reviewer checking.

  **Arming a sole arbiter is TWO artifacts, and the model is not the DDL — `Users (TenantId, Email)`
  is the worked example.** `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs:95-97`
  states that DB-level uniqueness, *not* the app pre-check, is what closes the register/update TOCTOU
  race, and all four `User`-creating writers (`Register`, `RegisterEmployee`, `CreateAdminUser`, social
  provisioning) are read-then-insert with no lock — so by the test above the index is the arbiter. It
  shipped for months as `.IsUnique()` alone, admitting unlimited duplicate `(NULL, email)` rows, which is
  the exact "confidently-wrong comment" form named above. `:112-114` now carries
  `.AreNullsDistinct(false)` (ADR-0050 D1), **but the emitted DDL does not yet**: the option only reaches
  Postgres through the owner-run `Initial` regen, which is gated on a duplicate census (ADR-0050 §D3 —
  the index cannot be created over pre-existing duplicates). **So a model assertion goes green the moment
  the builder call lands and says nothing about the database** — do not read one as evidence of the
  other. **ADR-0050 is `proposed`**
  (`docs/decisions/adr-0050.md:3`).
  **Retires when:** that status line stops reading `proposed`.

  **Arming one also creates a new failure mode, and it ships in the same change or not at all.** The
  losing racer stops silently inserting a duplicate and starts raising `23505` at commit — a 500 where
  there was quiet success, which is worse for the user than the bug. Every writer therefore maps that
  violation to the business error its own pre-check would have produced, **keyed on the constraint name**
  (`DbConstraintViolation.IsUniqueViolationOn`), never on the driver's message text, and a violation of
  any other index in the same commit still propagates. Because the pipeline commits *after* the handler
  returns, the map has to be a **flush inside the handler** (`GenerateInvoice` is the precedent shape).
  Deviating form: a diff that lands the option without the mapping.

  **Enforced by:** `src/Cleansia.Tests/Infrastructure/NullsNotDistinctIndexModelTests.cs` (theory +
  negative control), run by `.github/workflows/backend-ci.yml:69-74` with no `continue-on-error` —
  **`T1-CI`** over the **five indexes on its `[InlineData]` roster** (`FiscalCounter`,
  `EmployeePayoutDetails`, `PromoCodeRedemption`, `MembershipBenefitUsage`, `User`), **baseline 0**: all
  five green today. It asserts the **EF model only** — SQLite cannot express the option, so the DDL half
  is the reviewer's, per the emitted-DDL rule above. The roster is **hand-maintained** and is therefore a
  closed roster — a new sole-arbiter index is not caught until someone adds a row, and
  `LiveActivityTokens` is named in the first bullet above without being on it. The mapping half is
  **`T1-CI`**, **baseline 0**, over `src/Cleansia.Tests/Features/Auth/UserEmailRaceMappingTests.cs`
  (both directions, all four writers) and `src/Cleansia.Tests/Common/DbConstraintViolationTests.cs`.

- **Moving a gate onto a new denormalized column keeps the old term until a backfill retires it
  (ADR-0034 D7, `accepted`).** A flag defaulting to `false` is `false` for every existing row on release
  morning, so `!string.IsNullOrEmpty(LegacyColumn)` → `NewFlag` is a behaviour change for the whole
  table, not a refactor. Ship the writer first, then the gate as `NewFlag || <old condition>`, with a
  comment naming the outage and the retirement condition. **Both terms must be scalars on the row the
  gate already loads** — a navigation term is decided by a hand-written `.Include` list, and the same
  applies to erasure, which stays an id-keyed repository call (load-and-remove, not `ExecuteDelete`, so
  it rides the caller's unit of work). Deviating form: a gate flip whose only test builds the aggregate
  by hand; the pin is a host/route test seeding the pre-release row shape
  (`PayoutGateDeployDayTests`) plus a real-PostgreSQL erasure test through the owning service's own
  query shape (`PayoutDetailsErasureTests`). Full rule in `patterns-backend.md`.

- **A comment claiming a query PLAN property is pinned by `EXPLAIN` over the statement EF actually
  emitted — and the assertion is the `Index Cond`, not "no Seq Scan" (T-0540).** Two call sites tested
  `Order.CurrentStatus` with `Contains` over different shapes (an instance `IEnumerable` vs a
  `private static readonly` array) and both claimed in prose to seek on
  `IX_Orders_CurrentStatus_CleaningDateTime`. Measured, they emit **different SQL** — `= ANY (@p)` vs an
  inlined `IN (0, 1, 2, 3, 4)`, because EF parameterises a runtime value and folds a static readonly to
  constants — which PostgreSQL normalises to the same `ScalarArrayOpExpr`. So the claim was true, and
  nothing kept it true. Two rules:
  - **EXPLAIN the captured statement, never a hand-written copy of it.** A `DbCommandInterceptor` that
    re-runs `"EXPLAIN " + command.CommandText` on the *same* connection, transaction and parameters
    pins the plan the query gets; SQL retyped into the test pins the plan of the retyping. Drive the
    real production entry point — a handler, repository method or specification — so the statement is
    one production actually issues (`OrderStatusSetPredicatePlanTests`,
    `UserMembershipCancellationSweepIndexPlanTests`).
  - **"No Seq Scan" is not the assertion.** Pushing the status term inside an `OR` leaves the planner
    on the same index and merely **demotes the term out of the `Index Cond` into a residual filter** —
    green under a seq-scan check, and exactly the ADR-0040 fail-open regression. Assert that the term
    appears in the `Index Cond:` of the node naming the index, and assert the value set that reaches
    PostgreSQL against a **golden literal** (reading it off the private field under test cannot detect
    that field being widened). Deviating forms: a plan assertion on an empty or uniform table; a
    seq-scan-only assertion; an expectation derived from the code under test.
  - **A PARTIAL index needs the seed populated inside its filter, or only the filter is under test.**
    Measured twice: the same `OR` demotion costs 2.4× against five rows inside
    `IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation` and **55×** against eight thousand, and is
    a seq scan in neither. With a handful of rows inside the filter, an index keyed on *anything* passes
    and the key columns are never exercised — so seed thousands INSIDE the partial filter but OUTSIDE
    the sargable band, and assert that population as its own test. State the selectivity as an assertion
    too: a predicate that matches most of the table makes a seq scan the **correct** plan and the whole
    pin meaningless.

These judgment calls are **Architect-owned**; changing one is an ADR, not an ad-hoc reversal.

## Interim implementations must name their end state (ADR-0038 §D4, amended AM-7)

> **Enforced by:** `InterimMarkerTripwireTests` in `Cleansia.Tests` (walks `src/`, validates the marker
> pattern, resolves each id against the backlog manifest) — **`(gate pending: FT-38.2)` → T1-CI**
> via `backend-ci.yml:71` when that ticket lands. *Deliberately **not** a `check-consistency.mjs` rule:
> that tool appears in **zero** `.github/` workflows, so it can never set an exit code, and a
> T2-ADVISORY orphan-check on a code comment is — precisely — a comment with a ticket number in it.*

An interim with no named end state is how a stop-gap becomes the architecture. Any deliberately
temporary implementation shipped ahead of its end state carries, on the changed member:

```csharp
// INTERIM(ADR-NNNN §Dn → T-xxxx): <what this is>; delete when <the end state> lands.
```

- The **`§Dn` segment is part of the canonical form** — it resolves the marker to a *clause*, not to a
  600-line document. The checker's pattern must therefore be
  `INTERIM\(ADR-\d{4}(\s+§D[\d.]+)?\s*→\s*T-\d{4}\)` — `§Dn` **explicit and optional**. *(AM-7: the
  first version of this rule stated the pattern without `§Dn` while the template and the only shipped
  marker both had one, so a checker built to spec would have matched **zero** markers and reported OK.)*
- The ticket id must be **filled, present in the live manifest, open, *and not blocked*** before
  the interim merges — an unfilled marker blocks review, and so does an id whose row carries a 🔴
  do-not-start gate. *(AM-7: "open" alone goes green while the retirement work is forbidden — which is
  the exact state ADR-0038's own interim shipped in, blocked on the ADR that authorized it.)*
- The end-state PR **deletes the marker**; a PR that lands the end state and leaves the marker is
  incomplete.
- The ADR must state the **acceptance test for retirement** — the property/properties the end state
  restores, as tests, not as prose. ("Restores" is measured against the *intended* property, not the
  previous state, which may itself have been broken.)
- **Anti-vacuity, and not the usual shape** (ADR-0032 D3): the corpus is *legitimately* empty once the
  last interim retires, so "assert at least one marker exists" would be a test that must be deleted to
  stay true. Assert the **mechanism** instead — the walker resolved `src/` and enumerated a non-trivial
  file count, and the pattern matches a known-good fixture string inside the test. **An empty result is
  legal; an empty scan is not.**

## Catalog claims about the tree — the deviating forms (2026-08-09)

> **Enforced by:** `agents/tools/check-catalog-claims.mjs` —
> **`agents/tools/check-catalog-claims.mjs`** (T-0574, landed `d8f357f1`) — **T1-CI**, blocking, on
> the corpus and on its own self-test. It shipped `T2-ADVISORY` because the baseline it measured
> (**16**, then **15** at `d8f357f1`) was not zero, and claiming a blocking tier over a dirty baseline
> would break the very rule being enforced. The `docs/sprint-15-decisions` sweep drove it to
> `C1 0 · C2 0 · C3 0`, so `--warn` came off `.github/workflows/catalog-claims.yml` in the same
> change. The split it encoded still holds and is worth keeping in view — `--warn` was advisory about
> the **catalog** and blocking about the **tool**, because a checker reporting zero violations while
> blind is the defect it exists to close. Rule and the rejected alternatives: `conventions.md`
> §*"A claim about the tree carries its own retirement condition"*.
>
> **What the last six taught, since it is the reusable part:** every one of them needed a *ruling*,
> not an edit. Two are exhibits that cite a dead line **on purpose** — the entry's whole subject is
> that the citation rotted — and "fixing" them by inventing live line numbers destroys the exhibit;
> they are quoted instead, which is how this corpus tells a claim from a display. One was a
> regenerated migration filename (the pre-prod `Initial` is rebuilt, not stacked, so its id moves).
> One was a member that no longer exists anywhere, extracted to a shared helper by ADR-0035 AM-10 —
> the only repair that had to be re-reported rather than re-pointed. And the C1 was resolved on the
> **ADR** side: `patterns-mobile.md`'s card matched the tree, ADR-0022's header did not.

Four forms are deviations from the day this entry lands. Each is a **form**, not a judgement about the
claim's truth — three of the four instances below were *true when written*, which is the whole point.

1. **A status banner that names an ADR without quoting that ADR's own status token, and without naming
   the token as its retirement condition.** *Live instances fixed:*
   `roles/membership-benefit-usage.md` (PROPOSED over an `accepted` ADR-0035),
   `patterns-backend.md:979` (*"ADR-0039 is `proposed`"* over an `accepted` ADR-0039),
   `roles/express-waiver-resolver.md:3-10` (hand-patched 2026-08-05 — the precedent that proves a hand
   patch does not close a class).
2. **A "NOT YET BUILT / no ticket yet" banner that does not name the path whose existence retires it.**
   *Live instance fixed:* `roles/payout-reference-allocator.md` — true for **2 h 11 m**, then falsified
   by `d410f002`. Writing the card early is **not** the deviation; writing it without the trigger is.
3. **A `file:line` citation that does not resolve** — file missing, or fewer lines than cited. *Live
   instance fixed:* `roles/membership-benefit-usage.md` invariants 5 and 6, which cited
   *"`PromoCodeRedemptionRepository.cs:85-93` and `:99-109`"* in a **65-line** file, rotted by an
   unrelated refactor (`da88b695`). The invariants were still **true**; only the evidence was dead —
   which is the worst variety, because a reader who checks the citation concludes the invariant is dead
   too. **The two dead ranges above are quoted, not asserted**: an exhibit of a rotted citation must
   ride inside the `*"…"*` convention, which the checker's quoted-span mask skips, or this sentence
   becomes an instance of the very form it names. Write the exhibit bare and you re-earn the finding.
4. **Any sentence of the form "there are exactly *N* …" about the tree.** Write a **roster + membership
   test** instead: the test is normative and keeps deciding the next case, the roster is descriptive and
   is falsifiable one file at a time. *Live instance fixed:* the self-commit exceptions list in
   §"Post-commit ordering" limb (a), which was wrong twice — and is now the worked example of the
   replacement shape.

**Not a deviation, and do not "fix" it:** a claim about the tree inside an **`accepted` ADR**. Accepted
ADRs are immutable records of a past reading (`adr/README.md`); when the world moves past one, the
instrument is a **dated record-only closure**, never an edit. `ADR-0032:96`'s *"`.swiftlint.yml` has no
`custom_rules:` block"* is exactly this — false at HEAD (`src/cleansia_ios/.swiftlint.yml:27`), correct
to leave standing.

## Verbatim wire bodies stored without a clock — the deviating form (2026-08-10)

> **Enforced by:** `DeadLetterRetentionTests` + `DeadLetterRetentionPostgresTests` —
> **`(gate pending: dead-letter retention sweep + the Failed-outbox-body clock — two tickets owed, PM
> to file; T-0584 is the first)`** → **`T1-CI`** when both land. Rule and the rejected alternatives:
> `patterns-backend.md` §*"A durable store of a VERBATIM wire body declares a clock"*; decision:
> ADR-0002 §"Partial supersede — 2026-08-10 (architect, T-0584)".
>
> **This entry exists because the rule puts code that exists today in violation** (ADR-0033 routing
> test 1), so the superseded form is recorded here and the canonicalization is ticketed rather than
> assumed. **The baseline is non-zero and measured — two instances, both live at the time of writing.**

**The deviating form:** an entity property holding a **verbatim wire body** whose type appears in no
retention, prune or GDPR path — i.e. the type name occurs in neither `Features/DataRetention/**` nor
`GdprDeletionService`. It is a *form*, not a judgement about intent: both instances below were
deliberate and were **right when written**, which is the whole point.

| Instance | State | Disposition |
|---|---|---|
| `DeadLetter.RawBody` (`Core.Domain/DeadLettering/DeadLetter.cs:34-38`) — *"stored as `text` (unbounded) so nothing is truncated"*, no sweep, and the `send-email` body it holds carries the recipient address, the real name and (until `e84aed25`) a live reset token | **being closed** by T-0584's build (two clocks: redact `RawBody` at 7 d, delete the row at 90 d) | the entity + `IDeadLetterStore.RecordAsync`'s *"stored unbounded"* param doc are rewritten by that build |
| `OutboxMessage.Body` on `Status = Failed` rows (`Core.Domain/Outbox/OutboxMessage.cs:16-17`) — `PruneOutbox` deletes **only** `Dispatched` (`PruneOutbox.cs:72-74`; `IOutboxRetentionConfig.cs:15-17` states it), so a permanently-failed `send-email` row keeps the identical bytes forever | **OPEN** | deliberately **not** folded into T-0584: a `Failed` row is genuinely re-drivable, so it has a real recovery role and its clock is its own decision (ADR-0002 §A8). **Ticket owed.** |

**Two things this class teaches that a "does it have a retention job?" audit would miss.**

1. **"Durable" was read as "permanent" by everyone downstream, including the entity's own author.**
   ADR-0002 D3 said *durable* and *"the recovery source"*; the docstring turned that into *unbounded*;
   nobody decided permanence. If a comment answers **truncation**, say separately what answers
   **retention** — the two words sit one sentence apart and mean unrelated things.
2. **The recovery role was nominal and nobody had checked.** Nothing read a `DeadLetter` row — no
   query, no admin endpoint, no replay command (verified 2026-08-10; the claim retires when
   `IDeadLetterRepository` gains a reader, which ADR-0002 §A3 makes a superseding-ADR event). Before
   writing a retention rule over a store, *grep its repository for a reader* — the answer changes the
   **shape** of the rule, not just its numbers.

## Rendering a server-redacted field off an entitlement flag — the deviating form (2026-08-11)

> **Enforced by:** per-surface behavioural tests at the entitled-but-not-assigned shape —
> `OrderDisclosurePresentationTest` (Android) and `OrderDetailRedactionGateTests` (iOS) — run by
> `:partner-app:testDebugUnitTest` (`.github/workflows/android-ci.yml:79`) and the `CleansiaPartner`
> scheme (`.github/workflows/ios-ci.yml:185-187`) — **`T1-CI`**. *(Was
> `(gate pending: the ADR-0047 canonicalization ticket)`; **T-0590 closed the roster and the baseline**,
> which is the claim made below by the lane that ran the build. The 2026-08-11 ADR panel had no shell
> and does not restate it as its own measurement.)* Rule, scope and rejected alternatives:
> `patterns-mobile.md` §*"The redaction narrowing of rule (1)"*;
> decision: **ADR-0047**, which is `accepted` **with amendments A1–A4**
> (`docs/decisions/adr-0047.md:3`).
> **Retires when:** that status line stops reading `accepted`.
>
> **This entry exists because the rule puts code that exists today in violation** (ADR-0033 routing
> test 1), so the superseded form is recorded here and the canonicalization is ticketed rather than
> assumed. **The baseline is non-zero and read from the tree, not counted.**

The server redacts a cleaner's view of an order on `CanAccessOrderAsync` (`GetOrderDetails.cs:58`,
applied at `:137-139`) and separately reports `isAssignedToCurrentUser` off the assignment list
(`:81-82`). The two disagree for the **employee who books a cleaning for their own home** — entitled,
not assigned — so a client gating a *rendered field* on the flag hides that person's own data from them
and is a second authorization implementation beside the server's.

**The deviating form (normative — it decides the next case):** *a conditional whose body renders a
field that `OrderPiiRedaction.RedactForBrowsingCleaner` blanks (`OrderPiiRedaction.cs:25-31`,
`:37-53`), and whose condition names `isAssignedToCurrentUser` or a local aliasing it.*

**Roster — CLOSED 2026-08-11 by T-0590 (`7fdce902` Android, `327013db` iOS). The baseline is zero.**
All three rows on both platforms were converted; each field's gate is now a named property on the
presentation model reading that field's own arrival. **Enforced by:** `OrderDisclosurePresentationTest`
(Android) and `OrderDetailRedactionGateTests` (iOS) — **`T1-CI`**, `android-ci.yml` / `ios-ci.yml`.
A call site that passes the deviating-form test and is not here means the **roster** is stale.

| # | Field | Android | iOS |
|---|---|---|---|
| 1 | `CustomerPhone` — call + SMS chips | `CustomerCard.kt:86-87` | `OrderDetailCards.swift:96-97` |
| 2 | `AccessInstructions` — access card | `OrderDetailScreen.kt:481-483` | `OrderDetailContent.swift:19-23` |
| 3 | `OrderNotes`/`OrderIssues` — notes & issues section | `OrderDetailScreen.kt:630` | `OrderDetailContent.swift:124` |

⚠️ **Two things the conversion taught that the rule as written did not say, both worth more than the
roster.**

**A named property that is only PARTLY named is not pinnable.** The Android lane's first pass left the
gate as a `val` inside the composable — still a name, still not an inline `if` in the condition — and
the mutation reinstating the entitlement flag on it **passed green**. Only moving the whole gate onto
the presentation model made it red. So the obligation is not "give it a name"; it is *the gate is
computed where a test can reach it without a UI harness*, and the way you find out is by mutating it.

**Blank is not one shape.** The rule *said* the server redacts to `string.Empty` and `[]`, and both
lanes checked rather than inheriting that: `OrderPiiRedaction.cs` sends `CustomerPhone = string.Empty`
and `OrderNotes = []` — but `AccessInstructions = null`. A `!= null` test covers neither the phone nor
a whitespace-only value; the test is `isNullOrBlank`/`isEmpty` in every case, which is what the rule
already said and what checking confirmed for a different reason than the one given.
**ADR-0047 §D4 has since been corrected to match (amendment A2, 2026-08-11):** the redaction is
**mixed** — string scalars to `""` (`OrderPiiRedaction.cs:25-29`, `:37-41`), collections to `[]`
(`:49-50`), and every free-text field plus `Address` to `null` (`:40-53`) — so **the roster spans both
forms and no single-form test covers it.** Three shipped doc comments still assert the old premise and
are ticketed for correction: `OrderDetail.swift:133`, `OrderDisclosurePresentation.kt:21`,
`OrderDetailRedactionGateTests.swift:17`.

**One conjunct GAINED rather than lost.** `canAddNotes` on both platforms had been riding the render
gate that was withdrawn, so withdrawing it alone would have offered "Add note" on a stranger's job.
Withdrawing a render gate obliges you to check what was silently depending on it.

**Explicitly NOT deviations, and this is a ruling rather than an omission** — each gates an **action**
or a **request**, fails closed, and stays exactly as written: `showWorkSections`/`showsWorkSections`
(`OrderDetailScreen.kt:607-608`, `OrderDetail.swift:119-127`) and every arm of the primary action
(`OrderPrimaryAction.kt:59`, `:97`, `:113`). *The rule is about what is rendered, never about what is
offered.* A canonicalization that deletes these is worse than the defect.

**Two things this class teaches that a "does it check entitlement?" audit would miss.**

1. **The fix is a no-op for every caller class but one.** For a browsing cleaner the server already
   blanked the field, so both terms agree; for an assignee both terms agree. They differ only for the
   entitled non-assignee. So the migration cannot widen disclosure to anyone the server withheld from —
   it is reading what the server sent.
2. **The flag is not defence-in-depth, because it is also a server field.** Keeping it "just in case"
   asks the same server a different question and calls the answer independent.

## A generated DTO coerced, or refused at the call site — the deviating form (2026-08-11)

> **Enforced by:** the per-repository `*WireTest` suites **and `WireContractRosterTest`**
> (`src/cleansia_android/core/src/test/java/cz/cleansia/core/network/WireContractRosterTest.kt`), run
> by `:core:testDebugUnitTest` / `:partner-app:testDebugUnitTest` / `:customer-app:testDebugUnitTest`
> (`.github/workflows/android-ci.yml:79`) — **`T1-CI`**. **Scope, narrower than the rule:** the roster
> is derived from the tree rather than written down — a data-layer source that names a generated
> response model and has no `*WireTest.kt` in its package fails the build — so a new repository joins
> it by existing. The per-field judgement is still not expressible by `check-consistency.mjs`: deciding
> it needs the spec's nullability for the schema the mapper targets, which is not line-local. What is
> mechanical is the presence of a pin, plus rule 1's numeric-zero coercions. Rule and rejected
> alternatives: `patterns-mobile.md` §*"And the RESPONSE side"*; decision: **ADR-0048**, which is
> `accepted` **with amendments B1–B6**
> (`docs/decisions/adr-0048.md:3`).
> **Retires when:** that status line stops reading `accepted`.
>
> **This entry exists because the rule puts code that exists today in violation** (ADR-0033 routing
> test 1). **The baseline is non-zero and read from the tree, not counted.**

**The deviating form (normative), two limbs:**

- **(a) Coercion.** A mapper from a generated model that *supplies* a value for a field the spec marks
  `nullable: false` — `?: 0.0`, `?: 0`, `?: false`, `?: ""`, `?: <n>` — or calls `.orEmpty()` on the
  **response body** rather than on a collection member, **except** where the payload-level default
  clears all three of ADR-0048 §D4 fact 4's conditions (amendment B1) *and the mapper's doc comment
  says which*: absence and empty are the same product decision on that surface, nothing sums / counts /
  paginates it, and no affordance is derived from its emptiness that a user would read as a fact.
- **(b) Call-site transport.** A refusal whose outcome is decided by the caller rather than by one
  shared wrapper — so that a 2xx body can resolve to `ApiResult.Success` or to `ApiError.Network`.
  **`ApiError.Network` stays correct for a `null` from `networkCall` (the transport really did fail);
  it is never correct for a 2xx body that breaks the contract.**

> ⚠️ **Limb (a)'s exception is not a loophole — it is §D2's own discriminator applied to the payload,
> and three live sites depend on it.** `orders/OrderApi.kt:109` and `orders/OrderRepository.kt:250`
> (the favourite-cleaner picker: *"the picker just shows an empty state so the booking proceeds without
> a preference"*, `OrderRepository.kt:240-246`) and `catalog/CatalogRepository.kt:87,92` (extras:
> *"best-effort by existing design … never a wrong add-on price"*). The same method refuses services and
> packages at `:82-83`, which is what makes `CatalogRepository.refresh` the worked example of the line.
> **Without the exception the rule puts these in violation and the "fix" degrades the booking flow.**

**Roster (descriptive — read 2026-08-11).** Limb (a), customer app under `core/`: `referral/ReferralApi.kt`,
`disputes/DisputeApi.kt`, `promo/PromoCodeApi.kt`, `notifications/NotificationPreferencesApi.kt` — each
still calling `.orEmpty()` on a list **body** rather than on a member, so a 200 with no body reads as
an empty page reported as Success.

**Three left the roster on the T-0602 follow-up, and the fourth surface it ruled is the one to read
first.** `catalog/CatalogApi.kt`, `user/SavedAddressApi.kt` and `recurring/RecurringBookingApi.kt` now
refuse a bodiless list — but `getExtras` **keeps** `.orEmpty()` as a ratified B1 exception, alongside
the three above, because it clears all three conditions **on its own reading** rather than inheriting
the file's: `selectedExtraSlugs` goes **up** to the server and the price comes **back** on the quote,
so no client-side figure moves. That is the load-bearing difference from services and packages in the
same file, which `ConfirmStep.kt:103-104` sums itself. Its helper is named `degrading` so the exception
has to be asked for by name rather than reached by habit.

⚠️ **Ruling the adapter is worth much less than following the body one layer up, and this is the case
that proves it.** `AddressRepository.kt:83` fed `response.body().orEmpty()` straight into
`writeCache(…)`, so a 2xx with no body did not merely render as *"you have no saved addresses"* — **it
wrote that answer to DataStore and deleted the customer's saved homes off the handset**, reporting
`Success`. `RecurringBookingRepository.kt:52` latched the same empty answer into `_loaded = true`, so
the manage screen rendered it as a settled fact. Ask what the empty answer **becomes**: persisted,
latched, or merely rendered. **A roster row means "not every mapper in this file is converted",
never "this file is untouched"** — sweep per mapper (ADR-0048 amendment B6).
**`memberships/MembershipApi.kt` left the roster on T-0602**: every mapper in it is now total,
`getPlans` refuses a bodiless plan list instead of defaulting it, and the fabricated `membershipId`
its cancel and swap responses carried is gone — the field is on neither C# record.
Limb (b) is **closed**: T-0588 migrated `orders/OrderRepository.kt`, whose `?: return
ApiResult.Success(Unit)` and 2xx-body `?: networkError()` sites became `wireResult { … requiredBody() }`
(`:88-143`). The surviving `?: networkError()` calls now guard a `null` from `networkCall`, which is
the correct use.

> ⚠️ **T-0588's row states the tell is *"the return type, still a generated `*Dto`"*. That is false at
> HEAD and a lane sweeping on it will read the wrong files.** `ReferralApi.getMy()` returns a
> **hand-written** `ReferralAccountDto` (`ReferralApi.kt:20-23`) through a mapper that coerces
> (`:44-50`, `:59-68`); `DisputeApi` likewise returns hand-written DTOs (`DisputeApi.kt:30-38`).
> **Sweep on the mapper's null-handling, never on the return type** — which is the same fact one level
> up: *a `toDomain()` that coerces scores clean on a "does it have a mapper?" audit.*

**Two things this class teaches that a per-field audit would miss.**

1. **The refusal can be worse than the coercion it replaced.** Customer `OrderApi.kt:122-126` refuses a
   null `total` because *"a defaulted zero silently ends pagination, so the customer's older orders
   stop existing rather than fail to load"* — and one layer up, `OrderRepository.kt:84` turns that
   refusal into `ApiResult.Success(Unit)`, which is the same silent outcome. **Rule the transport in the
   same change as the rule, or the rule is implemented N ways.**
2. **`ApiError.Network` is never an available channel for a contract violation.** The reasoning is
   already in the tree and is adopted rather than discarded — `RecurringBookingRepository.kt:110-115`:
   *"that channel is the silent one … reusing it here turns a failed write into a no-op the user never
   sees."* The network did not fail; the payload did.

## A disclosure block shipped past its sentence's truth — the deviating form (2026-08-11)

> **Enforced by:** `src/Cleansia.Tests/Features/Orders/PreferredOfferDisclosureTests.cs`
> (`.github/workflows/backend-ci.yml:69-71`) — **`T1-CI`**.
> Rule, scope and rejected alternatives: `patterns-backend.md` §*"A DISCLOSURE BLOCK is withheld by the
> server when its sentence stops being true"*; decision: **ADR-0049**, which is `accepted`
> **with amendments C1–C6**
> (`docs/decisions/adr-0049.md:3`).
> **Retires when:** that status line stops reading `accepted`.
>
> **This entry exists because the rule put code that existed at the time in violation** (ADR-0033
> routing test 1). **The baseline is read from the tree, not counted** — and unusually it was a
> *single* producer, because the block has one producer. **T-0595 repaired that producer**, so the
> entry is now a mutation guard rather than a sweep: it exists to name the shape so the next block
> does not repeat it.

**The deviating form (normative — it decides the next case):** *a read handler that populates a block
of DTO fields whose only job is to select a customer-facing sentence, without evaluating whether that
sentence is still true of the row it is about.*

The instance it was written from, **now repaired**: `GetOrderDetails.ResolvePreferredOfferAsync`
(`src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs:150-182`) derives
`PreferredOfferState` from four columns that contain no fulfilment status and no seat count
(`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:36-53`) — so until T-0595 the customer's order
detail said *"The request for the cleaner you asked for has ended. This booking is now open to our
whole team."* (`src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json:1740-1741`) on a cancelled
booking, on a finished one, **and on a live booking a different cleaner already took**. The repair is
`PreferredOffer.IsDisclosable` conjoined at the resolver, not a fifth input to the derivation.

**Explicitly NOT a deviation, and this is a ruling rather than an omission:**

- **`RespondByUtc`'s suppression** (`GetOrderDetails.cs:178-180`) is the *correct* form of this rule
  already shipping in the same method — a field withheld because its sentence is not being said.
- **A client-side status conjunct is not the remedy** and adding one to the web facade would collide
  with the guard `d5ba1484` left behind
  (`src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-preferred-offer.models.spec.ts:164-188`).
- **iOS's `isUpcoming` conjunct** (`src/cleansia_ios/CleansiaCustomer/Sources/Features/Orders/PreferredOfferPresentation.swift:23-24`)
  is recorded as **knowing duplication with a retirement condition** (ADR-0049 §D6), not as a violation
  — it agrees with the server on every input.

  > ⚠️ **Deleting it early is itself a finding, and the ordering is DEPLOY, not merge (ADR-0049
  > amendment C4).** A shipped iOS binary cannot be redeployed; a build without the conjunct, pointed at
  > an environment whose server still sends the block on concluded bookings, reopens the defect for an
  > App Store review window plus the update tail. **The conjunct is deleted the first time
  > `PreferredOfferPresentation.swift` is opened AFTER the ADR-0049 server change is live on the target
  > environment — not on the next edit for any reason, and not on merge.**
  >
  > **Carriers (both required before deletion is permitted).** (1) The Swift file's own doc comment
  > beside the conjunct — today `PreferredOfferPresentation.swift:16-19` says the **opposite** (*"the
  > narrowing is made here"*), with no ADR reference and no precondition, so a lane reading only the
  > file concludes the term is load-bearing while a lane reading only the ADR concludes it is
  > deletable. (2) A `blocked-by: ADR-0049 backend DEPLOYED` row on the deletion ticket. *A rule that
  > lives only where the deleting lane will not look is not a rule.*

**What this class teaches that a "does the client check the status?" audit would miss:** *the grouping
the ticket asks for cannot express the defect.* Every candidate status membership contains `Confirmed`,
and the sharpest false sentence is on a `Confirmed`, fully-staffed booking. The distinguishing term is
`Order.AvailableSpots <= 0` (`src/Cleansia.Core.Domain/Orders/Order.cs:136`) — a **seat count**, not a
status — which is also why `AssignedEmployees.Count > 0` is the wrong term: with
`RequiredEmployees = ceil(EstimatedTime / 120)` (`Order.cs:697-707`) a booking over two hours has a
second seat, and the sentence is still true while it is free.
