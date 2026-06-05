

# ===== employees CODE QUALITY =====

The grep for `MapToDto()` is too broad to confirm the dead `EmployeeListItem.MapToDto`. I confirmed earlier that `EmployeeListItem` the type appears only in 2 files (its definition + the mapper), so neither the type nor its mapper is consumed — dead. `GetEmployeeDocuments` confirms the A5 violation (hand-built `PagedData<T>` + manual `pageNumber`, missing `.AsNoTracking()`).

I have a comprehensive picture across all layers. Let me write the findings report.

---

# Employee Domain Audit — Code Quality / Spaghetti / Consistency

Scope audited: `Features/Employees`, `Features/EmployeeDocuments`, `Features/Dashboard` (backend); admin `employee-management` + partner `profile` (web); partner-app `dashboard` + `profile` (mobile). Rules cited from `agents/knowledge/{consistency,conventions,security-rules}.md`. Mechanical `check-consistency.mjs` run reported **16 violations** in these dirs (pasted inline below where relevant). Audit only — no code changed.

Two findings (EMP-SEC-1, EMP-SEC-2) are **security** concerns — recommend the PM invoke the Security Reviewer to own the formal gate.

---

## CRITICAL

### EMP-SEC-1 — Dashboard analytics endpoints trust `EmployeeId` from the query string (IDOR)
- **Type:** security (S1/S3 — broken object-level authorization) · **Layers:** backend
- **Where:** `Features/Dashboard/GetTimeAnalytics.cs:18-23`, `GetOrderAnalytics.cs:18-23`, `GetProductivityMetrics.cs:19-22`; exposed by `Cleansia.Web.Mobile.Partner/Controllers/DashboardController.cs:71,82,93` and `Cleansia.Web.Partner/Controllers/DashboardController.cs:61,72,83`.
- **Impact:** `GetDashboardStats` and `GetEarningsAnalytics` correctly gate by role — non-admin callers are forced onto `orderAccessService.GetCallerEmployeeIdAsync()` (`GetDashboardStats.cs:38-48`, `GetEarningsAnalytics.cs:31-41`). The other three take `EmployeeId` straight off the query string with **no role gating and no ownership check**. Any authenticated partner can call `GetTimeAnalytics?employeeId=<someone-else>` and read another cleaner's completed-order history, service breakdown, and productivity. This is exactly the S1 "UserId is server-truth" / S3 "resource-by-id must check ownership" law. Same `[Permission(Policy.CanGetCurrentEmployee)]` on all five endpoints hides the asymmetry.
- **Fix:** apply the identical role-gate the other two handlers use — if caller is not Administrator, overwrite `employeeId` with `GetCallerEmployeeIdAsync()`; only an admin may pass an arbitrary `EmployeeId`. Extract the gate into one shared helper so all five dashboard handlers resolve the target employee the same way (removes the copy-paste in `GetDashboardStats`/`GetEarningsAnalytics` too).
- **Size:** M · **Functional GAP?** No (regression in existing endpoints) — but warrants a security story.

---

## MAJOR

### EMP-1 — `GetAllEmployees` is a dead, hand-rolled parallel paged query
- **Type:** dead code + spaghetti (A1/A2/A3/A5 archetype deviation) · **Layers:** backend
- **Where:** `Features/Employees/GetAllEmployees.cs` (whole file).
- **Impact:** A second admin employee-list query that **no controller references** (only self-references in the codebase — verified). It violates the A-archetype wholesale: `record Query` with inline `Page`/`PageSize` (A1✗), inline `Skip/Take` instead of `GetPagedSort<EmployeeSort>` (A4✗), inline `new AdminEmployeeListItem(...)` projection instead of `MapToAdminDto()` (A5/B9✗), manual `Math.Ceiling` page math (A5✗), and returns a bespoke `Response` instead of `PagedData<T>` (A2✗). It duplicates `GetPagedEmployees` (which is canonical). Dead code that also models the *wrong* pattern is doubly costly — someone will copy it.
- **Fix:** delete the file (and its `Validator`/`Response`). The canonical `GetPagedEmployees` already covers the use case.
- **Size:** S · **Functional GAP?** No.

### EMP-2 — Ownership/session check duplicated into 6 validators (`AllowedToUpdateEmployee`)
- **Type:** spaghetti + security smell (B4, S3; extends tracked F5) · **Layers:** backend
- **Where:** identical private `AllowedToUpdateEmployee(...)` + `RuleFor(c => c).MustAsync(...)` block in `UpdateEmployee.cs:39-41,183-188`, `UpdatePersonalInfo.cs:25-27,50-55`, `UpdateAddressInfo.cs:28-30,62-67`, `UpdateBankDetails.cs:25-27,39-44`, `UpdateEmergencyContact.cs:25-27,40-45`, `UpdateAvailability.cs:26-28,76-81`, `UpdateIdentificationInfo.cs:31-33,108-113`.
- **Impact:** B4 is explicit: "Do **not** put ownership/session checks in the validator… ownership belongs in the handler, S3." The consistency doc already tracks `UpdateEmployee` under F5, but the **6 partner self-update siblings are not tracked** and repeat the exact 7-line ownership check. Each handler then `GetByIdAsync` + `employee!` (null-forgiving) on the same row the validator already loaded — duplicate fetch and a latent NRE if the validator path ever changes. The ownership logic living in the validator means it silently does not run if a future caller sends the command via a different pipeline.
- **Fix:** move the ownership guard into each handler (`var emp = await repo.GetByIdAsync(id); if (emp is null || emp.Id != callerEmployeeId) return Failure(NotFound)`), drop the `MustAsync(AllowedToUpdateEmployee)` from the validators, and resolve the caller's employee-id once via `IUserSessionProvider`. Validators keep shape-only rules.
- **Size:** M · **Functional GAP?** No.

### EMP-3 — Per-section partner self-update is 7 near-identical CQRS features (god-cluster by copy-paste)
- **Type:** duplication / tangled responsibility · **Layers:** backend (+ contract → mobile/web)
- **Where:** `UpdateEmployee.cs`, `UpdatePersonalInfo.cs`, `UpdateAddressInfo.cs`, `UpdateBankDetails.cs`, `UpdateEmergencyContact.cs`, `UpdateAvailability.cs`, `UpdateIdentificationInfo.cs` (all in `Features/Employees`).
- **Impact:** Beyond the ownership duplication (EMP-2), these 7 share: the `EmployeeId` validator block, `Response(EmployeeId)`, the handler `GetByIdAsync` + `employee!` guard, and — critically — `BeValidAvailability` + `ConvertAvailability` + `TimeRangeDto` are copy-pasted verbatim in **three** of them (`UpdateEmployee.cs:150-181,295-316,217`, `UpdateAvailability.cs:42-74,105-126,88`, `AdminUpdateEmployeeAvailability.cs:30-62,88-109,69`). `UpdateEmployee` is also a 339-line god-handler doing address geocoding + blob document upload + availability conversion + user update in one `Handle` (conventions "Handler file < ~200 lines" / "too many responsibilities"). The identification rules are deliberately "mirrored" across `UpdateEmployee` and `UpdateIdentificationInfo` (comment at `UpdateIdentificationInfo.cs:61-66`) — two copies that must stay in lock-step or drift into a bug.
- **Fix:** (1) extract availability parse/validate/convert into one shared `EmployeeAvailability` validator-rule + mapper used by all three. (2) Decide one canonical self-update surface: either the granular per-section commands OR the monolithic `UpdateEmployee`, not both — the monolith's document-upload concern belongs in `SaveMyDocuments` (which already exists), so `UpdateEmployee` should shrink to delegate. Architect-owned (touches the API contract / NSwag + mobile client) — raise as a design ticket.
- **Size:** L · **Functional GAP?** No.

### EMP-4 — Pervasive hardcoded English strings in partner-app profile ViewModels (E8)
- **Type:** hardcoded user-facing strings · **Layers:** mobile
- **Where:** ~18 occurrences across `partner-app/.../features/profile/viewmodels/`: `PersonalSectionViewModel.kt:82,86,91`; `BankSectionViewModel.kt:62,66`; `EmergencySectionViewModel.kt:70,71,74`; `AddressSectionViewModel.kt:201,205,220`; `IdentificationSectionViewModel.kt:131,141,144,147,150,153`.
- **Impact:** Global rule + E8: "All user-facing text via `stringResource`/`getString`." These are raw English literals (`"First name is required"`, `"IBAN is required"`, `"This country isn't serviced yet"`, `"Enter your registration number (IČO)"`) shown via `snackbar.showError(...)` / field-error state. They never localize — a Czech/Ukrainian cleaner sees English validation errors. This is the single most-repeated new finding in the domain.
- **Fix:** move every literal to `R.string.*` and surface via `appContext.getString(...)` (the VM already injects nothing for context — inject `@ApplicationContext Context` per E3). Add keys to the partner-app string resources.
- **Size:** M · **Functional GAP?** No.

### EMP-5 — Partner profile section ViewModels are flag-bag UiStates with loose action booleans (E1/E2)
- **Type:** bug-risk / spaghetti (E1, E2; extends tracked F13/F14 to a new feature) · **Layers:** mobile
- **Where:** `PersonalSectionUiState` (`PersonalSectionViewModel.kt:17-30`) and the sibling `*SectionUiState` data classes; same in `BankSectionViewModel`, `EmergencySectionViewModel`, `IdentificationSectionViewModel`, `AddressSectionViewModel`.
- **Impact:** E1 forbids the "single `data class` with `isLoading`/`error`/`isSaved` booleans (permits impossible states)." `PersonalSectionUiState` carries `isLoading`, `isSaving`, `error`, `isSaved`, plus per-field error strings — exactly the flag-bag E1 names as the defect (e.g. `isSaving=true` + `isSaved=true` simultaneously is representable). The one-shot `save()` uses loose `isSaving`/`error`/`isSaved` instead of the shared `ActionState` + `SharedFlow(replay=0)` success effect (E2). The consistency doc tracks `DashboardUiState` (F13) and customer VMs (F14) but **not** these profile-section VMs.
- **Fix:** split form-field state from screen state; model the load as sealed `Loading/Error/Loaded` and the save as `ActionState` (Idle/Submitting/Error) + a `SharedFlow` "saved" effect. Cross-cutting — fold into the F13/F14 canonicalization tickets.
- **Size:** M · **Functional GAP?** No.

### EMP-6 — `GetEmployeeDocuments` hand-builds `PagedData<T>` and skips `AsNoTracking` (A5/A6)
- **Type:** spaghetti / perf · **Layers:** backend
- **Where:** `Features/EmployeeDocuments/GetEmployeeDocuments.cs:57-64`.
- **Impact:** Mechanical check flagged `A5 Hand-built new PagedData<T>`. It computes `pageNumber` manually and constructs `new PagedData<EmployeeDocumentItem>(...)` instead of `documents.MapToDto(total, request)` (A5✗), and the read path omits `.AsNoTracking()` on an admin read (A6 — tracking overhead on a list query). It also duplicates the count-spec build (`specification.SatisfiedBy()` called 3×).
- **Fix:** return via the `items.MapToDto(totalItems, request)` extension; add `.AsNoTracking()`; build the spec filter once. Mirror the canonical `GetPagedEmployees`.
- **Size:** S · **Functional GAP?** No.

### EMP-7 — `EmployeeDocuments` admin commands inherit `UserEmailValidator` base (B3) — 5 new untracked violations
- **Type:** spaghetti (B3) · **Layers:** backend
- **Where:** mechanical check: `ApproveDocument.cs:23`, `RejectDocument.cs:23`, `DeleteDocument.cs:22`, `DeleteMyDocument.cs:23`, `SaveMyDocuments.cs:45` all `Validator : UserEmailValidator<Command>`.
- **Impact:** B3: "Validator inherits `AbstractValidator<Command>` (✗ not custom bases like `UserEmailValidator`)… compose shared rules with `.SetValidator(...)`." F6 tracks only the PayConfig/PayPeriod/Employee cases — these 5 EmployeeDocuments validators are a **new, unrecorded** instance of the same anti-pattern. `UserEmailValidator` smuggles a session lookup into validation, the same B4 concern as EMP-2.
- **Fix:** inherit `AbstractValidator<Command>`; compose the user-email rule via `.SetValidator(new UserEmailValidator<...>())` only where genuinely a shape rule, and move the existence/ownership checks to handlers.
- **Size:** M · **Functional GAP?** No.

### EMP-8 — `ProfileFacade` uses `any` and non-canonical signal names (no-any, D1)
- **Type:** spaghetti / type-safety · **Layers:** frontend
- **Where:** `partner-features/profile/.../profile.facade.ts:51` (`private profileData$: Observable<any> | null`), and the `tap(([employee, countries]) => …)` consuming it untyped.
- **Impact:** Hard rule "No `any` (TS)." The combined `getCurrentEmployee()`/`getServiced()` result has generated types (`EmployeeItem`, country DTO) — `any` defeats them and the `mapEmployeeToFormData(employee)` call is unchecked. The form facade also names its signals `profileLoading`/`profileSubmitLoading` rather than the D1 canonical `loading`/`saving`, and lacks the separate `createXxx`/`updateXxx` split (it only updates).
- **Fix:** type `profileData$` as `Observable<[EmployeeItem, CountryOverviewDto[]] | null>`; rename signals to `loading`/`saving` per D1.
- **Size:** S · **Functional GAP?** No.

---

## MINOR

### EMP-9 — Hardcoded error strings in backend handlers/validators (no-`BusinessErrorMessage`)
- **Type:** hardcoded user-facing strings · **Layers:** backend
- **Where:** `DeleteMyDocument.cs:47` (`"Cannot delete an approved document. Contact admin for assistance."`); raw i18n keys bypassing the constant: `UpdateEmployee.cs:105,119` and `UpdateIdentificationInfo.cs:80,95` (`"validation.registration_number.invalid_format"`, `"validation.vat_number.invalid_format"`).
- **Impact:** Global rule: backend user-facing text → `BusinessErrorMessage` codes (dot notation), every key mirrored in all 5 locales. The `DeleteMyDocument` string is a raw English sentence that will never localize and has no `errors.*` mirror. The `validation.*` literals are inline strings, not `BusinessErrorMessage` constants, so they can drift from the i18n files silently.
- **Fix:** add `BusinessErrorMessage.EmployeeDocument.CannotDeleteApproved` + the two registration/VAT format codes as constants; reference them; add the 5-locale keys.
- **Size:** S · **Functional GAP?** No.

### EMP-10 — Dead `EmployeeListItem` DTO + `MapToDto(this Employee)` mapper
- **Type:** dead code · **Layers:** backend
- **Where:** `Features/Employees/DTOs/EmployeeListItem.cs:6-16` (the non-admin `EmployeeListItem` record) and `Mappers/EmployeeMappers.cs:26-39`.
- **Impact:** The type `EmployeeListItem` appears only in its own definition and its mapper — no query, controller, or DTO references it (verified). It is superseded by `AdminEmployeeListItem`/`EmployeeItem`. Conventions: "No dead code. Delete unreferenced methods/classes."
- **Fix:** delete the record and the `MapToDto(this Employee)` extension.
- **Size:** S · **Functional GAP?** No.

### EMP-11 — Dead dashboard constants `DefaultEfficiencyRate` / `DefaultBestEfficiencyScore`
- **Type:** dead code / magic-number residue · **Layers:** backend
- **Where:** `Features/Dashboard/DashboardConstants.cs:13,18`.
- **Impact:** Neither constant is referenced by any handler (verified — the productivity/time handlers compute their own rates). `DefaultBestEfficiencyScore = 98.5` is a "placeholder value" per its own XML doc — leftover scaffolding.
- **Fix:** delete both constants.
- **Size:** S · **Functional GAP?** No.

### EMP-12 — Dashboard analytics handlers bypass `IQueryHandler`/`BusinessResult` (handler-shape drift)
- **Type:** spaghetti (handler-shape inconsistency) · **Layers:** backend
- **Where:** `GetEarningsAnalytics.cs:27` (`IRequestHandler<Query, BusinessResult<EarningsAnalyticsDto>>`); `GetTimeAnalytics.cs:25`, `GetOrderAnalytics.cs:27`, `GetProductivityMetrics.cs:27` (`IRequestHandler<Query, RawDto>` returning the bare DTO, no `BusinessResult`).
- **Impact:** The queries in this same domain split three ways: `GetDashboardStats` uses `IQueryHandler<Query, Dto>` (canonical), `GetEarningsAnalytics` hand-writes `IRequestHandler<…, BusinessResult<Dto>>`, and three others return a raw DTO with no `BusinessResult` envelope at all. The controllers then diverge to match (some `HandleResult`, some return the DTO directly — `DashboardController.cs:71-85`). Reuse rule: use `IQuery`/`IQueryHandler` + `BusinessResult`, don't hand-wire MediatR generics.
- **Fix:** standardize the four analytics queries on `IQuery<Dto>` + `IQueryHandler<Query, Dto>` returning `BusinessResult.Success(...)`, and `HandleResult<Dto>` in both controllers.
- **Size:** M · **Functional GAP?** No.

### EMP-13 — EmployeeDocuments DTOs are mutable classes, not records (naming/shape convention)
- **Type:** spaghetti · **Layers:** backend
- **Where:** `GetMyDocuments.cs:16-34` (`Response`/`MyDocumentDto` classes), `SaveMyDocuments.cs:18-43` (`Command`/`DocumentToSave`/`Response`/`SavedDocument` classes), `ApproveDocument.cs:12-21`, `DeleteDocument.cs:12-20`, `DeleteMyDocument.cs:13-21`.
- **Impact:** Convention: "All DTOs are `record` types with positional syntax." These features use `public class { get; init; } = default!;` DTOs and even class-based `Command`s with mutable collection inits — inconsistent with the record-based shape used everywhere else in the domain (`AdminEmployeeListItem`, `EmployeeItem`, etc.). Also `GetMyDocuments` (`MyDocumentDto`) duplicates `SaveMyDocuments.SavedDocument` and `EmployeeDocumentItem` — three overlapping document-shape DTOs.
- **Fix:** convert to positional `record`s; consolidate the three near-identical document DTOs into one shared shape where the fields genuinely match.
- **Size:** M · **Functional GAP?** No.

### EMP-14 — `IsProfileComplete` recomputed via a redundant private wrapper + duplicated profile-completeness logic
- **Type:** spaghetti / duplication · **Layers:** backend
- **Where:** `Mappers/EmployeeMappers.cs:140-143` (`IsEmployeeProfileComplete` just calls `employee.IsProfileComplete()`); `ApproveEmployee.cs` checks `IsProfileComplete()` in **both** validator (`:34-47`) and handler (`:107-112`) with a separate `.Include` graph each time.
- **Impact:** The private wrapper adds an indirection that does nothing. `ApproveEmployee` loads the employee with includes twice (validator builds a full `Include(User/Address/Documents)` graph to call `IsProfileComplete`, handler re-queries with a different include set) — two round-trips for one approval, and the completeness rule is asserted in two places that can diverge.
- **Fix:** inline the wrapper; keep the `IsProfileComplete` guard in the handler only (B4 — handler enforces business rules), drop it from the validator.
- **Size:** S · **Functional GAP?** No.

### EMP-15 — Partner mutation endpoints drop `CancellationToken`
- **Type:** convention (CT propagation) · **Layers:** backend
- **Where:** `Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:43-48,54-58,64-68,74-78,84-88,94-98,104-108` — every `UpdateEmployee`/`UpdatePersonalInfo`/`UpdateIdentificationInfo`/`UpdateAddressInfo`/`UpdateBankDetails`/`UpdateEmergencyContact`/`UpdateAvailability` action signature has no `CancellationToken` and calls `Mediator.Send(command)` without one.
- **Impact:** Conventions: "CancellationToken propagation through every async IO path." These handlers do blob uploads + geocoding (IO) that can't be cancelled when the client disconnects. The read/document endpoints in the same controller correctly pass `cancellationToken` — inconsistent within one file.
- **Fix:** add `CancellationToken cancellationToken` to each action and forward it to `Mediator.Send`.
- **Size:** S · **Functional GAP?** No.

### EMP-16 — `AdminUpdateEmployeeAvailability` carries an unused `Request` record
- **Type:** dead code · **Layers:** backend
- **Where:** `AdminUpdateEmployeeAvailability.cs:71` (`public record Request(...)`).
- **Impact:** The controller binds `AdminUpdateEmployeeAvailability.Request` at `AdminEmployeeController.cs:73`, so this one is used — but note the file also defines `TimeRangeDto` (`:69`) duplicating the same record in `UpdateAvailability.cs:88` and `UpdateEmployee.cs:217` (three definitions of the identical `record TimeRangeDto(string Start, string End)`), plus the domain `TimeRange` and the DTO `TimeRange` in `EmployeeListItem.cs:70`. Five overlapping time-range shapes.
- **Fix:** define one shared `TimeRangeDto`/`TimeRange` in the Employees DTO namespace and reuse. (Folds into EMP-3's availability extraction.)
- **Size:** S · **Functional GAP?** No.

### EMP-17 — `AdminUpdateEmployee` partial-update handler is a tangled null-coalescing block
- **Type:** spaghetti (handler complexity / B7) · **Layers:** backend
- **Where:** `AdminUpdateEmployee.cs:103-159`.
- **Impact:** The handler hand-merges every field with `command.X ?? employee.Y ?? ""` across ~50 lines, re-implements the address create/update branch already present in `UpdateAddressInfo`, and re-serializes `employee.Availability.ToDictionary(...)` purely to pass it back unchanged into `UpdateEmployeeDetails`. The empty-string fallbacks (`?? ""`) for `NationalityId`/`PassportId`/`Iban` can write blanks over real data if the domain method doesn't guard. It's the admin twin of the partner monolith (EMP-3) with its own copy of the merge logic.
- **Fix:** give the `Employee` aggregate intent-named partial-update methods (e.g. `employee.AdminUpdateContactDetails(...)`) so the handler expresses *what changed*, not field-by-field coalescing (B7 — call rich domain methods). Share the address-merge with `UpdateAddressInfo`.
- **Size:** M · **Functional GAP?** No.

---

## Ranked summary (by impact)

| ID | Title | Sev | Type | Size | GAP |
|---|---|---|---|---|---|
| EMP-SEC-1 | Dashboard analytics IDOR (`EmployeeId` from query) | Critical | security S1/S3 | M | no |
| EMP-1 | Dead hand-rolled `GetAllEmployees` paged query | Major | dead code/A-archetype | S | no |
| EMP-2 | Ownership check duplicated in 6 validators (B4/S3) | Major | spaghetti/sec | M | no |
| EMP-3 | 7 near-identical per-section update features + god-handler | Major | duplication | L | no |
| EMP-4 | ~18 hardcoded English strings in partner profile VMs (E8) | Major | i18n | M | no |
| EMP-5 | Flag-bag UiStates + loose action booleans (E1/E2) | Major | bug-risk | M | no |
| EMP-6 | `GetEmployeeDocuments` hand-built `PagedData` + no `AsNoTracking` | Major | spaghetti/perf | S | no |
| EMP-7 | 5 new `UserEmailValidator` base inheritances (B3) | Major | spaghetti | M | no |
| EMP-8 | `ProfileFacade` `any` + non-canonical signal names | Major | type-safety/D1 | S | no |
| EMP-9 | Hardcoded backend error strings | Minor | i18n | S | no |
| EMP-10 | Dead `EmployeeListItem` DTO + mapper | Minor | dead code | S | no |
| EMP-11 | Dead dashboard constants | Minor | dead code | S | no |
| EMP-12 | Analytics handlers bypass `IQueryHandler`/`BusinessResult` | Minor | spaghetti | M | no |
| EMP-13 | EmployeeDocuments DTOs are mutable classes not records | Minor | spaghetti | M | no |
| EMP-14 | Redundant `IsProfileComplete` wrapper + double-query in Approve | Minor | duplication | S | no |
| EMP-15 | Partner mutation endpoints drop `CancellationToken` | Minor | convention | S | no |
| EMP-16 | 5 overlapping `TimeRange`/`TimeRangeDto` definitions | Minor | dead code/dup | S | no |
| EMP-17 | `AdminUpdateEmployee` null-coalescing merge tangle | Minor | spaghetti/B7 | M | no |

**No functional GAPs** found in this dimension/area — the employee CRUD, approval, documents, and dashboard surfaces are all wired and reachable. (The known unreachable payroll/settlement and admin-order-intervention gaps cited in the brief sit outside this employees-domain slice.)

## Notes for the PM / Architect (pattern-evolution loop)
- **Repeating new finding — validator-side ownership checks (EMP-2, EMP-7):** the `AllowedToUpdateEmployee`/`UserEmailValidator` anti-pattern now appears in **12+** files across Employees + EmployeeDocuments, far beyond the 4 tracked in F5/F6. This is past the "3+ times" threshold — recommend the Architect (a) widen the F5/F6 canonicalization ticket and (b) add a `check-consistency.mjs` rule flagging `IUserSessionProvider` injected into any `*Validator`.
- **Repeating new finding — hardcoded strings in mobile VMs (EMP-4):** systemic in partner-app profile. Recommend an E8 mechanical check (flag string literals passed to `snackbar.showError(`/`*Error =` in `*ViewModel.kt`).
- **`check-consistency.mjs` false positives:** the B1 hits on `AdminUpdateEmployeeAvailability.cs:73`, `ApproveEmployee.cs:70`, `RejectEmployee.cs:39`, `UpdateAvailability.cs:90`, `UpdateBankDetails.cs:51`, `UpdateEmergencyContact.cs:53`, `UpdateIdentificationInfo.cs:126`, `UpdatePersonalInfo.cs:66` are nested **`Response`/`Request`** DTO records, not command records — the UoW suffix rule does not apply. The checker should exclude records named exactly `Response`/`Request`/`Query`/`Filter`/`Sort` from the "ends in `Command`" rule. Worth fixing so the signal isn't diluted.

Key files: `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Dashboard\GetTimeAnalytics.cs`, `...\GetOrderAnalytics.cs`, `...\GetProductivityMetrics.cs`, `...\Features\Employees\GetAllEmployees.cs`, `...\Features\Employees\UpdateEmployee.cs`, `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\cleansia_android\partner-app\src\main\java\cz\cleansia\partner\features\profile\viewmodels\PersonalSectionViewModel.kt`, `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-partner-features\profile\src\lib\profile\profile.facade.ts`.


# ===== catalog-config CODE QUALITY =====

This confirms both systemic findings with hard counts: **10 hardcoded English BadRequest strings** across catalog admin controllers (the same route/command-id-mismatch pattern duplicated), and `showApiError` is used in **only 1** admin feature (admin-login) across the entire admin app — confirming the catalog-config facades uniformly swallow errors. I now have everything needed for a thorough, ranked report.

---

# Audit Findings — Domain: catalog-config

Scope: `Features/{Services,Packages,Currencies,Countries,Languages,Company,Codes,FeatureFlags,EmailTemplates}` (backend `Cleansia.Core.AppServices` + `Cleansia.Web.Admin`/`Cleansia.Web.Customer` controllers) and their admin UIs in `Cleansia.App/libs/cleansia-admin-features/{service,package,currency,country,language,company,template}-management`.

Mechanical check `node agents/tools/check-consistency.mjs` returns **OK** — every issue below is one the current checker does not yet catch, which is itself a signal (see "Rules to codify").

Ranked by impact (critical first).

---

## CRITICAL

### CAT-01 — Service/Package deletes hard-delete with no in-use guard; will FK-fail or orphan order history
- **Severity:** critical · **Type:** correctness / data-integrity · **Size:** M · **Layers:** backend, db
- **Where:** `src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs:31-38`; `src/Cleansia.Core.AppServices/Features/Packages/DeletePackage.cs:30-37`
- **Evidence:** Both handlers do `repo.Remove(x!)` after a validator that checks **existence only**. `OrderService.ServiceId` (`Cleansia.Core.Domain/Orders/OrderService.cs:11`) and `OrderPackage.PackageId` (`OrderPackage.cs:11`) are real FK references to historical orders. The sibling deletes — `DeleteCurrency.cs:55`, `DeleteCountry.cs:45`, `DeleteLanguage.cs:45` — all guard with `IsInUseAsync(...)`; Service/Package do not.
- **Impact:** Deleting a catalog Service/Package that appears on any past order either throws an FK violation at `SaveChanges` (surfaced as an opaque 500, then swallowed by CAT-02) or, if a cascade is configured, destroys order-line history and breaks receipts/pay calculation retroactively. This is exactly the soft-delete/B6 case the conventions call out for "user- or business-facing entity… preserves history/audit."
- **Rule:** consistency.md **B6** (soft-delete for business entities), security-rules.md **S10** (catalog items are `IsActive`-filtered, never hard-removed once referenced). Also inconsistent with the in-use guard already standard in this same domain.
- **Fix (long-term):** add `IServiceRepository.IsInUseAsync`/`IPackageRepository.IsInUseAsync` (Orders + any pay-config references), guard in the handler returning `BusinessResult.Failure<Response>(new Error(nameof(command.ServiceId), BusinessErrorMessage.ServiceInUse))`; for referenced rows switch to `repo.Deactivate(entity)` (soft-delete) so `GetServiceOverview`'s existing `Where(s => s.IsActive)` hides them while history survives. Mirror for Package.
- **GAP:** yes (half-built delete — guard is missing). Needs a story.

### CAT-02 — Every catalog-config admin facade swallows API errors (no `showApiError` anywhere)
- **Severity:** critical · **Type:** consistency / UX correctness · **Size:** M · **Layers:** frontend
- **Where:** all of `Cleansia.App/libs/cleansia-admin-features/{currency,country,language,service,package,company,template}-management/**/*.facade.ts`. Representative: `currency-management.facade.ts:55`, `currency-form.facade.ts:66,94`, `service-form.facade.ts:141,182`, `company-info-list.facade.ts:104`.
- **Evidence:** Every client call uses `catchError(() => of(null))` (or `of([])`) and **no catalog facade calls `SnackbarService.showError`/`showApiError`** — grep finds `showApiError` in exactly one admin feature total (`admin-login`). Failures (validation error, `CurrencyInUse`, `CountryInUse`, `MissingTranslationForLanguage`, FK error from CAT-01) silently reset `loading`/`saving` and do nothing.
- **Impact:** An admin who tries to delete an in-use currency, or saves a Service with one language blank (see CAT-03), sees the form sit there with no message — a UX dead-end on the happy-path-only bar conventions explicitly reject. Hides real backend errors including CAT-01's FK failure.
- **Rule:** consistency.md **C4** ("Errors surface via `SnackbarService` (`showError`/`showApiError`); never inline strings") and **C3** (delete pipes also omit the `finalize` leg).
- **Fix:** in every facade's `catchError`, call `this.snackbarService.showApiError(err)` before `return of(null)`; add the missing `finalize(() => this.loading.set(false))` to the delete pipes (`currency-management.facade.ts:53`, `company-info-list.facade.ts:102` have no `finalize`).
- **GAP:** no (defect, not missing feature).

---

## MAJOR

### CAT-03 — Service/Package translation save silently drops blank languages, then the dropped-language error is swallowed
- **Severity:** major · **Type:** bug (interaction of two layers) · **Size:** S · **Layers:** frontend, backend
- **Where:** `service-management/src/lib/service-form/service-form.facade.ts:118-125,158-165` (`if (trans.name || trans.description)`); backend validator `Features/Services/CreateService.cs:67-74` / `UpdateService.cs:71-78` requires `allLanguageCodes.SetEquals(providedCodes)`.
- **Impact:** Frontend only sends a translation entry when a field is non-empty; backend rejects unless **all 5** language codes are present → `MissingTranslationForLanguage`. Combined with CAT-02, the admin gets no error and no save. Same shape in `package-form`.
- **Rule:** conventions.md "production-ready bar" (empty/error states are part of the work); contract/validator parity.
- **Fix:** send an entry for every language the form renders (even empty) so the backend's all-languages rule and the UI agree, or relax the validator to allow partial translations with a defined fallback — an Architect/contract decision; raise via ticket. Surfacing the error (CAT-02) is the minimum.
- **GAP:** partial (the all-languages contract is enforced one-sidedly).

### CAT-04 — FeatureFlags have full backend CRUD but **no admin UI**
- **Severity:** major · **Type:** functional gap · **Size:** L · **Layers:** frontend
- **Where:** backend complete (`Features/FeatureFlags/*`, `AdminFeatureFlagController.cs`, generated `admin-client.ts` has the client); **zero** references to feature-flags in `libs/cleansia-admin-features` or `apps/cleansia-admin.app` (grep finds none).
- **Impact:** Feature flags — which gate behavior platform-wide by global/country/tenant scope — can only be created/toggled/deleted by hitting the API directly. No operator UI. Matches the prior audit's "largely unreachable" theme for this domain.
- **Fix:** build a `feature-flag-management` admin feature (list + create/toggle/delete) using the C/D archetypes and the generated `adminFeatureFlagClient`. Depends on CAT-07 (scope enum) for a clean picker.
- **GAP:** yes — needs a user story.

### CAT-05 — Two near-identical 60-line test-email dispatch handlers (duplication + inline magic strings)
- **Severity:** major · **Type:** spaghetti / duplication · **Size:** M · **Layers:** backend
- **Where:** `Features/EmailTemplates/SendTestEmail.cs:56-113` and `SendTestEmailByType.cs:48-105` — the same `switch (EmailType)` with the same dummy arguments.
- **Evidence:** Both contain inline fixtures `"Test User"`, `"123456"`, `"RESET123"`, `"ORD-2025-0001"`, `"$99.99"`, `"Test Customer"`, `"Test Employee"`, `"2025-01"`. `"$99.99"` is a hardcoded currency-formatted string (wrong for non-USD).
- **Impact:** Adding a new `EmailType` means editing two switches; the inline fixtures are magic strings violating "no magic strings/constants live in a named home." Both `Handle` methods are pure duplication.
- **Rule:** conventions.md "Duplication" (same 60 lines in 2 places), "No magic numbers/strings."
- **Fix:** extract one `IEmailService.SendTestAsync(EmailType, recipient, languageCode, ct)` (or a `TestEmailFixtures` constant set) and have both handlers delegate; `SendTestEmail` resolves `languageCode` from the template then calls the same path.
- **GAP:** no.

### CAT-06 — `CreateFeatureFlag` Scope is a `string` with hardcoded user-facing validation message and magic literals
- **Severity:** major · **Type:** spaghetti / hardcoded strings · **Size:** M · **Layers:** backend (+ db for column, frontend later)
- **Where:** `Features/FeatureFlags/CreateFeatureFlag.cs:25-26` — `.Must(s => s is "global" or "country" or "tenant").WithMessage("Scope must be 'global', 'country', or 'tenant'.")`; entity `Cleansia.Core.Domain/Configuration/FeatureFlag.cs:22` stores `Scope` as `string`.
- **Impact:** `"global"/"country"/"tenant"` are magic strings repeated in entity default, validator, and provider lookups; the validation message is a hardcoded English literal bypassing `BusinessErrorMessage` (so it has no i18n key in any of the 5 locales).
- **Rule:** conventions.md "No hardcoded user-facing strings" (must be a `BusinessErrorMessage` code) and "No magic strings… an enum"; security-rules S8-adjacent (scope drives tenant/country gating, should be a closed enum).
- **Fix:** introduce `FeatureFlagScope` enum, store it, validate with `.IsInEnum().WithMessage(BusinessErrorMessage.FeatureFlag.InvalidScope)`, add the i18n key to all 5 locales. Flag `manual_step: ef-migration` (string→enum/int column) and `manual_step: nswag-regen`.
- **GAP:** no (refactor of existing).

### CAT-07 — `GetCurrencyOverview` does not filter `IsActive`; leaks soft-deleted currencies to clients
- **Severity:** major · **Type:** correctness (S10) · **Size:** S · **Layers:** backend
- **Where:** `Features/Currencies/GetCurrencyOverview.cs:17-21` — no `.Where(c => c.IsActive)`. Contrast `GetCountryOverview.cs:18` and `GetLanguageOverview.cs:18`, which both filter (with explanatory comments), and `GetServiceOverview.cs:21`.
- **Impact:** A deactivated currency still appears in any picker fed by the overview; if `DeleteCurrency` ever moves to soft-delete (B6/CAT-01), this query would expose deleted rows.
- **Rule:** security-rules.md **S10** ("catalog… must exclude deactivated" — no global filter for `IsActive`).
- **Fix:** add `.Where(c => c.IsActive)` to match the Country/Language overviews.
- **GAP:** no.

### CAT-08 — Inconsistent delete-guard archetype across the same domain (validator-only vs validator+handler double-check)
- **Severity:** major · **Type:** consistency · **Size:** M · **Layers:** backend
- **Where:** validator+handler double-check: `DeleteCurrency.cs`, `DeleteCountry.cs`, `DeleteLanguage.cs` (each repeats the in-use check in both layers — see explicit `// Double-check in handler as well for safety` at `DeleteCountry.cs:44`, `DeleteLanguage.cs:44`). Validator-only + `x!`: `DeleteService.cs:35`, `DeletePackage.cs:33`, `DeleteFeatureFlag.cs:28`, `SetCountryServiced.cs:37`, `ToggleFeatureFlag.cs:28`, `UpdateService.cs:104`.
- **Impact:** Two opposite interpretations of B4 coexist. The double-checkers duplicate the existence/in-use logic in validator and handler (redundant — B4 says the handler fetch-and-guard is the canonical single home); the validator-only group relies on the validator's `ExistsAsync` and uses `!` null-forgiving, which is fragile if the validator is ever bypassed. Three different `Response` shapes too (`Response(string Id)` / `Response(bool Success)` / `Response(string XId)`).
- **Rule:** consistency.md **B4** (validator validates shape; handler does the fetch-and-guard — once, not in both), **B1** (consistent `Response`).
- **Fix:** standardize on handler fetch-and-guard for all deletes/updates in this domain; keep the in-use business check only in the handler; converge on one `Response(string Id)` shape.
- **GAP:** no.

### CAT-09 — Send-test-email endpoints have no rate limiting
- **Severity:** major · **Type:** security (S5) · **Size:** S · **Layers:** backend · **flag for `security` reviewer**
- **Where:** `AdminEmailTemplateController.cs:42-59` (`SendTestEmailByType`) and `:110-128` (`SendTestEmail`) — `[Permission]` but no `[EnableRateLimiting]`.
- **Impact:** An authenticated admin can drive unbounded SendGrid sends to an arbitrary `RecipientEmail` (cost + potential abuse to spam third parties from the platform domain). S5 requires a narrower per-user limit on email-sending mutations.
- **Rule:** security-rules.md **S5**.
- **Fix:** add a per-user rate-limiting policy to both send-test endpoints. PM should route to `security` for the gate.
- **GAP:** no.

---

## MINOR

### CAT-10 — 10 hardcoded English BadRequest strings (route/command-id mismatch) duplicated across admin catalog controllers
- **Severity:** minor · **Type:** hardcoded strings / duplication · **Size:** S · **Layers:** backend
- **Where:** `AdminServiceController.cs:89`, `AdminPackageController.cs:73`, `AdminCurrencyController.cs:69`, `AdminCountryController.cs:69`, `AdminLanguageController.cs:69`, `AdminCompanyController.cs:71`, `AdminEmailTemplateController.cs:55,104,124` (+ `AdminServiceCityController.cs:49` outside scope).
- **Impact:** Same guard copy-pasted 10×, each with a raw English literal (no `BusinessErrorMessage`, no i18n).
- **Rule:** conventions.md "No hardcoded user-facing strings"; "Duplication."
- **Fix:** a shared controller helper `EnsureRouteMatches(routeId, commandId)` returning a `BusinessErrorMessage.Common.RouteIdMismatch` failure.
- **GAP:** no.

### CAT-11 — `GetPagedServices` read-path order deviation (already tracked as F3, lives in this domain)
- **Severity:** minor · **Type:** spaghetti/perf · **Size:** S · **Layers:** backend
- **Where:** `Features/Services/GetPagedServices.cs:33-39` — `AsNoTracking` before `Include`, then materialize-then-`.Select(MapToDto)`. Canonical is `Include → AsNoTracking → Select(MapToDto) → ToListAsync` (as `GetPagedPackages.cs:33-37`, `GetPagedCompanyInfo.cs:35-40`, `GetPagedEmailTemplates.cs:35-39` all do).
- **Rule:** consistency.md **A6**. Already recorded as **F3** in `consistency-violations.md` — do not double-count; included for domain completeness.
- **GAP:** no.

### CAT-12 — Overview queries use raw `IRequest`/`IRequestHandler` instead of the project's `IQuery`/`IQueryHandler`
- **Severity:** minor · **Type:** reuse-the-real-types drift · **Size:** S · **Layers:** backend
- **Where:** `GetServiceOverview.cs:11-13`, `GetCurrencyOverview.cs:11-13`, `GetCountryOverview.cs:11-13`, `GetLanguageOverview.cs:11-13`, `GetServicedCountries.cs:15-17`, `GetCodeOverview.cs:11-12`. The CQRS commands/single-item queries in the same domain use `IQuery<T>`/`IQueryHandler<T>` (e.g. `GetCompanyInfo.cs:12`).
- **Impact:** Two query abstractions in one domain; `IRequest` bypasses whatever cross-cutting behavior `IQuery` carries. `GetServiceOverview` also materializes-then-maps without `AsNoTracking` (`GetServiceOverview.cs:20-25`).
- **Rule:** conventions.md "Reuse the real types"; consistency.md A-family intent.
- **Fix:** convert overview/list queries to `IQuery<IEnumerable<T>>` + `IQueryHandler<...>`; add `AsNoTracking()` to `GetServiceOverview`.
- **GAP:** no.

### CAT-13 — `DeleteFeatureFlag` returns bare `ICommand` with no `Response`; `ToggleFeatureFlag` validator lacks `Cascade`/message
- **Severity:** minor · **Type:** consistency · **Size:** S · **Layers:** backend
- **Where:** `Features/FeatureFlags/DeleteFeatureFlag.cs:11` (`ICommand`, B1); `ToggleFeatureFlag.cs:17` (`.NotEmpty()` with no `.Cascade(CascadeMode.Stop)` and no `BusinessErrorMessage`, B3); both handlers name the param `request` not `command` and use `flag!`/`x!` null-forgiving.
- **Rule:** consistency.md **B1**, **B3** (new instances of the F4-class issue, not yet tracked for FeatureFlags).
- **Fix:** wrap delete output in a `Response(string Id)`; add `.Cascade(CascadeMode.Stop)` + `BusinessErrorMessage.Required`; rename params to `command`.
- **GAP:** no.

### CAT-14 — Contradictory doc-comments on Country overview audience
- **Severity:** minor · **Type:** doc drift · **Size:** S · **Layers:** backend
- **Where:** `GetServicedCountries.cs:8-12` says `GetCountryOverview` is "admin-only", but `GetCountryOverview.cs:17` comments "Customer-facing — hide countries the admin has deactivated." One of the two is wrong.
- **Impact:** Misleads the next developer about which query is safe to expose to end users.
- **Fix:** reconcile — confirm intended audience and correct the comment (and verify the consuming controllers match).
- **GAP:** no.

### CAT-15 — Form facades are heavily duplicated across catalogs (load/create/update triplet)
- **Severity:** minor · **Type:** duplication · **Size:** M · **Layers:** frontend
- **Where:** `currency-form.facade.ts`, `service-form.facade.ts`, `country-form`, `language-form`, `company-info-form` — structurally identical `load/create/update + navigate + saving signal` bodies differing only in client/command types.
- **Impact:** Maintenance multiplier; the CAT-02 error-swallowing bug had to be fixed in N places because the shape was copied N times. Per conventions.md "Duplication," confirm intent before unifying (these genuinely mean the same thing).
- **Fix:** consider a typed generic `CatalogFormFacade<TCreate, TUpdate, TDetail>` base, or at minimum fix CAT-02/CAT-03 uniformly. Architect call before unifying.
- **GAP:** no.

---

## Rules to codify (pattern-evolution loop → Architect)
Two findings recur enough to deserve a mechanical check rather than hand-catching:
1. **C4 swallowed errors** — `catchError(() => of(null|[]))` without a `showApiError`/`showError` in the same pipe is present in *every* catalog facade and was invisible to `check-consistency.mjs`. Recommend a checker rule: flag a `catchError` returning `of(...)` in a `*.facade.ts` that has no `Snackbar` error call in the same chain.
2. **Hardcoded `BadRequest("...")`/`.WithMessage("literal")`** — recommend a checker rule flagging string literals passed to `BadRequest(`/`.WithMessage(` that aren't a `BusinessErrorMessage.*` reference (10 controller hits + `CreateFeatureFlag.cs:26`).

## Suggested story-worthy GAPs (for the PM)
- **CAT-01** (Service/Package soft-delete + in-use guard) — critical, M.
- **CAT-04** (FeatureFlag admin UI) — major, L.
- **CAT-03/CAT-06/CAT-07** carry contract/migration decisions (`manual_step: ef-migration`, `nswag-regen`) — Architect input before build.

**Verdict for the domain: CHANGES REQUESTED.** CAT-01 and CAT-02 are the two that should block a "done" on anything in this domain; the rest are canonicalization/gap tickets. Route CAT-09 to the `security` reviewer and CAT-03/06/07/15 design decisions to the `architect`.


# ===== disputes-addresses PERFORMANCE =====

Confirmed: the customer disputes feature imports DTO types (`DisputeListItem`, `DisputeReason`, `DisputeStatus`, `AddDisputeMessageCommand`, etc.) and `OrderListItem` from `@cleansia/partner-services` rather than `@cleansia/customer-services`. The actual API call uses `customerClient` (correct), but the type imports pull from the partner client library. For type-only imports this is usually tree-shaken, but it creates a cross-app coupling and risks pulling the partner client into the customer SSR bundle. Let me verify whether these are type-only or whether enums (runtime values) are imported, since enums are runtime code.

The imports include `DisputeReason` and `DisputeStatus` which are used as runtime enum values (`DisputeReason.QualityIssue`, switch on `DisputeStatus.Pending`), so this is a runtime dependency on `@cleansia/partner-services` from the customer app — a real bundle concern. I now have a complete picture. Writing the findings report.

## Performance & Cost Audit — Disputes / Addresses / SavedAddresses

Scope read: backend `Features/Disputes`, `Features/SavedAddresses`, `Features/Addresses` (DTO-only), domain entities + EF configs + repositories, customer + mobile-customer + partner controllers, Angular customer disputes feature + NgRx dispute store, Android customer `features/disputes` + `features/addresses`. Grounded against `agents/knowledge/patterns-backend.md` (rule A6: read path is `.Include(only-what-mapper-reads) → .AsNoTracking() → .Select(MapToDto) → .ToListAsync(ct)`; line 138 "include ONLY what the mapper reads (perf)"; line 193 "Use `.AsNoTracking()` + `.AsSplitQuery()` on read paths") and `consistency.md` A4/A6.

Ranked by impact.

---

**PERF-D1 — `GetPagedDisputes` over-fetches two whole collection graphs the list mapper never reads (A6 violation)**
- Severity: **major** · Type: over-fetch / N+1-class · Layer: backend (AppServices + Infra)
- `src/Cleansia.Core.AppServices/Features/Disputes/GetPagedDisputes.cs:44-53`
- The list query does `.Include(d => d.Order).Include(d => d.User).Include(d => d.Messages).Include(d => d.Evidence).AsSplitQuery().AsNoTracking().Select(d => d.MapToListItem())`. `MapToListItem` (`DisputeMappers.cs:12-26`) only reads `Order.DisplayOrderNumber`, `User.First/LastName/Email` and scalar dispute columns — it never touches `Messages` or `Evidence`. With the trailing `.Select(projection)`, EF either ignores the collection `Include`s (wasted code) or, with `AsSplitQuery`, fires extra `SELECT * FROM DisputeMessages/DisputeEvidence WHERE DisputeId IN (...)` round trips per page — pulling every message body (up to 2000 chars each) and every evidence row for the whole page just to discard them. On the admin dispute list (hot, paged, all-tenant) this is the dominant cost.
- Cost: 2 extra split-query round trips per page + transfer of full message/evidence rows that are thrown away; grows with thread length.
- Fix: drop the `Messages` and `Evidence` includes; keep only `Order` and `User`. Better, project entirely in SQL with `.Select(...)` of the exact `DisputeListItem` fields (no `Include` needed for the two `*-to-one` navs once projected) per the canonical A6 pattern. Verify the generated SQL is a single statement.
- Size: **S** · Functional GAP: no

---

**PERF-D2 — Three write handlers load the full dispute aggregate (Order + User + Messages + Authors + Evidence) to mutate one scalar/row**
- Severity: **major** · Type: over-fetch on write path · Layer: backend (AppServices + Infra)
- `AddDisputeMessage.cs:48`, `ResolveDispute.cs:45`, `UpdateDisputeStatus.cs:37` all call `GetDisputeWithDetailsAsync` / `GetByIdAsync`, both of which (`DisputeRepository.cs:26-48`) eager-load `Order`, `User`, `Messages.ThenInclude(Author)`, `Evidence` with `AsSplitQuery`.
  - `AddDisputeMessage` only needs `dispute.UserId` + `dispute.TenantId` to authorize and append one child row — yet it materializes the entire thread and every author User on every customer reply.
  - `ResolveDispute` / `UpdateDisputeStatus` only flip status + a few scalars — they don't read any navigation.
- Cost: per-message and per-status-change, 4–5 extra joins/split queries plus full message-body transfer; this scales linearly with thread size, so the busiest disputes (most messages) pay the most on every reply — the worst possible shape.
- Fix: add a lightweight `GetForUpdateAsync(id, ct)` (no includes, tracked) on `IDisputeRepository` and use it in these three handlers. Note `GetDisputeWithDetailsAsync` also **ignores the CancellationToken** (see PERF-D3). `AddMessage`/`AddEvidence` append to the tracked aggregate without needing the collection pre-loaded.
- Size: **S** · Functional GAP: no

---

**PERF-D3 — `GetDisputeWithDetailsAsync` drops `CancellationToken` and never uses `AsNoTracking` on read paths**
- Severity: **major** · Type: async hygiene / tracking · Layer: backend (Infra + AppServices)
- `IDisputeRepository.cs:24` and `DisputeRepository.cs:26-36`: signature takes no `CancellationToken`, so `GetDisputeDetails.Handler` (`GetDisputeDetails.cs:24`) cannot propagate cancellation — a customer who abandons the detail dialog leaves the DB query (with 4 split sub-queries) running and the connection held. Violates the system rule "`CancellationToken` propagated so cancelled requests free connections."
- Additionally the method is used by the **read** path `GetDisputeDetails` with no `.AsNoTracking()`, so EF builds change-tracking snapshots for the Order + User + every Message + Author + Evidence on a pure read. Contradicts patterns-backend line 193.
- Fix: add `CancellationToken` to the interface + impl and thread it through `GetDisputeDetails`. Split into two methods: a tracked one for the (now removed per D2) write callers and an `AsNoTracking` one for `GetDisputeDetails`. Apply `.AsNoTracking()` on the read variant.
- Size: **S** · Functional GAP: no

---

**PERF-A1 — Address dedup lookup has no supporting index; runs a 4-predicate `citext` scan on every saved-address create/update (and likely order create)**
- Severity: **major** · Type: missing index · Layer: backend (DB) — coordinate with DB Master
- `AddressRepository.GetAddressAsync` (`AddressRepository.cs:9-16`) filters `Street == ? && City == ? && ZipCode == ? && CountryId == ?`. `Street`/`City` are `citext` (`AddressEntityConfiguration.cs:13-21`). There is **no index** on `Addresses` for this combination (the config defines none beyond the PK). Every `AddSavedAddress`/`UpdateSavedAddress` (and any order-creation dedup using the same repo) does a sequential scan of the whole `Addresses` table, case-insensitively comparing two `citext` columns — cost grows linearly with total platform address count across all tenants.
- Fix: add a composite index `(CountryId, ZipCode, City, Street)` (most-selective-first; `citext` columns index fine for equality). Coordinate the migration with DB Master. Add a `MANUAL_STEP` for the migration.
- Size: **S** · Functional GAP: no

---

**PERF-A2 — `AddSavedAddress` issues redundant per-request DB round trips for country + duplicate-check**
- Severity: **minor** · Type: extra round trips · Layer: backend (AppServices)
- `AddSavedAddress.cs:95-134`: when `CountryId` is empty it does `GetByIsoCodeAsync("CZE")` (+ possible fallback `FirstOrDefaultAsync`), then later `GetByIdAsync(countryId)` again to fetch the same country's `Name` for the response (2 reads of the same row). It also calls `savedAddressRepository.GetByUserAsync(userId)` (which eager-loads every saved address + Address + Country for the user) purely to test `existing.Any(s => s.AddressId == address.Id)` — materializing the whole list to do an existence check. `UpdateSavedAddress.cs:122-149` repeats the double country fetch.
- Cost: 1–2 redundant single-row reads + one full saved-address-graph materialization per create; small per call but on a write path with a default country constant.
- Fix: reuse the already-fetched country entity for the response `Name` instead of re-querying by id. Replace the `GetByUserAsync(...).Any(...)` existence check with a targeted `AnyAsync(s => s.UserId == userId && s.AddressId == address.Id, ct)` on the repository. Cache/short-circuit the default-country lookup (it's a constant).
- Size: **S** · Functional GAP: no

---

**PERF-A3 — `SetDefaultSavedAddress` / `UpdateSavedAddress` / `DeleteSavedAddress` re-load the same row 2–3× across validator + handler**
- Severity: **minor** · Type: duplicate fetch · Layer: backend (AppServices)
- In each of `SetDefaultSavedAddress.cs`, `UpdateSavedAddress.cs`, `DeleteSavedAddress.cs`, the validator calls `GetByIdAsync(id)` twice (`ExistsAsync` then `BeOwnedByCallerAsync`), and the handler calls `GetByIdAsync(id)` again — 3 identical single-row reads per request. `SetDefault` additionally fetches then runs `ClearDefaultForUserAsync` which re-queries+materializes all of the user's default rows.
- Cost: 2 redundant round trips per mutating request; correctness is fine, it's pure waste.
- Fix: collapse the two validator rules into one `MustAsync` that fetches once and checks existence+ownership together (the codebase already has the per-row fetch). The duplicate handler fetch is inherent to the validator/handler split, but `ClearDefaultForUserAsync` can be a single set-based `ExecuteUpdate`/targeted update rather than load-all-then-loop.
- Size: **S** · Functional GAP: no

---

**PERF-F1 — Customer disputes feature depends on `@cleansia/partner-services` (enums are runtime) — cross-app coupling + SSR bundle risk**
- Severity: **minor** · Type: bundle / wrong dependency · Layer: frontend (customer SSR app)
- `disputes.component.ts:6-10` and `disputes.facade.ts:15-21` import `DisputeReason`, `DisputeStatus` (used as runtime enum values — `DisputeReason.QualityIssue`, `switch(status.value){ case DisputeStatus.Pending }`), plus `DisputeListItem`, `AddDisputeMessageCommand`, `CreateDisputeCommand`, `OrderListItem` from `@cleansia/partner-services`. The runtime API calls correctly use `customerClient`, but the runtime enum imports pull the **partner** generated client lib into the **customer** app's module graph, risking the whole partner NSwag client being bundled (it is SSR-rendered, so this also lands on the server build).
- Fix: import these DTOs/enums from `@cleansia/customer-services` (the customer client must expose equivalent generated types). Flag `manual_step: nswag-regen` if the customer client doesn't yet emit them. Verify with a bundle analyzer that `partner-services` is no longer in the customer chunk.
- Size: **S** · Functional GAP: no

---

**PERF-F2 — Disputes page eagerly fetches up to 100 orders on every init regardless of whether the user files a dispute**
- Severity: **minor** · Type: needless fetch · Layer: frontend
- `disputes.component.ts:109` calls `facade.loadOrdersForSelect()` in `ngOnInit`, which dispatches `loadCustomerOrders({ offset: 0, limit: 100 })` (`disputes.facade.ts:64-65`). Every visit to "My disputes" pulls 100 order list items solely to populate the create-dialog `p-select`, even for users who only want to read existing disputes.
- Fix: lazy-load the order options when `showCreateDialog` is first opened (or when arriving with a `?orderId` query param), not on page init.
- Size: **S** · Functional GAP: no

---

**PERF-M1 — Android Address Manager list renders all saved-address rows eagerly (no `LazyColumn`), each with a `DropdownMenu`**
- Severity: **minor** · Type: recomposition / eager compose · Layer: mobile (customer Android)
- `AddressManagerScreen.kt:261-303`: `ListPane` uses `Column(...).verticalScroll(rememberScrollState())` + `addresses.forEach { SavedAddressRow(...) }`. Every row (each containing a `DropdownMenu` with three items) is composed and measured up-front rather than windowed. Saved-address counts are normally small, so impact is low, but the codebase already uses `LazyColumn` for the disputes list — this is the inconsistent, heavier path.
- Fix: convert to `LazyColumn` with `items(addresses, key = { it.id })`. Keeps a single stable-keyed list consistent with `DisputesListScreen`.
- Size: **S** · Functional GAP: no

---

**PERF-M2 — Dispute detail thread builds items via `forEach { item { } }` and recomputes derived state per recomposition (minor)**
- Severity: **minor** · Type: recomposition · Layer: mobile (customer Android)
- `DisputeDetailScreen.kt:322-326,343-356`: messages and evidence are emitted with `dispute.messages?.forEach { item(key=...) { } }` / `evidence.forEach { item(...) }` instead of the `items(list, key=)` LazyList DSL — functionally keyed but bypasses the optimized item factory. `disputeAllowsMessages`/`disputeStatusColor` (`:296,372,373,237`) are recomputed on every recomposition; cheap, but could be `remember`-derived from the status value.
- Note: this screen is otherwise exemplary — image decode is capped to screen size (`:632-642`, prevents the documented ANR), file IO is on `Dispatchers.IO` (`:157`), and the list is keyed. `DisputesListScreen` correctly uses `derivedStateOf` for the load-more trigger.
- Fix: use `items(dispute.messages, key = { it.id })` and `items(evidence, key = { it.id })`; wrap the two derived helpers in `remember(dispute.status?.value)`.
- Size: **S** · Functional GAP: no

---

### Not defects (checked, conform to documented norms)
- Count-then-page in `GetPagedDisputes` (`GetCountAsync` + `GetPagedSort`) is the **documented** A4/A5 pattern — not a round-trip defect.
- `GetDisputesByUserIdAsync` (`DisputeRepository.cs:10-17`) has no `AsNoTracking` **correctly** — it feeds GDPR anonymize/cascade-delete which mutates the loaded graph; tracking is required there.
- `SavedAddresses` partial-unique + lookup indexes on `UserId` exist (`SavedAddressEntityConfiguration.cs:32-39`); `Disputes` has indexes on `OrderId/UserId/Status/CreatedOn`, message/evidence on `DisputeId/CreatedOn|UploadedOn` — adequate for current filters/sorts.
- `UploadDisputeEvidence` reads with a single `FirstOrDefaultAsync(ct)` and no over-include — fine.

### Cross-cutting note for the PM
These are all efficiency findings; none is a functional GAP requiring a user story. PERF-A1 needs a coordinated EF migration (`MANUAL_STEP`), and PERF-F1 may need `manual_step: nswag-regen` if the customer client lacks the dispute enums/DTOs. Highest-leverage quick wins: PERF-D1, PERF-D2, PERF-D3 (all backend, all size S, all on hot dispute paths) and PERF-A1 (single index).


# ===== identity-auth SECURITY S =====

Confirmed a critical cross-app privilege issue. Both `Cleansia.Web.Mobile.Customer` and `Cleansia.Web.Mobile.Partner` validate the SAME audience `JwtAudiences.Mobile`... wait, mobile customer pins `ValidAudience = JwtAudiences.Customer` (line 160) but its login issues via `HostAudienceProvider(JwtAudiences.Customer)`. Mobile partner pins `JwtAudiences.Mobile`. So a token minted by mobile-customer login has audience `customer`, validated by both Web.Customer AND Mobile.Customer. The mobile partner host validates `Mobile` audience. 

But the key problem: `PartnerLogin` enforces `Profile in {Employee, Administrator}`, and mobile-partner login uses `PartnerLogin`. However the mobile-partner `GoogleAuth` uses the shared `GoogleAuth` handler which does NOT check profile — a Customer can `GoogleAuth` against the partner mobile host and receive a `JwtAudiences.Mobile` token with Customer role. Then partner endpoints gated `EmployeeOrAdmin` would still reject by role. So role gating saves it for partner endpoints. The real exposures remain GoogleAuth identity-binding, the rate limiter, and the codes.

I now have everything needed. Let me compose the findings report.

## SECURITY AUDIT — identity-auth domain (Auth, Users, AdminUsers, Devices, Gdpr, Notifications, DataRetention)

Scope read: 7 controllers each in Customer/Partner/Admin + 3 mobile hosts (Mobile.Customer, Mobile.Partner), all Auth/Users/AdminUsers/Devices/Gdpr/Notifications command+query handlers, `GdprDeletionService`, `PolicyBuilder`, the rate-limiter config, the EF tenant filter, and the `User` entity. Findings ranked by impact.

---

### IDA-SEC-01 — Google sign-in trusts the client-supplied email/GoogleId instead of the verified token claims (account takeover)
- Severity: critical · Type: S1 / broken authentication · Size: M · Layers: backend
- File: `src/Cleansia.Core.AppServices/Features/Auth/GoogleAuth.cs:59-75` (validator) and `:93-103` (handler)
- Concrete risk: `ValidateGoogleUserAsync` calls `GoogleJsonWebSignature.ValidateAsync(token, ...)` but **throws away the result** — it never compares the verified token's `Email`/`Subject` against `command.Email`/`command.GoogleId`. The handler then looks the user up by the **body-supplied** `command.Email` (`userRepository.GetByEmailAsync(command.Email)`) and issues a full JWT for that account. An attacker presents a genuine Google token for their own throwaway Google account but sets `Email: victim@x.com` in the JSON body, and receives a valid session for the victim — provided the victim's `AuthenticationType == Google` (the only gate, `UserAuthenticationTypeIsGoogle`, also keys off the spoofed email). This is a direct, no-credential account takeover of every Google-auth user, on both the customer web host (`Cleansia.Web.Customer/Controllers/AuthController.cs:51`) and partner mobile host (`Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:59`).
- Secondary: `if (_googleConfig.IsDevelopment) return true;` (`GoogleAuth.cs:63-66`) bypasses all signature validation when the dev flag is set — a config slip in a deployed environment makes auth fully forgeable.
- Long-term fix: validate the token first, then bind identity to the **payload**: `var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings)` with `Audience` restricted to your Google client id; require `payload.EmailVerified`; then in the handler resolve the user by `payload.Subject` (Google `sub`) / `payload.Email` — never from the request body. Drop `Email`/`GoogleId` from the command (or treat them as untrusted and overwrite). Remove the `IsDevelopment` short-circuit or gate it behind a test-only fake validator registered in DI, not a runtime branch.
- Functional GAP: no (security defect in existing code), but the fix touches the DTO contract → `manual_step: nswag-regen`.

---

### IDA-SEC-02 — The "auth" rate limiter is a single global bucket, not per-IP/per-account (brute-force is not actually limited; it is a global DoS)
- Severity: critical · Type: S5 · Size: M · Layers: backend
- File: `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:76-92`
- Concrete risk: `AddFixedWindowLimiter("auth", …) { PermitLimit = 10 }` is configured with **no partition key** (no `PartitionedRateLimiter`/`GetPartition`/`RequireHeaderOrClaim`). A named fixed-window policy with no partition is one bucket shared by **all callers**. Two consequences: (1) the comment "10 req/min/partition" is false — there is no per-IP or per-account isolation, so a distributed credential-stuffing run is not throttled relative to the global pool, and a 6-digit confirmation/reset code (IDA-SEC-03) can be ground down because the limiter doesn't track the attacker individually; (2) far worse, any single client can spend the 10/min on `/Login` and lock out **every** legitimate login/register/reset across the whole API for the rest of each window — a trivial global denial-of-service. This affects every endpoint tagged `[EnableRateLimiting("auth")]` across all five hosts.
- Long-term fix: replace the named limiters with `options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(partitionKey: <client IP, or IP+email for login>, …))` so each partition gets its own 10/min window. Partition on `HttpContext.Connection.RemoteIpAddress` (honoring `X-Forwarded-For` behind the proxy) for anonymous auth routes and on the user id for authenticated mutations. Keep `QueueLimit = 0`. Add an integration test asserting client A's 429s don't affect client B.
- Functional GAP: partial — the throttling control is half-built (policies exist, partitioning never wired).

---

### IDA-SEC-03 — Email-confirmation and password-reset codes are 6-digit non-cryptographic numbers, looked up by code alone (brute-forceable; reset → account takeover)
- Severity: critical · Type: broken auth / weak secret (S5-adjacent) · Size: M · Layers: backend, db
- File: `src/Cleansia.Core.Domain/Users/User.cs:95-96, 113-116, 173-176` (code generation); `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:45-48` (`GetByConfirmationCodeAsync` — code-only lookup); `ConfirmUserEmail.cs:35-53`; `ChangePassword.cs` reset path
- Concrete risk: both `ConfirmationCode` and `ResetPasswordCode` are `Random.Shared.Next(100000, 999999)` — a 900k-space, **non-cryptographic** PRNG (predictable from observed outputs), valid for 15 minutes. `ConfirmUserEmail` resolves the user by **the code alone** (`GetByConfirmationCodeAsync(code)`), with no email/identifier binding — so an attacker doesn't even need to target a specific account; any of the currently-outstanding 6-digit codes confirms (and logs in as, since `ConfirmUserEmail` returns a JWT) whichever account holds it. Combined with the broken rate limiter (IDA-SEC-02), 900k guesses are feasible within a window. The reset code at least binds to email, but is the same weak 6-digit space and directly resets the password → takeover.
- Long-term fix: generate codes with a CSPRNG. For password reset and email confirmation use a high-entropy URL-safe token (≥128 bits, `RandomNumberGenerator`), store only a hash, and look up reset by `(email, token)`. If a 6-digit OTP UX is required, bind it to the account, cap attempts per code (e.g. 5) and invalidate on exhaustion, and fix the rate limiter. Never resolve a session-issuing flow by code alone.
- Functional GAP: no — hardening of existing flow; touches schema (token columns/hashes) → `manual_step: ef-migration`.

---

### IDA-SEC-04 — Any Employee can read any user's full PII by id (no ownership check; `OwnerOrElevated` grants all employees)
- Severity: major · Type: S3 / S4 · Size: S · Layers: backend
- File: `src/Cleansia.Web.Partner/Controllers/UserController.cs:28-39` (`GetById` → `GetUser.Query`), handler `src/Cleansia.Core.AppServices/Features/Users/GetUser.cs:31-41`, policy map `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:48` (`CanViewUserDetail = OwnerOrElevated`), policy impl `src/Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:211-228`
- Concrete risk: `GetUser.Handler` returns a `UserItem` containing `Email`, `PhoneNumber`, `FirstName`, `LastName`, `Id` for an arbitrary `UserId` with **no ownership or relationship check**. The gating policy `OwnerOrElevated` returns `true` for **any Employee** (`user.IsInRole(Employee) → true`), not just admins. So any cleaner can enumerate every customer's email and phone number in their tenant by iterating ids — a wholesale PII harvest. (Tenant boundary is held by the `ExistsAsync` filter, so it's in-tenant only, but that's still every customer.) The `Policy.cs:50` comment "Authenticated (All roles)" also mismatches the actual `OwnerOrElevated` mapping.
- Long-term fix: this lookup-any-user-by-id is an admin/support capability — map it to `AdminOnly`, or add an explicit relationship check in the handler (e.g. the employee is assigned to an order for that customer) before returning contact PII. If employees legitimately need a customer's contact info for an assigned job, expose only that narrow, order-scoped field set, not the full `UserItem`.
- Functional GAP: partial — the intended "owner can read self" path is also broken (see IDA-SEC-09), so the access model here is half-specified.

---

### IDA-SEC-05 — PII and the confirmation code are written to logs at Warning level
- Severity: major · Type: S6 · Size: S · Layers: backend
- File: `src/Cleansia.Core.AppServices/Features/Auth/ConfirmUserEmail.cs:41` and `:47-48`
- Concrete risk: `_logger.LogWarning("Email confirmation failed: no user found with code {Code}", command.Code)` logs the **confirmation code** (a session-issuing secret) at Warning, and the expiry branch logs `user.Email` plus the code at Warning. Codes and email land in centralized log storage (queryable, longer-retained, broader access than the DB) — a code logged here is replayable within its 15-min TTL by anyone with log access, and the email is plain PII. `Register.cs:102-111` also logs `ReferralCode` at Information/Warning (lower sensitivity, but still a user-entered token).
- Long-term fix: never log confirmation/reset codes at any level. Log the failure with a non-reversible discriminator (a hash prefix or just "code not found"/"code expired") and `userId` instead of `user.Email`. Drop the `{Code}` and `{Email}` template args; demote any unavoidable PII to `LogDebug`.
- Functional GAP: no.

---

### IDA-SEC-06 — Customer/Partner refresh-token rotation does not re-check the user's profile, only the audience
- Severity: major · Type: S2 / privilege scoping · Size: S · Layers: backend
- File: `src/Cleansia.Web.Customer/Controllers/AuthController.cs:90`, `src/Cleansia.Web.Partner/Controllers/AuthController.cs:101`, `src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:106`, handler `src/Cleansia.Core.AppServices/Features/Auth/RefreshToken.cs:75-79`
- Concrete risk: `RefreshToken.Handler` only enforces `RequiredProfile` when the controller sets it. The Admin host sets `RequiredProfile = Administrator` (`AdminAuthController.cs:48`), but **Customer, Partner, and both mobile hosts set only `RequiredAudience`**. The login commands enforce role at login (`PartnerLogin` requires Employee/Admin; `AdminLogin` requires Administrator) — but `Login` (customer) enforces **no** profile at all, so an Administrator/Employee who authenticates through the customer host gets a `customer`-audience refresh token, and nothing on the refresh path re-validates that the bearer's role still matches the host's intended audience population. More concretely: refresh is the place where a deactivated-then-reactivated or role-changed account should be re-pinned to the host; today a partner-audience refresh will happily re-mint for a user whose profile was demoted to Customer, because only audience is checked. The protection that *does* exist (re-loading the user and checking `IsActive` at `:68-73`) is good and should be the template.
- Long-term fix: have each host pass the `RequiredProfile` it expects (Customer host: Customer; Partner host: Employee/Administrator) so refresh re-pins role on every rotation, mirroring the Admin host. Equivalently, validate the role claim against the host's allowed set in the controller before `Mediator.Send`.
- Functional GAP: partial — the `RequiredProfile` mechanism exists but is only wired on one of four hosts.

---

### IDA-SEC-07 — `JwtTokenResponse.HasAdminAccess` defaults to `true`; only `AdminLogin` sets it explicitly
- Severity: major · Type: S4 / authorization-signal integrity · Size: S · Layers: backend, frontend
- File: `src/Cleansia.Core.AppServices/Shared/DTOs/ResponseModels/JwtTokenResponse.cs:16` (`bool HasAdminAccess = true`); set only in `AdminLogin.cs:101`; left default in `Login`, `PartnerLogin`, `GoogleAuth`, `ConfirmUserEmail`, `RefreshToken`
- Concrete risk: the DTO's `HasAdminAccess` positional default is `true`, so **every** token-issuing path that doesn't override it (customer login, partner login, Google, email-confirm, and crucially `RefreshToken` for the admin host — `RefreshToken.cs:91` constructs the response without `HasAdminAccess`) returns `HasAdminAccess = true`. The comment claims server-side is source of truth and this is a UI hint, and the API authorization does enforce roles server-side — but the admin SPA uses this exact flag to decide whether to show the admin shell, so a non-admin who reaches an admin-host token (or an admin user whose token is refreshed) gets an inconsistent/over-permissive client signal. A "UI hint" that fails open to `true` is the wrong default for an admin-access flag.
- Long-term fix: default `HasAdminAccess = false` and have every issuer compute it from the user's profile (or remove it entirely and let the client read the `Role` field, which is already returned). Ensure the admin `RefreshToken` path sets it from the reloaded user.
- Functional GAP: no (defaulting/wiring defect); DTO default change → `manual_step: nswag-regen`.

---

### IDA-SEC-08 — Admin GDPR delete/export and admin-user deactivate have no self/last-admin protection
- Severity: major · Type: authorization / availability · Size: S · Layers: backend
- File: `src/Cleansia.Core.AppServices/Features/Gdpr/AdminDeleteUserAccount.cs:15-24` (validator only checks existence), `src/Cleansia.Web.Admin/Controllers/AdminUserController.cs:79-92` (`DeactivateAdminUser`), `AdminDeleteUserAccount.Handler`
- Concrete risk: `AdminDeleteUserAccount` accepts any in-tenant `UserId` and anonymizes/deactivates it with no guard against the actor deleting **their own** admin account or the **last remaining** administrator. An admin can GDPR-delete (irreversibly anonymize via `User.Anonymize()`) another administrator, or deactivate the final admin, locking the tenant out of its own admin console with no recovery path. There is no "can't target an Administrator via the GDPR customer-deletion tool" check, even though that tool is meant for customer/employee data-subject requests.
- Long-term fix: in the admin-delete/deactivate validators, reject when `target.Profile == Administrator` (admins are managed only through the AdminUsers feature), reject self-deletion, and reject deactivating/deleting the last active administrator in the tenant. Add the same last-admin guard to `DeactivateAdminUser`.
- Functional GAP: yes — missing guardrails; warrants a small user story "Protect admin accounts from self/last-admin deletion & deactivation."

---

### IDA-SEC-09 — `OwnerOrElevated` owner-branch is dead code for the only endpoint that uses it (route key mismatch)
- Severity: minor · Type: correctness / latent S3 · Size: S · Layers: backend
- File: `src/Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:221-225` and the equivalent in each host; consumer `UserController.cs:28-34`
- Concrete risk: the policy's customer/self branch reads `http.Request.RouteValues["id"]` and compares it to the caller's `sub`. But `GetById` binds `GetUser.Query` from **query string** (`?UserId=`), and the route has no `{id}` segment — so `RouteValues["id"]` is always null and the owner branch always returns `false`. Today this only *removes* access (customers can't reach it), so it's not an active hole — but it means the access model is silently not what the code intends, and if anyone later adds a `{id}` route or relies on "owner can read self," the check will behave unexpectedly. It also masks IDA-SEC-04 (the elevated branch is what's actually granting everyone).
- Long-term fix: make the policy read the resource id from the same place the action binds it (or pass the id explicitly), and add a unit test that a customer can read only their own detail and an employee's access is governed by the intended rule (per IDA-SEC-04's resolution).
- Functional GAP: partial.

---

### IDA-SEC-10 — Anonymous confirm/reset/login flows are inert in multi-tenant mode (tenant filter excludes them)
- Severity: minor (functional, with a security edge) · Type: S8 · Size: M · Layers: backend, db
- File: tenant filter `src/Cleansia.Infra.Database/CleansiaDbContext.cs:111-179`; consumers `GetByConfirmationCodeAsync`/`GetByEmailAsync` via `GetDbSet()` in `UserRepository.cs`
- Concrete risk: `User : ITenantEntity`, so all `GetDbSet()` reads get the global tenant filter. On `[AllowAnonymous]` routes there is no `tenant_id` claim, so `GetCurrentTenantId() == null`, and the filter matches **only rows where `TenantId == null`** (the single-tenant clause). In a real multi-tenant deployment (users created with a non-null `TenantId`), anonymous `Login`, `ConfirmUserEmail`, `ResendConfirmationEmail`, and password reset would find **no user** — the flows silently fail for all tenanted users. The security edge: teams may be tempted to "fix" this with `IgnoreQueryFilters()`, which would then make confirm-by-code (IDA-SEC-03) match across **all** tenants — turning a weak code into a cross-tenant takeover primitive. Flagging so the fix is done safely.
- Long-term fix: resolve the tenant on anonymous auth routes from a non-claim source (host/subdomain → tenant) and set a tenant override before the lookup, rather than ignoring filters. If single-tenant is the only supported mode today, document that explicitly and add a guard test so a future multi-tenant rollout doesn't ship broken auth or an `IgnoreQueryFilters` cross-tenant leak.
- Functional GAP: yes (multi-tenant auth unbuilt) — user story: "Resolve tenant on anonymous auth endpoints without bypassing the global filter."

---

### Items checked and PASS (no action)
- S1 enrichment: `Devices` (`RegisterDevice`/`UnregisterDevice`), `Notifications`, `Gdpr` consent grant/withdraw, `DeleteUserAccount`, `ExportUserData`, `UpdateCurrentUser` all derive `userId` from `IUserSessionProvider`/JWT and ignore body ids. `UpdateCurrentUser` additionally enforces ownership (`AllowedToUpdateUser`, `UpdateCurrentUser.cs:66-71`). `GrantConsent` correctly moved IP/UserAgent server-side (`GrantConsent.cs:14-16`).
- S2: every controller method in the seven controllers across all hosts carries `[Permission]`, `[Authorize]`, or `[AllowAnonymous]`. No bare endpoints found.
- S4 self-DTOs: `MyProfileDto` deliberately drops `Id` and exposes no `TenantId`/`StripeCustomerId`/hashes; `GdprExportDto` is self-scoped by design (the data subject's own export). `JwtTokenResponse` exposing self `UserId`/`Email` is the documented exception (the caller is the subject) — except for the `HasAdminAccess` default in IDA-SEC-07.
- S7: `GdprDeletionService` is idempotent (`HasPendingRequestAsync` guard, `GdprDeletionService.cs:51-54`) and blocks on active orders/invoices; Stripe cancellation failure is logged, not retried blindly. `RegisterDevice` is idempotent on `(UserId, DeviceId)`. `GrantConsent` checks existing consent before re-granting.
- S8 isolation: tenant filter applied to all `ITenantEntity`; the one `IgnoreQueryFilters()` (`UserRepository.GetByIdIgnoringTenantAsync`) is not reachable from the audited identity endpoints (verify its other callers separately). `RefreshToken.Handler` re-checks `user.IsActive` (`:69`).
- S10: `GdprDeletionService` and `RefreshToken` both honor `IsActive`; refresh-token cleanup retains revoked tokens for forensics (`RefreshTokenCleanupService.cs`).

### Highest-impact, fix first
1. IDA-SEC-01 (Google account takeover) 2. IDA-SEC-03 (weak codes → reset/confirm takeover) 3. IDA-SEC-02 (rate limiter is global, not per-partition) 4. IDA-SEC-04 (employee PII harvest) 5. IDA-SEC-05 (code/PII in logs). IDA-SEC-01/02/03 are mutually reinforcing — 02 removes the throttle that would otherwise blunt 03, and all three sit on the same anonymous auth surface.

Do not approve any change to `GoogleAuth`, the rate-limiter config, the confirmation/reset code path, or the `OwnerOrElevated`/`CanViewUserDetail` mapping until the developer's fix is re-verified against S1/S3/S4/S5.


# ===== identity-auth FUNCTIONAL GAPS =====

Decisive: the customer web profile has an "Email Notifications" toggle (`p-toggleSwitch` bound to `emailNotifications = signal(true)`) that is **purely local state — it is never read back from or persisted to the `NotificationPreferences` backend**. It always shows "on", and toggling it does nothing. This is a dead-end UI control. The full 11-category `NotificationPreferences` API exists and is consumed only by the customer Android app.

I now have a thorough, well-grounded set of findings. Let me compile the report.

# Identity-Auth Domain — Functional Gaps & Half-Built Features Audit

Scope audited: `Features/Auth`, `Features/Users`, `Features/AdminUsers`, `Features/Devices`, `Features/Gdpr`, `Features/Notifications`, `Features/DataRetention`, plus the login/register/forgot/confirm/gdpr/profile/notification surfaces across customer-web (SSR), partner-web, admin-web, and both Android apps. I read the actual handlers, controllers, DTOs, domain entities, mappers, and the consuming UI. Findings cite `conventions.md` (no dead code, three explicit data states, production-ready bar) and `consistency.md` where relevant. Ranked by impact.

---

## CRITICAL

### IA-01 — Admin GDPR back-office has zero UI; the entire request/consent audit surface is unreachable
- **Type:** functional GAP (endpoints with no consumer) — needs story
- **Severity:** critical
- **Where:** `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs:15-49` exposes `export/{userId}`, `delete-account/{userId}`, `consents/{userId}`, `requests`. Handlers exist: `Features/Gdpr/AdminExportUserData.cs`, `AdminDeleteUserAccount.cs`, `AdminGetUserConsents.cs`, `GetAllGdprRequests.cs`. **No admin frontend feature consumes any of them** — the admin feature roster (`libs/cleansia-admin-features/*/project.json`) has no `gdpr`/`data-protection` library; `Grep` for `getAllGdprRequests|adminExportUserData|getUserConsents` across `cleansia-admin-features` returns nothing.
- **Impact:** The platform records a GDPR request audit log (`GdprRequest` rows, Article-30-style) and consent history, and supports admin-initiated export/erasure — but an admin/DPO has **no way to view requests, fulfil a data-subject access request for another user, inspect a user's consents, or erase an account from the back office.** For a multi-tenant platform going to PROD in the EU this is a compliance-blocking hole: the data exists, the API exists, the operator screen does not.
- **Fix:** Add an admin "Data Protection" feature: a paged GDPR-requests list (consuming `requests`), a per-user consent viewer (`consents/{userId}`), and admin export/erase actions gated by the existing `Policy.CanAdminExportUserData` / `CanAdminDeleteUserAccount` / `CanViewGdprRequests`. Follow consistency rules C1–C8.
- **Size:** L · **Layers:** admin-web (NSwag client already generated), i18n (5 locales)

---

## MAJOR

### IA-02 — Customer-web "Email Notifications" toggle is a dead-end; the 11-category preferences API is web-orphaned
- **Type:** half-built feature (dead-end UI + endpoint with no web consumer) — needs story
- **Severity:** major
- **Where:** `libs/cleansia-customer-features/profile/.../profile.component.html:349-356` renders `<p-toggleSwitch [ngModel]="emailNotifications()" ...>`; `profile.component.ts:115` is the entire backing: `emailNotifications = signal(true);`. It is **never loaded from or saved to** the backend. The real API — `NotificationPreferencesController` (customer-web + customer-mobile), handlers `Features/Notifications/GetMyNotificationPreferences.cs` / `UpdateNotificationPreferences.cs`, 11 categories (`OrderUpdates`, `CleanerOnTheWay`, `Promo`, `DisputeReply`, …) — is consumed **only by the Android customer app** (`customer-app/.../core/notifications/NotificationPreferencesRepository.kt`).
- **Impact:** A customer on the web flips "Email Notifications", sees it stick locally, and changes nothing server-side; it always reads "on". The granular preference model the rest of the system already honors is invisible on web. Violates `conventions.md` "empty/loading/error states are part of the work" and the three-data-states bar — this control has no real state at all.
- **Fix:** Build a customer-web notification-preferences section (or facade) that loads via `GetMyNotificationPreferences` and persists via `UpdateNotificationPreferences`, mapping all 11 categories; remove the fake local-only signal.
- **Size:** M · **Layers:** customer-web, i18n

### IA-03 — Partner GDPR page is static legal text; partners cannot export/erase/manage consent despite full backend support
- **Type:** half-built feature (dead-end UI) — needs story
- **Severity:** major
- **Where:** `libs/cleansia-partner-features/gdpr/.../gdpr.component.ts:22` is `export class PartnerGdprComponent {}`; the template (`gdpr.component.html`) is five static `<p>` blocks plus a "go back" button. Meanwhile `src/Cleansia.Web.Partner/Controllers/GdprController.cs:16-61` fully exposes `export`, `delete-account`, `consents` (GET/grant/withdraw), and a working `GdprFacade` already exists in customer-features wired to those exact endpoints.
- **Impact:** Employees/partners (also data subjects) have a GDPR page that *looks* functional but offers no data-subject rights at all — no export, no account deletion, no consent management — even though the API and a reusable facade exist. Asymmetric with the customer app, and a GDPR exposure for the partner population.
- **Fix:** Wire `PartnerGdprComponent` to a partner `GdprFacade` (mirror the customer one) calling the partner GDPR endpoints; add export/delete/consent UI. Reuse, don't reinvent (conventions prime directive).
- **Size:** M · **Layers:** partner-web, i18n

### IA-04 — `LastLoginAt` is a permanently-null DTO field surfaced to admins; no login tracking exists
- **Type:** half-built feature (entity field missing behind an exposed DTO) — needs story
- **Severity:** major
- **Where:** `Features/AdminUsers/DTOs/AdminUserDetailDto.cs:18` and `AdminUserListItem.cs:16` both carry `DateTimeOffset? LastLoginAt`, but `Mappers/AdminUserMappers.cs:22` and `:41` hardcode `LastLoginAt: null`. The `User` entity (`Core.Domain/Users/User.cs`) has **no last-login field** and none of the login handlers (`Login`, `AdminLogin`, `PartnerLogin`, `GoogleAuth`, `RefreshToken`) records a login timestamp.
- **Impact:** The admin user detail/list contract promises "last login" — useful for spotting dormant/compromised admin accounts and for deactivation decisions — but it is structurally always null. It's a phantom feature: someone built the DTO and (likely) a column intending to show it, then never populated it. Violates `conventions.md` "no dead code" and the production-ready bar.
- **Fix:** Add `LastLoginAt` to `User` (+ migration, owner-run), set it in the token-issuing handlers (or a login-recorded domain event), and populate it in the mappers. If login tracking is intentionally deferred, remove the field from the DTOs instead of shipping a guaranteed-null column.
- **Size:** M · **Layers:** backend, DB migration (`manual_step: ef-migration`), nswag-regen, admin-web display
- **Open question for owner:** is per-login tracking desired, or should we drop the field? → `questions/open.md`

### IA-05 — No device / active-session management anywhere; `DeviceDto` and a "list my devices" query never existed
- **Type:** functional GAP (orphaned DTO, missing query, no UI) — needs story
- **Severity:** major
- **Where:** `Features/Devices/` has only `RegisterDevice` and `UnregisterDevice` (single-device, called by the caller's own client). `Features/Devices/DTOs/DeviceDto.cs` exists but is referenced **only by itself** (Grep: the DTO file + a done refactor plan). There is **no `GetMyDevices` query**, no controller endpoint returning a device list, and no "manage devices / sign out everywhere" UI on any client. Refresh tokens are rotated/revoked (`RefreshToken.cs`, `Logout.cs`) but there is no surface to enumerate or revoke *other* sessions.
- **Impact:** A user who loses a phone cannot see or revoke that device's session; an admin cannot see a user's registered devices. `DeviceDto` is dead code shaped exactly like the missing query's output — a classic half-built feature (someone modeled the read side and stopped). The data-retention sweep silently deletes stale devices (`DataRetentionBackgroundService.CleanStaleDevicesAsync`) which is the *only* device lifecycle a user benefits from, and it's invisible.
- **Fix:** Add a `GetMyDevices` query returning `DeviceDto`, expose it on the device controllers, and add a "your devices / revoke" surface (at minimum in mobile profile; ideally also admin user-detail). Either wire `DeviceDto` to the new query or delete it.
- **Size:** M · **Layers:** backend, nswag-regen, mobile/web UI
- **Open question:** scope (self-service only vs. admin visibility) → `questions/open.md`

### IA-06 — `RegisterEmployee` silently no-ops for an already-confirmed email — no error, no resend, no UI feedback
- **Type:** half-built lifecycle (unhandled path) — needs story
- **Severity:** major
- **Where:** `Features/Auth/RegisterEmployee.cs:68-86`. The validator only blocks emails that exist **and are confirmed** (`UserWithEmailNotExistsAsync` returns true when unconfirmed). When a confirmed user re-submits, validation passes, but the handler's `if (userEntity is null)` is false and `if (userEntity.Employee is null)` is typically false, so the handler **falls through to re-sending a confirmation email for an already-confirmed account** (`SendEmailConfirmationAsync` with a now-null `ConfirmationCode!` after `ConfirmEmail()` cleared it — a latent NRE) and returns success. Compare the customer `Register.cs` which at least refreshes the code for the unconfirmed re-reg case.
- **Impact:** Confusing/incorrect behavior for partner re-registration: either a success with a spurious confirmation email or a null-reference on `ConfirmationCode!`. The "already registered, please log in" path is unspecified and unhandled.
- **Fix:** Define the re-registration contract for confirmed accounts (return a clear `BusinessErrorMessage.ExistingUserWithEmail`); guard the confirmation-email send so it never runs for a confirmed user; align with the customer `Register` re-reg semantics.
- **Size:** S · **Layers:** backend, i18n (error key in 5 locales), partner-web register surface

---

## MINOR

### IA-07 — GDPR `Processing` status is effectively unreachable for export requests; request lifecycle is inconsistent
- **Type:** lifecycle state rarely reachable / inconsistency between layers
- **Severity:** minor
- **Where:** `GdprRequestStatus` has `Pending/Processing/Completed/Failed` (`Core.Domain/Enums/GdprRequestStatus.cs`). `GdprRequest.MarkProcessing()` is called **only** in `GdprDeletionService.cs:70` (deletion path). Both export handlers (`ExportUserData.cs:35-47`, `AdminExportUserData.cs:39-51`) go straight Pending→Completed/Failed and **never** call `MarkProcessing`. The retention query that anonymizes old requests filters on `Status == Completed` only (`DataRetentionBackgroundService.cs:124`).
- **Impact:** For exports, `Processing` is a defined state no row ever occupies; the admin requests list (once IA-01 builds it) would render a status badge that can't appear for the most common request type. Minor data-model inconsistency, but it will confuse the future admin UI and any status-based reporting.
- **Fix:** Either mark exports `Processing` at start for symmetry, or document that export is synchronous and the badge set for export rows excludes `Processing`. Decide one and make the enum usage consistent.
- **Size:** S · **Layers:** backend (+ admin-web badge mapping once IA-01 lands)

### IA-08 — Admin self-service profile/password is absent from the admin app; admins have no in-app password change
- **Type:** functional GAP (missing flow) — needs story
- **Severity:** minor (security-adjacent)
- **Where:** Forgot/reset-password endpoints live on `UserController` for customer (`Web.Customer/.../UserController.cs:39-61`) and partner (`Web.Partner/.../UserController.cs:52-72`); the forgot-password UI exists only in `apps/cleansia.app` and `apps/cleansia-partner.app` routes (Grep). `AdminAuthController` exposes only Login/Refresh/Logout; there is no admin "change my password" or "forgot password" path, and `AdminUserController` lets admins manage *other* admins but not rotate their own credentials.
- **Impact:** An admin who needs to rotate their password must rely on another admin re-creating them or on DB access. No self-service credential hygiene for the most privileged role.
- **Fix:** Decide whether admin password reset reuses the existing `RequestPasswordChange`/`ChangePassword` (which key off email and are currently `[AllowAnonymous]` on the other APIs) or gets an authenticated "change my password" admin endpoint. Add the admin-web surface.
- **Size:** M · **Layers:** backend (possibly), admin-web, i18n
- **Open question:** admin reset via email-link vs. authenticated change → `questions/open.md`

### IA-09 — `AdminUserDetailDto.BirthDate`/`PreferredLanguageCode` are read-only ghosts; create/update don't accept them
- **Type:** half-built feature (DTO carries fields no command writes)
- **Severity:** minor
- **Where:** `AdminUserDetailDto.cs:14-15` exposes `BirthDate` and `PreferredLanguageCode`, but `CreateAdminUser.Command` (`CreateAdminUser.cs:15-20`) and `UpdateAdminUser.Command` (`UpdateAdminUser.cs:13-17`) accept only name/phone. `UpdateAdminUser` calls `user.Update(..., birthDate)` with no argument, so `BirthDate` is reset toward null on every edit (the `User.Update` signature defaults `birthDate = null`).
- **Impact:** The admin detail screen can display a birth date / language that the admin edit form can never set, and an unrelated edit silently wipes `BirthDate`. Inconsistent read/write contract; a data-loss footgun on update.
- **Fix:** Either add these fields to create/update (and pass them through `user.Update`) or drop them from the detail DTO; in all cases stop `UpdateAdminUser` from nulling `BirthDate`.
- **Size:** S · **Layers:** backend, nswag-regen, admin-web form

### IA-10 — `consents/withdraw` is split across two generated sub-clients in the consumer (layer drift, fragile)
- **Type:** inconsistency between layers (not a missing feature)
- **Severity:** minor
- **Where:** `libs/cleansia-customer-features/gdpr/.../gdpr.facade.ts:52-57` grants via `customerClient.gdprClient.consentsPost(...)` but withdraws via `customerClient.consentsClient.withdraw(...)` — two different generated client groupings for one consent toggle, and the facade imports consent types from `@cleansia/partner-services` while calling the customer client.
- **Impact:** Confusing cross-package coupling that will silently break on the next NSwag regen if the controller grouping changes; not user-visible today. Flag for cleanup, not a story.
- **Fix:** Normalize both calls onto one generated client group; import DTOs from the matching `@cleansia/customer-services` package.
- **Size:** S · **Layers:** customer-web

---

## Notes / non-findings (verified working, listed to bound the audit)
- Customer & partner forgot-password/reset is fully wired end-to-end (`RequestPasswordChange` + `ChangePassword` handlers, controllers, both apps' routes, `EmailType.ResetPassword` template).
- Refresh-token rotation with theft detection (`RefreshToken.cs` / `RefreshTokenValidationException`), HttpOnly-cookie auth, idempotent logout, and audience pinning per app are implemented and consumed.
- Admin user create/update/activate/**deactivate** all exist with a self-deactivation guard (`DeactivateAdminUser.cs:32-34`, `BusinessErrorMessage.CannotDeactivateSelf`) and admin-management UI.
- GDPR deletion's blocking guards (active order / pending invoice / pending request) and full anonymization fan-out are implemented (`GdprDeletionService.cs`).
- Customer **mobile** notification preferences and delete-account are fully wired — the web gaps in IA-02/IA-03 are asymmetries, not total absences.

## Most important takeaways for the PM
1. **IA-01 (admin GDPR UI)** is the single highest-impact gap — a compliance operator surface that is entirely missing despite a complete backend.
2. **IA-02 / IA-03 / IA-04** are textbook half-built features (a fake toggle, an empty page, a guaranteed-null column) that look done at a glance and will fail the first real audit.
3. **IA-05 (device/session management)** and **IA-06 (partner re-registration)** are missing flows with a security angle.

Key files: `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs`, `src/Cleansia.Core.AppServices/Features/Gdpr/`, `src/Cleansia.Core.AppServices/Mappers/AdminUserMappers.cs`, `src/Cleansia.Core.AppServices/Features/Devices/DTOs/DeviceDto.cs`, `src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs`, `src/Cleansia.Core.AppServices/Features/Notifications/`, `src/Cleansia.App/libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts`, `src/Cleansia.App/libs/cleansia-partner-features/gdpr/src/lib/gdpr/gdpr.component.ts`.


# ===== Error-contract parity and test coverage gap =====

I have everything needed. The frontend has only 8 spec files (none touching the error interceptor/i18n mapping), and the testing.md must-cover list confirms all 7 priority areas. I have concrete evidence for both checks. Let me compile the findings report.

---

# BLIND-SPOT PASS — Findings Report

Two independent checks. All findings cite real code (file:line) and the project's own `agents/knowledge/testing.md` must-cover list. **Audit only — no code changed.**

## Method note (so the numbers are trustworthy)

The frontend does **not** key errors under `errors.*`. The HTTP interceptor (`libs/core/services/src/lib/interceptors/http-error.interceptor.ts:29,41`) maps every backend error to **`api.${dotKey}`** via `translate.instant(\`api.${errorKey}\`)`. So the real translation namespace to cross-reference is **`api.*`**, per app. ngx-translate returns the **raw key string** on a miss, so a missing key surfaces to the user as literal text like `api.order.cancellation_window_closed`. The admin app's separate top-level `errors.*` block (146 keys) is used by bespoke component logic, not the interceptor; I credited it where a key matched.

Counts: backend `BusinessErrorMessage` = **225** dot-key values. `api.*` keys present: customer **7**, partner **165**, admin **160**. Of the 225 backend keys, **54 have no translation under any namespace in any app**, and **18 more** exist only under a bespoke namespace (not reachable through the generic interceptor's `api.` prefix).

---

## CHECK 1 — ERROR-CONTRACT PARITY

### EP-1 — Customer app has only 7 `api.*` keys; ~all interceptor-surfaced customer errors render as raw keys
- **Severity:** High
- **Type:** Functional defect / i18n contract break
- **Evidence:** `apps/cleansia.app/src/assets/i18n/en.json` `api` block (lines 1302–1316) = `common`, `address`, `service_area` only. Interceptor at `http-error.interceptor.ts:29,41` does `api.${errorKey}` for **all** server errors.
- **Impact:** Any customer-facing 4xx outside those 7 keys shows the customer literal text like `api.order.cancellation_window_closed`, `api.order.already_cancelled`, `api.order.weekly_limit_reached`, `api.gdpr.export_failed`. The customer order-cancellation flow (CancelOrder, per `CLAUDE.md` "customer-hardcoded") is the worst hit — every cancellation-policy rejection is unreadable. The membership/recurring/gdpr/promo/referral flows escape this only because they use **bespoke** mappers under their own namespaces (see EP-3), which means the codebase has two inconsistent error-display mechanisms.
- **Long-term fix:** Add the full `api.*` subtree to the customer app for every backend key reachable from a customer endpoint (cancellation, review, gdpr export/delete-failure, address ownership, country/city not-serviced, order limits). Align on ONE mechanism (interceptor `api.*`) and delete bespoke per-feature error maps, or formally document the split.
- **Size:** M  **Layers:** frontend (i18n) only — no backend change
- **Functional GAP needing a story?** Yes — "Customer error-message parity" story; the cancellation subset is P1 because it is on a money path.

### EP-2 — 54 backend error keys have NO translation anywhere (raw key shown to user)
- **Severity:** High (subset on money/legal paths), Medium overall
- **Type:** i18n contract gap
- **Evidence:** Cross-reference of `BusinessErrorMessage.cs` values vs all three `en.json` files. The 54 orphans, grouped by blast radius:
  - **Customer order/money path (P1):** `order.already_cancelled`, `order.already_completed`, `order.in_progress_cannot_cancel`, `order.cancellation_window_closed`, `order.cleaning_date.below_lead_time`, `order.address_exactly_one_required`, `order.weekly_limit_reached`, `order.preferred_employee.not_eligible`, `order.not_completed`, `order.review.rating_invalid`, `address.not_owned_by_user`, `address.label_required`, `dispute.not_owned_by_user`
  - **GDPR (legal, customer-visible):** `gdpr.export_failed`, `gdpr.deletion_failed`, `gdpr.consent_not_found`, `gdpr.consent_already_granted`
  - **Promo/Referral/Loyalty (customer-visible, money):** all 14 `promo.*`, all 4 `referral.*`, all 4 `loyalty.*` (note: backend returns these as enum-stringified values mapped manually in the wizard under `pages.order.promo.error_*` / `referral.error_*` — the canonical `BusinessErrorMessage` dot-keys themselves are never translated, so any handler that returns them directly is unreadable)
  - **Admin/config (admin-visible):** `country.not_serviced`, `country.required`, `service_city.not_found`, `service_city.already_exists`, `city.not_serviced`, `service.category_not_found`, `feature_flag.not_found/already_exists`, `tenant_config.not_found/key_already_exists`, `country_config.not_found/already_exists_for_country`
  - **Auth:** `auth.invalid_refresh_token`
  - **`employee_already_has_order_in_progress`, `order.note.content_required`, `order.issue.description_required`** (partner-visible)
- **Impact:** User sees an untranslated machine key on a real failure. On the cancellation and GDPR paths this is both a UX failure and, for GDPR, a compliance-visibility concern.
- **Long-term fix:** Add the 54 keys under the correct `api.*` (or admin `errors.*`) namespace in all 5 locales. Add a CI guard (a Jest/unit test) that parses `BusinessErrorMessage.cs` and asserts every key reachable from each API has a translation in that app's `en.json` — this prevents regression. The catalog (`agents/knowledge/conventions.md`) already states "every backend error key must have a frontend translation," so this is an enforcement gap, not a judgment call.
- **Size:** M (keys) + S (CI guard)  **Layers:** frontend i18n + a test
- **Functional GAP needing a story?** Yes — "Backend↔frontend error-key parity guard + backfill."

### EP-3 — Two inconsistent error-display mechanisms (interceptor `api.*` vs bespoke per-feature maps)
- **Severity:** Medium
- **Type:** Architecture inconsistency (maintainability)
- **Evidence:** Generic interceptor uses `api.${key}` (`http-error.interceptor.ts:29,41`); membership/recurring/gdpr/promo/referral use top-level namespaces (`membership.*`, `recurring_booking.*`, `gdpr.deletion_blocked_by_*`, `pages.order.promo.error_*`) mapped in feature code. This is why the 18 "bespoke-only" keys (membership/recurring/gdpr-blocked) work despite having no `api.*` entry.
- **Impact:** Every new error needs the author to know which of two mechanisms a given endpoint uses; easy to add a backend key and forget the bespoke map, producing a silent raw-key leak. Directly causes EP-1/EP-2 to keep recurring.
- **Long-term fix:** Converge on the interceptor `api.*` path; have feature error handlers reuse it instead of hand-rolling maps. Document the one mechanism in `patterns-frontend.md`.
- **Size:** M  **Layers:** frontend  **Functional GAP?** No — refactor, not user-facing behavior.

---

## CHECK 2 — TEST COVERAGE

### What actually exists (full inventory)
- **`Cleansia.Tests` (unit):** 9 logic files — 5 auth validators, 3 order validators (`StartOrderValidator`, `NotifyOnTheWayValidator`, plus helpers), and **`BookingPolicyTests.cs`** (the cancellation-fee-rate state machine — the one genuinely thorough money test, 16 cases incl. boundaries and oops-window).
- **`Cleansia.IntegrationTests`:** 11 files — 5 auth flows (login/register/confirm/google/refresh-token), 4 **read-only** overview queries (currencies/languages/packages/services), 1 GDPR `DeleteUserAccount`. No write-path money/order tests.
- **`Cleansia.TestUtilities`:** Postgres testcontainer fixture, mock factories for currency/language/package/service/user. **No factories for Order, EmployeePayConfig, PayPeriod, Invoice, Dispute** — so even writing the missing tests starts from zero builders.
- **Frontend:** 8 `.spec.ts` total, **none** touching the error interceptor, error pipe, or i18n mapping.

### Prioritized MUST-COVER gap list (highest-risk WRITE paths with ZERO tests)

Ranked by impact. Each maps to a numbered item in `agents/knowledge/testing.md`'s must-cover list.

**TC-1 — Pay calculation: ZERO tests (must-cover #1)** — *Severity: Critical*
- `Core.Domain/EmployeePayroll/EmployeePayConfig.cs:134` `CalculatePay` (the `clamp(base + extraPerRoom·r + extraPerBathroom·b + distanceRate·d, min, max)` formula) and `Core.Domain/EmployeePayroll/Services/PayCalculator.cs` (entire file) are **pure static logic** and **completely untested**.
- Pure logic with money output — the single highest-value, lowest-cost gap. Cover: clamp at min, clamp at max, no-clamp (min/max=0 disables), the per-employee `EmployeePayConfig` override-vs-service-config precedence (IMP-3), `CalculateTotalPay` floor-at-zero (line 41–42), `CalculateExtrasPay`, `CalculateDistancePay` negative guards, `ProratePay`, `SplitPayForMultipleEmployees`, `GenerateInvoiceNumber` format.
- **Live bug surfaced while reading:** `PayCalculator.IsHoliday` (line 262–266) is a stubbed `return false` with a TODO — `CalculatePeakTimeBonus` silently never pays a holiday bonus. A test would have caught/flagged this. Severity Medium, type functional defect, **GAP needing a story** (holiday calendar).
- **Size:** S (tests are trivial — pure functions). **Layers:** backend unit.

**TC-2 — Stripe order webhook: ZERO tests (must-cover #6 idempotency, #2 lifecycle)** — *Severity: Critical*
- `Core.AppServices/Features/Payments/HandlePaymentNotification.cs` (whole handler). This drives `Pending→Confirmed→Paid` on success and `→Cancelled/Failed` on expiry, fires receipt + push side effects, and is the system's main money entry point.
- Untested behavior includes: the idempotency gate (`HasProcessedAsync`, line 144) and stamp-before-side-effects (line 156) — the exact S7 re-delivery scenario testing.md #6 calls non-negotiable; the terminal-state guards (line 232, 267) that prevent double-confirm/double-cancel; CompletedSession vs PaymentIntentSucceeded dispatch parity (web vs mobile, line 205); invalid-signature rejection (line 128).
- **Size:** M (needs handler unit tests with mocked repos/queue + at least one integration test re-posting the same event). **Layers:** backend unit + integration.

**TC-3 — Stripe subscription webhook: ZERO tests** — *Severity: High*
- `Core.AppServices/Services/StripeSubscriptionWebhookHandler.cs`. Membership lifecycle (activate/cancel/swap) with real billing. Same idempotency/state concerns as TC-2.
- **Size:** M. **Layers:** backend unit.

**TC-4 — CreateOrder: ZERO tests (must-cover #2, #3)** — *Severity: High*
- `Core.AppServices/Features/Orders/CreateOrder.cs`. Order creation incl. total-price match (`TotalPriceNotMatch`), promo/membership/tier discount application, payment-gateway init. The money-in path; no happy-path or failure-branch test.
- **Size:** M. **Layers:** backend unit + integration.

**TC-5 — Order state transitions / illegal transitions: ZERO tests (must-cover #2)** — *Severity: High*
- `TakeOrder.cs`, `StartOrder`, `CompleteOrder.cs`, `CancelOrder.cs`. Only the cancellation *fee-rate* math (BookingPolicy) is tested; the actual transition handlers and their **illegal-transition rejections** (cancel a completed order → `OrderAlreadyCompleted`; start an unconfirmed order → `OrderNotConfirmed`; etc.) are untested. These are exactly the `BusinessErrorMessage` paths in EP-2 with no frontend translation — double exposure.
- **Size:** M. **Layers:** backend unit + integration.

**TC-6 — Invoice generation, numbering, pay-period close: ZERO tests (must-cover #7)** — *Severity: High*
- `Core.AppServices/Features/EmployeePayroll/GenerateInvoice.cs`, `CalculateOrderPay.cs`, `Services/PayPeriodBackgroundService.cs`, invoice approve/mark-paid/cancel transitions. Gap-free/format-correct numbering (`GenerateInvoiceNumber`) and period open→close→invoice flow untested. The prior audit's note that "payroll settlement lifecycle is largely unreachable" makes test coverage here the safety net for whatever ships.
- **Size:** L. **Layers:** backend unit + integration.

**TC-7 — Refunds / dispute-resolution money math: ZERO tests (must-cover #3)** — *Severity: High*
- Refund = cancellation `feeRate × amount` and dispute `InvalidRefundAmount` validation. The fee-rate function is tested; the **refund-amount computation and the dispute resolution that issues it** are not. No `charge.refunded` path exists in the order webhook (TC-2), so refund issuance lives in dispute/cancel handlers — untested.
- **Size:** M. **Layers:** backend unit. **GAP?** Coordinate with the prior finding that admin order intervention / refund issuance may be partly unreachable — if so it's also a functional story.

**TC-8 — The 16 Azure Functions: ZERO tests** — *Severity: High (selective)*
- `src/Cleansia.Functions/Functions/*` — none tested. Risk-ranked subset that must be covered:
  - `CalculateOrderPayFunction` + `GenerateInvoiceFunction` (money — pair with TC-1/TC-6)
  - `GenerateReceiptFunction` (fiscal/receipt — must-cover #4)
  - `RetryFailedFiscalRegistrationsFunction` (fiscal retry — testing.md #4 explicitly: "failed registrations are retried" and "customer completion is never blocked")
  - `MaterializeRecurringBookingsFunction` + `AutoCancelStaleRecurringOrdersFunction` + `CleanupStalePendingOrdersFunction` (create/cancel orders on a timer — state machine + idempotency on re-run)
  - `SendSitewidePromoFanoutFunction`, `SendMembershipLifecycleNotificationsFunction` (fan-out — must not double-send on retry, S7)
  - Lower priority: the reminder/digest/token-cleanup timers.
- The functions are thin wrappers; extract and unit-test the inner handlers (most logic already lives in AppServices features).
- **Size:** L overall (M for the money/fiscal subset). **Layers:** backend unit.

**TC-9 — Authorization / cross-tenant ownership: ZERO write-path tests (must-cover #5)** — *Severity: High*
- No integration test asserts a cross-user or cross-tenant resource-by-id access returns NotFound. The only ownership coverage is the GDPR delete flow. Pairs with the Security Reviewer's S2/S3 findings — testing.md #5 says this must be a test, not just review.
- **Size:** M. **Layers:** backend integration.

**TC-10 — Fiscal-mode selection (None/AsyncBackground/BlockingOnline): ZERO tests (must-cover #4)** — *Severity: Medium-High*
- Routing per `CountryConfiguration` and the guarantee that customer completion is never blocked by fiscal registration is untested. Admin app even ships a `fiscal_failures` i18n block, implying this path is user-visible.
- **Size:** M. **Layers:** backend unit + integration.

**TC-11 — Frontend error-mapping has no specs** — *Severity: Medium*
- The interceptor (EP-1) and error pipe have zero Jest coverage; a spec asserting "known backend key → resolved string, unknown → fallback (not raw key)" would have caught EP-1/EP-2 and is the natural home for the parity guard in EP-2.
- **Size:** S. **Layers:** frontend.

---

## Ranked summary (by impact)

| Rank | ID | Title | Sev | Layers | Story? |
|---|---|---|---|---|---|
| 1 | TC-1 | Pay calc (clamp + override) zero tests | Critical | be-unit | — (also: holiday-bonus stub bug → story) |
| 2 | TC-2 | Order Stripe webhook zero tests (idempotency/state) | Critical | be-unit+int | — |
| 3 | TC-3 | Subscription webhook zero tests | High | be-unit | — |
| 4 | EP-1 | Customer app 7 `api.*` keys → raw-key errors on cancellation | High | fe-i18n | Yes |
| 5 | EP-2 | 54 backend keys untranslated anywhere | High | fe-i18n+test | Yes |
| 6 | TC-4 | CreateOrder zero tests | High | be-unit+int | — |
| 7 | TC-5 | Order transitions / illegal transitions zero tests | High | be-unit+int | — |
| 8 | TC-6 | Invoice gen / numbering / period close zero tests | High | be-unit+int | — |
| 9 | TC-7 | Refund / dispute money math zero tests | High | be-unit | maybe (refund reachability) |
| 10 | TC-8 | 16 Functions zero tests (money/fiscal subset) | High | be-unit | — |
| 11 | TC-9 | Cross-tenant/cross-user write-path zero tests | High | be-int | — |
| 12 | TC-10 | Fiscal-mode selection zero tests | Med-High | be-unit+int | — |
| 13 | EP-3 | Two inconsistent error-display mechanisms | Med | fe | No |
| 14 | TC-11 | No frontend error-mapping specs | Med | fe | No |

**Functional GAPs needing a user story:** EP-1, EP-2, the `IsHoliday` stub (TC-1 sub-finding), and possibly TC-7 (refund reachability — confirm with the admin-intervention finding from the prior pass). Everything else is test debt the dev agents must close under Gate 6, not new product behavior.

**Key relevant files:** `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Common\BusinessErrorMessage.cs`; `...\src\Cleansia.App\libs\core\services\src\lib\interceptors\http-error.interceptor.ts`; `...\src\Cleansia.App\apps\cleansia.app\src\assets\i18n\en.json`; `...\src\Cleansia.Core.Domain\EmployeePayroll\EmployeePayConfig.cs`; `...\src\Cleansia.Core.Domain\EmployeePayroll\Services\PayCalculator.cs`; `...\src\Cleansia.Core.AppServices\Features\Payments\HandlePaymentNotification.cs`; test roots `...\src\Cleansia.Tests`, `...\src\Cleansia.IntegrationTests`, `...\src\Cleansia.TestUtilities`.


# ===== Azure Functions trigger graph and reliability =====

I have everything needed, including the readiness checklist that several findings violate directly (items 4, 5, 6 — durable/transactional enqueue, idempotency, visible dead-end for poison messages). Let me check one PII concern: `GenerateReceipt` logs and whether `messageText` (which may contain customer data) is logged at Error, and the SendPush logs `messageText` containing UserId.

The receipt and push error handlers log the full `messageText` at Error level. For receipt that's an OrderId (not PII). For `SendPushNotificationMessage`, `messageText` includes `UserId` and `Args` which for promo carries title/body, and for membership is empty — UserId is an identifier not strict PII. I'll note this as a low-severity PII-adjacent concern per S10/readiness item 1.

I have completed a thorough audit. Here is my findings report.

---

# Blind-Spot Pass: Azure Functions Trigger Graph & Reliability — Findings Report

## A. Complete trigger → command graph (16 functions)

| Function | Trigger | Cron / Queue | Drives (MediatR cmd or service) | Lifecycle driven |
|---|---|---|---|---|
| GenerateInvoice | Queue `generate-invoice` | — | **NOTHING (no-op stub)** | none — dead |
| GenerateReceipt | Queue `generate-receipt` | — | `ReceiptService` + `EmailService` (no MediatR) | receipt issuance + fiscal |
| RetryFailedFiscalRegistrations | Timer | `0 */5 * * * *` (5 min) | `IFiscalRetryService.ProcessDueRetriesAsync` | **fiscal retry — ALIVE** |
| CalculateOrderPay | Queue `calculate-order-pay` | — | `CalculateOrderPay.Command` (+ `EnsureOpenPeriodAsync`) | **payroll pay-row — ALIVE** |
| PayPeriodTimer (CloseExpiredPayPeriods) | Timer | `0 0 2 * * *` (daily 02:00) | `IPayPeriodBackgroundService.CloseExpiredPeriodsAndOpenNewAsync` | **payroll period close + invoice gen — ALIVE** |
| AutoCancelStaleRecurringOrders | Timer | `0 0 * * * *` (hourly) | `AutoCancelStaleRecurringOrders.Command` | recurring lifecycle |
| CleanupStalePendingOrders | Timer | `0 */15 * * * *` (15 min) | `CleanupStalePendingOrders.Command(1h)` | order lifecycle |
| MaterializeRecurringBookings | Timer | `0 */2 * * * *` (**2 min**, doc says daily) | `MaterializeRecurringBookings.Command(7)` | recurring materialization (no templates yet) |
| SendPushNotification | Queue `notifications-dispatch` | — | `IPushDispatcher.SendAsync` + device prune | notification fan-out leaf |
| SendMembershipLifecycleNotifications | Timer | `0 */2 * * * *` (**2 min**, doc says daily) | `SendMembershipLifecycleNotifications.Command` | membership lifecycle |
| SendSitewidePromoFanout | Queue `sitewide-promo-fanout` | — | pages users → enqueues `notifications-dispatch` | promo fan-out |
| SendNewJobsDigest | Timer | `0 0/2 * * * *` (**2 min**, doc says 30 min) | `INewJobsDigestService.SendDigestsAsync` | cleaner digest |
| SendRecurringOrderReminders | Timer | `0 */2 * * * *` (**2 min**, doc says daily 02:30) | `SendRecurringOrderReminders.Command` | recurring reminders |
| PeriodReminderTimer | Timer | `0 0 9 * * *` (daily 09:00) | `IPeriodReminderBackgroundService.SendPeriodEndRemindersAsync` | payroll reminders |
| DataRetentionTimer | Timer | `0 0 3 * * 0` (weekly Sun 03:00) | `IDataRetentionBackgroundService.RunAllRetentionTasksAsync` | GDPR retention |
| RefreshTokenCleanupTimer | Timer | `0 30 3 * * *` (daily 03:30) | `IRefreshTokenCleanupService.CleanupAsync` | auth hygiene |

### Re-validation of prior "dead lifecycle" verdicts
- **Fiscal retry — OVERTURNED (alive).** `RetryFailedFiscalRegistrationsFunction` → `FiscalRetryService.ProcessDueRetriesAsync` runs every 5 min, picks up `receiptRepository.GetDueForRetryAsync`, and releases held BlockingOnline emails. Fully reachable. (`FiscalRetryService.cs:24-94`)
- **Payroll pay-row creation — OVERTURNED (alive).** `CompleteOrder.cs:264-270` enqueues `calculate-order-pay`; `CalculateOrderPayFunction` drives `CalculateOrderPay.Command`. Reachable on every order completion.
- **Payroll period close + invoice generation — alive but degraded.** `PayPeriodTimerFunction` (daily 02:00) → `CloseExpiredPeriodsAndOpenNewAsync` generates invoice PDFs **inline** (`PayPeriodBackgroundService.cs:234, 295-419`). See F1.
- **Invoice generation via queue — CONFIRMED DEAD.** `generate-invoice` queue has **zero producers** anywhere in `src` and the consumer is a no-op stub. See F1.
- **Recurring materialization — alive but no data.** Command exists and runs; no template UI, so no-op today. The prior "unreachable" should be re-stated as "reachable, no source data yet."

---

## Findings (ranked by impact)

### F1 — `GenerateInvoiceFunction` is a no-op stub; `generate-invoice` queue is fully dead
- **Severity:** High · **Type:** Dead code / latent reliability trap · **Layer:** Functions / payroll
- **File:** `src/Cleansia.Functions/Functions/GenerateInvoiceFunction.cs:11-27`; `QueueNames.cs:6`; `Messages/GenerateInvoiceMessage.cs`
- **Impact:** The function deserializes the message and logs "not yet implemented", then `Task.CompletedTask`. No producer enqueues `generate-invoice` (grep confirms zero `SendAsync(QueueNames.GenerateInvoice…)`). Invoice PDFs are instead generated **synchronously inside the nightly timer** (`PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync`), so a slow/failing PDF or email for one employee runs in the same loop as period closure (mitigated by inner try/catch at `PayPeriodBackgroundService.cs:251-257/276-283`, but it serializes all employees on one thread with no retry/DLQ). The stub is a maintenance trap: a future dev wiring a producer gets a silent no-op.
- **Long-term fix:** Either (a) delete the function, `GenerateInvoiceMessage`, and `QueueNames.GenerateInvoice` and document that invoice gen is inline in the timer; or (b) finish the extraction — have `PayPeriodBackgroundService` enqueue one `generate-invoice` per employee and implement the consumer with the same idempotency guard as receipts. Decide one; don't ship the stub.
- **Size:** S (delete) / M (implement) · **GAP needing a story:** Yes — "Invoice generation: pick async-queue vs inline-timer and remove the dead path."

### F2 — Enqueue-before-commit: `CompleteOrder` (and siblings) publish to 3 queues inside the handler, before the UnitOfWork pipeline commits
- **Severity:** High · **Type:** Dual-write / transactional-outbox gap · **Layer:** AppServices ↔ Functions
- **File:** `src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs:219, 227, 266`; commit happens later in `UnitOfWorkPipelineBehavior.cs:19-20`
- **Impact:** `queueClient.SendAsync` for `generate-receipt`, `notifications-dispatch`, and `calculate-order-pay` all execute during handler body; the DB `CommitAsync` runs **after** in the outer pipeline behavior. If the commit fails (or the process dies between enqueue and commit), consumers receive messages for an order whose `Completed` transition was never persisted: `GenerateReceiptFunction`/`CalculateOrderPayFunction` read the order in its **pre-completion** state, and the customer gets an "All done!" push for an order that isn't done. There is no transactional outbox reconciling enqueue with the write. Violates `runtime-readiness.md` checklist item 4 ("Side effects are enqueued — durable + retried") in spirit, and the S7/B8 reconciliation guarantee. Same pattern in `CreateOrder.cs:376`, `ConfirmRecurringOrder.cs:112`, `HandlePaymentNotification.cs:241`.
- **Long-term fix:** Introduce a transactional outbox (write outbox rows in the same DbContext transaction; a dispatcher drains them post-commit), or at minimum move enqueues to a post-commit hook. Document the chosen ordering rule in `consistency.md` (extends B8).
- **Size:** L · **GAP needing a story:** Yes — "Transactional outbox for queue side-effects in command handlers."

### F3 — No poison-queue / dead-letter consumer for ANY of the 5 queues; failures vanish silently after 5 dequeues
- **Severity:** High · **Type:** Reliability / observability gap · **Layer:** Functions / infra
- **File:** `host.json` (`maxDequeueCount: 5`, no per-queue override, no poison handler); no `*-poison` trigger anywhere in `src` (grep confirms)
- **Impact:** `GenerateReceipt`, `SendPushNotification`, `SendSitewidePromoFanout` re-throw on failure to force queue retry (`GenerateReceiptFunction.cs:107`, `SendPushNotificationFunction.cs:120`, `SendSitewidePromoFanoutFunction.cs:162`). After 5 failures Azure auto-moves the message to `<queue>-poison` — but **nothing consumes those poison queues** and nothing alerts. A receipt that fails 5× (e.g. company-info misconfig, fiscal outage longer than retry window) is lost with no human-visible dead-end. Code comments repeatedly cite "the poison-message pipeline" as if it handles this (`SendSitewidePromoMessage.cs:23`, `CalculateOrderPayFunction.cs:59`) — it does not exist. Violates `runtime-readiness.md` checklist item 6 ("visible dead-end for failures") and "alert when a queue backs up."
- **Long-term fix:** Add a generic `*-poison` consumer that logs at Critical + raises an alert (App Insights) and optionally persists to a `FailedMessage` table for admin replay. Set per-queue `maxDequeueCount` where 5 is too low for outage windows (receipts).
- **Size:** M · **GAP needing a story:** Yes — "Poison-queue handling + failed-message admin visibility/replay."

### F4 — `GenerateReceiptFunction` is not idempotent across an uncommitted retry: consumes a fiscal sequence number and uploads a blob before the single commit (S7)
- **Severity:** High · **Type:** Idempotency / fiscal-compliance · **Layer:** Functions / receipts
- **File:** `GenerateReceiptFunction.cs:72-99`; `ReceiptService.GenerateReceiptAsync` at `ReceiptService.cs:49-95`
- **Impact:** `GenerateReceiptAsync` calls `GetNextSequenceForYearAsync` (consumes a receipt number), `receiptRepository.Add` (in-memory only), and uploads the PDF to blob — all before the function's only `CommitAsync` (`GenerateReceiptFunction.cs:99`). The `order.Receipt is not null` guard (line 66) only protects across **committed** runs. If email-send (line 95) or the commit throws, the message retries and a **brand-new receipt with a new sequence number** is generated — producing a gap/duplicate in the legally-sequenced receipt numbering and potentially a **second customer email**. Directly violates S7 ("writes a financial record (invoice, receipt, payout) must be idempotent — check whether the side effect already happened before doing it again").
- **Long-term fix:** Make receipt creation idempotent on `OrderId` — reserve/lookup the receipt row in a committed step before email; only allocate a sequence number once per order; guard the email with `receipt.EmailSent`. Reuse the `ReferralService`/`LoyaltyService` ledger-check pattern named in S7.
- **Size:** M · **GAP needing a story:** Yes (correctness-critical).

### F5 — Cron cadence mismatch: 4 notification/recurring timers run every 2 minutes, not the documented daily/30-min cadence
- **Severity:** High · **Type:** Functional / cost / correctness · **Layer:** Functions
- **Files & evidence:**
  - `MaterializeRecurringBookingsFunction.cs:19` — `0 */2 * * * *` (every 2 min); doc line 9 says "Daily at 02:00 UTC"
  - `SendMembershipLifecycleNotificationsFunction.cs:24` — `0 */2 * * * *`; doc says "Daily sweep … tightened to a daily slot (03:00 UTC)"
  - `SendRecurringOrderRemindersFunction.cs:23` — `0 */2 * * * *`; doc says "Daily at 02:30 UTC … runs 30 min after Materialize"
  - `SendNewJobsDigestTimerFunction.cs:24` — `0 0/2 * * * *`; doc says "every 30 minutes (`0 0,30 * * * *`)"
- **Impact:** These are dev placeholders never promoted to production. Consequences: (1) the **"runs 30 min after Materialize" ordering guarantee is false** — both fire on the same 2-min tick, so `SendRecurringOrderReminders` can sweep before that tick's materialization completes, missing same-day orders until the next tick; (2) 720 runs/day each = constant cross-tenant table scans (membership, orders, prefs) — wasted DB load and Function execution cost; (3) reliance on idempotency stamps to suppress duplicate user sends becomes load-bearing for correctness rather than a safety net (any stamp gap → push spam every 2 min). Idempotency stamps currently DO hold (verified F-note below), so this is cost+correctness-of-ordering rather than user-visible spam today — but it's fragile.
- **Long-term fix:** Promote to the documented production crons (use `%AppSetting%` cron syntax so dev/prod differ without code change). Re-establish the Materialize→Reminder ordering (Reminder at 02:30 after Materialize at 02:00).
- **Size:** S · **GAP needing a story:** Partial — config fix is S; "drive timer schedules from app settings" is a small story.

### F6 — `FiscalRetryService` commits the whole 50-receipt batch in one `CommitAsync` with no per-receipt durability; a commit failure loses all attempts and the timer never retries
- **Severity:** Medium · **Type:** Reliability · **Layer:** AppServices (timer-driven)
- **File:** `FiscalRetryService.cs:38-93`
- **Impact:** Per-receipt failures are swallowed (line 79-86, correct), but all state changes accumulate in one DbContext and commit once at line 91. If `CommitAsync` throws (DB blip), every receipt's retry-tracking/`FiscalNextRetryAt`/email-sent state in that batch is lost. Because this is a **timer** (not a queue), there is no automatic retry of the lost work — the next 5-min tick re-reads `GetDueForRetryAsync` and may re-send already-sent held emails (the email release at line 70-72 sets `MarkEmailSent` only in-memory until the failed commit). Partial S7 exposure on the held-email release.
- **Long-term fix:** Commit per receipt (or in small chunks) inside the loop, or wrap the batch so a failed commit re-queues only the unprocessed remainder. Ensure the held-email release checks `receipt.EmailSent` post-commit.
- **Size:** S · **GAP needing a story:** No (refactor).

### F7 — `SendPushNotification` has no per-message idempotency; queue retry re-sends duplicate pushes
- **Severity:** Medium · **Type:** Idempotency · **Layer:** Functions / notifications
- **File:** `SendPushNotificationFunction.cs:91-120`
- **Impact:** The function dispatches via FCM (line 91), then re-throws on any later failure (line 120) to force retry. FCM send is not deduplicated, so a failure **after** a successful FCM send (e.g. the dead-token prune commit at line 107 throws) causes the whole message to retry and the user gets the push twice. Pushes aren't financial, so this is Medium not High, but it's a real S7-class double-side-effect. Also: dead-token pruning only commits when `InvalidTokens.Count > 0` (line 100-108) — correct, but means a pure-success path does no commit (fine).
- **Long-term fix:** Either accept at-least-once for pushes explicitly (document it) and split the prune-commit so it can't trigger a re-send, or add a lightweight dedup key (event+user+window) the dispatcher checks.
- **Size:** S · **GAP needing a story:** No (decision + small fix).

### F8 — `SendSitewidePromoFanout` re-throws and retries the ENTIRE campaign on any post-partial failure, with no resume cursor — duplicate fan-out
- **Severity:** Medium · **Type:** Idempotency / amplification · **Layer:** Functions
- **File:** `SendSitewidePromoFanoutFunction.cs:104-163`
- **Impact:** Per-user enqueue failures are swallowed and skipped (line 137-146, good). But any failure **outside** that inner try (e.g. a `ToListAsync` page read throws on page 40 of 50, line 108) hits the outer catch and re-throws (line 162), so the queue retries the campaign **from offset 0** — re-enqueuing pushes to all users already processed on the prior attempt. Up to 5× duplicate promo pushes to early-paged users on a flaky run. No persisted cursor/idempotency key per campaign.
- **Long-term fix:** Persist a per-campaign progress cursor (last `UserId` processed) and resume from it on retry, or mark each recipient sent. Make the campaign idempotent on `(CampaignId, UserId)`.
- **Size:** M · **GAP needing a story:** Yes — "Resumable, idempotent sitewide promo fan-out."

### F9 — Tenant-override leakage risk on queue-trigger functions that don't clear the override before processing
- **Severity:** Medium · **Type:** Tenant-isolation (S8) · **Layer:** Functions
- **File:** `GenerateReceiptFunction.cs:54-57`, `SendPushNotificationFunction.cs:56-59`, `SendSitewidePromoFanoutFunction.cs:71-74` — all call `SetTenantOverride` but **never `ClearTenantOverride`**, unlike `FiscalRetryService.cs:44` which correctly clears first
- **Impact:** With `host.json batchSize: 1` and a per-invocation scoped `ITenantProvider`, this is currently safe (each message gets a fresh scope). But the pattern is inconsistent with `FiscalRetryService`, and if `batchSize` is ever raised or the provider is registered with a wider lifetime, a tenant override from message N would leak into message N+1's reads/writes — a cross-tenant data-write bug. Relates to S8 ("tenant isolation correctness") and consistency (queue-trigger functions should clear-then-set uniformly).
- **Long-term fix:** Standardize: every queue-trigger function calls `ClearTenantOverride()` at the top of the try (mirror `FiscalRetryService`). Codify as a consistency rule for Functions and add to `check-consistency.mjs` if checkable.
- **Size:** S · **GAP needing a story:** No (hardening) — but flag to Architect to codify the rule (see Pattern-evolution note).

### F10 — Error handlers log full `messageText` at Error level (PII-adjacent) for push/promo queues
- **Severity:** Low · **Type:** Logging / privacy (S10) · **Layer:** Functions
- **File:** `SendPushNotificationFunction.cs:118-119`, `SendSitewidePromoFanoutFunction.cs:159-161`, `GenerateReceiptFunction.cs:105-106`
- **Impact:** On failure the entire raw message is logged. `SendPushNotificationMessage` contains `UserId` (identifier) and `Args` (for promo: title/body marketing copy; for order events: orderId/orderNumber). Not strict PII, but `runtime-readiness.md` checklist item 1 says "no PII above Debug" and IDs in error logs aggregate into App Insights retention. Low severity because no names/emails/addresses are in these payloads.
- **Long-term fix:** Log identifiers (OrderId/UserId/EventKey) explicitly; drop the raw `messageText` to Debug.
- **Size:** S · **GAP needing a story:** No.

### F11 (secondary, not Functions-specific) — `UnitOfWorkPipelineBehavior` commits even when validation fails
- **Severity:** Low · **Type:** Pipeline correctness · **Layer:** AppServices
- **File:** `UnitOfWorkPipelineBehavior.cs:14-22` (outer) wraps `ValidationPipelineBehavior.cs` (inner), registered in that order at `FluentValidationExtensions.cs:13-14`
- **Impact:** Validation returns a failed `BusinessResult` (not an exception), so the outer UnitOfWork behavior still reaches `await unitOfWork.CommitAsync` unconditionally for any `*Command`. Harmless when the handler didn't run (no tracked changes), but a validator or earlier-stage code that touches the DbContext would persist on a "rejected" command. Surfaced here because timer Functions drive these commands frequently and without a transaction boundary above them.
- **Long-term fix:** Skip commit when the response `IsFailure`, or reorder so validation is the outer behavior. Architect call.
- **Size:** S · **GAP needing a story:** No.

---

## Idempotency verification (what HOLDS today)
- `SendMembershipLifecycleNotifications` stamps `RenewalReminderSentAt`/`CancellationReminderSentAt` and filters on them (`SendMembershipLifecycleNotifications.cs:77, 96, 114, 134`); the `*Command` name triggers `UnitOfWorkPipelineBehavior` commit (`UnitOfWorkPipelineBehavior.cs:27` checks `EndsWith("Command")`) — so stamps **do** persist and the every-2-min cadence does not currently spam users. Correct, but load-bearing (see F5).
- `AutoCancelStaleRecurringOrders` and `CleanupStalePendingOrders` are idempotent via status-filter (a cancelled order stops matching) — verified by design comments and consistent with the command filters.
- `CalculateOrderPay` consumer acks validator rejections (no poison) and throws only on infra errors (`CalculateOrderPayFunction.cs:47-65`) — correct retry semantics.

## Pattern-evolution note for the PM → Architect
Two recurring Functions-layer gaps are now appearing across multiple functions and warrant codified rules + a `check-consistency.mjs` check:
1. **Queue-trigger functions must `ClearTenantOverride()` before `SetTenantOverride()`** (F9 — inconsistent across 3 functions vs the correct `FiscalRetryService`).
2. **Command handlers must not enqueue before the pipeline commit** (F2 — present in 4 handlers). Extend B8 to mandate post-commit/outbox dispatch.
Recommend a new `consistency.md` archetype section "F. Azure Functions / background jobs" capturing: clear-then-set tenant, idempotency guard on every side-effecting consumer (S7), explicit poison/dead-end, and cron-from-app-setting.

## Stories to open (functional GAPs)
F1 (invoice path decision), F2 (transactional outbox), F3 (poison handling + admin replay), F4 (idempotent receipt sequence), F5 (timer schedules from app settings + restore ordering), F8 (resumable promo fan-out).

**Relevant files:**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Functions\Functions\` (all 16)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Functions\host.json`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Orders\CompleteOrder.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\ReceiptService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\FiscalRetryService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\PayPeriodBackgroundService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Behaviors\UnitOfWorkPipelineBehavior.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Queue.Abstractions\QueueNames.cs`


# ===== identity-auth CODE QUALITY =====

AdminGdprController is well-protected (S2/S3 via policies). `MyProfileDto` correctly excludes `Id` (good S4 hygiene). The `AdminGdprController` uses `v{version:apiVersion}` route while other admin controllers don't — minor inconsistency but not in scope. The `DeleteUserAccount` uses `HandleResult<object>` (smell). `GetAllGdprRequests` controller takes inline `page`/`pageSize` instead of the canonical `DataRangeRequest` — already noted.

I have comprehensive coverage now. Let me write up the findings report, ranked by impact.

---

# Audit Findings — Identity-Auth Domain (Code Quality / Spaghetti / Consistency)

Scope covered: `Features/{Auth, Users, AdminUsers, Devices, Gdpr, Notifications, DataRetention}`, the 4 web auth controllers + 2 web user controllers, customer/partner login + forgot-password facades, customer/partner mobile auth VMs + repositories. Rules cited from `consistency.md` (A–E), `conventions.md`, `security-rules.md` (S1–S10). Mechanical `check-consistency.mjs` run included (11 B1/B3 hits, see IA-2/IA-3).

## CRITICAL

### IA-1 — `CreateAdminUser` double-hashes the password → new admins cannot log in
- **Type:** functional bug (data correctness) · **Severity:** critical · **Size:** S · **Layers:** backend
- **Where:** `src/Cleansia.Core.AppServices/Features/AdminUsers/CreateAdminUser.cs:74` (`command.Password.HashAndSaltPassword()`), interacting with `src/Cleansia.Infra.Database/Converters/PasswordConverter.cs:6` and `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs:14-16`.
- **Detail:** `User.Password` has an EF value converter (`PasswordConverter`) that calls `HashAndSaltPassword()` on every write. Every other create/update path (`Register`, `RegisterEmployee`, `ChangePassword` → `User.UpdatePassword`, `User.CreateWithPassword`) stores the **raw** password and lets the converter hash it once. `CreateAdminUser` pre-hashes with `HashAndSaltPassword()` and then the converter hashes the result **again**, persisting `Hash(Hash(password))`. Login verifies `VerifyPassword(rawPassword, stored)`, which fails.
- **Impact:** Any admin created through this endpoint cannot authenticate; they'd need a password reset to ever log in. Silent — no error at creation time.
- **Fix:** Remove the manual `HashAndSaltPassword()` call in the handler; pass the raw password to `User.CreateWithPassword` (consistent with `Register`). Add a unit test asserting `VerifyPassword(raw, user.Password)` after a round-trip. Longer-term, the hidden hashing-in-a-value-converter (IA-12) is the structural root cause — a write converter that mutates the value invites exactly this class of bug.
- **GAP:** No (bug in built code).

### IA-2 — Three near-identical login commands + four near-identical auth controllers (massive duplication)
- **Type:** spaghetti / duplication · **Severity:** critical (maintenance) · **Size:** M · **Layers:** backend
- **Where:** `Features/Auth/Login.cs`, `Features/Auth/PartnerLogin.cs`, `Features/Auth/AdminLogin.cs` (validators are byte-for-byte identical; handlers differ only by a profile check). Controllers: `Cleansia.Web.Customer/Controllers/AuthController.cs`, `Cleansia.Web.Partner/Controllers/AuthController.cs`, `Cleansia.Web.Admin/Controllers/AdminAuthController.cs`, `Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs`, `Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs` — the `HandleTokenIssuingResult` helper and Refresh/Logout cookie bodies are copy-pasted verbatim across hosts.
- **Detail:** `Login`/`PartnerLogin`/`AdminLogin` share the same 45-line validator (email/password/rememberMe rules, `UserAuthenticationTypeIsInternal`, `HasValidPassword`) and the same handler skeleton; the only real difference is `if (user.Profile != X)`. This is the textbook "same operation written N ways" `conventions.md` calls out as the prime threat. The login validator also duplicates `HasValidPassword`/`UserAuthenticationTypeIsInternal` 3×.
- **Impact:** Any auth fix (e.g. timing-safe lookup, lockout, error-key change) must be made in 3 handlers + 4 controllers and is already drifting (see IA-5, IA-6).
- **Fix:** One `Login.Command` carrying an allowed-profiles set (or a `RequiredProfile`/`LoginContext` enum like `RefreshToken.Command` already does), one validator, one handler doing the profile gate from that input. Extract a shared base auth controller (or a single helper service) for `HandleTokenIssuingResult` + cookie-first Refresh/Logout. Architect-owned because it spans the host boundary.
- **GAP:** No.

### IA-3 — Mobile users can request a password reset but cannot complete one (web is inverse)
- **Type:** functional gap / cross-surface inconsistency · **Severity:** critical · **Size:** M · **Layers:** backend, mobile, frontend
- **Where:** Mobile auth controllers expose `POST ForgotPassword` → `RequestPasswordChange` but **no `ChangePassword` endpoint** (`Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs:80-88`, `Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:88-97`). The web controllers expose `ChangePassword` on `UserController` (`Cleansia.Web.Customer/Controllers/UserController.cs:53`, `Cleansia.Web.Partner/Controllers/UserController.cs:64`) but **no `ForgotPassword`/`RequestPasswordChange` on the *AuthController*** (it lives on UserController as `PUT RequestPasswordChange`). The mobile `AuthRepository.changePassword` exists and calls `api.changePassword` — but the customer mobile controller has no route for it, so the reset flow is half-wired.
- **Detail:** The reset flow needs both legs (request code → submit code+new password). Mobile has leg 1 only at the controller level; web split the two legs across two controllers with different HTTP verbs (`PUT` on web vs `POST` on mobile). The endpoint surface for the *same flow* differs per host.
- **Impact:** A mobile user who forgets their password gets the reset email but the app's "set new password" call may 404 (customer mobile). Locked-out users cannot recover on mobile.
- **Fix:** Add the `ChangePassword` route to both mobile auth controllers; standardize the reset flow's endpoints (verb + controller) across all hosts. Verify the generated mobile clients actually have `changePassword`.
- **GAP:** Yes — needs a user story to complete + harmonize the password-reset surface across web/mobile/customer/partner.

## MAJOR

### IA-4 — GDPR handlers misuse `Error(code, message)` and embed hardcoded English strings
- **Type:** consistency (B5) + hardcoded user-facing string · **Severity:** major · **Size:** S · **Layers:** backend
- **Where:** `Features/Gdpr/ExportUserData.cs:27-28` (`new Error(BusinessErrorMessage.UserNotFound, "User not found")`), `Features/Gdpr/GrantConsent.cs:45-46` (`..., "Consent already granted")`), `Features/Gdpr/WithdrawConsent.cs:35-36` (`..., "Consent not found")`).
- **Detail:** `Error` is `Error(string code, string message)` (`Cleansia.Infra.Common/Validations/Error.cs:3`). B5 requires `new Error(nameof(command.Field), BusinessErrorMessage.X)`. These pass the **message-key constant as the `code`** and an inline **English literal as the `message`** — both wrong. It violates B5 and the `conventions.md` "no hardcoded user-facing strings" rule (the literal bypasses the 5-locale i18n contract).
- **Impact:** The `Message` field carries untranslated English; if any client/serializer reads `.Message` instead of `.Code` it shows English to cs/sk/uk/ru users. The `code` semantics are inverted vs every other handler.
- **Fix:** `new Error(nameof(field), BusinessErrorMessage.X)` for each (e.g. `nameof(request.ConsentType)` / a user field). Same backwards pattern exists outside scope at `EmployeePayroll/DownloadInvoice.cs:46,56`, `PromoCodes/Admin/*` — worth a sweep.
- **GAP:** No.

### IA-5 — Refresh-token profile pinning is inconsistent across hosts (partner refresh isn't profile-locked)
- **Type:** security-adjacent consistency · **Severity:** major · **Size:** S · **Layers:** backend · **flag to Security Reviewer**
- **Where:** `Cleansia.Web.Admin/Controllers/AdminAuthController.cs:48` pins `RequiredProfile = UserProfile.Administrator` **and** audience. `Cleansia.Web.Partner/Controllers/AuthController.cs:101` and `Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:106` pin **only** `RequiredAudience` (no `RequiredProfile`). Customer hosts also pin audience only.
- **Detail:** `RefreshToken.Command` supports `RequiredProfile` (`Features/Auth/RefreshToken.cs:32`, enforced at `:75-79`). Admin uses it; the partner host (which should only ever serve Employee/Administrator) does not, so audience is the only guard. Whether that's a real hole depends on how audiences are minted per login — the inconsistency itself is the smell and should be a deliberate, documented decision, not an accident.
- **Impact:** Potential for a token minted in one profile context to refresh on a host it shouldn't, if audience scoping is ever loosened. At minimum it's an undocumented divergence in a security-critical path.
- **Fix:** Decide the intended invariant and apply it uniformly (pin profile on partner/customer the way admin does, or document why audience-only suffices). Security Reviewer should own the call.
- **GAP:** No.

### IA-6 — Email max-length and password rules diverge across registration / reset paths (4+ copies)
- **Type:** spaghetti / duplication / correctness · **Severity:** major · **Size:** M · **Layers:** backend, frontend
- **Where:**
  - Email max length: `BaseAuthValidator.AddEmailRules` → `MaximumLength(50)` (`Features/Auth/Validators/BaseAuthValidator.cs:21`); `CreateAdminUser` → `MaximumLength(150)` (`Features/AdminUsers/CreateAdminUser.cs:34`).
  - Password regex: backend `BaseAuthValidator` `^(?=.*[a-zA-Z])(?=.*\d).{8,}$` and `ChangePassword.cs:21` (same); `CreateAdminUser` only `MinimumLength(8)` (no complexity); customer FE `forgot-password.facade.ts:115` `^(?=.*[a-zA-Z])(?=.*\d).{8,}$`; partner FE `forgot-password.facade.ts:113` `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$` (stricter).
- **Detail:** The same business rule (valid email length, valid password) is encoded in ~5 places with 3 different password rules and 2 different email lengths. Admin passwords are materially weaker than customer/partner. The partner FE rejects passwords the backend accepts.
- **Impact:** Inconsistent security posture (weak admin passwords), and UX bugs (partner user types a backend-valid password, FE blocks it). Adding a rule means hunting 5 files.
- **Fix:** Single source of truth: a shared password rule extension on the backend (composed via `.SetValidator`, per B3) and a single exported regex/validator constant on the frontend consumed by both apps. Align admin to the same complexity. Pick one email max length.
- **GAP:** No.

### IA-7 — `Register`/`RegisterEmployee` are near-duplicates; RegisterEmployee adds two real defects
- **Type:** spaghetti + functional bug · **Severity:** major · **Size:** S · **Layers:** backend
- **Where:** `Features/Auth/Register.cs` vs `Features/Auth/RegisterEmployee.cs`.
- **Detail:** Validators are identical (email/first/last/password/language rules + `UserWithEmailNotExistsAsync`). Two defects in `RegisterEmployee.cs`: (a) on the new-user branch it `employeeRepository.Add(Employee.CreateWithUser(...))` at line 74 and then **again** at line 79 (`if (userEntity.Employee is null)`), adding the Employee twice for a brand-new user; (b) it creates a `Cart` for an Employee (line 73) — carts are a customer concept. `Register` also creates a Cart for Customer (correct).
- **Impact:** Duplicate Employee row (or a DB constraint failure) on partner self-registration; stray Cart rows for cleaners.
- **Fix:** Extract a shared registration helper parameterized by profile (+ post-create hook for Cart vs Employee); fix the double-add by guarding the Employee creation in one place; drop the Cart for employees.
- **GAP:** No.

### IA-8 — Two near-identical forgot-password facades; both swallow errors and skip the canonical pipe
- **Type:** spaghetti + C-archetype violations · **Severity:** major · **Size:** M · **Layers:** frontend
- **Where:** `libs/cleansia-customer-features/forgot-password/.../forgot-password.facade.ts`, `libs/cleansia-partner-features/forgot-password/.../forgot-password.facade.ts` (~95% identical).
- **Detail:** Both: (a) have **no `error` handler** on `requestPasswordChange`/`changePassword` subscribes → API failures are silently swallowed (violates C4 "errors surface via SnackbarService" and the production-ready "error states are part of the work" bar); (b) don't use the C3 pipe (`takeUntil → catchError → finalize`); (c) use raw `new FormGroup`/`new FormControl` instead of `fb.nonNullable.group` (D2); (d) use mutable public booleans (`isEmailSent`, `isResendDisabled`, `resendCodeTimeout`) instead of signals (C2/D1); (e) implement the resend cooldown two different ways with inline magic numbers (`30`, `30_000`); (f) customer facade imports `ChangePasswordCommand`/`RequestPasswordChangeCommand` from `@cleansia/partner-services` (wrong client package — cross-app type coupling).
- **Impact:** A failed reset shows nothing to the user; duplicated drifting logic; wrong-package imports couple customer to partner client.
- **Fix:** Unify into one shared forgot-password facade (or a shared base) extending `UnsubscribeControlDirective`, signal state, C3 pipe + `showApiError`, `fb.nonNullable.group`, named cooldown constant; import commands from the correct per-app client.
- **GAP:** No.

### IA-9 — Login facades: partner swallows login errors; both deviate from the form archetype
- **Type:** functional bug + consistency · **Severity:** major · **Size:** S · **Layers:** frontend
- **Where:** `libs/cleansia-partner-features/login/.../login.facade.ts:39-51` (subscribe has **no `error` handler** — failed partner login shows nothing), vs customer `login.facade.ts:56-58` (has one). Customer `login.facade.ts:9` imports `JwtTokenResponse` from `@cleansia/partner-services` (wrong package). Both use raw `new FormControl`/`new FormGroup` (D2) and don't use the C3 pipe.
- **Impact:** Partner users who mistype a password get no feedback. Cross-app import coupling.
- **Fix:** Add `error: (err) => snackbarService.showApiError(...)` to the partner facade; fix the customer import; move to `fb.nonNullable.group`. Consider a shared login facade base given the duplication.
- **GAP:** No.

### IA-10 — `GetAllGdprRequests` is a paged read that ignores the paged-query archetype
- **Type:** consistency (A1–A5, B9) · **Severity:** major · **Size:** M · **Layers:** backend
- **Where:** `Features/Gdpr/GetAllGdprRequests.cs` + controller `AdminGdprController.cs:42-49`.
- **Detail:** Uses `record Query(int Page = 1, int PageSize = 20)` with 1-based paging and manual `offset = (Page-1)*PageSize` (A1: should be `Request : DataRangeRequest` with Offset/Limit), returns `List<GdprRequestDto>` not `PagedData<T>` (A2/A5 — client can't know total count for paging UI), inline-projects the DTO in the handler (B9), no Specification (A3). Same shape leaks to the controller's inline `page`/`pageSize` query params.
- **Impact:** The admin GDPR-requests list can't render correct pagination (no total). Diverges from every other admin list.
- **Fix:** Convert to the canonical paged query: `Request : DataRangeRequest`, `GdprRequestSpecification` + `GetPagedSort<GdprRequestSort>`, `MapToDto(total, request)` extension, return `PagedData<GdprRequestDto>`.
- **GAP:** Partial — it works but the missing total is a real UX gap for the admin list.

### IA-11 — Dead code: `scrap/partner-app-pre-rebuild/` ships duplicate auth classes in live packages
- **Type:** dead code · **Severity:** major · **Size:** S · **Layers:** mobile
- **Where:** `src/cleansia_android/scrap/partner-app-pre-rebuild/kotlin/auth/{viewmodels,screens}/*.kt` (13 files: Login/Register/ForgotPassword VMs + screens, ProfileFormState, ProfileValidator).
- **Detail:** These files declare the **same package** as the live code (`cz.cleansia.partner.features.auth.viewmodels`, confirmed). They're an unreferenced pre-rebuild backup. `conventions.md`: "No dead code. Delete unreferenced methods/classes." Risks duplicate-class confusion and accidental edits to the wrong copy.
- **Fix:** Delete the `scrap/` tree (it's in git history if ever needed).
- **GAP:** No.

### IA-12 — Hidden password-hashing inside an EF value converter (root cause of IA-1)
- **Type:** design smell / footgun · **Severity:** major · **Size:** M · **Layers:** backend, db
- **Where:** `Cleansia.Infra.Database/Converters/PasswordConverter.cs:6`, wired at `UserEntityConfiguration.cs:14-16`.
- **Detail:** A write-side value converter that calls `HashAndSaltPassword()` means "assigning a string to `User.Password`" silently hashes on persistence. This is non-obvious (callers can't tell raw vs hashed), it broke IA-1, and it makes the entity's in-memory `Password` differ from the persisted value within the same context. There are also now **two** password verifiers (`Domain/Extensions/PasswordExtensions.VerifyPassword` and `AppServices/Extensions/AuthExtensions.CheckIfPasswordSame`, the latter just delegating) — a thin redundant indirection.
- **Impact:** Any future "set password" code must know not to pre-hash; the converter also re-hashes if EF ever marks `Password` modified on an unrelated update path.
- **Fix:** Move hashing to an explicit domain method (`User.SetPassword(raw)` → hashes once) and make the converter identity (or remove it). Collapse the two verifier helpers to one. Architect/DB-owned (touches persistence + needs care that existing hashes still verify). 
- **GAP:** No.

## MINOR

### IA-13 — `Devices` handlers throw raw `UnauthorizedAccessException` with hardcoded English; magic-string platform; hard-delete
- **Type:** consistency / hardcoded string / magic value · **Severity:** minor · **Size:** S · **Layers:** backend
- **Where:** `Features/Devices/RegisterDevice.cs:35-36` & `UnregisterDevice.cs:27-28` (`?? throw new UnauthorizedAccessException("User ID not found in claims.")` — hardcoded English, bypasses `BusinessResult`); `RegisterDevice.cs:24` (`p is "android" or "ios"` magic strings — should be an enum per "no magic strings"); `UnregisterDevice.cs:34` (`repo.Remove` hard-delete, B6).
- **Fix:** Return `BusinessResult.Failure(new Error(...BusinessErrorMessage.X))` instead of throwing; introduce a `DevicePlatform` enum; keep `Remove` only if device rows are truly scratch (document it).

### IA-14 — `RequestPasswordChange` controller actions declare a bogus generic return type
- **Type:** dead/incorrect type · **Severity:** minor · **Size:** S · **Layers:** backend
- **Where:** `Cleansia.Web.Customer/Controllers/UserController.cs:48` and `Cleansia.Web.Partner/Controllers/UserController.cs:60` both `return HandleResult<UserListItem>(result)` while `RequestPasswordChange.Command : ICommand` returns no value. The `<UserListItem>` generic is meaningless here.
- **Fix:** Use the no-value `HandleResult` overload (matching the void command).

### IA-15 — Notification-preferences category list duplicated; DTO inline-projected (B9)
- **Type:** duplication · **Severity:** minor · **Size:** S · **Layers:** backend
- **Where:** `Features/Notifications/UpdateNotificationPreferences.cs:48-71` and `GetMyNotificationPreferences.cs:41-53` — the 11-category `Set(...)`/projection lists are repeated; `UpdateNotificationPreferences` inline-projects the DTO instead of a shared `entity.MapToDto()` (B9).
- **Impact:** Adding a 12th `NotificationCategory` means editing the same list in 2-3 places; easy to miss one.
- **Fix:** One `UserNotificationPreferences.MapToDto()` extension + a single `ApplyFrom(command)` domain method.

### IA-16 — Partner-web password endpoints lack the `"auth"` rate limit that customer-web has (S5)
- **Type:** security consistency · **Severity:** minor · **Size:** S · **Layers:** backend · **flag to Security Reviewer**
- **Where:** `Cleansia.Web.Customer/Controllers/UserController.cs:40,52` apply `[EnableRateLimiting("auth")]` to `RequestPasswordChange`/`ChangePassword`; `Cleansia.Web.Partner/Controllers/UserController.cs:52,63` do **not**.
- **Impact:** Partner password-reset request/confirm endpoints are un-throttled — email-bombing / code-brute-force surface (S5).
- **Fix:** Add `[EnableRateLimiting("auth")]` to the partner UserController's password actions.

### IA-17 — `ConfirmUserEmail` logs PII (email) and the confirmation code at Warning (S6); re-fetches in handler
- **Type:** logging hygiene + minor spaghetti · **Severity:** minor · **Size:** S · **Layers:** backend · **flag to Security Reviewer**
- **Where:** `Features/Auth/ConfirmUserEmail.cs:41` logs the **confirmation code** (`{Code}`) and `:47-48` logs `user.Email` + expiry at `LogWarning`. S6 forbids email/confirmation-code in logs at Information+; the code is effectively a secret. The handler also re-runs `GetByConfirmationCodeAsync` (`:65`) that the validator already executed.
- **Fix:** Drop the code from logs; log `user.Id` not `user.Email`; lower to `LogDebug` if needed for local debugging. (Optional: have the validator stash the resolved user.)

### IA-18 — `CreateAdminUser` existence check bypasses the canonical helper and `ToLower()`s in SQL
- **Type:** consistency / perf · **Severity:** minor · **Size:** S · **Layers:** backend
- **Where:** `Features/AdminUsers/CreateAdminUser.cs:36-39` — `userRepository.GetAll().AnyAsync(u => u.Email.ToLower() == email.ToLower())` instead of the existing `userRepository.ExistsWithEmailAsync` used everywhere else (Login/Register/ResendConfirmation). `ToLower()` on both sides prevents index usage.
- **Fix:** Use `ExistsWithEmailAsync` (and ensure case-insensitive matching lives there once).

### IA-19 — Audit-reason / actor magic strings in GDPR deletion
- **Type:** magic string · **Severity:** minor · **Size:** S · **Layers:** backend
- **Where:** `Features/Gdpr/DeleteUserAccount.cs:23` (`"GDPR_DELETION"`), `AdminDeleteUserAccount.cs:33,37-38` (`"admin"`, `"GDPR_ADMIN_DELETION"`, `$"Admin deletion by {adminEmail}"`).
- **Fix:** Promote the reason codes to named constants (a `GdprAuditReason` static class) for consistency with `RetentionDefaults`-style constant homes.

### IA-20 — Pre-existing B1/B3 archetype deviations across Auth/Users (mechanical-check confirmed)
- **Type:** consistency (B1, B3) · **Severity:** minor · **Size:** M · **Layers:** backend
- **Where:** `check-consistency.mjs` reports 11 hits: B3 (validators inherit `BaseAuthValidator`/`BaseUserValidator` instead of `AbstractValidator` + composed rules) in `Login/PartnerLogin/AdminLogin/GoogleAuth/Register/RegisterEmployee` and `UpdateCurrentUser`; B1 (command returns raw scalar, no `Response` record) in `Logout`, `Register`, `RegisterEmployee`, `ResendConfirmationEmail`. These overlap the already-tracked F5/F6 but the specific Auth files are not yet listed in `consistency-violations.md`.
- **Fix:** Fold into the existing "Refactor validators to AbstractValidator + composed shared rules" (F6) and a new "wrap bool-returning auth commands in Response records" ticket. **Pattern-evolution note for PM/Architect:** the B3/`BaseAuthValidator` deviation now recurs across 7 files in this domain alone — either codify `BaseAuthValidator` as an accepted composition seam (amend B3) or schedule the sweep; it keeps being "caught by hand."

---

## Notes for the PM
- **Security Reviewer (`security`)** should own the verdict on IA-5 (refresh profile pinning), IA-16 (partner rate-limit gap), IA-17 (PII/secret in logs).
- **Architect (`architect`)** decisions: IA-2 (cross-host auth consolidation), IA-12 (password-hashing-in-converter redesign), and the B3/`BaseAuthValidator` rule question in IA-20.
- **Top 3 by impact:** IA-1 (admins can't log in), IA-3 (mobile password reset half-built), IA-2 (auth duplication across 7 files). IA-1 and IA-7 are concrete bugs in shipped code; the rest are spaghetti/consistency/gap.

Key files: `src/Cleansia.Core.AppServices/Features/AdminUsers/CreateAdminUser.cs`, `src/Cleansia.Infra.Database/Converters/PasswordConverter.cs`, `src/Cleansia.Core.AppServices/Features/Auth/{Login,PartnerLogin,AdminLogin,Register,RegisterEmployee,ConfirmUserEmail}.cs`, `src/Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs`, `src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs`, `src/Cleansia.Core.AppServices/Features/Gdpr/{ExportUserData,GrantConsent,WithdrawConsent,GetAllGdprRequests}.cs`, `src/Cleansia.App/libs/cleansia-{customer,partner}-features/forgot-password/src/lib/forgot-password/forgot-password.facade.ts`, `src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/auth/viewmodels/LoginViewModel.kt`, `src/cleansia_android/scrap/partner-app-pre-rebuild/`.


# ===== disputes-addresses FUNCTIONAL GAPS =====

Confirmed: `LinkStripeDispute` / `StripeDisputeId` has a column and a domain method but **zero callers** — no Stripe chargeback webhook ever links a platform dispute to a Stripe dispute. The Anonymize GDPR path is wired. I have a complete picture. Let me write the findings report.

---

# Audit: Disputes & Addresses — Functional Gaps and Half-Built Features

Scope: `Features/Disputes`, `Features/Addresses`, `Features/SavedAddresses`, plus customer web/mobile disputes & addresses UI. Audit only; no code changed. Rules cited from `agents/knowledge/security-rules.md` (S1–S10) and `consistency.md` (B/C/E sections).

## Summary verdict
The **disputes** domain is half-built and the missing half is the entire **admin/support side**. Customers can open disputes and chat, but **no admin can triage, respond, resolve, escalate, or close a dispute from the Admin app** — those operations are physically mounted only on the *Partner* API, and there is no admin dispute UI at all. Three of six `DisputeStatus` states are unreachable. The **saved-addresses** domain is essentially complete and consistent across web + mobile; its issues are minor parity/quality items. The most severe finding is a **privilege-escalation hole** (S1) in the dispute message endpoint.

Ranked by impact below.

---

### D-01 — No Admin dispute management surface; resolve/respond/status live only on the Partner API
- **Severity:** critical · **Type:** missing flow / endpoints-on-wrong-host · **Layers:** backend (API host), admin frontend · **Size:** L · **GAP → needs story**
- **Evidence:**
  - `ResolveDispute`, `UpdateDisputeStatus`, `AddMessage` are exposed **only** in `src/Cleansia.Web.Partner/Controllers/DisputeController.cs:66-88` (Resolve/UpdateStatus) — there is **no** `src/Cleansia.Web.Admin/Controllers/*Dispute*` controller (confirmed: Admin has 26 controllers, none for disputes).
  - Policy intent in `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:77-78` maps `CanResolveDispute` / `CanUpdateDisputeStatus` = `AdminOnly`, and `Policy.cs:103-105` comments them "Only admins can resolve / update". So the design intends admin to do this, but the admin host never mounts the endpoint.
  - No admin disputes feature library exists (`libs/cleansia-admin-features/**` has zero dispute features; the only "dispute" hits there are unrelated `PaymentStatus.Disputed` labels).
- **Impact:** Disputes can be filed by customers but **cannot be worked by support/admin through the Admin app**. Resolution is only reachable by a partner/cleaner hitting the Partner API directly — which is also the wrong actor for refund decisions. Practically, disputes are a write-only inbox: they accumulate with no operational resolution path. High business + trust risk.
- **Fix (long-term-correct):** Add an Admin dispute controller exposing list/detail/respond/resolve/update-status/escalate/close, and an `admin-features/dispute-management` library (list + detail + resolution panel) following the C-section list archetype. Remove resolve/update-status from the Partner controller (a cleaner is not the dispute arbiter) unless a deliberate "partner can comment" scope is defined.

### D-02 — Customer can self-promote to "staff": `IsStaffMessage` is client-supplied (privilege escalation, S1)
- **Severity:** critical · **Type:** security / trusted-client-input · **Layers:** backend · **Size:** S · **GAP (security defect; story + ticket)**
- **Evidence:** `AddDisputeMessage.Command` carries `bool IsStaffMessage` from the request body (`AddDisputeMessage.cs:34-38`). The handler's ownership guard is **skipped when the flag is true**: `if (!request.IsStaffMessage && dispute.UserId != userId)` (`AddDisputeMessage.cs:50`). The customer web/mobile `DisputeController.AddMessage` forwards the command verbatim (`Cleansia.Web.Customer/Controllers/DisputeController.cs:58-62`), and `CanRespondToDispute` is mapped to **`Authenticated`**, not Admin (`PolicyBuilder.cs:76`) and is mounted on the customer + mobile hosts.
- **Impact:** Any authenticated customer can POST `isStaffMessage=true` to (a) **post a message into a dispute they don't own** (ownership check bypassed) and (b) have that message rendered to the real owner as an official **staff** reply (`disputes.component.html:228` styles staff bubbles distinctly, and `AddDisputeMessage.cs:65` even fires a `DisputeReply` push to the owner). This is impersonation of support + cross-tenant/cross-user write. Violates S1 (server-truth, never trust client flags) and S3 (ownership in handler).
- **Fix:** Remove `IsStaffMessage` from the wire command. Derive staff-vs-customer from the caller's role claim server-side; enforce ownership for non-staff unconditionally (no flag short-circuit). Gate the staff path behind an admin-only host/policy.

### D-03 — `Closed` and `Escalated` states (and `Escalate`/`Close` domain methods) are unreachable
- **Severity:** major · **Type:** lifecycle state never reachable / dead domain code · **Layers:** backend · **Size:** M · **GAP → needs story**
- **Evidence:** `DisputeStatus` has `UnderReview(2)`, `WaitingForResponse(3)`, `Closed(5)`, `Escalated(6)` (`Enums/DisputeStatus.cs`). `Dispute.Close()` and `Dispute.Escalate()` exist (`Dispute.cs:92-102`) but have **zero callers** anywhere in the solution. The only status transition command is the generic `UpdateDisputeStatus` (sets any enum value but runs no resolution/escalation side effects), and even that is admin-host-unreachable per D-01. `Resolve()` is the only rich transition that's wired (to the Partner host).
- **Impact:** The documented dispute lifecycle is mostly fictional: there is no "escalate to senior support", no "close without refund", and `UnderReview`/`WaitingForResponse` are never set by any flow, so the customer's status tag is effectively stuck at `Pending` until a partner resolves. Reporting/SLAs on dispute states are impossible.
- **Fix:** Define the real lifecycle (which transitions are legal from which state, who can trigger each) and implement `EscalateDispute` / `CloseDispute` commands plus transition guards in the domain (reject illegal jumps). Tie into the Admin surface from D-01. Escalate via `questions/open.md`: *what are the legal dispute transitions and who owns each?*

### D-04 — Customer web disputes UI never uploads or shows evidence, and never shows refund amount (web ↔ mobile parity gap)
- **Severity:** major · **Type:** UI dead-end / endpoint with no web consumer / cross-platform inconsistency · **Layers:** customer frontend · **Size:** M · **GAP → needs story**
- **Evidence:**
  - Backend `UploadDisputeEvidence` is fully built and exposed on the customer web host (`Cleansia.Web.Customer/Controllers/DisputeController.cs:64-93`, `CanUploadDisputeEvidence` = CustomerOnly). `DisputeDetails` returns `Evidence` and `RefundAmount` (`DTOs/DisputeDetails.cs:10,13`).
  - The **web** customer UI never calls `uploadEvidence` (grep across `libs/cleansia-customer-features`: no hits) and the detail dialog renders description, resolution notes, and the message thread but **never the `evidence` list nor `refundAmount`** (`disputes.component.html:188-264`).
  - The **Android** customer app implements both: evidence upload with client-side size/MIME validation and an evidence list (`DisputeDetailViewModel.kt:112-134`, `core/disputes/DisputeApi.kt:58-69`).
- **Impact:** Web customers cannot attach photos/PDF proof to a quality/damage dispute (the single most important input for `DamagedProperty`/`QualityIssue` claims) and never see the agreed refund. Mobile users can. This is a real feature-parity hole and weakens every dispute filed from the web.
- **Fix:** Add evidence upload (file picker + the C3 client-call pipe via the facade) and render `evidence` (with the SAS `blobUrl`) and `refundAmount` in the detail dialog. Mirror the backend's MIME/size whitelist client-side as Android already does.

### D-05 — No way to file a dispute from an order; create flow is disconnected from the order context
- **Severity:** major · **Type:** missing flow / documented-intent gap · **Layers:** customer frontend (web + mobile) · **Size:** M · **GAP → needs story**
- **Evidence:** The create-dispute dialog makes the customer **re-pick an order from a dropdown** loaded via `loadOrdersForSelect()` capped at 100 (`disputes.facade.ts:64-66`, `disputes.component.html:110-123`). There is no "Report a problem / Open dispute" CTA on an order detail/completed order that pre-fills the order. (Order-detail facades in customer-features contain no dispute create call.)
- **Impact:** Disputes are filed from a standalone page divorced from the order the customer is unhappy about; the order picker is also silently truncated at 100 orders (long-tenured customers can't select older orders). High friction → fewer legitimate disputes filed correctly, and a hard cap defect.
- **Fix:** Add an "Open dispute" action on eligible orders (e.g. Completed within a refund window) that deep-links to the create flow with `orderId` pre-bound; remove the standalone order picker (or make it the fallback). Define eligibility window via `questions/open.md`.

### D-06 — Stripe chargeback linkage is a dead stub: `LinkStripeDispute` / `StripeDisputeId` has no caller
- **Severity:** major · **Type:** half-built feature / column+method, no command/webhook · **Layers:** backend · **Size:** M · **GAP → needs story**
- **Evidence:** `Dispute.StripeDisputeId` (column in every migration snapshot, configured at `DisputeEntityConfiguration.cs:42`) and `Dispute.LinkStripeDispute(...)` (`Dispute.cs:104-108`) exist, but grep shows **zero callers** of `LinkStripeDispute` and no Stripe `charge.dispute.*` webhook handler anywhere. The DTOs don't expose it either.
- **Impact:** When a customer files an actual **card chargeback** with their bank, Stripe sends a `charge.dispute.created` webhook — the platform has the data model to correlate it to an internal `Dispute`/`Order` but never does. Finance has no link between platform disputes and real money chargebacks; double-handling and reconciliation gaps result. This is the partially-built half of the disputes feature.
- **Fix:** Implement a `charge.dispute.created/updated/closed` webhook that finds the order by Stripe charge id, creates-or-links a `Dispute` via `LinkStripeDispute`, and reflects Stripe's status. Must be idempotent (S7) against webhook re-delivery.

### D-07 — Consistency violations in dispute commands are explicitly catalogued but unfixed (B1/B2/B4)
- **Severity:** minor · **Type:** inconsistency between layers · **Layers:** backend · **Size:** S · (quality, not a user-facing gap)
- **Evidence (cite `consistency.md`):**
  - **B1:** `CreateDispute` returns `ICommand<string>` and `UpdateDisputeStatus` returns bare `ICommand` — the catalog names both as deviations; commands must return a real `Response` record (`consistency.md:46-48`; `CreateDispute.cs:44`, `UpdateDisputeStatus.cs:29`).
  - **B4/S3:** `UpdateSavedAddress` and `DeleteSavedAddress` do **ownership checks in the validator** (`UpdateSavedAddress.cs:44-45`, `DeleteSavedAddress.cs:31-32`); the catalog says ownership belongs in the handler (`consistency.md:61-63`). Note `ResolveDispute`/`UpdateDisputeStatus` correctly guard in the handler — the pattern is applied inconsistently across the two domains.
- **Impact:** Maintenance drift; ownership-in-validator is also an S3 placement smell (auth logic should hold regardless of host). Not user-visible.
- **Fix:** Wrap responses in records; move saved-address ownership guards into handlers. Tracked canonicalization, not urgent.

### D-08 — Customer disputes feature mixes NgRx + facade signals (C8 deviation)
- **Severity:** minor · **Type:** inconsistency · **Layers:** customer frontend · **Size:** S · (quality)
- **Evidence:** `consistency.md:104-105` explicitly names "customer `disputes` mix both" — the facade pulls list/detail state from NgRx (`dispute.effects.ts`, selectors) while also calling the client directly for create/message (`disputes.facade.ts:78-126`). Single-feature state should live in facade signals; NgRx is for cross-feature state only.
- **Impact:** Two state paradigms in one small feature; harder to reason about loading/error. No user-facing defect.
- **Fix:** Collapse the dispute list/detail state into the facade signals (drop the dedicated NgRx slice) per C2/C8.

### D-09 — `GetSavedAddresses` has no `IsActive` filter (latent S10), and saved addresses are hard-deleted (B6)
- **Severity:** minor · **Type:** latent rule gap · **Layers:** backend · **Size:** S · (quality / latent)
- **Evidence:** S10 explicitly lists "list my saved addresses" as a query that must exclude deactivated rows (`security-rules.md:120`). `SavedAddressRepository.GetByUserAsync` filters only by `UserId` with no `IsActive` predicate (`SavedAddressRepository.cs:12-18`), and `DeleteSavedAddress` uses **hard delete** `Remove` (`DeleteSavedAddress.cs:56`) rather than `Deactivate` (B6, `consistency.md:67-72`). Today this is benign *only because* nothing ever deactivates a saved address; the moment delete becomes soft-delete, deleted addresses will reappear in the list.
- **Impact:** No current user-facing bug, but the soft-delete migration (the catalog's stated long-term direction) would silently resurrect deleted addresses. Worth flagging now.
- **Fix:** When moving saved addresses to soft-delete per B6, add `.Where(s => s.IsActive)` to `GetByUserAsync`/`GetDefaultForUserAsync`. Track together.

### D-10 — No customer-facing status filter / unread indicator on disputes list
- **Severity:** minor · **Type:** missing flow · **Layers:** customer frontend · **Size:** S · **GAP (small story)**
- **Evidence:** `DisputeFilter` supports status filtering on the backend (used by `GetPagedDisputes`), but the customer store action only passes `offset/limit` (`dispute.actions.ts:8-11`, `dispute.effects.ts:17-22`) and the list has no status filter or "new staff reply" badge — even though staff replies fire a `DisputeReply` push (`AddDisputeMessage.cs:65-77`). The list card shows status but offers no way to find "the one with a new reply".
- **Impact:** Customers with several disputes can't filter open vs resolved or spot which has a new support reply; the notification arrives but the list doesn't reflect unread state.
- **Fix:** Surface a status filter and a per-dispute "new reply since last viewed" indicator (needs a last-read timestamp — escalate the data model via `questions/open.md`).

---

## Items checked and found OK (not gaps)
- **SavedAddresses CRUD** (Add/Update/SetDefault/Delete/GetMine) is fully wired backend → web (`SavedAddressStore` + profile facade) → mobile (`AddressManagerSheet`/`SavedAddressApi`), exposed on both web and mobile hosts. Single-default invariant enforced via `ClearDefaultForUserAsync`.
- **Dispute GDPR anonymization** is wired (`GdprDeletionService.cs:205` calls `dispute.Anonymize()`, cascading to messages/evidence).
- **Android customer disputes** (list/detail/create/message/evidence) is the most complete dispute client and is the reference for the web gaps in D-04.

## Open questions to route to `questions/open.md`
1. What are the **legal `DisputeStatus` transitions**, and which actor (admin vs senior support vs partner) may trigger each (`Escalate`, `Close`, `UnderReview`, `WaitingForResponse`)? (D-03)
2. **Who arbitrates disputes** — admin only, or do partners get a comment-only role? Determines whether resolve/status stay on the Partner host. (D-01)
3. What is the **eligibility window** for opening a dispute from an order (e.g. N days after Completed)? (D-05)
4. Should saved-address delete become **soft-delete**, and if so confirm the `IsActive` list filter. (D-09)

Key files: `src/Cleansia.Core.Domain/Disputes/Dispute.cs`, `src/Cleansia.Core.AppServices/Features/Disputes/AddDisputeMessage.cs`, `src/Cleansia.Web.Partner/Controllers/DisputeController.cs`, `src/Cleansia.Web.Customer/Controllers/DisputeController.cs`, `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs`, `src/Cleansia.App/libs/cleansia-customer-features/disputes/src/lib/disputes/disputes.component.html`, `src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/disputes/DisputeDetailViewModel.kt`, `src/Cleansia.Infra.Database/Repositories/SavedAddressRepository.cs`.


# ===== employees PERFORMANCE =====

This `MapToDto` is a flat constructor projection with only direct property reads — EF Core *can* translate this inside `.Select()`, so `GetEmployeeDocuments` projects to SQL correctly (no over-fetch). Good — contrast with `MapToAdminDto` which calls `IsProfileComplete()`. This sharpens the GetPagedEmployees finding: the issue is specifically the method call to `IsEmployeeProfileComplete`/`IsProfileComplete` inside the projection.

Wait — I need to double-check whether EF can even run that `.Select(employee => employee.MapToAdminDto())`. It calls a static extension method, which EF cannot translate at all, so EF will throw or evaluate client-side. Since the query has `.Include()` calls before it, EF (v3+) does NOT auto client-evaluate arbitrary `Select` of method calls — it throws `could not be translated`. But this code presumably runs in production, meaning EF must be materializing entities. Actually EF Core treats a final `Select` calling an un-translatable instance/extension method by translating the *source* (the entity with Includes) and running the selector client-side — this is the one place EF still allows client eval (the top-level final projection). So it works but over-fetches the full entity graph including Documents. The finding stands as over-fetch + client-eval, not a crash.

I have a comprehensive picture. Let me write the findings report.

---

# Performance & Cost Audit — Employees Domain

Scope: `Features/Employees`, `Features/EmployeeDocuments`, `Features/Dashboard`, admin employee-management (web), partner dashboard/profile (web + mobile). Audit only; no code changed. Findings cite real code and the conventions in `agents/knowledge/conventions.md` (CancellationToken propagation, "production-ready long-term bar", reuse the real paging idiom).

Ranked by impact.

---

## CRITICAL

### PERF-EMP-01 — Partner dashboard load fans out into ~7 endpoints, several re-pulling the cleaner's full order history into memory
- **Type:** N+1 / over-fetch / chatty composition (read path, hottest partner screen)
- **Severity:** critical
- **Files:**
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetDashboardStats.cs:89-209` (Handler)
  - `src/Cleansia.App/libs/cleansia-partner-features/dashboard/src/lib/dashboard/dashboard.facade.ts:73-84,180-182`
  - `src/Cleansia.Infra.Database/Repositories/OrderRepository.cs:34-57` (`GetCompletedOrdersByDateRangeAsync`)
- **Impact:** One web dashboard open fires `getCurrentEmployee` → `loadDashboardStats` + `loadUpcomingOrders` + `refreshAllAnalytics` (which itself dispatches Earnings + Time + Order + Productivity = 4 handlers). `GetDashboardStats.Handle` alone issues 15+ sequential `await`s (4 count calls, 3 `GetCompletedOrdersByDateRangeAsync`, configs ×2, booked pay, invoice, pending earnings, active period, rating, currency). The three completed-order fetches are for **overlapping** windows (today ⊂ week ⊂ month) and each pulls the **full Order graph** (status history, assigned employees, services+`Service`, packages+`Package`) via `AsSplitQuery`, then they're concatenated and `DistinctBy`'d in C#. None of those order reads use `AsNoTracking()`, so the change tracker snapshots the whole graph on a read-only path. Cost grows with each cleaner's order count and is paid on every dashboard paint and every pull-to-refresh.
- **Long-term-correct fix:** (1) Fetch the month window once and derive the today/week subsets in memory from that single result instead of three DB round trips. (2) Add `AsNoTracking()` to `GetCompletedOrdersByDateRangeAsync` / `GetEmployeeOrdersByDateRangeAsync` (read-only analytics). (3) For the counts and earnings sums, project only the scalar columns the estimator needs (order id, service/package ids, `OrderEmployeePay` total) rather than the entity graph. (4) Run the independent calls concurrently where the UoW/DbContext model allows, or split stats vs. analytics so the screen paints on stats and lazy-loads charts.
- **Size:** L
- **Layers:** backend (AppServices, Infra.Database), frontend (partner)
- **Functional GAP?** No — needs a perf refactor story, not a feature story.

---

## MAJOR

### PERF-EMP-02 — `GetPagedEmployees` over-fetches whole entity graphs and runs the projection client-side
- **Type:** over-fetch + client-side projection (admin list, paged read path)
- **Severity:** major
- **Files:**
  - `src/Cleansia.Core.AppServices/Features/Employees/GetPagedEmployees.cs:36-44`
  - `src/Cleansia.Core.AppServices/Mappers/EmployeeMappers.cs:76-91` (`MapToAdminDto` → `IsProfileComplete`)
  - `src/Cleansia.Core.Domain/Users/Employee.cs:269-318`
- **Impact:** The query does `.Include(User).Include(Nationality).Include(Address).Include(Documents)` then `.Select(employee => employee.MapToAdminDto())`. `MapToAdminDto` calls `IsEmployeeProfileComplete(employee)` → `employee.IsProfileComplete()`, an un-translatable C# method, forcing EF to materialize the full `Employee` graph (including the entire `Documents` collection, which `AdminEmployeeListItem` never uses) and run the selector in memory for every row on every page. Contrast `GetEmployeeDocuments` (`EmployeeDocumentMappers.MapToDto`, a flat constructor) which projects cleanly to SQL — proving the codebase can do it right. The sibling `GetAllEmployees.cs:70-81` already hand-writes a SQL-translatable `.Select(new AdminEmployeeListItem(...))` projecting `IsProfileComplete()` is *also* called there (line 81) — same trap, so verify both.
- **Long-term-correct fix:** Project directly to `AdminEmployeeListItem` in the `.Select` using only the needed columns; compute "profile complete" either as a translatable boolean expression inline or as a persisted/computed column. Drop `.Include(Documents)` and `.Include(Address)` from this list query — neither is used by the list DTO. Keep `AsNoTracking()`.
- **Size:** M
- **Layers:** backend (AppServices)
- **Functional GAP?** No.

### PERF-EMP-03 — Document version-history walk is a per-version N+1 loop
- **Type:** N+1 (sequential DB query per version in a `while` loop)
- **Severity:** major
- **Files:**
  - `src/Cleansia.Infra.Database/Repositories/EmployeeDocumentRepository.cs:34-60` (`GetVersionHistoryAsync`)
  - consumed by `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/GetDocumentVersionHistory.cs:45`
- **Impact:** `GetVersionHistoryAsync` loads the head document, then loops `while (currentDoc.PreviousVersionId != null)` issuing **one DB round trip per version** to walk the linked list. A document re-uploaded N times = N sequential queries for one history view. Each fetch also `.Include(PreviousVersion)` (eager-loads a row the loop then re-queries anyway). `GetByIdAsync` here also `.Include(Employee).ThenInclude(User)` which the history mapper doesn't need.
- **Long-term-correct fix:** Version chains share an `EmployeeId`+`FileName` (see `GetLatestByFileNameAsync`, line 71). Fetch the whole chain in a single query — `WHERE EmployeeId = @e AND FileName = @f ORDER BY Version` — and order in SQL, eliminating the loop. Drop the unnecessary `Employee`/`User` include and add `AsNoTracking()` (read path).
- **Size:** M
- **Layers:** backend (Infra.Database)
- **Functional GAP?** No.

### PERF-EMP-04 — `GetMyDocuments` does 3 round trips and over-fetches all versions to discard all but the latest
- **Type:** redundant round trips + over-fetch + in-memory filtering
- **Severity:** major
- **Files:** `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/GetMyDocuments.cs:44-84`
- **Impact:** Handler loads the `User` by email (`GetByEmailAsync`), then loads the `Employee` via `GetByUserEmailAsync` (which `.Include(User).Include(Address).Include(Nationality).Include(Documents)` — three joins, none used except to get `employee.Id`), then queries documents a **third** time via `GetByEmployeeIdAsync`. It then pulls **every** document version and does "latest version per filename" grouping in C# (`GroupBy(FileName).Select(g.OrderByDescending(Version).First())`), discarding the rest. On a hot partner-profile screen this is 3 DB calls where 1 projected query suffices, and it materializes superseded versions that are immediately thrown away. The `userRepository.GetByEmailAsync` result is used only for a null check that `GetByUserEmailAsync` already covers.
- **Long-term-correct fix:** One query: resolve the employee id from session, then a single projected query selecting only the latest version per filename (window function / `GroupBy` translated to SQL, or `GetLatestByFileNameAsync`-style per-name max-version) directly into `MyDocumentDto`. Drop the redundant user lookup and the unused includes. `AsNoTracking()`.
- **Size:** M
- **Layers:** backend (AppServices, Infra.Database)
- **Functional GAP?** No.

### PERF-EMP-05 — Dashboard analytics handlers pull full Order graphs to compute aggregates that belong in SQL
- **Type:** over-fetch + in-memory aggregation (4 analytics endpoints)
- **Severity:** major
- **Files:**
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetProductivityMetrics.cs:39-46,108-115` (calls `GetCompletedOrdersByDateRangeAsync` twice, one **all-time**)
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetOrderAnalytics.cs:31-61`
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetTimeAnalytics.cs:29-58`
  - `src/Cleansia.Infra.Database/Repositories/OrderRepository.cs:17-57,177-192`
- **Impact:** Each analytics handler loads the cleaner's full Order graph for a range and computes counts / averages / group-bys in C#. `GetProductivityMetrics.CalculatePersonalBestsAsync` loads orders from `AllTimeStartDate` — an **unbounded** fetch of the cleaner's entire history (full graph, no `AsNoTracking()`) every time the productivity panel loads, just to find max-per-day and best efficiency. `GetAverageRatingForEmployeeAsync` (OrderRepository:184-191) materializes **all** ratings into a `List` to call `.Average()` in C# instead of `AverageAsync`/`CountAsync`. These all scale linearly with order history and are paid on every dashboard analytics refresh.
- **Long-term-correct fix:** Replace entity-graph fetches with DB-side aggregate/group-by queries projecting only scalar columns (`CompletedAt`, `EstimatedTime`, `ActualCompletionTime`, service id/name). Use `AverageAsync`/`CountAsync` for the rating. Cap "all-time" with a server-side `GROUP BY` rather than streaming the full history into memory. Add `AsNoTracking()` to all analytics reads.
- **Size:** L
- **Layers:** backend (AppServices, Infra.Database)
- **Functional GAP?** No.

### PERF-EMP-06 — Hot per-session reads use tracking queries and over-broad includes
- **Type:** missing `AsNoTracking()` + over-fetch on read paths
- **Severity:** major
- **Files:**
  - `src/Cleansia.Infra.Database/Repositories/EmployeeRepository.cs:9-42` (`GetByUserEmailAsync`, `GetAllActiveWithUserAsync`, `GetByIdAsync` — none `AsNoTracking()`)
  - callers: `GetCurrentEmployeeDetail.cs:34`, `CheckCurrentEmployee.cs:53`, `GetMyDocuments.cs:54`
- **Impact:** `GetByUserEmailAsync` (`.Include(User).Include(Address).Include(Nationality).Include(Documents)`) backs `GetCurrentEmployeeDetail` and `CheckCurrentEmployee` — both read-only and hit on partner app/session start and on every registration-status poll. Tracking forces the change tracker to snapshot the whole graph for reads that never mutate. `CheckCurrentEmployee` only needs `Documents.Any(IsActive)` + profile completeness, yet pulls the full graph; it also runs `ExistsWithUserEmailAsync` in the validator and then `GetByUserEmailAsync` in the handler (two queries for the same employee). `GetAllActiveWithUserAsync` (used by the new-jobs digest sweep, every 30 min) also lacks `AsNoTracking()`.
- **Long-term-correct fix:** Add `AsNoTracking()` to all three read methods. Give `CheckCurrentEmployee` a slim projection (profile-completeness fields + a `Documents.Any(d => d.IsActive)` boolean) instead of the full entity. Avoid the validator+handler double-fetch by having the handler tolerate the existence check.
- **Size:** M
- **Layers:** backend (AppServices, Infra.Database)
- **Functional GAP?** No.

---

## MINOR

### PERF-EMP-07 — Employee search filter uses leading-wildcard `LOWER(...) LIKE '%term%'`, unindexable
- **Type:** unindexed scan on a paged admin list
- **Severity:** minor (major as the table grows)
- **Files:** `src/Cleansia.Core.Domain/Specifications/EmployeeSpecification.cs:33-42`; index picture in `EmployeeEntityConfiguration.cs` (no trigram/search index)
- **Impact:** `SearchTerm` builds `x.User.FirstName.ToLower().Contains(searchLower) || ... LastName ... Email ... PhoneNumber.Contains(...)` → four `LOWER(col) LIKE '%term%'` predicates. Leading wildcards can't use a B-tree index, so every admin employee search is a full scan + per-row `LOWER()`. Fine at small scale, linear cost as headcount grows.
- **Long-term-correct fix:** Coordinate with DB Master: add a `pg_trgm` GIN index on the searched `User` columns (and use case-insensitive `ILIKE` / `citext` to drop the `LOWER()` call), or a denormalized search column. Flag `manual_step: ef-migration` for the index.
- **Size:** S (index) / M (query rewrite)
- **Layers:** backend (Domain spec), DB (index — coordinate with DB Master)
- **Functional GAP?** No.

### PERF-EMP-08 — `Employee` collection/property accessors allocate a copy on every read
- **Type:** allocation in hot mappers
- **Severity:** minor
- **Files:** `src/Cleansia.Core.Domain/Users/Employee.cs:96,99,102,105,108`
- **Impact:** `Availability => _availability.ToDictionary().AsReadOnly()`, `Documents => _documents.ToList().AsReadOnly()`, etc. allocate a fresh copy on **every** property access. Mappers like `MapToAdminDetailDto` / `ToRegistrationCompletionStatus` read `Documents`/`Availability` multiple times per call, each triggering a full copy. Multiplied across list mapping this is avoidable GC pressure.
- **Long-term-correct fix:** Expose the backing collection wrapped once (e.g. `_documents.AsReadOnly()` over a `List<>`, or return `IReadOnlyCollection` without re-copying) so reads don't re-materialize. Cache the read result in the mapper local where read repeatedly.
- **Size:** S
- **Layers:** backend (Domain)
- **Functional GAP?** No.

### PERF-EMP-09 — Partner web dashboard builds stat cards (and trend math) in the template on every change-detection cycle
- **Type:** logic-in-template / recompute per CD (frontend)
- **Severity:** minor
- **Files:**
  - `src/Cleansia.App/libs/cleansia-partner-features/dashboard/src/lib/dashboard/dashboard.facade.ts:115-178` (`getStatCards`, `calculateTrend`)
  - `dashboard.component.html:40` (`@for (card of facade.getStatCards(); ...)`), `:55,57` (`getTrendClass`/`getTrendIcon` called per card)
- **Impact:** `getStatCards()` is invoked directly in the template `@for`, so the whole 4-card array (plus `calculateTrend` and `toLocaleString`) is rebuilt on every change-detection pass, not just when stats change. `getTrendClass`/`getTrendIcon` are likewise method calls per card per CD. Violates conventions.md "no logic in templates; memoize/derive". Component is correctly `OnPush`, which limits the blast radius, but signals updates still re-run it needlessly.
- **Long-term-correct fix:** Convert `getStatCards` to a `computed()` signal derived from the `stats` signal; precompute trend icon/class into the card model so the template only reads properties.
- **Size:** S
- **Layers:** frontend (partner)
- **Functional GAP?** No.

### PERF-EMP-10 — Mobile dashboard refresh runs three independent endpoints sequentially
- **Type:** serial network calls that could be concurrent (mobile)
- **Severity:** minor
- **Files:** `src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/data/dashboard/DashboardRepository.kt:100-137`
- **Impact:** `refresh` awaits `getStats`, then `getUpcomingOrders`, then `getAvailableJobsPreview` one after another inside the mutex. They're independent; serializing them roughly triples dashboard refresh latency vs. firing them concurrently. (The screen and ViewModel are otherwise well-built: singleton cache, staleness window, `remember(key)` derivations, LazyColumn — no recomposition issues found.)
- **Long-term-correct fix:** Launch the three calls with `async { }` and `awaitAll()` (still inside the `refreshLock`), updating the snapshot as each completes; keep stats as the error-bearing critical call.
- **Size:** S
- **Layers:** mobile (partner Android)
- **Functional GAP?** No.

---

## Notes / verified-clean
- `EmployeeDocument` has good indexes (`EmployeeId`, `DocumentType`, `Status`, composite `(EmployeeId, DocumentType)`) — `EmployeeDocumentEntityConfiguration.cs:56-59`. `Employee.ContractStatus` and `WorkCountryId` are indexed for the digest sweep — `EmployeeEntityConfiguration.cs:71,78`.
- `GetEmployeeDocuments` paged query (`GetEmployeeDocuments.cs`) projects via a flat translatable `MapToDto` — no over-fetch there.
- Admin employee-management web (`employee-management.component.ts`) and employee-detail are `OnPush` with proper `@for` `track` keys; debounced search (500ms). Detail facade caches countries/options. No frontend N+1.
- Mobile `DashboardScreen.kt` / `DashboardViewModel.kt` are clean (no recomposition storms; derivations keyed with `remember`).

**No functional GAPs found in this dimension** — every item is a perf/cost refactor, not missing/half-built behavior. The two largest wins are PERF-EMP-01 (dashboard fan-out + overlapping full-graph fetches) and PERF-EMP-05 (analytics in-memory aggregation incl. the unbounded all-time fetch); both should be one refactor story spanning `GetDashboardStats` + the four analytics handlers + `OrderRepository` read methods.


# ===== loyalty-growth CODE QUALITY =====

The commit triggers on type name ending with "Command". The inner records are named `Command`, so commit fires. The grant/revoke handlers use `EnsureForUserAsync`/`GetByUserIdAsync` which return tracked entities (via `GetDbSet()`), so `account.GrantPoints()` mutations get saved. Good — manual grant/revoke do persist. But note `PreviewTierThresholdImpact` is a `Command` (commits unnecessarily) despite being read-only — minor inefficiency / CQRS semantic violation.

I now have a comprehensive picture across all five domains and three stacks. Let me compile the findings report.

## Loyalty-Growth Audit — Code Quality / Spaghetti / Consistency

Scope read in full: backend `Features/{Loyalty,Referrals,PromoCodes,Memberships,Marketing}` + their services (`LoyaltyService`, `ReferralService`, `PromoCodeService`), domain (`LoyaltyAccount`, `LoyaltyTierConfig`, `PromoCode`, enums), repositories, admin controllers, `OrderFactory` integration; frontend admin facades (promo-codes, referrals, user-detail, grant-points-dialog) + customer `RewardsFacade`/`MembershipFacade`; mobile `MembershipViewModel`/`RewardsActivityViewModel`/`LoyaltyRepository`; and i18n coverage. Mechanical `check-consistency.mjs` run (16 hits) cross-referenced against `backlog/audits/consistency-violations.md` to separate new from tracked.

Rules cited from `agents/knowledge/consistency.md` (A/B/C/D/E) and `conventions.md`.

---

### CRITICAL

**LG-01 — Admin tier-threshold config is dead: domain hardcodes thresholds the whole admin tooling pretends to edit**
- Severity: critical · Type: correctness / dead-config / spaghetti · Size: M · Layers: backend (domain + admin feature)
- `src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs:98-113` — `RecomputeTier()` hardcodes `>= 5000 / >= 2000 / >= 500` literals (also magic numbers, conventions.md "no magic numbers"). Meanwhile `LoyaltyTierConfig.LifetimePointsThreshold` (`LoyaltyTierConfig.cs:14`) is a per-tenant, admin-editable column driven by `UpdateTierConfig.cs` and `PreviewTierThresholdImpact.cs` + `AdminLoyaltyTierController`.
- Impact: An admin who edits a tier threshold (or the `PreviewTierThresholdImpact` "how many users move tier?" preview at `PreviewTierThresholdImpact.cs:50`) changes **nothing** about actual tier assignment — `RecomputeTier` never reads the config. The preview computes against proposed config thresholds that the engine will ignore, so it lies to the admin. Tier discounts (`LoyaltyService.ResolveTierDiscountForOrderAsync`, which *does* read config) are then applied based on a `CurrentTier` that was computed from a *different*, hardcoded ladder — config discount and config threshold are silently inconsistent.
- Fix: `RecomputeTier` must load the tenant's `LoyaltyTierConfig` rows and resolve tier by `LifetimePointsThreshold` (the same `ResolveTier(points, thresholds)` switch already written in `PreviewTierThresholdImpact.Handler`). Inject the config (or pass thresholds into the domain method) so the editable config is the single source of truth. Delete the literals.
- Functional GAP: yes (half-built — admin edit/preview UI exists but is non-functional). Needs a user story.

---

### MAJOR

**LG-02 — `RevokePointsManually` records the wrong ledger source (`ManualGrant`), corrupting the audit ledger and breaking idempotency semantics**
- Severity: major · Type: correctness / bug · Size: S · Layers: backend
- `src/Cleansia.Core.AppServices/Features/Loyalty/Admin/RevokePointsManually.cs:57` passes `source: LoyaltyEarnSource.ManualGrant` to `RevokePointsManuallyAsync`. There is no `ManualRevoke` source value (`LoyaltyEarnSource.cs` has only `OrderCompleted/OrderCancelled/Referral/ManualGrant`). The revoke transaction is therefore stamped `Source=ManualGrant`, `Type=Revoke` — indistinguishable from a manual grant in the ledger by source.
- Impact: Admin loyalty activity (`GetUserLoyaltyActivity`) shows revokes labeled as `ManualGrant`; any future analytics/filtering by source is wrong; the `(orderId, source)` idempotency key in `LoyaltyService` would collide a manual grant with a manual revoke if an orderId were ever supplied.
- Fix: add `LoyaltyEarnSource.ManualRevoke = 5` (append-only enum, `[SwaggerEnumAsInt]`) and pass it from `RevokePointsManually`. Flag `manual_step: nswag-regen` (enum changes the generated client).
- Functional GAP: no (bug in shipped code).

**LG-03 — Backend loyalty/promo/membership error codes have no frontend translation in any of the 5 admin locales**
- Severity: major · Type: i18n gap / spaghetti · Size: M · Layers: frontend (i18n) + cross-cutting
- Backend emits `loyalty.tier_config_not_found`, `loyalty.points_exceed_sanity_cap`, `loyalty.reason_required`, `loyalty.tier_perks_json_invalid`, `promo.validity_range_invalid`, `promo.amount_must_be_positive`, `promo.invalid_format`, `membership.swap_same_plan` (verified values in `BusinessErrorMessage`). Confirmed absent from `errors.*` in all of `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`, and no app registers `SNACKBAR_ERROR_MAPPINGS` (only `DEFAULT_SNACKBAR_ERROR_MAPPINGS` in `snackbar.service.ts` exists, covering order/service-area codes).
- Impact: violates conventions.md "Every backend error key has a matching frontend `errors.*` key in all 5 locales". Every loyalty/promo/membership validation failure collapses to the generic `api.common.error_occurred` toast — the admin can't tell *why* a promo create/tier edit failed (bad perks JSON vs bad validity range vs sanity cap all look identical).
- Fix: add the missing keys to all 5 admin locales and register the normalized-code → key mappings via `SNACKBAR_ERROR_MAPPINGS` in the admin `app.config.ts` (or extend `DEFAULT_SNACKBAR_ERROR_MAPPINGS`). A wording decision goes to owner via `questions/open.md`.
- Functional GAP: partial (the error surfacing path is half-built for this domain).

**LG-04 — New paged-query consistency violations (A1/A5) beyond the ones already tracked**
- Severity: major · Type: spaghetti / consistency · Size: M · Layers: backend
- Mechanical check (Gate 8) flagged 16; cross-referencing `consistency-violations.md` F1 (which only names `GetPagedPromoCodes` + `GetPagedReferrals`), these are **not yet tracked**:
  - `Features/Loyalty/GetLoyaltyActivity.cs:12,36` — A1 (`record Query` w/ inline `Offset`/`Limit`, not `Request : DataRangeRequest`) + A5 (hand-built `new PagedData<T>`, manual `pageNumber` math).
  - `Features/Loyalty/Admin/GetUserLoyaltyActivity.cs:17,39` — A1 + A5.
  - `Features/PromoCodes/Admin/GetPromoCodeRedemptions.cs:15,26` — A1 + A5.
  - Also re-confirms tracked: `GetMyReferrals.cs`, `GetPagedReferrals.cs`, `GetPagedPromoCodes.cs` (F1).
- Impact: five+ different hand-rolled paging shapes in one domain; each recomputes `pageNumber`, bypasses `DataRangeRequest`/`Specification`/`GetPagedSort`/`MapToDto`. This is exactly the "same operation five ways" spaghetti the catalog targets.
- Fix: convert each to `class Request : DataRangeRequest, IRequest<PagedData<T>>` + `internal Handler` returning `PagedData<T>` via `items.MapToDto(total, request)`; use a `Specification`/`GetPagedSort<XxxSort>` where a real filter exists, else keep the repo method but return through `MapToDto`. Extend tracked F1 to include these three new files.
- Functional GAP: no.

**LG-05 — Membership commands violate B5 (Error field name) across all four handlers; only one is tracked**
- Severity: major · Type: consistency / spaghetti · Size: S · Layers: backend
- B5 requires `new Error(nameof(command.<Field>), ...)`. Found `nameof(Command)` / `nameof(UserMembership)` (type names, not fields) at:
  - `CreateMembershipSubscription.cs:47` (`nameof(Command)`), `:61` (`nameof(UserMembership)`) — tracked F7 names only this file but only line 46.
  - `CreateMembershipCheckoutSession.cs:44,57` — **not tracked**.
  - `CancelMembershipSubscription.cs:34` (`nameof(Command)`) — **not tracked**.
  - `SwapMembershipPlan.cs:41` (`nameof(Command)`) — **not tracked**.
- Also B5 in `CreatePromoCode.cs:113`: `new Error(BusinessErrorMessage.PromoCodeAlreadyExists, BusinessErrorMessage.PromoCodeAlreadyExists)` — first arg is the message constant, not `nameof(command.Code)`.
- Impact: error responses carry a meaningless field key (the type name), so the frontend can't field-map the error; inconsistent with every other command.
- Fix: replace with the offending field's `nameof`. Extend tracked F7 to all four membership files + `CreatePromoCode`.
- Functional GAP: no.

**LG-06 — Membership commands call Stripe with no provider try/catch (B8 / S7)**
- Severity: major · Type: bug-risk / consistency · Size: M · Layers: backend
- B8: side-effecting commands wrap each external call in a narrow provider try/catch. `CreateMembershipSubscription.cs` (Stripe `CreateCustomerAsync` :67, `CreateSubscriptionAsync` :85, `CreateSetupIntentAsync`/`CreateEphemeralKeyAsync` :107-108), `CreateMembershipCheckoutSession.cs:64,80`, `SwapMembershipPlan.cs:61`, `CancelMembershipSubscription.cs:38` all call Stripe bare. Tracked F8 names only `CreateMembershipSubscription`.
- Impact: a transient Stripe failure bubbles as an unhandled 500 instead of a `PaymentGatewayUnavailable` business failure (the pattern `CreateOrder.cs:362` already follows). Worse in `CreateMembershipSubscription` phase-2: `CreateSubscriptionAsync` succeeds at Stripe, then if the subsequent commit fails the user is billed with no local `UserMembership` row — no idempotency guard exists to reconcile on retry.
- Fix: narrow `catch (StripeException)` → `BusinessResult.Failure(...PaymentGatewayUnavailable)` around each Stripe call; add an idempotency guard keyed on the user/plan so a retried phase-2 doesn't double-subscribe. Extend tracked F8.
- Functional GAP: partial (error-path hardening missing).

**LG-07 — `RewardsFacade` violates the C1/C2/C3 cleanup paradigm (leak risk)**
- Severity: major · Type: spaghetti / leak-risk · Size: M · Layers: frontend
- `libs/cleansia-customer-features/rewards/src/lib/rewards/rewards.facade.ts`: does NOT extend `UnsubscribeControlDirective`, is `@Injectable({ providedIn: 'root' })` (not provided on the component), raw `.subscribe()` with no `takeUntil(this.destroyed$)` at lines 59, 89, 107 (C1), a loading flag-bag (`loading`/`loadingMore`/`activityLoading`/`referralAccountLoading` + string `error`) instead of the canonical signals (C2), and no `catchError → finalize` pipe (C3). Tracked as F10 (named "rewards") — confirm and keep on the canonicalization ticket.
- Impact: subscriptions outlive the component; inconsistent with every conforming admin facade in the same domain (`PromoCodesListFacade`, `ReferralsListFacade`, `UserLoyaltyDetailFacade` are all clean).
- Functional GAP: no.

**LG-08 — `RewardsActivityViewModel` is a flag-bag (E1) — not covered by tracked F13/F14**
- Severity: major · Type: bug-risk / consistency · Size: M · Layers: mobile
- `customer-app/.../features/rewards/RewardsActivityViewModel.kt:28-41` exposes five loose StateFlows (`_items`, `_loading`, `_loadingMore`, `_loaded`, `_total`) instead of a sealed `*UiState` (Loading/Error/Loaded) per E1. F13 lists only partner-app VMs; F14 lists customer `MembershipViewModel`/`ProfileViewModel`/`CreateDisputeViewModel` — this rewards-activity VM is unlisted.
- Impact: permits impossible states (e.g. `loaded=true` + `loading=true` + empty items + nonzero total) and there is no `Error` state at all — failures are silently swallowed (comment line 67-71 confirms "Silent on failure"), so a page-load error shows a permanently empty list with no retry affordance.
- Fix: sealed `RewardsActivityUiState` with an `Error` arm; add to the F14 canonicalization ticket.
- Functional GAP: partial (no error state).

---

### MINOR

**LG-09 — `PreviewTierThresholdImpact` is a read modeled as a `Command` (CQRS violation + needless commit)**
- Severity: minor · Type: spaghetti / consistency · Size: S · Layers: backend
- `PreviewTierThresholdImpact.cs:21` declares `record Command(...) : ICommand<Response>` but performs no mutation (pure projection). Because its inner record is named `Command`, `UnitOfWorkPipelineBehavior` (`:27`) fires an unnecessary `CommitAsync` on every preview. Should be an `IQuery`. Exposed via `AdminLoyaltyTierController.cs:46` as POST.
- Fix: convert to `record Query : IQuery<Response>` + `IQueryHandler`. (`ValidateReferral.cs` is the correct in-domain precedent — a read-only check modeled as `IQuery`.)
- Functional GAP: no.

**LG-10 — Referral code alphabet doc/bias comment is wrong (29 chars, not 28; bias is `256 % 29 = 24`, not 4)**
- Severity: minor · Type: correctness / misleading-comment · Size: S · Layers: backend
- `ReferralService.cs:36-37` alphabet `"BCDFGHJKLMNPQRSTVWXYZ23456789"` is **29** chars; comment (lines 31-35) claims "28 chars … 28^6 ≈ 481M", and line 266-268 claims "256 % 28 = 4 … immaterial". Verified actual `256 % 29 = 24` — a meaningfully larger modulo bias toward the first 24 symbols. Conventions.md "comments explain WHY, never WHAT" + comment must be true.
- Fix: correct the count/bias math, or (better) use rejection sampling to remove bias since the comment's own premise (4-value bias) is what justified skipping it.
- Functional GAP: no.

**LG-11 — Stale/incorrect WHAT comment in `OrderFactory` about the tier discount floor**
- Severity: minor · Type: misleading-comment · Size: S · Layers: backend
- `OrderFactory.cs:48-49`: "Tier discount respects the per-tier floor (today 1000 CZK uniformly, enforced in LoyaltyService)." The floor is actually per-config `config.MinimumOrderAmountForDiscount` (admin-editable per tier, `LoyaltyService.cs:171`) — not "1000 CZK uniformly." The comment will mislead the next dev (and is already inconsistent with LG-01's config story).
- Fix: correct to reference the per-tier configured minimum.
- Functional GAP: no.

**LG-12 — Duplicated `ParsePerks` JSON parser across three loyalty queries**
- Severity: minor · Type: duplication · Size: S · Layers: backend
- Near-identical `ParsePerks(string? perksJson)` + private `PerkRow` record in `GetMyLoyalty.cs:64`, `GetUserLoyaltyAccount.cs:81`, and `GetLoyaltyTiers.cs:48` (the last already delegates to `GetMyLoyalty.Handler.ParsePerks` via a cross-feature `internal static` call — itself a smell: a feature reaching into another feature's handler). Same 3+ lines in 3+ places meaning the same thing (conventions.md duplication bar met).
- Fix: extract a single `TierPerk` parser (e.g. a small `LoyaltyPerks` helper or a domain method on `LoyaltyTierConfig`) and have all three call it; remove the cross-feature static reach-in.
- Functional GAP: no.

**LG-13 — `SendSitewidePromo` command has no `Response` (B1) and uses 10 positional locale fields**
- Severity: minor · Type: consistency · Size: S · Layers: backend
- `Marketing/SendSitewidePromo.cs:40` is `record Command(...) : ICommand` (bare, no `<Response>`), and the controller (`AdminMarketingController.cs:30`) papers over it with `HandleResult<object>`. B1 requires every command return `ICommand<Response>` with a real record (cf. tracked F4). Also the 10 flat `Title*/Body*` fields per locale are a magic-shaped DTO; a `Dictionary<string,string>`/`LocalizedText` value object would be more maintainable and not bypass the all-5-locale validation the way flat fields invite.
- Fix: give it a `Response` (e.g. enqueued message id/count); consider a localized-text VO.
- Functional GAP: no.

**LG-14 — Promo-list `deactivate` swallows errors without surfacing (C4) and skips `finalize`**
- Severity: minor · Type: consistency · Size: S · Layers: frontend
- `promo-codes-list.facade.ts:82-101`: `deactivate` uses `catchError(() => of(null))` with no `SnackbarService.showError/showApiError` on failure (C4) and no `finalize`. The same facade's `loadPromoCodes` does it correctly — inconsistent within one file.
- Fix: add `showApiError` in the error path.
- Functional GAP: no.

**LG-15 — `MembershipFacade` (web) uses `.subscribe({next,error})` with inline loading resets instead of the C3 `catchError → finalize` pipe**
- Severity: minor · Type: consistency · Size: S · Layers: frontend
- `profile/.../membership.facade.ts`: `refresh`/`cancel`/`swapPlan`/`createCheckoutSession` each reset their loading signal in both `next` and `error` branches (C3 forbids inline reset; mandates `finalize`). Extends correctly from `UnsubscribeControlDirective` (C1 OK).
- Fix: route through `takeUntil → catchError(...) → finalize(...)`.
- Functional GAP: no.

**LG-16 — Duplicated policy constant `POINTS_MAX = 100000` (frontend) vs `100_000` (backend); grant dialog uses `fb.group` without the D2 nullable justification comment**
- Severity: minor · Type: duplication / consistency · Size: S · Layers: frontend
- `grant-points-dialog.component.ts:28` hardcodes `POINTS_MAX = 100000`, duplicating the backend sanity cap (`GrantPointsManually.cs:35` `LessThanOrEqualTo(100_000)`) with no shared home (conventions.md "no magic numbers… named home"). The form uses `fb.group` with a nullable `points` control; D2 allows a nullable control but requires a comment explaining why — none present.
- Fix: source the cap from a shared constant/config; add the D2 justification comment (or restructure).
- Functional GAP: no.

**LG-17 — `LoyaltyRepository.clear()` (mobile) doesn't reset `_loading`**
- Severity: minor · Type: bug-risk · Size: S · Layers: mobile
- `customer-app/.../core/loyalty/LoyaltyRepository.kt:102-106` `clear()` resets `_account/_tiers/_loaded` but not `_loading`. A sign-out during an in-flight `refresh()` leaves `_loading=true` for the next session, and `refresh`'s `if (_loading.value) return` (line 60) would then no-op the next user's first load.
- Fix: reset `_loading.value = false` in `clear()`.
- Functional GAP: no.

---

### Notes for the PM / pattern-evolution loop
- **Architect:** LG-01 is a real **design** defect (config vs domain source-of-truth) — route to `architect`; the threshold ladder belongs in `LoyaltyTierConfig`, read by the domain. The B5/B8 membership findings (LG-05/06) and the new paged-query files (LG-04) should be folded into the existing `consistency-violations.md` tickets **F1/F7/F8** rather than opened fresh — they are the same rules recurring, which is the signal that F1/F7/F8's scope was under-counted. The mechanical check already enforces A1/A5/B5; consider adding a B8 "Stripe call outside try/catch" heuristic.
- **No security gate trip from this slice:** all admin loyalty endpoints carry `[Permission(...)]`, identity comes from `IUserSessionProvider` not the wire, and grant/revoke take `UserId` in the body but `actorId` from session (correct for an admin acting on a user). No `security` invocation needed for these findings.
- Ranking by impact: **LG-01 > LG-02 > LG-03 > LG-06 > LG-05 > LG-04 > LG-07 > LG-08** > minors (LG-09…LG-17).

Files of interest (all absolute):
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Loyalty\LoyaltyAccount.cs` (LG-01)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Loyalty\Admin\RevokePointsManually.cs` (LG-02)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Memberships\CreateMembershipSubscription.cs`, `CreateMembershipCheckoutSession.cs`, `CancelMembershipSubscription.cs`, `SwapMembershipPlan.cs` (LG-05/06)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Loyalty\GetLoyaltyActivity.cs`, `Loyalty\Admin\GetUserLoyaltyActivity.cs`, `PromoCodes\Admin\GetPromoCodeRedemptions.cs` (LG-04)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\ReferralService.cs` (LG-10), `Features\Orders\OrderFactory.cs` (LG-11)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\rewards\src\lib\rewards\rewards.facade.ts` (LG-07)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\cleansia_android\customer-app\src\main\java\cz\cleansia\customer\features\rewards\RewardsActivityViewModel.kt` (LG-08), `...\core\loyalty\LoyaltyRepository.kt` (LG-17)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\apps\cleansia-admin.app\src\assets\i18n\{en,cs,sk,uk,ru}.json` (LG-03)


# ===== identity-auth PERFORMANCE =====

Confirmed. In `RefreshToken` (every token rotation for an employee — happens constantly), the handler calls `employeeRepository.GetByUserEmailAsync` which eager-loads `User + Address + Nationality + Documents` — the entire employee document graph — just to read `employee.Id`. That's a heavy over-fetch on the hottest auth path. Plus the `userRepository.GetByIdAsync` earlier in the same handler triggers the Orders include.

I now have a complete, well-evidenced picture. Writing the findings report.

---

# Performance & Cost Audit — Identity-Auth Domain

Scope: `Features/{Auth,Users,AdminUsers,Devices,Gdpr,Notifications,DataRetention}`, their repositories, EF configs, and the login/register/profile/gdpr surfaces across web (Angular) + mobile (Kotlin). Audit-only; no code changed. Rules cited: `consistency.md` A6 (read-path `Include→AsNoTracking→Select(MapToDto)`), `patterns-backend.md` ("Use `.AsNoTracking()` on read paths", "include ONLY what the mapper reads"), `database.md` ("Explicit indexes on FKs and frequently queried columns").

Note: `database.md` is stale — it claims `Guid` PKs and `Email citext` "to avoid LOWER() calls"; actual PKs are 26-char ULID strings and there is **no index on Email at all**.

## Ranked findings

### PERF-IDA-01 — `User.Email` (and other lookup columns) have NO database index — CRITICAL
- Type: missing index. Layers: db/backend.
- Evidence: `Migrations/CleansiaDbContextModelSnapshot.cs` User entity has only `HasIndex("PreferredLanguageCode")` and `HasIndex("TenantId")`. `UserEntityConfiguration.cs:28-51` defines `Email`, `PhoneNumber`, `ConfirmationCode`, `ResetPasswordCode`, `GoogleId` as plain columns with no `HasIndex`. Yet `UserRepository.cs` filters on every one of them: `GetByEmailAsync` (L18-23), `GetByPhoneNumberAsync` (L25), `GetByEmailOrPhoneNumberAsync` (L30), `ExistsWithEmailAsync` (L35), `GetByConfirmationCodeAsync` (L45), `ExistsWithConfirmationCodeAsync` (L40).
- Impact: every login, register, password-reset, email-confirm and profile-load does a **sequential scan of the Users table**. Cost grows linearly with user count; under load this is the single most expensive identity query and will dominate DB CPU as the tenant base grows.
- Fix: add `builder.HasIndex(u => u.Email).IsUnique()` (citext makes it case-insensitive), plus non-unique indexes on `PhoneNumber`, `ConfirmationCode`, `ResetPasswordCode`, and `GoogleId` (filtered/partial where nullable). `manual_step: ef-migration`.
- Size: S (config) + M (migration on a populated table — build index CONCURRENTLY).
- Functional GAP: no (perf hardening), but warrants a story because it needs a migration + a fix to PERF-IDA-05 (uniqueness is currently only enforced in app code).

### PERF-IDA-02 — `UserRepository.GetQueryable()` eager-loads `Orders` on every single-user fetch — CRITICAL
- Type: over-fetch / N+1-by-graph. Layers: backend/db.
- Evidence: `UserRepository.cs:10-16` overrides `GetQueryable()` to always `.Include(u => u.Orders).Include(u => u.PreferredLanguage)`. The base `GetByIdAsync` (`BaseRepository.cs:35-39`) and `GetAll()` (`BaseRepository.cs:107-110`) route through it. Consumers that fetch one user and never read orders: `GetUser` (`GetUser.cs:37`), `RefreshToken` (`RefreshToken.cs:68`), `ExportUserData` (`ExportUserData.cs:25`), `UpdateAdminUser` (`UpdateAdminUser.cs:60` via `GetAll()`), `GetAdminUserById` (`GetAdminUserById.cs:37`). None of their DTOs reference orders (`UserMappers.cs`, `AdminUserMappers.cs`).
- Impact: loading one user pulls that user's **entire order history** every time — a customer with hundreds of orders pays hundreds of rows + a join per profile load / token refresh. Violates `patterns-backend.md` ("include ONLY what the mapper reads"). EF strips the include under a `.Select` projection (so the paged lists are spared), but every `GetByIdAsync`/`FirstOrDefaultAsync(GetAll())` materialization is hit.
- Fix: remove the blanket includes from `GetQueryable()`; keep the base queryable lean and add `Include` explicitly only in the few call sites that need a nav (none in this domain need Orders). Keep `PreferredLanguage` only where the DTO actually reads `PreferredLanguage.Name` — better, project the name in the query.
- Size: M (touches the repo + must re-verify every `IUserRepository` consumer repo-wide, not just identity-auth).
- Functional GAP: no.

### PERF-IDA-03 — Login / ChangePassword / GoogleAuth / ConfirmUserEmail re-fetch the same user 2–5× per request — CRITICAL (Login, ChangePassword) / MAJOR (others)
- Type: redundant round trips (validator-then-handler N+1). Layers: backend/db.
- Evidence:
  - `Login.cs`: `UserAuthenticationTypeIsInternal` (L34 WhenAsync + L43) → 2× `GetByEmailAsync`; `HasValidPassword` (L31) → 1×; `ExistsWithEmailAsync` (L40) → 1×; Handler (L80) → 1×. Up to **5 email lookups per login attempt**.
  - `ChangePassword.cs`: `ExistsWithEmailAsync` (L28 + L53 WhenAsync), `ValidateUserTokenAsync` (L58), `CheckIfPasswordDifferentAsync` (L68), Handler (L87) → up to **5 lookups per reset**.
  - `GoogleAuth.cs`: validator `UserAuthenticationTypeIsGoogle` (L55) + Handler (L95) → 2×.
  - `ConfirmUserEmail.cs`: validator `GetByConfirmationCodeAsync` (L37) + Handler (L65) → 2×.
- Impact: each lookup is currently a full table scan (compounds PERF-IDA-01) and each carries the Orders include (PERF-IDA-02). Login and password-reset are rate-limited auth endpoints, so the cost is bounded per-IP but the per-request multiplier is pure waste. 5 scans where 1 would do.
- Fix: fetch the user once per request. Preferred: have the validator load the user, stash it (e.g. `RootContextData`/a scoped per-request cache keyed by email), and let the handler reuse it; or move identity resolution into the handler and keep validators to shape-only checks (aligns with `consistency.md` B4 "ownership + the entity fetch live in the handler"). At minimum, collapse the duplicate validator rules so `GetByEmailAsync` is called once.
- Size: M.
- Functional GAP: no (correctness is fine; it's wasteful).

### PERF-IDA-04 — `RefreshToken` over-fetches the full Employee graph to read one Id — MAJOR
- Type: over-fetch. Layers: backend/db.
- Evidence: `RefreshToken.cs:84` calls `employeeRepository.GetByUserEmailAsync`, which (`EmployeeRepository.cs:11-16`) `.Include(User).Include(Address).Include(Nationality).Include(Documents)` — then uses only `employee?.Id` (L85). Same handler also hits PERF-IDA-02 via `GetByIdAsync` (L68).
- Impact: token refresh is one of the highest-frequency authenticated calls (every access-token expiry, all employee mobile/web sessions). Each one pulls the employee's address, nationality and **all documents** to extract a single GUID-string. Large per-call payload + join cost on a constantly-hit path.
- Fix: add a lightweight `GetEmployeeIdByUserEmailAsync` (or `...ByUserIdAsync`) that does `.Where(...).Select(e => e.Id).FirstOrDefaultAsync(...)` — one column, no includes, AsNoTracking. The user's `Profile`/employee-id could also be carried as a JWT claim and avoided entirely on refresh.
- Size: S.
- Functional GAP: no.

### PERF-IDA-05 — No DB-level uniqueness on `User.Email` / `User.PhoneNumber`; uniqueness enforced only by app-code pre-checks — MAJOR (correctness-adjacent, but here it also costs queries)
- Type: missing unique constraint (+ extra query). Layers: db/backend.
- Evidence: uniqueness is checked via `ExistsWithEmailAsync`/`UserWithPhoneNumberNotExistsAsync` validator round trips (`Register.cs:33`, `UpdateCurrentUser.cs:56`) with no backing unique index (PERF-IDA-01). Race windows aside, every create/update pays an extra scan that a unique index would both speed up and make authoritative.
- Impact: the app-side check is a TOCTOU race (two concurrent registrations can both pass) AND a full scan. A unique index removes the race and makes the existence check index-backed.
- Fix: ship the unique index from PERF-IDA-01 and treat the `ExistsWithEmail` check as a fast-path UX message, relying on the DB constraint for correctness. Priority order security>correctness>perf — this is correctness, so it should land with PERF-IDA-01.
- Size: S (folds into the PERF-IDA-01 migration).
- Functional GAP: partial — the missing constraint is a half-built guarantee; worth a small hardening story.

### PERF-IDA-06 — `GetAllGdprRequests` paginates before ordering (wrong page + no index-backed sort) and returns `List` not `PagedData` — MAJOR
- Type: paging/round-trip + functional half-build. Layers: backend/db.
- Evidence: `GetAllGdprRequests.cs:32-38` — `GetPaged(offset, pageSize)` applies `.Skip().Take()` (`BaseRepository.cs:48-52`) and only **then** `.OrderByDescending(r => r.CreatedOn)`. Ordering after Skip/Take means the wrong rows are selected and then sorted within the page. Returns `List<GdprRequestDto>` with no total count, so the admin UI can't page properly.
- Impact: admin GDPR audit list shows arbitrary rows per page (an Article-30 compliance surface), and without a total the client can't compute page count — this is a latent correctness bug as much as a perf one. No index on `GdprRequest.CreatedOn` either, so the sort is unindexed.
- Fix: use the canonical paged-query shape (`consistency.md` A1–A6): `GetPagedSort<GdprRequestSort>` + `GetCountAsync` + `MapToDto(total, request)`, with ordering applied before paging in the query, and project the DTO in the query (`AsNoTracking().Select(...)`). Add an index on `(CreatedOn)` or `(Status, CreatedOn)`.
- Size: M.
- Functional GAP: YES — the endpoint is half-built (no total, broken ordering). Needs a user story.

### PERF-IDA-07 — `UpdateAdminUser` / `GetAdminUserById` use `GetAll()` (Orders include) on the admin update path — MINOR
- Type: over-fetch. Layers: backend/db.
- Evidence: `UpdateAdminUser.cs:60-64` and `GetAdminUserById.cs:37-42` call `GetAll()` → `GetQueryable()` includes Orders. `GetAdminUserById` correctly adds `.AsNoTracking()`; `UpdateAdminUser` is a write path so tracking is fine, but the Orders include is pure waste (admins rarely have orders).
- Impact: low (admin-only, infrequent, small order counts), but it's the same anti-pattern; resolved automatically once PERF-IDA-02 lands.
- Fix: covered by PERF-IDA-02; until then, fetch via a queryable without the Orders include.
- Size: S. Functional GAP: no.

### PERF-IDA-08 — Single-user read handlers materialize tracked entities instead of projecting — MINOR
- Type: missing AsNoTracking / no projection on read path. Layers: backend.
- Evidence: `GetUser.cs:37`, `GetCurrentUser.cs:34`, `GetUserByEmail.cs:37`, `GetUserConsents` repo read (`UserConsentRepository.cs:10-16`), `GdprRequestRepository.GetByUserIdAsync` (L10-16) all fetch full entities with change-tracking on read-only paths, then map in memory. `consistency.md` A6 and `patterns-backend.md` mandate `AsNoTracking` on reads.
- Impact: extra change-tracker snapshots and memory per request; small individually, real in aggregate on `GetCurrentUser` (hit on most authenticated page loads). The paged user/admin queries already do this correctly (`GetPagedUsers.cs:43`).
- Fix: add `.AsNoTracking()` to the single-entity read repo methods / query handlers; ideally project to the DTO in the query. Note `GetByEmailAsync` is shared with write paths (Login mutates nothing, but ChangePassword/Register reuse it and DO mutate) — so add a separate no-tracking read method rather than flipping the shared one.
- Size: S. Functional GAP: no.

### PERF-IDA-09 — Customer SSR app imports the Partner NSwag client for identity DTOs/enums — MINOR (bundle)
- Type: bundle bloat / wrong dependency. Layers: frontend.
- Evidence: identity-auth customer features import from `@cleansia/partner-services` instead of `@cleansia/customer-services`: `login.facade.ts:9` (`JwtTokenResponse`), `register.facade.ts:10`, `profile.component.ts:30` + `profile.facade.ts:15` (`ChangePasswordCommand`, `UpdateCurrentUserCommand`), `gdpr.component.ts:4` + `gdpr.facade.ts:11` (`ConsentType`, `GrantConsentCommand`, `WithdrawConsentCommand`, `UserConsentDto`, `GdprExportDto`), `forgot-password.facade.ts:8`. (Widespread beyond identity too: orders, order-wizard, disputes.)
- Impact: drags the partner API client surface into the customer SSR bundle. Generated NSwag clients are class-based and not reliably tree-shaken, so this risks shipping (and SSR-parsing) the partner client to customer browsers. Larger bundle + slower TTI/SSR.
- Fix: import these DTOs/enums from `@cleansia/customer-services` (regenerate the customer client so it exposes the GDPR/consent/profile types — `manual_step: nswag-regen`). Add an ESLint `no-restricted-imports` rule banning `@cleansia/partner-services` inside `cleansia-customer-features`.
- Size: M (cross-feature cleanup). Functional GAP: no (it's a structural cleanup; the cross-app types happen to be shape-identical).

### PERF-IDA-10 — Retention sweeps for stale Devices filter on un-indexed `LastActiveAt`; consent/document sweeps materialize then RemoveRange — MINOR
- Type: missing index + delete round-trips. Layers: db/backend.
- Evidence: `DataRetentionBackgroundService.cs:101-104` filters `device.IsActive && device.LastActiveAt < cutoff`; Device indexes are only `TenantId`, `UserId`, `(UserId,DeviceId)` (snapshot) — no `LastActiveAt` index. `CleanWithdrawnConsentsAsync` (L181-189) and `CleanStaleDevicesAsync` (L101-108) batch-load full entities then `RemoveRange` + `CommitAsync`, where the code that doesn't need the entity body could `ExecuteDeleteAsync` in one round trip (the document sweep legitimately needs the entities for blob deletion).
- Impact: low frequency (background job), but the stale-device scan is a full table scan each batch loop as the Devices table grows; the materialize-then-delete pattern doubles round trips vs. a set-based delete.
- Fix: add a `(IsActive, LastActiveAt)` index on Devices; convert the entity-body-not-needed sweeps (stale devices, withdrawn consents) to `ExecuteDeleteAsync`. Keep the document sweep as-is (needs FilePath for blob cleanup).
- Size: S. Functional GAP: no.

## Clean (verified, no action)
- `RefreshTokenEntityConfiguration` is well-indexed (`TokenHash`, `(UserId, RevokedAt)`); `RefreshTokenCleanupService` delegates to a set-based `DeleteStaleAsync`.
- `DeviceConfiguration` `(UserId, DeviceId)` unique index correctly backs `GetByUserAndDeviceIdAsync`.
- `GetPagedUsers` / `GetPagedAdminUsers` follow A6 (`AsNoTracking().Select(MapToDto)`); EF strips the repo's Orders include under projection.
- `GdprExportService` (cold path) already uses `AsNoTracking()` + projected `Select`; acceptable for an export.
- Mobile `AuthViewModel` / `ProfileViewModel`: StateFlow + `asStateFlow()`, IO on `viewModelScope`, in-flight guards (`if (_loading.value) return`), no main-thread blocking, no recomposition storms. No issues.
- Frontend `LoginComponent` / `GdprComponent` / `ProfileComponent`: `OnPush` + signals + `computed`, `@for ... track`, logic in facades. No issues.

## Recommended sequencing
1. PERF-IDA-01 + PERF-IDA-05 (one migration: unique index on Email + indexes on the other lookup columns) — biggest win, unblocks the rest.
2. PERF-IDA-02 (drop blanket Orders include) — high blast radius, re-verify all `IUserRepository` consumers.
3. PERF-IDA-03 + PERF-IDA-04 (collapse per-request duplicate fetches; lean employee-id lookup).
4. PERF-IDA-06 (GDPR list paging — also a functional bug; **needs a user story**).
5. PERF-IDA-07/08/10 (mechanical hardening, mostly subsumed by 1–2).
6. PERF-IDA-09 (frontend import hygiene; needs nswag-regen of the customer client).

Key files: `src/Cleansia.Infra.Database/Repositories/UserRepository.cs`, `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs`, `src/Cleansia.Core.AppServices/Features/Auth/{Login,RefreshToken,GoogleAuth,ConfirmUserEmail}.cs`, `src/Cleansia.Core.AppServices/Features/Users/{ChangePassword,GetUser,GetCurrentUser,GetUserByEmail}.cs`, `src/Cleansia.Core.AppServices/Features/Gdpr/GetAllGdprRequests.cs`, `src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs`, `src/Cleansia.Infra.Database/EntityConfigurations/DeviceConfiguration.cs`, `src/Cleansia.App/libs/cleansia-customer-features/{login,register,profile,gdpr,forgot-password}/**`.


# ===== loyalty-growth PERFORMANCE =====

I have a complete picture. The two activity handlers (`GetLoyaltyActivity`, `GetUserLoyaltyActivity`) are well-built (batch order lookup, no per-row N+1). The key issue is `GetForAccountAsync` lacks `AsNoTracking`. Let me write the findings report.

---

# Performance & Cost Audit — Loyalty-Growth Domain

Scope: `Features/Loyalty`, `Referrals`, `PromoCodes`, `Memberships`, `Marketing`, `Codes` + their repositories, EF configs, admin/customer web UIs, and customer mobile screens. Audit only; no code changed. Rules cited from `agents/knowledge/consistency.md` (A4/A6), `patterns-backend.md`, `patterns-frontend.md`, `patterns-mobile.md`.

Overall the domain is in good shape: indexes are deliberate, admin paged repos use `AsNoTracking`, the activity handlers batch order-number lookups (no per-row N+1), mobile uses `LazyColumn`+stable keys+`derivedStateOf`, and all web components are `OnPush` with `track`. The real cost is concentrated in the loyalty account read path, which loads the entire transaction ledger on every booking quote and order completion.

Ranked by impact.

---

## LG-PERF-01 — Loyalty account read loads the entire transaction ledger on the booking/quote hot path
- **Severity:** major
- **Type:** over-fetch / missing projection on hot path
- **File:line:** `src/Cleansia.Infra.Database/Repositories/LoyaltyAccountRepository.cs:10-15` (`GetByUserIdAsync`) and `:17-31` (`EnsureForUserAsync`) — both unconditionally `.Include(a => a.Transactions)`.
- **Hot callers:** `LoyaltyService.ResolveTierDiscountForOrderAsync` (`src/Cleansia.Core.AppServices/Services/LoyaltyService.cs:157`) is invoked from `QuoteOrder`, `CreateOrder`, `OrderFactory` — i.e. **every quote and every order placement**. Also `GrantForCompletedOrderAsync:62`, `RevokeForCancelledOrderAsync:133`, `GrantPointsManuallyAsync:209`, `RevokePointsManuallyAsync:237`.
- **Impact:** A high-lifetime customer accumulates one `LoyaltyTransaction` per completed order forever (append-only ledger). `ResolveTierDiscountForOrderAsync` only reads `account.CurrentTier` (a scalar), yet every quote pulls and materializes the user's full ledger over the wire. This grows unboundedly with customer tenure and runs on the most-trafficked endpoint in the app. The grant/revoke paths only ever `_transactions.Add(...)` (verified in `LoyaltyAccount.GrantPoints` / `RevokePoints`, `Core.Domain/Loyalty/LoyaltyAccount.cs:54,78`) — they never read existing transactions, so loading them is pure waste there too.
- **Long-term-correct fix:**
  1. Add an `AsNoTracking()` overload (or a dedicated `GetTierForUserAsync`) that projects just `CurrentTier`/`LifetimePoints`/`CompletedBookingsCount` for the read-only `ResolveTierDiscount` and `GetMyLoyalty`/`GetUserLoyaltyAccount` paths (those handlers read only scalars + tier-config, never `account.Transactions`). Per consistency A6, read paths must `AsNoTracking().Select(...)`.
  2. For the grant/revoke mutation paths, fetch the account **without** the `Include` (tracked, but ledger-free). EF tracks the newly-added child on `Add` regardless; the existing rows don't need loading.
  3. Keep an Include-bearing variant only if some caller genuinely enumerates `Transactions` (none in this domain do).
- **Size:** M
- **Layers:** backend (infra repo + service)
- **Functional GAP?** No — perf regression in working code.

---

## LG-PERF-02 — `GetMyReferral` fetches the entire referral set to compute two counts
- **Severity:** major
- **Type:** over-fetch + count-then-fetch-all, should be a SQL aggregate
- **File:line:** `src/Cleansia.Core.AppServices/Features/Referrals/GetMyReferral.cs:31-36` — calls `CountByReferrerAsync`, then `GetByReferrerAsync(userId, 0, totalCount, ct)` using the count as the page limit, then `referrals.Count(...)` twice in memory.
- **Impact:** Two round trips, and the second one materializes **every** referral row for the inviter **with `.Include(r => r.Referred)` joined** (see `ReferralRepository.GetByReferrerAsync:21-36`) — just to compute `Qualified` and `Accepted` counts that the DB could return as a single grouped query. The joined `Referred` user data is fetched and immediately discarded. For a power-referrer this is an unbounded fetch + a User join on a screen-load endpoint (`/rewards`).
- **Long-term-correct fix:** Replace with a single grouped count query, e.g. a repo method `GetStatusCountsByReferrerAsync(userId)` returning `Dictionary<ReferralStatus,int>` via `GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() })` over the indexed `ReferrerUserId`. No `Include`, no row materialization. The `Status` and `ReferrerUserId` indexes already exist (`ReferralEntityConfiguration:76,79`).
- **Size:** S
- **Layers:** backend (handler + repo)
- **Functional GAP?** No.

---

## LG-PERF-03 — Loyalty activity ledger read path is tracked (missing `AsNoTracking`)
- **Severity:** major
- **Type:** missing `AsNoTracking` on a paged read path
- **File:line:** `src/Cleansia.Infra.Database/Repositories/LoyaltyTransactionRepository.cs:10-19` (`GetForAccountAsync`). `BaseRepository.GetQueryable()` (`BaseRepository.cs:148-151`) returns the tracking `DbSet` by default, and this method never opts out.
- **Impact:** `GetLoyaltyActivity` and `GetUserLoyaltyActivity` are pure read queries (they map to a DTO, never mutate). Tracking up to `Limit` ledger rows per call wastes identity-map memory and change-detection cost on a feed the user can scroll repeatedly. Violates consistency A6 ("`AsNoTracking` on read paths"). Contrast: the sibling `PromoCodeRedemptionRepository.GetPagedByPromoCodeAsync:34-35` and `ReferralRepository.GetPagedAdminAsync:66` correctly use `AsNoTracking` — this one is the inconsistent outlier.
- **Long-term-correct fix:** Add `.AsNoTracking()` to `GetForAccountAsync`. Optimally also project to a slim row (the handler only needs `Type, Points, Source, OrderId, OccurredOn`) per A6, avoiding materializing the full `LoyaltyTransaction` (incl. `Description`, audit fields).
- **Size:** S
- **Layers:** backend (infra repo)
- **Functional GAP?** No.

---

## LG-PERF-04 — Membership read path is tracked (`GetActiveForUserAsync` / `GetMyMembership`)
- **Severity:** minor
- **Type:** missing `AsNoTracking` on read path
- **File:line:** `src/Cleansia.Infra.Database/Repositories/UserMembershipRepository.cs:10-22` (`GetActiveForUserAsync`) — no `AsNoTracking`, with `.Include(m => m.MembershipPlan)`.
- **Impact:** `GetMyMembership` (`Features/Memberships/GetMyMembership.cs:35`) is a read-only screen query but the entity + plan are tracked. Also note this same method is on the **pricing pipeline** ("active membership for user lookups on every CreateOrder" per `UserMembershipEntityConfiguration:59-60`), so the tracking cost lands on the booking hot path too. Lower-impact than LG-PERF-01 because a user has at most one active membership (bounded, single row).
- **Long-term-correct fix:** Add `.AsNoTracking()` to `GetActiveForUserAsync` (the webhook-mutation path uses the separate `GetByStripeSubscriptionIdAsync`, so this read-only variant is safe to make no-tracking). The `(UserId, Status)` index already covers the filter.
- **Size:** S
- **Layers:** backend (infra repo)
- **Functional GAP?** No.

---

## LG-PERF-05 — Referral "first qualifying order" check uses a correlated `Any()` subquery over Orders/status-history with no supporting index
- **Severity:** minor
- **Type:** unindexed correlated subquery on a mutation path
- **File:line:** `src/Cleansia.Core.AppServices/Services/ReferralService.cs:192-195` — `orderRepository.GetQueryable().Where(o => o.UserId == userId && o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Completed)).CountAsync(...)`.
- **Impact:** Runs on every order completion for a referred user. EF emits a correlated `EXISTS` against the order-status-history table. Whether this is cheap depends on indexes on `Orders.UserId` and the status-history FK + `Status` — none of which is in this domain's configs to confirm. On a large orders table this is a per-completion scan. Coordinate with DB Master to confirm an index exists on `OrderStatusTrack(OrderId, Status)` (or equivalent) and on `Orders.UserId`.
- **Long-term-correct fix:** Confirm/add the supporting indexes; if the "first completed order" semantic is hot, consider a denormalized `CompletedOrdersCount` on the user or a cheaper `EXISTS`-with-`Take(2)` short-circuit instead of a full `COUNT`.
- **Size:** S (verify) / M (if denormalization needed)
- **Layers:** backend (service) + DB
- **Functional GAP?** No.

---

## LG-PERF-06 — Membership lifecycle sweep filters on `Status`/`CurrentPeriodEnd` but the index leads with `UserId`
- **Severity:** minor
- **Type:** index not aligned with cron query predicate
- **File:line:** query at `src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs:75-80` and `:112-118`; index at `UserMembershipEntityConfiguration.cs:60-61` is `(UserId, Status)`.
- **Impact:** The daily sweep selects by `Status = Active AND RenewalReminderSentAt == null AND CurrentPeriodEnd IN [range]` with **no `UserId`** — so the `(UserId, Status)` composite can't be used as a range seek; Postgres falls back to a scan filtered by `Status`. Tolerable at current membership volume, but the index is mis-shaped for this access pattern as subscriptions grow. (The sweep correctly stays tracked since it mutates the reminder stamps — that part is right.)
- **Long-term-correct fix:** Add an index suited to the sweep, e.g. `(Status, CurrentPeriodEnd)` (optionally a partial index `WHERE RenewalReminderSentAt IS NULL`). Coordinate with DB Master.
- **Size:** S
- **Layers:** backend (DB)
- **Functional GAP?** No.

---

## LG-PERF-07 — Admin paged loyalty/promo/referral lists materialize full entities then map in memory (A6 deviation)
- **Severity:** minor
- **Type:** over-fetch (no in-query projection)
- **File:line:** `ReferralRepository.GetPagedAdminAsync` (`:58-94`, `.Include(Referrer).Include(Referred)` → full `Referral` rows), `PromoCodeRepository.GetPagedAdminAsync` (`:24-65`, full `PromoCode` + `Currency`), `LoyaltyTransactionRepository.GetForAccountAsync` (`:10-19`), then mapped in the handlers (`GetPagedReferrals.cs:38`, `GetPagedPromoCodes.cs:36`, `GetUserLoyaltyActivity.cs:66`).
- **Impact:** Consistency A6 requires `.AsNoTracking().Select(x => x.MapToDto())` projecting in the query; these fetch whole entities (all columns + joined `User`/`Currency`/`Referrer`+`Referred` rows) and map after materialization, pulling columns the list DTOs don't use. They do use `AsNoTracking` (good), so cost is bounded by page size — admin-only, low-traffic. Genuinely low impact, but it's a consistent A6 miss across the domain's admin repos and inflates payload per row.
- **Long-term-correct fix:** Project to the list DTO inside the query (`.Select(...)`), pulling only the referenced scalar/nav fields (e.g. `Referrer.Email` rather than the whole `User`). Matches the canonical paged-query recipe.
- **Size:** M (several methods)
- **Layers:** backend (infra repos + handlers)
- **Functional GAP?** No.

---

## LG-PERF-08 — Web rewards-activity row calls `txLabel()`/`formatDate()` from inside `@for`
- **Severity:** minor
- **Type:** per-change-detection recomputation in template
- **File:line:** `src/Cleansia.App/libs/cleansia-customer-features/rewards/src/lib/rewards/rewards-activity.component.html:40,45` invoke `txLabel(item)` and `formatDate(item.occurredOn)` per row; methods at `rewards-activity.component.ts:55,70`.
- **Impact:** `formatDate` builds a locale map and a `new Date()` on every call; `txLabel` runs a branch chain. Under `OnPush` these only re-run on in-component CD and the list is page-bounded (≤50 rows), so impact is small — but `patterns-frontend.md` calls out "no logic in templates; memoize/derive instead of recompute per change detection." Tracking is also `track $index` (`:31`) rather than `track item.id`, a weaker key for a feed that can re-page.
- **Long-term-correct fix:** Precompute a view-model array in the facade (map each `GetLoyaltyActivityActivityItem` to `{ signed, labelKey, params, formattedDate }` once when the page loads) and iterate that; switch to `track item.id`.
- **Size:** S
- **Layers:** frontend (customer web)
- **Functional GAP?** No.

---

## LG-DATA-01 — `LoyaltyTransaction.OrderId` length mismatch: `[MaxLength(50)]` on entity vs `HasMaxLength(26)` in EF config
- **Severity:** minor
- **Type:** schema/consistency mismatch (not strictly perf, surfaced during the read)
- **File:line:** `src/Cleansia.Core.Domain/Loyalty/LoyaltyTransaction.cs:31` declares `[MaxLength(50)]`; `LoyaltyTransactionEntityConfiguration.cs:30` sets `HasMaxLength(26)`.
- **Impact:** The EF config wins for the column (26, the ULID length, which is correct), so the `[MaxLength(50)]` annotation is dead/misleading. No runtime cost, but it's an inconsistency a reader could trust and propagate. Flagging because order IDs are ULIDs (26) everywhere else in the domain.
- **Long-term-correct fix:** Change the annotation to `[MaxLength(26)]` to match.
- **Size:** S
- **Layers:** backend (domain)
- **Functional GAP?** No.

---

## Notable things checked and found clean (no action)
- `GetLoyaltyActivity` / `GetUserLoyaltyActivity` order-number resolution batches into **one** `AsNoTracking().Where(Contains).Select` dictionary query — no per-row N+1 (the obvious trap is avoided). `LoyaltyTransactions` has the `(LoyaltyAccountId, OccurredOn DESC)` index the feed needs.
- `PreviewTierThresholdImpact` streams only `LifetimePoints` via `AsNoTracking().Select(a => a.LifetimePoints)` — correct projection, documented rationale.
- Index coverage is deliberate and matches access patterns: `PromoCode (TenantId, Code)` unique, `PromoCodeRedemption (PromoCodeId, UserId)` + unique `OrderId`, `Referral` unique `ReferredUserId` + `(Status, AcceptedOn)`, `UserMembership` unique `StripeSubscriptionId`.
- `SendSitewidePromo` correctly enqueues a single fan-out message (no synchronous per-user loop in the request).
- Mobile `RewardsActivityScreen` / `RewardsTab`: `LazyColumn` with stable `key`, `derivedStateOf` scroll trigger gated on `hasMore && !loadingMore`, `remember(perk.labelKey)` around the reflective `getIdentifier` lookup, bounded `forEachIndexed` previews — no recomposition storm. Matches `patterns-mobile.md`.
- All 14 web loyalty-growth components are `ChangeDetectionStrategy.OnPush`; every list uses `@for ... track`.

## Cross-cutting recommendation
The single highest-leverage fix is **LG-PERF-01**: split the `LoyaltyAccount` repository into a ledger-free read variant (scalars-only, `AsNoTracking`) and a ledger-free mutation variant, removing the blanket `.Include(a => a.Transactions)`. That one change removes an unbounded-growth over-fetch from the booking quote/create path (the busiest endpoint), the order-completion path, and the two account-detail screens at once. LG-PERF-02 and LG-PERF-03 are the cheap follow-ons on the same read surface.

No findings rise to **critical**, and none are functional GAPs requiring a new user story — these are perf/cost regressions in otherwise-working, reachable features.


# ===== loyalty-growth FUNCTIONAL GAPS =====

Confirmed: no admin MembershipPlan management exists (no controller, no feature, no UI). Plans are SQL-seed-only with placeholder Stripe ids. I now have a comprehensive picture. Let me compile the findings report.

Final note on `PromoCode` entity: it has a `[Required] public bool IsActive` declared on the entity, but `Auditable`/`BaseEntity` already provides `IsActive` (per `ReferralCode` comment: "IsActive is inherited from BaseEntity"). That's a shadowing inconsistency but a consistency issue, not a functional gap. I'll focus the report on functional gaps and half-built features per the dimension.

---

# Audit: loyalty-growth domain — Functional Gaps & Half-Built Features

Scope: `Features/Loyalty`, `Features/Referrals`, `Features/PromoCodes`, `Features/Memberships`, `Features/Marketing`, the loyalty/referral/promo services, admin loyalty UIs, and customer membership/rewards (web + mobile). Audit only; no code changed. Verdict up front: the backend domain + CQRS + service layer for this domain is unusually complete and well-wired (loyalty grant/revoke, promo apply, referral accept/qualify, membership Stripe lifecycle, the LOY-003 best-of-three discount in `OrderFactory`). The gaps are concentrated at the **edges**: an unscheduled sweep, a missing public landing route that breaks the referral funnel, dropped audit data, and two admin capabilities that exist as data but have no management surface.

Severity = user/business impact. "GAP" = missing/half-built, needs a user story.

---

## CRITICAL

### LG-01 — Referral expiry sweep is implemented but never invoked (referrals never expire) — GAP
- **Type:** orphaned service method / missing scheduler. **Size:** S. **Layers:** backend + functions.
- **Evidence:** `ReferralService.ExpireStaleReferralsAsync` (`src/Cleansia.Core.AppServices/Services/ReferralService.cs:236`) and its interface declaration (`Services/Interfaces/IReferralService.cs:74`) have **zero callers** anywhere in the solution (grep returns only the definition + interface). Contrast: `SendMembershipLifecycleNotificationsFunction.cs:24` (timer) and `SendSitewidePromoFanoutFunction` exist; there is no `ExpireStaleReferrals` Function.
- **Impact:** The 90-day qualifying window is only enforced *lazily* inside `ProcessOrderCompletedAsync` (`ReferralService.cs:180`), which fires solely when the invitee completes an order. A referred user who signs up and never books leaves the `Referral` row stuck in `Accepted` **forever**. The admin "Expired" filter (`AdminReferralController` get-paged, `ReferralStatus.Expired`) is effectively dead — no row reaches that state through the intended automatic path. Referral program reporting (conversion/expiry rates) is permanently wrong.
- **Fix:** Add a timer-triggered Azure Function (mirroring `SendMembershipLifecycleNotificationsFunction`) that resolves `IReferralService` and calls `ExpireStaleReferralsAsync` on a daily cadence, committing via the unit of work. Confirm `GetExpirableAsync` filters `Status == Accepted && AcceptedOn < cutoff`.

### LG-02 — Web referral share links (`/r/{code}`) have no landing route — acquisition funnel dead-ends in 404 — GAP
- **Type:** UI dead-end / documented-but-missing flow. **Size:** M. **Layers:** frontend (customer web).
- **Evidence:** `rewards.component.ts:207-209` builds the shareable URL as `${window.location.origin}/r/${code}` and copy/share handlers push that link out. But `apps/cleansia.app/src/app/app.routes.ts` has **no `r/:code` route** — anything unmatched hits `path: '**' → NOT_FOUND` (lines 141-144). The signup form *can* take a code (`register.facade.ts` `referralCode` control + `validateReferralCodeNow`), but it is **not pre-filled from the URL** and there is no route to capture it.
- **Impact:** Every referral link a web customer shares lands the invitee on the 404 page. The invitee must independently discover signup, find the "Add a referral code" dialog, and re-type the code by hand. The core viral loop (the whole point of the referral feature) is broken on web. Mobile has `ReferralCodeBottomSheet`/deep-link handling; web does not.
- **Fix:** Add a public `r/:code` route that (a) stores the code, (b) redirects to `register` (or a landing page) with the code pre-applied and validated, and (c) carries it through signup so `Register` accepts it. Keep the share URL and the route in lock-step.

---

## MAJOR

### LG-03 — Admin manual grant/revoke "Reason" is collected but silently dropped (no audit trail) — GAP
- **Type:** half-built audit feature / data loss. **Size:** S. **Layers:** backend.
- **Evidence:** `GrantPointsManually.Command` requires a `Reason` (validated `NotEmpty` + `MaxLength(500)`, `GrantPointsManually.cs:38-43`) and `RevokePointsManually` mirrors it. The handler calls `loyaltyService.GrantPointsManuallyAsync(...)` **without passing the reason** (`GrantPointsManually.cs:54-60`). `LoyaltyService.GrantPointsManuallyAsync` has no `description` parameter (`LoyaltyService.cs:181-211`) and calls `account.GrantPoints(...)`, which creates the ledger row via `LoyaltyTransaction.Create(...)` with `description: null` (`LoyaltyAccount.cs:61`). The `LoyaltyTransaction.Description` column (`LoyaltyTransaction.cs:33`) exists precisely for this and is always null for manual grants.
- **Impact:** Admins are forced to type a justification for every manual point adjustment, but it is never persisted. The audit story ("why did this user get 5,000 points?") cannot be answered from the ledger — a compliance/trust gap on a money-adjacent feature. The customer rewards activity (`tx_manual`) and admin user-loyalty activity show the grant with no context.
- **Fix:** Thread `reason`/`description` through `GrantPointsManuallyAsync`/`RevokePointsManuallyAsync` → `GrantPoints`/`RevokePoints` → `LoyaltyTransaction.Create(description:)`. Surface it in `GetUserLoyaltyActivity` / `GetLoyaltyActivity` DTOs.

### LG-04 — No admin surface to manage Membership Plans (entity exists, CRUD does not) — GAP
- **Type:** entity+migration+seed exists, no command/UI. **Size:** L. **Layers:** backend + admin frontend.
- **Evidence:** `MembershipPlan` is a full entity with `Create`/`UpdatePricing`/`UpdateBenefits`/`Deactivate` domain methods (`Memberships/MembershipPlan.cs`), seeded via `insert_seed_data.sql:2445-2470` with **placeholder Stripe price ids** (`price_1TSiJ8…`) and an explicit comment "replace with the actual Price ids before deploying." There is **no `AdminMembershipController`**, no `Features/Memberships/Admin/*` commands, and no admin nav/route (`app.component.ts:100-131` has Loyalty + Marketing groups, no Membership; grep for `AdminMembership`/`CreateMembershipPlan` = no files). The customer `GetMembershipPlans` query is the only read path.
- **Impact:** Plans, prices, discount %, trial length, free-cancellation window, and the critical `StripePriceId` can only be changed by hand-editing the database. An admin cannot launch a price change, run a promo trial, deactivate a plan, or fix the placeholder Stripe ids without engineering + raw SQL. The "single SQL insert" claim in the entity doc is the *only* provisioning path — there's no operational tooling.
- **Fix:** Add `Features/Memberships/Admin` (`GetPagedMembershipPlans`, `GetMembershipPlanById`, `CreateMembershipPlan`, `UpdateMembershipPlan`, `DeactivateMembershipPlan`), an `AdminMembershipController`, and an admin UI + sidebar entry. Note the Stripe Product/Price registration is an owner step — surface `StripePriceId` as an explicit input.

### LG-05 — `AdminReferral/by-user/{userId}` endpoint has no consumer — GAP (orphaned endpoint)
- **Type:** endpoint with no consumer. **Size:** S. **Layers:** backend (live), admin frontend (missing).
- **Evidence:** `AdminReferralController.GetReferralsByUser` (`by-user/{userId}`) → `GetReferralsByUser.Query`. The generated admin client exposes `.byUser(userId)` (`admin-client.ts:9445`), but grep for `.byUser(` across `libs/cleansia-admin-features` returns **no matches** — only the generated client references it. The referrals admin UI (`loyalty-referrals/referrals-list.facade.ts`) calls only `getPaged`. No drill-in from the admin user detail or loyalty-user-detail into a user's referral history.
- **Impact:** Admins investigating a specific user (fraud, dispute, "did my friend's referral count?") cannot see that user's referral relationships in-app despite the backend being ready. The capability is built and shipped server-side but unreachable.
- **Fix:** Either wire a "Referrals" panel into the admin user / loyalty-user-detail screen consuming `byUser`, or, if intentionally deferred, document it as not-yet-surfaced. Prefer wiring it — the loyalty-user-detail screen already exists as the natural home.

### LG-06 — No admin intervention on a referral (read-only list; cannot reverse fraud or manually qualify) — GAP
- **Type:** missing flow / lifecycle action not reachable by admin. **Size:** M. **Layers:** backend + admin frontend.
- **Evidence:** `Referral` exposes `MarkQualified`/`MarkExpired` domain methods, but the only callers are `ReferralService` (system actor) on order completion / sweep. `AdminReferralController` is **read-only** (get-paged + by-user). The admin referrals UI (`referrals-list.component`) has filters and a table but no row actions. There is no command to reverse a fraudulent `Qualified` referral (and claw back the symmetric +150 grants) or to manually qualify a legitimate one stuck in `Accepted`.
- **Impact:** Referral fraud (self-referral rings via throwaway accounts that complete one cheap order) cannot be remediated in-product; the granted points stay. Edge cases (invitee's qualifying order was refunded/disputed) have no admin remedy. Combined with LG-01, the referral subsystem has *no* operational controls.
- **Fix:** Add admin commands (e.g. `ReverseReferral` that revokes both sides' points via `LoyaltyService.RevokePointsManually` and sets a terminal status; optionally `ForceQualifyReferral`) plus row actions in the admin UI, gated by a new permission.

---

## MINOR

### LG-07 — `CreateMembershipSubscription` Subscribe endpoint unused by web; web/mobile use different subscribe paths — partial GAP
- **Type:** endpoint with no web consumer (intentional split, undocumented). **Size:** S. **Layers:** backend + frontend.
- **Evidence:** `MembershipController.Subscribe` → `CreateMembershipSubscription` (SetupIntent + EphemeralKey flow, `CreateMembershipSubscription.cs`) is the native-SDK path. The web `MembershipFacade` uses only `createCheckoutSession`, `getMine`, `getPlans`, `cancel`, `swapPlan` — never `subscribe` (`membership.facade.ts:24-153`). Mobile (`customer-app/.../membership/MembershipViewModel.kt`) is the intended consumer. This is a legitimate web-Checkout vs mobile-PaymentSheet split, but nothing documents it, so `Subscribe` looks orphaned from the web side.
- **Impact:** Low — both paths function. Risk is maintenance confusion and the consistency debts already logged for this handler (`consistency.md` B5 `nameof(Command)` and B8 "Stripe with no try/catch"). Flag so it isn't mistaken for dead code and removed.
- **Fix:** Document the two-path design (web→Checkout, mobile→SetupIntent) in the feature; address the tracked B5/B8 consistency violations separately.

### LG-08 — Loyalty discount min-order floor is config-driven but the comment claims a hardcoded "1000 CZK uniform" — stale/misleading half-spec
- **Type:** inconsistency between layers (doc vs behavior). **Size:** S. **Layers:** backend.
- **Evidence:** `OrderFactory.cs:49-50` comment: "Tier discount respects the per-tier floor (today 1000 CZK uniformly, enforced in LoyaltyService)." Actual behavior reads `LoyaltyTierConfig.MinimumOrderAmountForDiscount` per tier (`LoyaltyService.ResolveTierDiscountForOrderAsync`, `LoyaltyService.cs:171-175`), which is admin-editable per tier (`UpdateTierConfig`) and seeded per row. There is no uniform 1000 CZK constant.
- **Impact:** Low functional risk, but the comment will mislead anyone reasoning about pricing; an admin who edits one tier's floor will see behavior diverge from the documented "uniform" claim. Symptom of the floor having moved from constant → config without updating call-site docs.
- **Fix:** Correct the comment to reference the per-tier `MinimumOrderAmountForDiscount` config; verify the seed values (`insert_seed_data.sql:2367-2377` UPDATE block) match the intended floors.

### LG-09 — `loyalty/users` admin route exists and is reachable but absent from the sidebar — minor discoverability gap
- **Type:** UI reachable only by deep-link from another screen. **Size:** S. **Layers:** admin frontend.
- **Evidence:** Route `loyalty/users` → `loyalty-user-detail` lib (`app.routes.ts:163-167`) is navigated to only from `admin-user-management.component.ts:205` (`/loyalty/users/{id}`). The Loyalty sidebar group (`app.component.ts:104-118`) lists Promo Codes, Tiers, Referrals — **not** a user-loyalty entry. This is defensible (it's a per-user drill-in, not a list), so it's minor, but there is no standalone "search a user's loyalty" entry.
- **Impact:** Low — admins reach it via the user list. Noted for completeness; not a true dead-end since a caller exists.
- **Fix:** Optional — leave as drill-in, or add a "Loyalty: look up user" entry if product wants direct access.

---

## What is NOT broken (verified, to bound the audit)
- Loyalty grant/revoke on order Complete/Cancel is fully wired and idempotent (`CompleteOrder.cs:252-254`, `LoyaltyService` Grant/Revoke).
- The LOY-003 best-of-three discount (tier + membership additive, 12% cap, promo replaces) is consistently applied across `QuoteOrder`, `OrderFactory`, and persisted on the order; `OrderPricingCalculator` correctly returns only the raw subtotal by design.
- Promo validate/preview/apply, per-user + global caps, currency match, idempotency on `(orderId)` are complete (`PromoCodeService`) and consumed by the wizard + admin CRUD.
- Membership Stripe lifecycle (subscribe, swap, cancel-at-period-end, webhook sync, lifecycle reminders on a timer) is complete and consumed on web (Checkout) and mobile.
- Tier-config admin (`AdminLoyaltyTierController` + `loyalty-tier-configs` UI + `PreviewTierThresholdImpact`) and sitewide promo (`AdminMarketingController` + fanout function) are wired end-to-end.

---

## Ranked ticket-title suggestions (PM can convert directly)
1. **[CRITICAL] Schedule the referral-expiry sweep so referrals actually expire** (LG-01)
2. **[CRITICAL] Add `/r/:code` referral landing route to deep-link invitees into signup** (LG-02)
3. **[MAJOR] Persist the admin grant/revoke reason on the loyalty ledger** (LG-03)
4. **[MAJOR] Build admin Membership-Plan management (CRUD + UI), replacing SQL-only provisioning** (LG-04)
5. **[MAJOR] Surface per-user referral history in admin (consume `by-user` endpoint)** (LG-05)
6. **[MAJOR] Add admin referral intervention (reverse fraudulent / force-qualify) with point clawback** (LG-06)
7. **[MINOR] Document web-Checkout vs mobile-SetupIntent subscribe split; resolve `Subscribe` B5/B8 debts** (LG-07)
8. **[MINOR] Fix stale "1000 CZK uniform floor" comment in OrderFactory; verify per-tier seed floors** (LG-08)
9. **[MINOR] Decide on a sidebar entry for user-loyalty lookup** (LG-09)

Key files: `src/Cleansia.Core.AppServices/Services/ReferralService.cs:236` (LG-01); `src/Cleansia.App/apps/cleansia.app/src/app/app.routes.ts` + `libs/cleansia-customer-features/rewards/src/lib/rewards/rewards.component.ts:207` (LG-02); `src/Cleansia.Core.AppServices/Features/Loyalty/Admin/GrantPointsManually.cs:54` + `Services/LoyaltyService.cs:181` + `Core.Domain/Loyalty/LoyaltyTransaction.cs:33` (LG-03); `sql-scripts/insert_seed_data.sql:2445` + absence of `Cleansia.Web.Admin/Controllers/AdminMembershipController.cs` (LG-04); `src/Cleansia.Web.Admin/Controllers/AdminReferralController.cs:44` + `libs/cleansia-admin-features/loyalty-referrals/...referrals-list.facade.ts` (LG-05/06); `src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs:49` (LG-08).


# ===== catalog-config SECURITY S =====

Confirmed. The Admin host's default policy is only `RequireAuthenticatedUser()` — no role requirement. So `AdminCodeController` with bare `[Authorize]` (and no `[Permission]`) lets **any authenticated principal that the Admin host's JWT scheme accepts** read the Code overview, with no specific permission. The Code overview is platform enum metadata (low sensitivity), so this is the lowest-severity S2 finding, but it's a real deviation from "every endpoint has `[Permission]`".

I now have a complete, evidence-backed picture. Here is the findings report.

---

# Security Audit — Catalog-Config Domain (S1–S10)

Scope swept: Admin CRUD controllers (Service, Package, Currency, Country, Language, Company, Code, FeatureFlag, EmailTemplate) + Customer read controllers (Service, Package, Country, Language, FeatureFlag) + their MediatR commands/queries/DTOs/EF configs. Partner (`Cleansia.Web`) and Mobile (`Cleansia.Web.Mobile`) hosts contain no controllers for this domain (scaffolding only).

Overall: authorization coverage is good (almost every admin endpoint carries a granular `[Permission]`). The serious problems are **two destructive data-integrity gaps in catalog deletes**, an **un-throttled open email primitive**, and a **tenant-isolation index defect**. Ranked by impact.

---

## CRITICAL

### CAT-SEC-01 — `DeleteService` / `DeletePackage` hard-delete with no in-use check, cascading away order history
- **Type:** Data loss / referential integrity (relates S7-adjacent destructive-side-effect + financial-history integrity)
- **Files:** `src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs:28-39`, `src/Cleansia.Core.AppServices/Features/Packages/DeletePackage.cs:28-40`; FK behavior in `src/Cleansia.Infra.Database/Migrations/20260519203658_Initial.cs:1734-1738` (`FK_OrderServices_Services_ServiceId` `onDelete: Cascade`) and `:1166-1170` (`FK_PackageServices_Services_ServiceId` `onDelete: Cascade`).
- **Impact:** `DeleteService.Handler` calls `serviceRepository.Remove(service)` with **no reference check**. The `OrderServices.ServiceId` FK is `ON DELETE CASCADE`, so deleting a service silently **deletes the corresponding line item from every historical order** that used it — including Completed, invoiced, and receipted orders — corrupting financial/booking history with no warning. Simultaneously `EmployeePayConfig → Service` is `Restrict` (`EmployeePayConfigEntityConfiguration.cs:61`), so if a pay config references the service the delete throws a raw DB exception → unhandled 500. Either outcome is bad. `DeletePackage` is identical via the `PackageServices` cascade. This is the catalog-config analog of the known "doubled financial side-effect" class: an admin click destroys financial records.
- **Contrast proving the bug:** `DeleteCurrency` (`DeleteCurrency.cs:32-34,55-59`), `DeleteCountry` (`DeleteCountry.cs:26-28,45-49`), and `DeleteLanguage` (`DeleteLanguage.cs:26-28,45-49`) **all** call `IsInUseAsync` before removing. `IServiceRepository`/`IPackageRepository` have **no `IsInUseAsync` method at all** (verified by grep).
- **Fix (long-term-correct):** Soft-delete the catalog entity (set `IsActive = false`; the customer overview already filters `IsActive` — `GetServiceOverview.cs:21`, `GetPackageOverview.cs:24`) instead of hard `Remove`; and/or add `IsInUseAsync` to both repos checking `OrderServices`/`PackageServices`/`EmployeePayConfig` and block hard-delete with a `ServiceInUse`/`PackageInUse` business error like the other catalog entities do. Never let order-history rows cascade-delete from a catalog edit.
- **Size:** M · **Layers:** AppServices (handler+validator), Domain.Repositories (interface), Infra.Database (repo impl, possibly FK behavior), error messages + 5 i18n keys.
- **Functional GAP?** Yes — needs a user story: "Catalog delete must be safe (soft-delete or in-use guard) and consistent with Currency/Country/Language."

---

## MAJOR

### CAT-SEC-02 — `SendTestEmail` / `SendTestEmailByType` are an un-rate-limited, non-idempotent open email primitive (S5, S7)
- **Type:** S5 (no rate limit on a side-effecting/email mutation) + S7 (non-idempotent) + abuse/cost vector
- **Files:** `src/Cleansia.Web.Admin/Controllers/AdminEmailTemplateController.cs:42-59` (`POST types/{emailType}/send-test`) and `:110-128` (`POST {emailTemplateId}/send-test`); handlers `src/Cleansia.Core.AppServices/Features/EmailTemplates/SendTestEmail.cs:39-116`, `SendTestEmailByType.cs:40-108`. Rate-limiter policies defined at `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:69-93`; Admin host applies `[EnableRateLimiting("auth")]` **only** on `AdminAuthController` (grep) — there is **no global limiter**.
- **Impact:** Both endpoints send a **real outbound email via SendGrid** to a fully caller-supplied `RecipientEmail` (validated only for email format), gated solely by `CanUpdateEmailTemplate`. There is **no `[EnableRateLimiting]`** on either endpoint and no global limiter, and **no idempotency key** — each call sends another email. A holder of `CanUpdateEmailTemplate` (or anyone who obtains such a token / a compromised admin session) can drive unlimited mail to arbitrary third-party addresses: outbound spam, SendGrid reputation/cost damage, and a phishing relay (the messages are genuine ConfirmationEmail/ResetPassword templates from your domain, lending them legitimacy). Per S5 this mutation "sends email" and must get a narrow per-user limit; per S7 it is a doublable side effect.
- **Fix:** Add a narrow per-user/per-tenant rate-limit policy (e.g. a `"send-test"` fixed window, a few/min) and apply it to both endpoints; consider restricting the recipient to the calling admin's own address or a verified allow-list rather than any email.
- **Size:** S (limiter) / M (recipient hardening) · **Layers:** Web.Admin (attributes), Config (new policy).
- **Functional GAP?** Partial — limiter is a fix; recipient-restriction may warrant a small story.

### CAT-SEC-03 — `CompanyInfo` (ITenantEntity) unique index is `RegistrationNumber`, not `(TenantId, RegistrationNumber)` (S8)
- **Type:** S8 tenant-isolation correctness
- **File:** `src/Cleansia.Infra.Database/EntityConfigurations/CompanyInfoEntityConfiguration.cs:38` — `builder.HasIndex(c => c.RegistrationNumber).IsUnique()`.
- **Impact:** `CompanyInfo` is `ITenantEntity` (`src/Cleansia.Core.Domain/Company/CompanyInfo.cs:7`). S8 requires unique indexes on tenant-scoped tables to be `(TenantId, X)` because the value is unique *per tenant*. With a global unique index on `RegistrationNumber`, two tenants cannot both register a company with the same legal registration number — a cross-tenant insert collision that (a) breaks legitimate multi-tenant onboarding and (b) leaks existence: a failed insert tells tenant B that some other tenant already uses that registration number. The correct pattern is already used right next door: `ServiceCategoryEntityConfiguration.cs:19` uses `HasIndex(c => new { c.TenantId, c.Slug }).IsUnique()`.
- **Fix:** Change to `HasIndex(c => new { c.TenantId, c.RegistrationNumber }).IsUnique()`. Requires a migration (owner `manual_step: ef-migration`, S9).
- **Size:** S · **Layers:** Infra.Database (config + migration). · **Functional GAP?** No (correctness fix), but ships with a migration manual step.

### CAT-SEC-04 — Anonymous catalog endpoints break/leak once any tenant has non-null `TenantId` rows (S3/S8)
- **Type:** S3 (anonymous routes must not return tenant-scoped data) / S8 (filter behavior under no-tenant context)
- **Files:** `src/Cleansia.Web.Customer/Controllers/ServiceController.cs:14-21` (`[AllowAnonymous] GetOverview`), `PackageController.cs:14-21`; entities `Service`/`Package`/`ServiceCategory` are `ITenantEntity`; filter logic `src/Cleansia.Infra.Database/CleansiaDbContext.cs:111-180`; tenant resolution `TenantProvider.cs:12-20`.
- **Impact:** On an `[AllowAnonymous]` request there is no `tenant_id` claim, so `GetCurrentTenantId()` returns `null` and the global filter collapses to `e.TenantId == null` (per the `singleTenantMatch` branch, `CleansiaDbContext.cs:154-156`). Today (single-tenant, all catalog rows have null TenantId) this is fine. But the design is fragile by construction: the moment a real tenant's services/packages are created with a non-null `TenantId`, (a) that tenant's **own** customers calling the anonymous catalog see **nothing** (their tenant rows are filtered out — a functional outage of the booking wizard), while (b) the shared null-tenant "default" catalog is served to everyone regardless of tenant. The anonymous catalog has no way to resolve which tenant the visitor belongs to (no subdomain/host→tenant mapping is applied on these routes). S3 explicitly warns anonymous routes bypass the tenant filter and "must not return tenant-scoped data unless gated by a different shared secret."
- **Fix:** Resolve tenant from host/subdomain (or an explicit tenant param) for anonymous catalog reads and set a tenant override before querying, or formally document these as platform-global (null-tenant only) catalog and forbid per-tenant catalog rows. Decide before the first real second tenant onboards.
- **Size:** M · **Layers:** Web.Customer (tenant-resolution middleware), Infra.Database (override wiring). · **Functional GAP?** Yes — multi-tenant catalog story; latent, becomes live on tenant #2.

---

## MINOR

### CAT-SEC-05 — `AdminCodeController` uses bare `[Authorize]` instead of `[Permission]` (S2)
- **Type:** S2 authorization granularity
- **File:** `src/Cleansia.Web.Admin/Controllers/AdminCodeController.cs:12` (`[Authorize]` at class level, no `[Permission]` on `GetOverview`).
- **Impact:** The Admin host default policy is only `RequireAuthenticatedUser()` (`src/Cleansia.Web.Admin/Extensions/ServiceExtensions.cs:189-193`, no role). So **any** principal the Admin JWT scheme authenticates can read the Code overview, with no specific permission — the exact "missing policy lets any authenticated user hit it" hole S2 describes. Severity is minor because the payload is platform enum/code metadata (`GetCodeOverview.cs` reflects enums from the domain assembly — `DomainAssemblyReference.Assembly.MapToCodeFromAssembly()`), not user data. Still a deviation from the S2 standard every other admin endpoint follows.
- **Fix:** Add `[Permission(Policy.CanView...)]` (a low-privilege view policy) consistent with the other admin controllers.
- **Size:** S · **Layers:** Web.Admin. · **Functional GAP?** No.

### CAT-SEC-06 — `CompanyInfoDetailDto` exposes bank account / IBAN / SWIFT — verify no non-admin path reaches it (S4)
- **Type:** S4 (note / defense-in-depth, not a confirmed live leak)
- **File:** `src/Cleansia.Core.AppServices/Features/Company/DTOs/CompanyInfoDetailDto.cs:18-20` (`BankAccountNumber`, `Iban`, `Swift`).
- **Impact:** This DTO carries the company's banking detail. It is currently only returned by `AdminCompanyController` endpoints, all gated by `CanViewCompanyInfo` (verified: no customer-host CompanyInfo controller exists). That is acceptable *today*, but the DTO is a single record reused for list/detail/legacy-get; if a customer or partner "company footer/contact" feature later reuses `CompanyInfoDetailDto`, it will leak banking data. Flag: split a public `CompanyContactDto` (name/address/phone/email/website only) from the admin `CompanyInfoDetailDto` before any customer-facing company endpoint is added.
- **Size:** S · **Layers:** AppServices (DTO split). · **Functional GAP?** No — preventive; becomes a real S4 leak if a public company endpoint is added without splitting.

---

## Items checked and PASSING (no finding)
- **S1:** No catalog command trusts a `userId`/`tenantId`/`email` from the body; these are platform-config operations with no per-user ownership. Route-vs-body id mismatch is guarded in every Update controller (e.g. `AdminServiceController.cs:87`).
- **S2:** All Service/Package/Currency/Country/Language/Company/FeatureFlag/EmailTemplate admin endpoints carry granular `[Permission]` (only `AdminCodeController` deviates — CAT-SEC-05).
- **S3:** Catalog entities are platform/tenant config, not per-user resources; no per-user ownership applies. The relevant isolation concern is S8/CAT-SEC-04.
- **S6:** No PII logged in any catalog handler or email path (grep clean — no `LogInformation`/recipient logging).
- **S10:** Customer overviews correctly filter `IsActive` (`GetServiceOverview.cs:21`, `GetPackageOverview.cs:24`); admin overviews intentionally do not (admins see all) — correct.
- **FeatureFlag toggle/delete:** `FeatureFlag` is **not** `ITenantEntity` (global platform config, scoped via `Scope`/`ScopeValue`); `ToggleFeatureFlag`/`DeleteFeatureFlag` by-id with no ownership check is acceptable for platform-admin global config, gated by `CanToggleFeatureFlag`/`CanDeleteFeatureFlag`.

---

## Ranked summary
1. **CAT-SEC-01** (critical) — catalog delete cascades away order/financial history; no in-use check. Needs story.
2. **CAT-SEC-02** (major) — un-throttled, non-idempotent open email primitive (spam/cost/phishing relay).
3. **CAT-SEC-03** (major) — `CompanyInfo` unique index not tenant-scoped (S8); migration.
4. **CAT-SEC-04** (major, latent) — anonymous catalog breaks/leaks on first real second tenant. Needs story.
5. **CAT-SEC-05** (minor) — `AdminCodeController` bare `[Authorize]` vs `[Permission]`.
6. **CAT-SEC-06** (minor) — banking DTO reuse risk; split before any public company endpoint.

I did not write a findings file (per instructions). Key files for the developer to fix: `src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs`, `src/Cleansia.Core.AppServices/Features/Packages/DeletePackage.cs`, `src/Cleansia.Infra.Database/EntityConfigurations/CompanyInfoEntityConfiguration.cs`, `src/Cleansia.Web.Admin/Controllers/AdminEmailTemplateController.cs`, `src/Cleansia.Web.Admin/Controllers/AdminCodeController.cs`, `src/Cleansia.Web.Customer/Controllers/ServiceController.cs` + `PackageController.cs`. I will re-verify after fixes; do not approve CAT-SEC-01 or CAT-SEC-02 until resolved.


# ===== disputes-addresses CODE QUALITY =====

I have enough to confirm the routing/policy mismatch. The Partner DisputeController exposes `CreateDispute` ([Permission(Policy.CanCreateDispute)] = `CustomerOnly`), `GetPaged`/`GetById`/`AddMessage`, plus the admin-only `Resolve` and `UpdateStatus` — all on the Partner API. There is no Admin API dispute controller. The dispute admin lifecycle (Resolve, UpdateStatus) is gated `AdminOnly` but only reachable through the Partner API host, while the partner-facing create/list are `CustomerOnly`/`Authenticated`. This means: admins must authenticate against the Partner API to resolve disputes, and there's no admin web/UI surface at all.

Let me do one final check — confirm `AddDisputeMessage` handler has no null guard on `dispute` (the validator's `ExistsAsync` uses the base `ExistsAsync` which is a different query than `GetDisputeWithDetailsAsync`), and confirm the empty `catch{}` blocks. I have all I need now.

I have completed a thorough read of the disputes-addresses domain across all layers. Here is my findings report.

---

# Audit Findings — Disputes & Addresses domain (code quality / spaghetti / consistency)

**Scope read:** `Features/Disputes/*`, `Features/SavedAddresses/*`, `Features/Addresses/DTOs`, `Domain/Disputes/*`, `Domain/Users/{Address,SavedAddress}.cs`, specs/sorts/filters, 3 backend Dispute controllers + 2 SavedAddress controllers, customer-features disputes UI + NgRx store, mobile `customer-app` disputes + addresses.

**Method note:** I cross-checked every finding against `consistency.md`, `conventions.md`, and `backlog/audits/consistency-violations.md`. Findings already tracked in that audit (F4, F5, F11, F14, F16) are listed at the end as **pre-existing / not re-flagged** per the audit protocol — they are NOT counted as new. Everything ranked below is a **new** finding not in the tracked list.

---

## CRITICAL

### DA-1 — Dispute admin lifecycle (Resolve / UpdateStatus) is hosted on the Partner API; there is no Admin surface at all — functional GAP
- **Type:** architecture / routing / functional gap · **Size:** L · **Layers:** backend, frontend (admin), routing
- **Where:** `src/Cleansia.Web.Partner/Controllers/DisputeController.cs:66-88` (`Resolve`, `UpdateStatus`); policies `Policy.CanResolveDispute`/`CanUpdateDisputeStatus` = `PhysicalPolicy.AdminOnly` (`PolicyBuilder.cs:77-78`). No `src/Cleansia.Web.Admin/Controllers/*Dispute*.cs` exists; no `libs/cleansia-admin-features/**/dispute*` exists.
- **Impact:** The only way to resolve/escalate/close a dispute is to call **admin-only endpoints mounted on the Partner API host**. There is no Admin API controller and no admin web UI. Admins cannot manage disputes through the admin app; the resolution workflow is effectively unreachable from the product surface. Mirrors the prior audit's "admin order intervention is missing" pattern.
- **Fix:** Decide the canonical home (Admin API + admin-features module is the consistent answer, matching `order-management`/`invoice-management`). Add `Cleansia.Web.Admin/Controllers/DisputeController` exposing GetPaged/GetById/Resolve/UpdateStatus/AddMessage(staff), and an admin-features `disputes-management` list+detail feature. Remove the admin-only actions from the Partner controller. **Needs a user story** (admin dispute management) — likely an Architect/ADR decision on host placement first.

### DA-2 — `Close`, `Escalate`, `LinkStripeDispute` domain transitions are unreachable; dispute status machine is half-built — functional GAP
- **Type:** dead code / functional gap · **Size:** M · **Layers:** backend, domain
- **Where:** `Domain/Disputes/Dispute.cs:92-108` (`Close`, `Escalate`, `LinkStripeDispute`). Grep confirms zero callers in `src`. `DisputeStatus` has `UnderReview`/`WaitingForResponse`/`Escalated`/`Closed` (`Enums/DisputeStatus.cs`) but the only write paths are `UpdateStatus` (free-set any value, no transition guard) and `Resolve`.
- **Impact:** Half the lifecycle is modeled but not wired. `StripeDisputeId` is never populated, so chargeback-linked disputes (the `UnauthorizedCharge`/`IncorrectAmount` reasons) have no Stripe correlation. `UpdateDisputeStatus.cs:45` sets any status with no legal-transition validation, so a dispute can jump `Pending → Closed` skipping resolution, or be re-opened after `Resolved`. Either the methods are dead code to delete, or the lifecycle is unfinished work.
- **Fix:** Make `UpdateStatus` call the intent-named domain methods and enforce a transition table (`Dispute.UpdateStatus` should reject illegal transitions, returning a `BusinessResult` failure via the handler). Wire `LinkStripeDispute` from the Stripe dispute webhook (or delete it + `StripeDisputeId` if chargeback linking is out of scope). **Needs a user story** to define the legal transition graph.

---

## MAJOR

### DA-3 — Cross-API controller triplication: identical Dispute/SavedAddress controllers copy-pasted across Customer / Mobile.Customer / Partner with drifting auth
- **Type:** duplication / spaghetti · **Size:** M · **Layers:** backend
- **Where:** `Web.Customer/Controllers/DisputeController.cs` and `Web.Mobile.Customer/Controllers/DisputeController.cs` are **byte-for-byte identical** (95 lines each). `Web.Partner/Controllers/DisputeController.cs` is the same body plus 2 extra actions. Same triplication for `SavedAddressController` (Customer + Mobile.Customer identical). The `CreateOrderNullableCustomerAddressFilter` swagger filter is triplicated across Mobile.Customer / Customer / Mobile.Partner.
- **Impact:** Three copies must be kept in lockstep; they already drift (the Partner copy carries admin-only actions; Customer/Mobile don't). A fix to one (e.g. the inline `"File is required."` string, DA-7) silently misses the others. This is exactly the "same operation written N ways" `conventions.md` Duplication rule targets.
- **Fix:** Extract a shared base/partial controller or a thin shared controller in a common Web library; each host subclasses and only declares its host-specific actions and attributes. At minimum, the Customer and Mobile.Customer controllers should be one shared type.

### DA-4 — `AddDisputeMessage` handler dereferences a possibly-null `dispute` (NRE risk) and the validator existence check uses a different query than the handler
- **Type:** bug-risk / spaghetti · **Size:** S · **Layers:** backend
- **Where:** `Features/Disputes/AddDisputeMessage.cs:48-56`. Handler does `var dispute = await ...GetDisputeWithDetailsAsync(...)` then immediately `dispute.UserId` (line 50) and `dispute.AddMessage(...)` (56) with **no null guard**. Existence is only asserted in the validator via `disputeRepository.ExistsAsync` (base query), but the handler loads via `GetDisputeWithDetailsAsync` (different query, no CT).
- **Impact:** A dispute that exists per `ExistsAsync` but is filtered out by a global query filter (tenant/soft-delete) in `GetDisputeWithDetailsAsync` returns null → **NullReferenceException** (unhandled 500). Contrast `UpdateDisputeStatus.cs:39` / `ResolveDispute.cs:47` / `UploadDisputeEvidence.cs:82` which all guard. This is the B4 canonical "fetch-and-guard in handler" being applied inconsistently within the same feature folder.
- **Fix:** Add the canonical `if (dispute is null) return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));` guard before line 50.

### DA-5 — Customer disputes facade pulls DTOs/enums from the wrong generated client (`@cleansia/partner-services`)
- **Type:** contract / spaghetti · **Size:** S · **Layers:** frontend
- **Where:** `disputes.facade.ts:15-21` and `disputes.component.ts:6-10` import `DisputeListItem`, `DisputeReason`, `DisputeStatus`, `CreateDisputeCommand`, `AddDisputeMessageCommand`, `OrderListItem` from `@cleansia/partner-services` — inside the **customer** app, which calls `CustomerClient`.
- **Impact:** The customer feature depends on the partner client's generated types. If the partner and customer OpenAPI specs diverge (they are separately generated — `manual_step: nswag-regen`), the customer disputes screen binds to types that don't match the data `CustomerClient.disputeClient` actually returns. Violates `conventions.md` "use the generated client wrapper" reuse rule (the *correct* wrapper). Latent break the next time clients are regenerated.
- **Fix:** Import all dispute DTOs/enums from `@cleansia/customer-services`.

### DA-6 — Customer disputes list ignores the list-feature archetype: no `cleansia-table`, raw PrimeNG forms, hardcoded error strings
- **Type:** consistency (C/D archetype) · **Size:** M · **Layers:** frontend
- **Where:** `disputes.component.ts` + `disputes.facade.ts`.
  - **C6:** Uses `PaginatorModule` + a hand-rolled template instead of `cleansia-table` fed by a `getDisputesTableDefinition()` returning `{columns, actions}`. The locales even define `pages.disputes.table.*` columns, implying a table was intended.
  - **D3:** The create dialog uses a plain object `createForm` with manual `createFieldError()` string-switching and raw `[(ngModel)]`-style PrimeNG inputs, not `cleansia-*` bound by `formControlName` + `ErrorPipe`.
  - **C3/C4:** `createDispute`/`sendMessage` use bare `.subscribe({next,error})` with `takeUntil` only (no `catchError → of(null) → finalize`), and surface errors via hardcoded `translate.instant('pages.disputes.create_error')` instead of `SnackbarService.showApiError(err)`.
- **Impact:** The feature reimplements paging/tables/forms/error-handling its own way; the backend `BusinessErrorMessage` codes are never shown to the user (generic toast instead). Maintenance and visual drift from every other customer list/form.
- **Fix:** Rebuild on `cleansia-table` + `getDisputesTableDefinition()`; convert the create dialog to a reactive form with `cleansia-*` + `ErrorPipe`; route errors through `showApiError`.

### DA-7 — No `errors.*` translations exist for any dispute/address backend error code (all 5 customer locales)
- **Type:** i18n / consistency · **Size:** M · **Layers:** frontend, backend-contract
- **Where:** `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — the `errors` object is **empty** (`errors: {}`). Backend emits `dispute.not_found`, `dispute.already_exists`, `dispute.invalid_refund_amount`, `dispute.not_owned_by_user`, `address.*`, `file.*`, `order.not_found`. None have a frontend `errors.*` key.
- **Impact:** Directly violates `conventions.md` Global rule: "Every backend error key has a matching frontend `errors.*` key in all 5 locales." Because they're missing, the disputes facade can't use `showApiError` and falls back to hardcoded generic strings (DA-6) — the user never sees the specific reason (e.g. "you already have an open dispute on this order").
- **Fix:** Add `errors.dispute.*`, `errors.address.*`, `errors.file.*` (+ `order.not_found`) to all five customer locale files, then switch the facade to `showApiError`.

### DA-8 — `AddSavedAddress` handler is a god-method: dedup, country-defaulting, duplicate-guard, default-clearing, persistence, and inline DTO projection all in one ~60-line `Handle`
- **Type:** spaghetti / tangled responsibilities · **Size:** M · **Layers:** backend
- **Where:** `Features/SavedAddresses/AddSavedAddress.cs:89-147`.
- **Impact:** Multiple violations in one method: (a) **B9** inline DTO projection (`new SavedAddressDto(...)`) instead of a `MapToDto()` extension — and it's duplicated verbatim in `UpdateSavedAddress.cs:150-161` and again in `GetSavedAddresses.cs:23-34` (three copies of the same 11-field projection); (b) country-default fallback logic (`GetByIsoCodeAsync("CZE") ?? first`) is duplicated in `AddSavedAddress.cs:95` and `UpdateSavedAddress.cs:127`; (c) the address-dedup/create dance (`GetAddressAsync ?? Create`, then re-check `GetByIdAsync`) is convoluted and partially duplicated. The handler also `throw new InvalidOperationException("No countries configured")` — a raw exception with a hardcoded English string instead of a `BusinessResult` failure.
- **Fix:** Add `SavedAddress.MapToDto(country)` mapper (one home); extract a `ResolveDefaultCountryAsync` helper or domain service; extract the address get-or-create into `IAddressRepository`/a small service; return a `BusinessResult` failure (with a `BusinessErrorMessage`) when no country is configured rather than throwing.

### DA-9 — `"CZE"` default-country and Mapbox coordinate bounds are magic constants scattered across handlers/validators
- **Type:** magic strings/numbers · **Size:** S · **Layers:** backend
- **Where:** `"CZE"` literal in `AddSavedAddress.cs:95` and `UpdateSavedAddress.cs:127`. Lat/long bounds `-90/90/-180/180` inline in `AddSavedAddress` validator (63-71) and `UpdateSavedAddress` validator (81-89). The 2000-char description cap appears in `CreateDispute` validator (line 35), the `Dispute.Description` annotation, the FE component (`disputes.component.ts:78`), and the mobile VM (`CreateDisputeViewModel.kt:59`) as bare `10..2000`.
- **Impact:** Violates `conventions.md` "No magic numbers/strings — constants live in a Policy class / enum / theme token." A change to the default country or a length cap must be hunted across backend + FE + mobile. The coordinate bounds are geographic constants with no named home.
- **Fix:** Introduce named constants (e.g. `Policy`/`AddressDefaults.FallbackCountryIso`, `GeoBounds.*`, `DisputeLimits.DescriptionMin/Max`). Mobile/FE should mirror from a single source where practical.

### DA-10 — `DeleteSavedAddress` hard-deletes a user-facing entity, and is inconsistent with the soft-delete canonical (B6)
- **Type:** data-loss-risk / consistency · **Size:** S · **Layers:** backend, db
- **Where:** `Features/SavedAddresses/DeleteSavedAddress.cs:56` `savedAddressRepository.Remove(saved)`.
- **Impact:** `SavedAddress : Auditable, ITenantEntity` carries audit fields and is referenced by orders' address history conceptually. B6 canonicalizes on `Deactivate` for user/business-facing entities. (Note: F9 in the tracked audit covers the *sweep* generically; this is the concrete disputes-addresses instance and worth naming in the canonicalization ticket.)
- **Fix:** `repo.Deactivate(saved)` once the B6 ADR lands; ensure `GetByUserAsync` filters `IsActive`.

---

## MINOR

### DA-11 — `GetDisputeWithDetailsAsync` drops `CancellationToken` through the entire dispute read path
- **Type:** convention violation (CT propagation) · **Size:** S · **Layers:** backend
- **Where:** `IDisputeRepository.cs:24` / `DisputeRepository.cs:26` — `GetDisputeWithDetailsAsync(string disputeId)` takes no CT. Called (without CT) by `GetDisputeDetails.cs:24`, `ResolveDispute.cs:45`, `AddDisputeMessage.cs:48`, all of which **have** a `cancellationToken` in scope.
- **Impact:** Violates `conventions.md` "CancellationToken propagation through every async IO path." A client disconnect won't cancel the (multi-include, split) query.
- **Fix:** Add `CancellationToken cancellationToken` to the signature and thread it from all three handlers.

### DA-12 — Three empty/swallowing `catch` blocks hide SAS-URI generation failures with no logging
- **Type:** spaghetti / observability · **Size:** S · **Layers:** backend
- **Where:** `UploadDisputeEvidence.cs:109-115` (`catch { }` — completely empty), `DisputeMappers.cs:67-75` (`catch { // Swallow }`). The blob-URL generation can fail silently and the only signal is a null `BlobUrl`.
- **Impact:** Violates `conventions.md` "Comments explain WHY" partly, but mainly this is a swallowed exception with zero telemetry — evidence that won't display becomes invisible to ops. Also duplicated SAS-generation logic between the handler (line 111) and the mapper (line 70).
- **Fix:** Catch the specific blob exception, log it (no PII), and extract the SAS-URI helper to one place reused by both the mapper and the upload handler.

### DA-13 — `DisputeMappers.MapToListItem` has a C# operator-precedence string bug + duplicated name/email concatenation
- **Type:** bug / duplication · **Size:** S · **Layers:** backend
- **Where:** `DisputeMappers.cs:18` and `:34`: `CustomerName: dispute.User?.FirstName + " " + dispute.User?.LastName ?? ""`. The `?? ""` binds only to the last operand; if `User` is null the whole expression is `null + " " + null ?? ""` = `" "` (a single space), not `""`. The `FirstName + " " + LastName` concat is duplicated in `MapToListItem`, `MapToDetails`, and `DisputeMessageDto` mapping (line 51, which correctly uses `.Trim()`).
- **Impact:** List/detail show a stray `" "` for disputes whose `User` failed to load, while messages show `""`. Inconsistent and a latent display glitch.
- **Fix:** Use a single `FullNameOrEmpty(user)` helper with `.Trim()`; reuse across all three sites.

### DA-14 — Dead `pl` locale mapping and an app-local `getLocale()` in the disputes component
- **Type:** dead code / spaghetti · **Size:** S · **Layers:** frontend
- **Where:** `disputes.component.ts:170-173`: `localeMap = { cs:'cs-CZ', en:'en-US', pl:'pl-PL' }`. The platform supports **en, cs, sk, uk, ru** (no `pl`), and **omits sk/uk/ru** — so Slovak/Ukrainian/Russian users fall through to `en-US` date formatting.
- **Impact:** Dead `pl` entry; three of five supported locales silently mis-format dates. Bespoke per-component locale map instead of a shared date utility.
- **Fix:** Remove `pl`, add `sk/uk/ru`, and move to a shared locale/date helper used platform-wide.

### DA-15 — `GetSavedAddresses` silently drops saved addresses whose `Address` is null
- **Type:** bug-risk / hidden data loss · **Size:** S · **Layers:** backend
- **Where:** `GetSavedAddresses.cs:22` `.Where(s => s.Address != null)`.
- **Impact:** If an `AddressId` FK is ever orphaned (the address hard-deleted elsewhere — possible given Address has no soft-delete guarantee), the user's saved address vanishes from the list with no error and no log. Masks a data-integrity problem rather than surfacing it.
- **Fix:** This filter is a band-aid over a referential-integrity gap. Either enforce the FK/soft-delete invariant so `Address` is never null, or log when one is skipped. Worth a small ticket.

### DA-16 — `UpdateSavedAddress` orphans the old shared `Address` row when street/city/zip change
- **Type:** correctness / data hygiene · **Size:** M · **Layers:** backend, db
- **Where:** `UpdateSavedAddress.cs:133-145`: on edit it does get-or-create a *new* `Address` and re-points `saved.AddressId`, never cleaning up the previously-referenced `Address`. `Address` rows are shared/deduped (`GetAddressAsync`), so the old row may still be referenced by orders — but if not, it leaks.
- **Impact:** Accumulating orphan `Address` rows over time; no reuse-count tracking. Because addresses are shared, you can't naively delete, which is why this is M (needs a deliberate decision), not a quick fix.
- **Fix:** Decide ownership semantics for shared `Address` rows (reference counting, or never-delete + periodic cleanup function). Likely an Architect call.

### DA-17 — `SetDefaultSavedAddress` and `DeleteSavedAddress` exist on the backend but have no web controller route (only mobile/web stores reference SetDefault)
- **Type:** functional gap (partial) · **Size:** S · **Layers:** backend, frontend
- **Where:** `SavedAddressController` (both Customer and Mobile.Customer) **does** expose `SetDefault` and `Delete` — confirmed. But there is no web customer-features address-management UI feature folder (grep found stores `libs/data-access/customer-stores/.../saved-addresses` and the mobile `AddressManager*`, but no `cleansia-customer-features/*address*` component). 
- **Impact:** The web customer app has a saved-address NgRx store + client but no dedicated management screen surfaced in features; address management is effectively mobile-only on the customer side. Worth confirming against product intent.
- **Fix:** Confirm whether a web saved-address management screen is in scope; if so it's a small **user story**.

---

## Pre-existing / tracked — noted, NOT re-flagged as new (per audit protocol)
These are already in `backlog/audits/consistency-violations.md`; I confirmed each still holds but do not count them as new findings:
- **F4** — `CreateDispute` (`ICommand<string>`), `UpdateDisputeStatus` (`ICommand`), `DeleteSavedAddress` (`ICommand`) lack a `Response` record (B1). Confirmed at `CreateDispute.cs:44`, `UpdateDisputeStatus.cs:29`, `DeleteSavedAddress.cs:11`.
- **F5** — Ownership/session checks in validators for `UpdateSavedAddress`/`DeleteSavedAddress`/`SetDefaultSavedAddress` (B4/S3). Confirmed (`UpdateSavedAddress.cs:104`, `DeleteSavedAddress.cs:40`, `SetDefaultSavedAddress.cs:40`).
- **F11** — Customer `disputes` facade mixes NgRx (`store.dispatch/select`) with direct client calls (C8). Confirmed (`disputes.facade.ts` uses both Store and `customerClient`).
- **F14** — `CreateDisputeViewModel` uses loose `_submitting`/`_error` instead of `ActionState` (E2). Confirmed (`CreateDisputeViewModel.kt:43-47`).
- **F16** — customer-app repos (`DisputeRepository`, `AddressRepository`) return `T?`/`Boolean` with snackbar-in-repo instead of `ApiResult<T>` (E5). Confirmed.
- **F9** — generic hard-delete sweep covers DA-10 at the program level (DA-10 is the concrete instance).

---

## Ranked summary (by impact)

| ID | Title | Sev | Size | GAP? | Layers |
|----|-------|-----|------|------|--------|
| DA-1 | Dispute admin lifecycle hosted on Partner API; no Admin surface | critical | L | **Yes** | be, fe-admin |
| DA-2 | Close/Escalate/LinkStripe unreachable; status machine half-built | critical | M | **Yes** | be, domain |
| DA-3 | Dispute/SavedAddress controllers triplicated with drifting auth | major | M | No | be |
| DA-4 | `AddDisputeMessage` NRE on null dispute | major | S | No | be |
| DA-5 | Customer disputes facade imports from partner client | major | S | No | fe |
| DA-6 | Disputes list ignores cleansia-table/form/error archetype | major | M | No | fe |
| DA-7 | No `errors.*` translations for dispute/address codes (5 locales) | major | M | partial | fe |
| DA-8 | `AddSavedAddress` god-method + 3x duplicated DTO projection | major | M | No | be |
| DA-9 | `"CZE"` / coord bounds / 2000-cap magic constants scattered | major | S | No | be, fe, mobile |
| DA-10 | `DeleteSavedAddress` hard-deletes audited entity (B6) | major | S | No | be, db |
| DA-11 | `GetDisputeWithDetailsAsync` drops CancellationToken | minor | S | No | be |
| DA-12 | Empty/swallowing SAS-URI catch blocks, no logging | minor | S | No | be |
| DA-13 | `MapToListItem` name `?? ""` precedence bug + dup concat | minor | S | No | be |
| DA-14 | Dead `pl` locale map; sk/uk/ru mis-format dates | minor | S | No | fe |
| DA-15 | `GetSavedAddresses` silently drops null-Address rows | minor | S | partial | be |
| DA-16 | `UpdateSavedAddress` orphans old shared Address row | minor | M | No | be, db |
| DA-17 | Web customer saved-address management UI absent | minor | S | **Yes** | be, fe |

**Functional GAPs needing a user story:** DA-1 (admin dispute management), DA-2 (dispute status lifecycle + Stripe linkage), DA-17 (web saved-address management); DA-15/DA-16 need a small data-integrity decision.

**Pattern-evolution note for the PM/Architect:** the inline-DTO-projection-instead-of-`MapToDto` smell (DA-8) and the cross-API-controller-copy smell (DA-3) each recur 3+ times in this one domain alone. If the wider audit confirms them elsewhere, they warrant a written `consistency.md` rule + a `check-consistency.mjs` mechanical check (B9 is stated but not mechanically enforced; controller-duplication has no rule).

**Relevant files:**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Partner\Controllers\DisputeController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Disputes\Dispute.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\AddDisputeMessage.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Mappers\DisputeMappers.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\SavedAddresses\AddSavedAddress.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\SavedAddresses\UpdateSavedAddress.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\disputes\src\lib\disputes\disputes.facade.ts`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\disputes\src\lib\disputes\disputes.component.ts`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\apps\cleansia.app\src\assets\i18n\{en,cs,sk,uk,ru}.json`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\PolicyBuilder.cs`


# ===== employees FUNCTIONAL GAPS =====

Confirmed. The "approval" gate for taking orders is `ContractStatus != Pending` (named `HasUploadedDocumentsAsync`, with message implying documents/approval), but a **Rejected** cleaner (status 5, set by `Reject()`) is `!= Pending`, so they pass. The intended gate should be `== Approved`. This is a security/correctness gap: rejected cleaners can still take and complete jobs.

I now have a thorough, evidence-backed picture. Let me compile the findings report.

# Employees Domain — Functional Gaps & Half-Built Features Audit

Scope: `Features/Employees`, `Features/EmployeeDocuments`, `Features/Dashboard` + admin employee-management and partner dashboard/profile (web + mobile). Audit only; no code changed. Rules cited from `agents/knowledge/{conventions,consistency,security-rules}.md`.

## Summary of impact ranking
The single most damaging cluster is the **ContractStatus lifecycle**: two of five enum states are unreachable, the "approval" gate on order-taking is wrong (rejected cleaners can still work), and there is no way to ever change a cleaner's status after the first approve/reject. After that: the admin document-management surface is half-built (3 commands with no endpoint, a version-history endpoint with no UI), and the partner-web vs mobile capability split (granular profile edits + document typing exist only on mobile).

---

## CRITICAL

### EMP-GAP-01 — Rejected cleaners can still take and complete orders
- Type: lifecycle state mishandled / security-correctness gap. GAP (needs story).
- File: `src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs:111-118` and `CompleteOrder.cs:139`.
- Evidence: the approval gate is `HasUploadedDocumentsAsync` returning `employee?.ContractStatus != ContractStatus.Pending`. `Employee.Reject()` sets `ContractStatus = Rejected (5)`, which is `!= Pending`, so a rejected cleaner passes. The intended gate is `== Approved`.
- Impact: a cleaner the admin explicitly rejected (failed vetting / documents rejected) can still claim available jobs and run them. Direct trust/liability exposure; defeats the whole approval workflow. Also: a cleaner whose docs are still `Pending` review but who has been `Approved` is allowed (fine), but the method name lies about what it checks.
- Fix: gate on `ContractStatus == Approved` (and decide explicitly whether `Active` counts — see EMP-GAP-02). Rename the rule to reflect "approved", give it its own `BusinessErrorMessage` key, and apply the same gate anywhere a cleaner acts on an order.
- Size: S. Layers: backend.

### EMP-GAP-02 — `ContractStatus.Active` and `.Terminated` are never reachable; no status lifecycle after approve/reject
- Type: enum states never set + missing flow. GAP (needs story).
- Files: enum `src/Cleansia.Core.Domain/Enums/ContractStatus.cs` (`Active=2`, `Terminated=3`); domain `src/Cleansia.Core.Domain/Users/Employee.cs:229-255` (`Approve`→Approved, `Reject`→Rejected); only other setter `UpdateContractStatus` (line 223) has no caller.
- Evidence: no command sets `Active` or `Terminated`. `UpdateContractStatus()` is never invoked by any handler. Admin UI `canApproveOrReject()` (`employee-detail.facade.ts:321-327`) only enables actions when status is `Pending`, so once a cleaner is Approved or Rejected the admin can never change their status again — no off-boarding/termination, no re-instatement, no un-reject.
- Impact: cannot terminate a misbehaving cleaner, cannot reverse a mistaken rejection, cannot deactivate without going through the generic `IsActive` flag (which the employee UI doesn't expose either). Half the lifecycle is dead. `Active` appears to be intended as "approved + onboarding finished" but is fully orphaned.
- Fix: decide the canonical lifecycle (likely Pending → Approved/Rejected → Active/Terminated, with allowed transitions) as an ADR; add `Terminate(reason)` / `Reactivate()` domain methods + admin commands + endpoints + UI, or delete the two unused enum values if they are not part of the model. Update the order-taking gate (EMP-GAP-01) to whatever "can work" means.
- Size: M-L. Layers: domain, backend, admin-frontend (+ mobile/web partner if status is surfaced).

---

## MAJOR

### EMP-GAP-03 — Admin cannot upload, replace, or delete employee documents (3 commands, 0 endpoints)
- Type: command exists, no consumer / dead-end admin UI. GAP (needs story).
- Files: commands `EmployeeDocuments/UploadEmployeeDocument.cs`, `UploadNewDocumentVersion.cs`, `DeleteDocument.cs` (admin-shaped, take an explicit `EmployeeId`/`DocumentId`); controller `Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs` exposes only get-paged / approve / reject / versions / download.
- Evidence: grep for these three commands across all `*Controller*.cs` returns only the partner-self `SaveMyDocuments`/`DeleteMyDocument`. The admin variants are wired nowhere. Admin docs facade (`employee-documents.facade.ts`) has no upload/delete/new-version methods.
- Impact: an operator vetting a cleaner cannot attach a signed contract, upload a missing document on the cleaner's behalf, or remove a wrong/duplicate file. Back-office document management is read-only (approve/reject/download). Fully-built handlers sit unused.
- Fix: add admin endpoints for upload / new-version / delete on `AdminEmployeeDocumentController` (with blob upload like `SaveMyDocuments`, which these commands assume already-uploaded `FilePath` — note they take a path, not a blob, so they're not even wired to the blob pipeline), then admin UI. Confirm whether `UploadEmployeeDocument` is meant to receive a `BlobFileDto` (consistency with `SaveMyDocuments`) — as written it expects a pre-uploaded `FilePath`, which no caller produces.
- Size: M. Layers: backend, admin-frontend. `manual_step: nswag-regen`.

### EMP-GAP-04 — Document version-history endpoint has no UI consumer
- Type: endpoint with no consumer / dead-end. GAP (needs story).
- Files: `AdminEmployeeDocumentController.cs:62 GetVersionHistory`; query `EmployeeDocuments/GetDocumentVersionHistory.cs`. Admin facade `employee-documents.facade.ts` never calls `.versions(...)`; the section template (`employee-documents-section.component.html:77`) shows `v{{ doc.version }}` but offers no way to view prior versions.
- Evidence: auto-versioning is implemented end-to-end (`EmployeeDocument.CreateNewVersion`, `latestVersionOnly` filter, version endpoint) but the only surfaced number is the current version badge. No "view history" action exists.
- Impact: the entire versioning investment is invisible to operators; they cannot see what a cleaner replaced or audit a re-upload. Combined with EMP-GAP-03 (no admin re-upload), versioning is effectively unused on the admin side.
- Fix: add a "version history" action on each document row that calls `.versions(documentId)` and renders prior versions (with download per version).
- Size: S. Layers: admin-frontend.

### EMP-GAP-05 — Partner web profile saves all documents as `DocumentType.Other` (web/mobile capability split)
- Type: half-built feature / inconsistent layers. GAP (needs story).
- Files: web profile facade `…/profile/src/lib/profile/profile.facade.ts:191-198` submits via `employeeClient.updateEmployee(...)`; the monolithic `UpdateEmployee.cs:281-289` hardcodes `DocumentType.Other` for every uploaded file. The typed path `SaveMyDocuments` (honors `DocumentType`) is wired in the partner web controller but unused by the web UI.
- Evidence: mobile partner `EmployeeController` exposes 7 granular update endpoints + `SaveMyDocuments`; web partner `EmployeeController` exposes only `UpdateEmployee` + the docs endpoints, and the web UI routes document uploads through `UpdateEmployee`.
- Impact: every document a cleaner uploads on the web is typed "Other", so admins can't tell a passport from a bank statement; the `DocumentType` taxonomy is meaningless for web-registered cleaners. Inconsistent with mobile, which types correctly.
- Fix: web profile should upload documents via `SaveMyDocuments` with a per-file `DocumentType` picker (as mobile does), and stop piggy-backing files on `UpdateEmployee`.
- Size: M. Layers: web-frontend (+ confirm `UpdateEmployee` should stop accepting `Documents` at all).

### EMP-GAP-06 — Granular profile-edit endpoints exist only on mobile; web partner has monolithic-only
- Type: command exists, partial consumer / inconsistent layers. GAP (needs story).
- Files: commands `Employees/UpdatePersonalInfo.cs`, `UpdateIdentificationInfo.cs`, `UpdateAddressInfo.cs`, `UpdateBankDetails.cs`, `UpdateEmergencyContact.cs`, `UpdateAvailability.cs`; exposed only by `Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:50-108`. Web partner `EmployeeController.cs` exposes none of them.
- Evidence: the web profile has per-section components (`profile-personal-info`, `profile-bank-details`, `profile-availability`, `profile-emergency-contact`) but the facade saves everything through one `updateEmployee` call requiring the **whole** form to be valid (`onSubmit` rejects if `!formGroup.valid`).
- Impact: a web cleaner cannot save one section (e.g. update only their IBAN or availability) without re-satisfying the entire onboarding form including `Consent`; partial edits are impossible. Mobile users get granular saves; web users don't. Per-section UI is misleading (looks editable section-by-section, isn't).
- Fix: expose the six granular commands on the web partner controller and wire each profile section to its own save, matching mobile.
- Size: M. Layers: backend (controllers), web-frontend. `manual_step: nswag-regen`.

### EMP-GAP-07 — `GetAvailableJobsPreview` is mobile-only; web partner dashboard cannot show available-jobs preview
- Type: endpoint with no web consumer / inconsistent layers. GAP (needs story, lower than above).
- Files: `GetAvailableJobsPreview.cs` + `AvailableJobPreviewDto`; wired only in `Cleansia.Web.Mobile.Partner/Controllers/DashboardController.cs:30`. Web `DashboardController.cs` omits it; web `dashboard.facade.ts` shows an `availableOrdersCount` stat (a number) but no preview list.
- Impact: feature parity gap — mobile cleaners get a "jobs waiting for you" preview on the dashboard; web cleaners only see a count. Not broken, but a built capability the web app can't reach.
- Fix: either expose on web + render a preview card, or explicitly document it as mobile-only.
- Size: S. Layers: backend (controller), web-frontend.

---

## MINOR

### EMP-GAP-08 — `GetAllEmployees` query is dead (no endpoint) and duplicates `GetPagedEmployees`
- Type: dead code / consistency deviation. Not a user-facing GAP.
- File: `Features/Employees/GetAllEmployees.cs` — full CQRS query with validator and hand-rolled paging; grep across all controllers finds no consumer. Superseded by `GetPagedEmployees` (the canonical `DataRangeRequest` + specification form).
- Evidence: also violates consistency A1/A3/A5 (record `Query` with inline `Page/PageSize`, hand-built `Response` with manual `TotalPages`, no specification). `conventions.md`: "No dead code."
- Impact: maintenance noise, a second non-canonical way to list employees that could get wired by mistake.
- Fix: delete `GetAllEmployees` (and its bespoke `Response`).
- Size: S. Layers: backend.

### EMP-GAP-09 — `EmployeeListItem` DTO + `Employee.MapToDto()` mapper are unused
- Type: dead code. Not a GAP.
- Files: `Features/Employees/DTOs/EmployeeListItem.cs:6` and `Mappers/EmployeeMappers.cs:26 MapToDto`. No caller (`employee.MapToDto()` grep in the Employees feature returns nothing; list paths use `MapToAdminDto`/`MapToEmployeeItem`).
- Impact: confusing parallel "list item" shape; risk of wiring the wrong DTO.
- Fix: delete both unless a near-term consumer is planned.
- Size: S. Layers: backend.

### EMP-GAP-10 — `applyGradeTemplate()` is an empty stub in the admin facade
- Type: half-built / dead method. Not a GAP on its own (the pay-config bulk path works via `bulkApplyGrade`).
- File: `…/employee-detail/employee-detail.facade.ts:536-539` — empty body with a "will be handled by the component" comment.
- Impact: a public facade method that does nothing; if a button is bound to it, it silently no-ops. `conventions.md`: no dead code, comments explain WHY not WHAT.
- Fix: remove it, or implement the grade-template preview it gestures at.
- Size: S. Layers: admin-frontend.

### EMP-GAP-11 — `AdminEmployeeDetail` leaks raw `ApprovedByUserId` / `RejectedByUserId`
- Type: DTO leak (S4), minor (admin-only context). Not a functional GAP.
- File: `DTOs/EmployeeListItem.cs:63-65` (`AdminEmployeeDetail`). Raw user ids of the approving/rejecting admin are sent to the client with no display use.
- Evidence: S4 lists "other users' ids" as fields to scrub; the UI shows approval notes/dates, not these ids.
- Impact: low (admin surface), but it's an unnecessary internal-id exposure and the pattern spreads.
- Fix: either drop these fields or replace with a resolved admin display name.
- Size: S. Layers: backend.

### EMP-GAP-12 — `UpdateEmployee` ownership check lives in the validator (consistency B4 violation)
- Type: consistency deviation (already a known class). Not a GAP.
- File: `Features/Employees/UpdateEmployee.cs:39-41,183-188` — `AllowedToUpdateEmployee` (ownership) is a validator rule. `consistency.md` B4 explicitly names `UpdateEmployee` as a violation: ownership belongs in the handler (S3), not the validator.
- Impact: pattern drift; ownership enforcement split from where the entity is loaded.
- Fix: move the owner-match into the handler's fetch-and-guard.
- Size: S. Layers: backend.

---

## Cross-cutting observations for the PM
- The **ContractStatus lifecycle** (EMP-GAP-01, -02) should be one ADR + one or two stories; everything else hangs off the decision of what the canonical states/transitions are and what "can take orders" means.
- The **admin document surface** (EMP-GAP-03, -04) is one coherent story: "Admin manages a cleaner's documents (upload / replace / delete / view history)".
- The **web/mobile partner parity** gaps (EMP-GAP-05, -06, -07) are real half-built features, not cosmetic: typed document upload and per-section profile edits exist on mobile and are simply not reachable on web despite the UI implying they are.

Key files for the PM/Architect:
- Lifecycle: `src/Cleansia.Core.Domain/Enums/ContractStatus.cs`, `src/Cleansia.Core.Domain/Users/Employee.cs:223-255`, `src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs:111-118`, `CompleteOrder.cs:139`, `…/employee-detail.facade.ts:321-327`.
- Admin docs: `src/Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs`, `Features/EmployeeDocuments/{UploadEmployeeDocument,UploadNewDocumentVersion,DeleteDocument,GetDocumentVersionHistory}.cs`, `…/employee-detail/employee-documents.facade.ts`.
- Web/mobile parity: `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs` vs `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs`, `Features/Employees/UpdateEmployee.cs:281-289`, `…/profile/src/lib/profile/profile.facade.ts:191-198`.
- Dead code: `Features/Employees/GetAllEmployees.cs`, `Features/Employees/DTOs/EmployeeListItem.cs:6`, `Mappers/EmployeeMappers.cs:26`, `…/employee-detail.facade.ts:536`.


# ===== Integration clients FCM/SendGrid/Mapbox/PDF/Handlebars =====

So named HttpClients (Mapbox) DO get `AddStandardResilienceHandler` via `ConfigureHttpClientDefaults` IF the host calls `AddServiceDefaults` (the IHostApplicationBuilder variant). But the Stripe client and EmailService bypass `IHttpClientFactory` entirely — they `new SendGridClient(...)` / `new global::Stripe.StripeClient(...)`, so they get NO resilience handler, no OTel instrumentation, and no service-discovery. That's a notable observability/resilience gap. Also important: `AddHttpClientInstrumentation` with the Mapbox token in the query string means OTel traces capture the full URL including `access_token=` — a secret-in-telemetry leak.

I now have a comprehensive picture. Let me write the findings report.

---

# BLIND-SPOT AUDIT: Integration Clients & Services (FCM / SendGrid / Mapbox / Stripe / PDF / Handlebars)

Scope: `src/Cleansia.Infra.Clients/{Fcm,SendGrid,Stripe}`, `src/Cleansia.Infra.Services/{Geocoding,Pdf,Templates}`, and the `EmailService` that actually drives SendGrid (`src/Cleansia.Core.AppServices/Services/EmailService.cs`). Audit only — no code changed. Findings cite `agents/knowledge/runtime-readiness.md`, `security-rules.md` (S1–S10), and `conventions.md`.

**Headline:** The integration layer is, for the most part, more careful than the prior audit's other areas — FCM token lifecycle, Stripe idempotency keys, and PDF failure handling are genuinely well done. The real defects are concentrated in **(a) email being sent inline-synchronously on the critical path so a SendGrid outage hard-fails registration/password-reset, (b) two secret-leak vectors (Mapbox token in URL → OTel/log capture; SendGrid error-body logged at Error with PII), and (c) a registered-but-dead Handlebars engine whose unescaped helpers are a latent XSS/crash trap if anyone wires it.**

---

## Ranked findings

### BLIND-1 — Email sent inline & synchronously on the critical path; SendGrid outage hard-fails registration and password-reset
- **Severity:** High
- **Type:** Resilience / graceful-degradation defect (functional)
- **Where:** `src/Cleansia.Core.AppServices/Features/Auth/Register.cs:89`, `RegisterEmployee.cs:84`, `ResendConfirmationEmail.cs:59`, `Users/RequestPasswordChange.cs:43`. Failure source: `EmailService.SendTemplatedAsync` throws `EmailDeliveryException` (`EmailService.cs:365`) after Polly exhausts 3 retries.
- **Impact:** `EmailService` is invoked **inline inside the command handler** and throws on delivery failure. These four handlers do **not** wrap the call. So if SendGrid is degraded/down (or the API key is rotated/invalid), `Register` throws after ~3 retries → the user's account row never commits (or commits then the request 500s) and **registration fails entirely**; same for password-reset. This is precisely the row the readiness matrix forbids: *"SendGrid (email) | Order/booking fails because the email didn't send | Email is a side effect — enqueue it; a send failure is logged + retried by the queue/Function, it does not fail the command."* (`runtime-readiness.md:45`). The inconsistency proves the intent: `CompleteOrder.cs:241`, `StartOrder.cs`, `TakeOrder.cs` already wrap the same call in try/catch + `LogWarning` (fail-soft), but the auth flows don't.
- **Long-term fix:** Make all transactional email a durable side effect — enqueue a `SendEmailMessage` to the queue and let a Function send it (mirroring `SendPushNotificationFunction` / `GenerateReceiptFunction`), so the command commits and the email retries independently. Minimum interim fix: wrap the four auth call-sites in the same fail-soft try/catch the order handlers use, so a SendGrid blip can't block account creation. (Note the trade-off: confirmation/reset emails are *the* point of those flows, so silent fail-soft is wrong there — enqueue-with-retry is the correct answer, not swallow.)
- **Size:** M (interim wrap: S)
- **Layers:** backend (AppServices handlers + a new Function + queue message)
- **Functional GAP needing a story?** **Yes** — "Move transactional email to the durable queue+Function outbox path" is a user story.

---

### BLIND-2 — Mapbox access token placed in the URL query string → leaked into OpenTelemetry traces and any HTTP logging
- **Severity:** High
- **Type:** Security — secret handling / logging hygiene (S6-adjacent)
- **Where:** `src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs:45` (`...&access_token={_config.GeocodingAccessToken}`). The "Mapbox" named client (`ServiceCollectionExtensions.cs:20`) is created via `IHttpClientFactory`, so it inherits `ConfigureHttpClientDefaults` → `AddHttpClientInstrumentation()` (`ServiceDefaults/Extensions.cs:44,52`), which records `http.url`/`url.full` **including the query string** on every span.
- **Impact:** Every geocode call exports a trace span (and potentially logs) carrying the **full Mapbox secret token** in the URL. Anyone with access to the telemetry backend (Sentry/OTel collector) or HTTP debug logs can harvest the token. This contradicts `conventions.md:104` ("Real secrets — never in appsettings… ") in spirit and S6 (no secret material in logs). Mapbox supports the token in a header / the SDK pattern is to keep it out of the path.
- **Long-term fix:** Pass the token via an `Authorization`/secret mechanism Mapbox accepts off-URL, OR scrub `access_token` from span/log URLs via an OTel processor and a redacting `HttpClient` logging filter. At minimum, redact query secrets in the instrumentation enrichment.
- **Size:** S
- **Layers:** backend (infra service + telemetry config)
- **Functional GAP needing a story?** No — security hardening task.

---

### BLIND-3 — SendGrid error response **body** logged at Error level; can contain recipient PII
- **Severity:** Medium-High
- **Type:** Security — logging hygiene (S6 violation)
- **Where:** `src/Cleansia.Core.AppServices/Services/EmailService.cs:363-364` and `:413-414` — `var body = await response.Body.ReadAsStringAsync(ct); logger.LogError("SendGrid returned {StatusCode}: {Body}", …, body);`
- **Impact:** On any non-2xx SendGrid response, the **raw provider body is logged at Error**. SendGrid 4xx error bodies routinely echo back the offending field, including the **recipient email address** and sometimes the from-name. S6: "No email… in logs at Information level or higher." Error > Information, so this is a direct S6 violation, and Error-level logs are exactly what flows to Sentry (`runtime-readiness.md:25`). Combined with the inline-throw path (BLIND-1), a SendGrid outage produces a burst of PII-bearing Error logs.
- **Long-term fix:** Log only the status code + a SendGrid error *code/category* (parse the JSON, log `errors[].field`/`message` without the recipient), and the correlation id. Never log the recipient email above Debug.
- **Size:** S
- **Layers:** backend
- **Functional GAP needing a story?** No — fix in place.

---

### BLIND-4 — `HandlebarsTemplateEngine` is registered but unused (dead code), and its custom helpers `WriteSafeString` unescaped data — a latent XSS/template-injection trap
- **Severity:** Medium (latent High the moment anyone wires it)
- **Type:** Security latent / dead code
- **Where:** `src/Cleansia.Infra.Services/Templates/HandlebarsTemplateEngine.cs` (registered at `ServiceCollectionExtensions.cs:13`). Confirmed **zero consumers** — `ITemplateEngine`/`CompileAsync` is referenced only in its own files; all real email goes through SendGrid **dynamic templates** (`EmailService` + `MergeTranslationsWithData`), so rendering happens in SendGrid's cloud, not here.
- **Impact:** Two problems. (1) **Dead code that looks load-bearing** — a future dev will reasonably reach for `ITemplateEngine` to render an HTML email or document and inherit its flaws. (2) The helpers (`formatCurrency`, `formatDate`, `formatDateTime`, `formatNumber`, `add`, `multiply`, `eq`) all call `writer.WriteSafeString(...)`, which **bypasses Handlebars HTML-encoding**. Several encode manually (`WebUtility.HtmlEncode`), but `formatNumber`/`add`/`multiply`/`eq` write their results with no encoding, and the engine is created with `Handlebars.Create()` defaults. If a template author ever passes user-controlled strings through these helpers (or uses `{{{triple}}}`), it's stored/reflected XSS in an HTML email. The real production XSS surface is the **SendGrid hosted templates** (out of repo) — if any of those use `{{{...}}}` around `CustomerName`/`Address`, user-supplied name/address is injected unescaped; that needs an out-of-band check of the SendGrid template definitions.
- **Long-term fix:** Either delete `HandlebarsTemplateEngine` + its DI registration (preferred — it's unused), or if kept for future use, configure it with `Configuration { NoEscape = false }`, make every helper `WriteSafeString` only HTML-encoded output, and document a "no `{{{ }}}` on user data" rule. Separately, **audit the SendGrid dynamic templates** for triple-stache around customer-controlled fields (`CustomerName`, `Address`, `EmployeeName`).
- **Size:** S (delete) / M (audit SendGrid templates)
- **Layers:** backend (+ an ops task to review SendGrid template HTML)
- **Functional GAP needing a story?** Partial — "Audit/escape SendGrid dynamic-template HTML for user-supplied fields" is a security story; the dead-code removal is a cleanup task.

---

### BLIND-5 — Stripe and SendGrid bypass `IHttpClientFactory`: no resilience handler, no OTel instrumentation, no connection reuse
- **Severity:** Medium
- **Type:** Resilience + observability defect
- **Where:** `src/Cleansia.Infra.Clients/Stripe/StripeClient.cs` — every method does `new global::Stripe.StripeClient(config.SecretKey)` (12 sites); `EmailService.cs:348,390` does `new SendGridClient(sendGridConfig.ApiKey)`. Neither goes through `IHttpClientFactory`, so neither inherits `AddStandardResilienceHandler` / `AddHttpClientInstrumentation` from `ServiceDefaults/Extensions.cs:25-29`. (Contrast: the Fiscal client correctly uses `AddHttpClient<…>().AddStandardResilienceHandler()` at `FiscalServiceCollectionExtensions.cs:54-55`, and Mapbox is a named client.)
- **Impact:** (1) **No outbound resilience** on Stripe/SendGrid beyond what each SDK does itself — `runtime-readiness.md:71` requires "Every external call classifies its error and logs the boundary"; these calls have neither standard transient-retry policy nor boundary instrumentation. EmailService has a hand-rolled Polly policy (good), but Stripe has **nothing** — a transient Stripe network blip surfaces raw. (2) **No distributed tracing** on Stripe/SendGrid spans, so a Stripe slowdown is invisible in traces (`runtime-readiness.md:27-28`: "a Stripe/SendGrid/Firebase slowdown is visible before it becomes an incident"). (3) Newing a `StripeClient`/`SendGridClient` per call means a fresh `HttpClient`/handler each time → socket churn (the classic `HttpClient` anti-pattern) unless the SDK caches statically.
- **Long-term fix:** Register Stripe's and SendGrid's HTTP transport through `IHttpClientFactory` (Stripe SDK supports a custom `HttpClient`/`SystemNetHttpClient`; SendGrid supports an injected `HttpClient`), so both get the standard resilience handler + OTel. Add explicit error classification (`Transient | Permanent | Configuration | Unknown`) at the boundary per `runtime-readiness.md:28,71`.
- **Size:** M
- **Layers:** backend (infra wiring)
- **Functional GAP needing a story?** Yes — "Route Stripe & SendGrid through IHttpClientFactory for resilience + telemetry" is a story.

---

### BLIND-6 — No error classification (`Transient/Permanent/Configuration`) anywhere in the integration layer; retries treat all failures alike
- **Severity:** Medium
- **Type:** Resilience / observability gap (functional)
- **Where:** `EmailService` Polly policy retries on **any** non-2xx (`EmailService.cs:33`) including permanent 4xx (bad template id, invalid recipient) — burning 3 attempts on errors that will never succeed; `MapboxGeocodingService.cs:68` collapses all exceptions to "return null"; `FcmPushDispatcher` does classify FCM per-token codes (the one place that does it right, `:96-98`) but its outer catch (`:75-81`) treats an init/transport failure as "all failed, prune nothing"; `StripeClient` does no classification at all.
- **Impact:** Violates `runtime-readiness.md:55-57`: "Retries read the error classification: Transient → retry; Permanent → stop + flag; Configuration → alert, don't retry forever" and the checklist item ":71 Every external call classifies its error and logs the boundary." Practically: a rotated SendGrid key (Configuration error, 401) retries 3× per email and never alerts as a config problem; a permanently-bad recipient retries needlessly. There is no `Permanent`/`Configuration` spike signal for the owner to alert on (`runtime-readiness.md:63`).
- **Long-term fix:** Introduce a shared error-classification helper (`Transient | Permanent | Configuration | Unknown`) used by all four clients; Polly retries only `Transient`; `Configuration` (401/403/bad-template) logs once at Error + emits a metric and does not retry; surface counts for owner alerting.
- **Size:** M
- **Layers:** backend
- **Functional GAP needing a story?** Yes.

---

### BLIND-7 — Mapbox geocoding has no rate-limit / 429 handling; silently returns no coordinates and pollutes orders with missing geo
- **Severity:** Medium
- **Type:** Resilience defect (functional)
- **Where:** `src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs:54-74`. `GetFromJsonAsync` throws `HttpRequestException` on 429, caught by the broad `when` filter at `:68` → logs a `Warning` ("…continuing without coordinates") → returns `null`. There is no distinction between "address genuinely not found" and "Mapbox rate-limited/down".
- **Impact:** Under Mapbox 429 (rate limit) or outage, **every** order created during the window silently lands with **no coordinates**, indistinguishable in logs from a real geocode miss, with no retry and no backfill. Mapbox v6 enforces per-token rate limits; a burst of bookings will hit them. Map/routing features and distance-based pay (`expensesPay = distance × distanceRate`) silently degrade. The 5s timeout (`ServiceCollectionExtensions.cs:22`) means a slow Mapbox also fails open to null.
- **Long-term fix:** Distinguish 429/5xx (transient → don't treat as "not found"; either retry with backoff via the resilience handler or enqueue a geocode-backfill job) from a genuine empty result set. Log the two cases differently so a rate-limit spike is visible (`runtime-readiness.md:63`). Consider an idempotent geocode-backfill Function for orders missing coordinates.
- **Size:** M
- **Layers:** backend
- **Functional GAP needing a story?** Yes — "Geocode-backfill + rate-limit-aware geocoding" story.

---

### BLIND-8 — Push dispatch is at-most-once and not idempotent; queue re-delivery double-sends, and a transient init failure prunes nothing but reports all-failed
- **Severity:** Low-Medium
- **Type:** Correctness / S7-adjacent
- **Where:** `src/Cleansia.Infra.Clients/Fcm/FcmPushDispatcher.cs:75-81` (broad catch → `PushDispatchResult(0, count, [])` then re-throw path is in the Function) and `SendPushNotificationFunction.cs:120` (`throw;` → "Azure Functions retries via queue").
- **Impact:** The Function re-throws on any exception so the queue redelivers, but there is **no dedup key** on the push send. If the FCM call partially succeeded before an exception (or the commit after pruning failed), redelivery **re-sends the push** to tokens that already got it. S7 calls out push as a doublable side effect class ("sends an email… push"). Most pushes are informational so the blast radius is low (duplicate notification, not duplicate charge) — hence Low-Medium — but `SendSitewidePromo` fan-out re-delivery could double-notify the whole user base. Separately, `FcmPushDispatcher.EnsureInitialized` returns `null` on transient init failure (`:170-179`) leaving `_initAttempted=false` to retry next time (good), but the *current* dispatch reports `(0, allFailed, [])` so the Function logs all-failed with nothing pruned — acceptable, but means a cold-start init race silently drops one event's pushes with only a Warning.
- **Long-term fix:** Add an idempotency/dedup marker per (userId, eventKey, dispatch-attempt) so redelivery doesn't re-push; or accept duplicates for informational categories but guard the marketing fan-out with a sent-ledger. Document the at-most-once vs at-least-once intent.
- **Size:** M
- **Layers:** backend (Function + a dedup store)
- **Functional GAP needing a story?** Partial — worth a story for the marketing fan-out path specifically.

---

### BLIND-9 — Handlebars numeric helpers `Convert.ToDecimal/ToInt32` throw `FormatException` on non-numeric input (DoS-on-render if ever wired)
- **Severity:** Low (latent, gated by BLIND-4's "unused")
- **Type:** Robustness / crash vector
- **Where:** `HandlebarsTemplateEngine.cs:33,84-85,104-105,117-118` — `Convert.ToDecimal(parameters[0])` / `Convert.ToInt32(...)` with no try-parse.
- **Impact:** If the engine is ever used and a template passes a non-numeric or null-ish value into `formatCurrency`/`formatNumber`/`add`/`multiply`, `Convert.ToDecimal` throws and the whole render throws — a single bad data value crashes the email/document generation. No callers today, so latent.
- **Long-term fix:** Use `decimal.TryParse`/`Convert.ToDecimal(..., InvariantCulture)` with a safe fallback; or delete the engine per BLIND-4. Also note `Convert.ToDecimal` uses current culture — locale-dependent parsing is itself a bug in a 5-language app.
- **Size:** S
- **Layers:** backend
- **Functional GAP needing a story?** No.

---

### BLIND-10 — Two divergent SendGrid send paths; the abstraction (`SendGridClientFactory`) is effectively dead while the real path re-`new`s the client
- **Severity:** Low
- **Type:** Consistency / maintainability (`conventions.md` "one way to do each thing")
- **Where:** `src/Cleansia.Infra.Clients/SendGrid/SendGridClientFactory.cs` (the intended abstraction, with `BusinessResult`-returning `SendTemplateEmailAsync`) vs `EmailService.cs:348,390` which ignores the factory and does `new SendGridClient(...)` with its own Polly + exception-throwing contract. Grep confirms `ISendGridClientFactory.SendTemplateEmailAsync` has no production consumer.
- **Impact:** Two contracts for the same operation — one returns `BusinessResult` (`SendGridClientFactory.cs:26-31`), the other throws `EmailDeliveryException`. The factory's `EmailNotSentError = "email.sending_failed"` constant and its `BusinessResult` failure mode are dead. A maintainer can't tell which is canonical; the unused one rots (e.g. it would never get the retry policy). Violates `conventions.md:16` (reuse the real types) and `:38` (one way to do each thing).
- **Long-term fix:** Pick one. Either route `EmailService` through `ISendGridClientFactory` (and move the Polly policy + IHttpClientFactory wiring there, addressing BLIND-5), or delete the unused factory method.
- **Size:** S
- **Layers:** backend
- **Functional GAP needing a story?** No — cleanup.

---

### BLIND-11 — FCM init failure is latched permanently when config is missing, but the "missing service-account" warning is logged on **every** dispatch
- **Severity:** Low
- **Type:** Observability / log-noise
- **Where:** `FcmPushDispatcher.cs:48-53` — when `messaging is null` because `ServiceAccountJson` is empty (and the ADC branch already latched `_initAttempted`), every single dispatch logs a `Warning` listing the token count and event key. The terminal config-missing case at `:152` correctly latches, but the empty-`ServiceAccountJson`-with-valid-ADC-failure and the no-messaging path re-warn per call.
- **Impact:** In an environment with push intentionally disabled (or misconfigured), this emits a Warning per notification event — log spam that buries real signals and inflates Sentry/log volume. Minor, but it's the kind of noise `runtime-readiness.md:61-63` wants suppressed in favor of a single actionable alert.
- **Long-term fix:** Latch a "logged once" flag for the disabled-state warning, or log it at Debug after the first occurrence; emit a single startup-time Configuration warning instead of per-dispatch.
- **Size:** S
- **Layers:** backend
- **Functional GAP needing a story?** No.

---

## Things that are correct (so they don't get "fixed" into regressions)
- **FCM permanent-vs-transient token classification and dead-token pruning** is correct and idempotent-safe: `FcmPushDispatcher.cs:89-102` classifies only `Unregistered/InvalidArgument/SenderIdMismatch` as dead; `SendPushNotificationFunction.cs:100-108` prunes those `Device` rows and commits. Good.
- **Stripe idempotency keys** are thoughtfully scoped (amount-in-key for re-openable intents/refunds, attempt-id for re-subscribe) — `StripeClient.cs:41,72,131,208,247` — and satisfy S7 for the Stripe side.
- **QuestPDF failure handling** logs + rethrows with context (`QuestPdfService.cs:38-43,60-65`) and the receipt-generation Function is the durable retry boundary; license is set in a static ctor so it can't be missed.
- **EmailService Polly retry** (3 attempts, backoff) exists — the gap is *where* it's invoked (BLIND-1), not that it's missing.

---

## Suggested story/ticket grouping for the PM
- **Story A (High):** Durable transactional-email outbox (queue + Function) — resolves BLIND-1, folds in BLIND-3, BLIND-6 (email), BLIND-10.
- **Story B (High, security):** Integration secret-leak hardening — Mapbox token off-URL + telemetry redaction (BLIND-2), SendGrid PII-in-logs (BLIND-3), SendGrid dynamic-template `{{{ }}}` audit (BLIND-4).
- **Story C (Medium):** Route Stripe + SendGrid through `IHttpClientFactory` with resilience + OTel + shared error classification (BLIND-5, BLIND-6).
- **Story D (Medium):** Rate-limit-aware geocoding + geocode-backfill (BLIND-7).
- **Cleanup tasks (no story):** delete/secure dead Handlebars engine (BLIND-4/BLIND-9), push fan-out dedup (BLIND-8), FCM warning-latch (BLIND-11).

**Routing note for the PM:** BLIND-2, BLIND-3, and BLIND-4 are security-touching — invoke `security` for the formal gate. BLIND-1, BLIND-5, BLIND-6, BLIND-7 are resilience/architecture — worth an `architect` pass on the outbox/error-classification pattern (it recurs across all four integrations and should likely become a written `runtime-readiness` rule + a `check-consistency` probe for "external SDK newed outside IHttpClientFactory").

Key files: `src/Cleansia.Infra.Clients/Fcm/FcmPushDispatcher.cs`, `src/Cleansia.Infra.Clients/Stripe/StripeClient.cs`, `src/Cleansia.Infra.Clients/SendGrid/SendGridClientFactory.cs`, `src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs`, `src/Cleansia.Infra.Services/Pdf/QuestPdfService.cs`, `src/Cleansia.Infra.Services/Templates/HandlebarsTemplateEngine.cs`, `src/Cleansia.Core.AppServices/Services/EmailService.cs`, `src/Cleansia.Core.AppServices/Features/Auth/Register.cs`, `src/Cleansia.Functions/Functions/SendPushNotificationFunction.cs`.


# ===== catalog-config FUNCTIONAL GAPS =====

Email Templates is fully wired (create/sendTest/update consumed). Not a gap.

I now have all the evidence I need. Let me compile the findings report, ranked by impact.

---

# Catalog-Config Domain Audit — Functional Gaps & Half-Built Features

Scope audited: `Features/{Services, Packages, Currencies, Countries, Languages, Company, Codes, FeatureFlags, EmailTemplates}` + their admin/customer UIs. Audit-only; no code changed. Findings cite real rules from `agents/knowledge/conventions.md` (No dead code; "production-ready, long-term" bar; reuse the real types). Ranked by business impact.

---

## CRITICAL

### CC-01 — Feature Flags: full backend CRUD + check endpoints, zero consumers (entire feature is dead)
- **Type:** Endpoints with no consumer / missing admin UI. **GAP — needs a user story (Admin).**
- **Evidence:**
  - Backend complete: `Features/FeatureFlags/{CreateFeatureFlag, ToggleFeatureFlag, DeleteFeatureFlag, GetAllFeatureFlags, CheckFeatureFlag}.cs`; controller `Cleansia.Web.Admin/Controllers/AdminFeatureFlagController.cs:16-60` (GET/POST/toggle/delete/check) with dedicated policies `CanViewFeatureFlags / CanCreateFeatureFlag / CanToggleFeatureFlag / CanDeleteFeatureFlag`. Customer check at `Cleansia.Web.Customer/Controllers/FeatureFlagController.cs:16`.
  - No frontend consumer: `AdminFeatureFlagClient` and `FeatureFlagClient` appear **only** in generated clients (`libs/core/*/client/*-client.ts`). No facade/effect/component imports them; no `FEATURE_FLAG` route in `libs/core/services/src/lib/enums/routes.enum.ts:15-34`; no `.check(...)` caller anywhere in app code.
- **Impact:** Operators cannot create, toggle, view, or delete feature flags; nothing in the apps reads a flag, so no rollout/kill-switch capability actually works. A whole subsystem (entity + migration + commands + policies) ships unreachable. Violates "No dead code" and the long-term bar.
- **Fix:** Build the Admin Feature Flags management page (list with scope filter, create form, toggle, delete) consuming `AdminFeatureFlagClient`; add `FEATURE_FLAG` route + nav entry + 5-locale strings; then wire at least one real `check(...)` consumer (e.g. gate a booking/membership feature) so the flag actually does something. If flags are not meant for v1, the Architect should instead decide to remove the dead surface.
- **Size:** L · **Layers:** frontend (admin), light backend (confirm a consumer), i18n.

### CC-02 — Services & Packages hard-delete with no in-use guard (orphans historical orders / pay configs)
- **Type:** Missing referential-integrity flow; inconsistency between layers. **GAP — needs a user story (Admin).**
- **Evidence:**
  - `Features/Services/DeleteService.cs:15-38` — validator checks only `NotEmpty` + `ExistsAsync`; handler calls `serviceRepository.Remove(service!)` unconditionally. No `IsInUseAsync`.
  - `Features/Packages/DeletePackage.cs:15-39` — identical: existence-only, hard `Remove`.
  - Contrast: `DeleteCurrency.cs:32-34`, `DeleteLanguage.cs:26-28`, `DeleteCountry.cs:26-28` **all** call `IsInUseAsync` and block deletion when referenced (`ICurrencyRepository/ILanguageRepository/ICountryRepository.IsInUseAsync`). `IServiceRepository`/`IPackageRepository` have no such method.
  - `Service` references are real: `Service.IncludedInOrders` (`OrderService`) and `Package`/`PackageService` (`Service.cs:35-36`, `Package.cs:23-24`). Per-service pay rates live on `EmployeePayConfig`. Deleting a service used by a completed order or a pay config orphans those rows.
- **Impact:** An admin deleting a live service/package can break historical orders, receipts, and pay calculation (`basePay = services × serviceRate`), corrupting fiscal/payroll records. High blast radius; inconsistent with the deliberate guard pattern used everywhere else in this domain.
- **Fix:** Add `IsInUseAsync` to `IServiceRepository`/`IPackageRepository` (checks `OrderService` / `PackageService` / `EmployeePayConfig`) and block hard-delete when in use, matching Currency/Language/Country. Preferred long-term: replace destructive delete with **soft-deactivate** (see CC-03) and reserve hard-delete for never-used catalog rows. `BusinessErrorMessage.ServiceInUse / PackageInUse` + 5-locale strings.
- **Size:** M · **Layers:** backend, db (repo query), i18n; possibly frontend confirm copy.

### CC-03 — Service/Package "deactivated" state is documented and filtered-on but unreachable (no activate/deactivate)
- **Type:** Lifecycle state never reachable; documented-but-missing flow. **GAP — needs a user story (Admin).**
- **Evidence:**
  - Customer catalog deliberately hides inactive services: `GetServiceOverview.cs:17-23` filters `s.IsActive` with the comment *"Deactivated services are admin-only state and must not appear in the booking wizard catalog."* `ServiceSpecification.cs:20-23` supports an `IsActive` filter.
  - But nothing ever sets `IsActive=false`: `UpdateService.cs` Handler (lines 100-122) never touches `IsActive`; there is **no** Deactivate/Activate command in `Features/Services` or `Features/Packages`; `Auditable.Deactivated()` (`Auditable.cs:35-42`) is never called for these entities. `ServiceFilter.cs` exposes only `SearchTerm` (no active filter), and `GetPagedServices.cs:26-28` never passes `isActive`.
  - Admin facade only deletes: `service-management.facade.ts:103-120` (no activate/deactivate); same shape for packages.
- **Impact:** The only way to remove a service from the booking catalog is the dangerous hard-delete of CC-02 — there is no safe "retire this service but keep history" path, even though the system is explicitly built to support it. The admin list also can't filter active vs. retired. This is the half-built half of CC-02.
- **Fix:** Add `DeactivateService`/`ActivateService` (and Package equivalents) commands + endpoints; surface a status toggle and an active/inactive filter in the admin list (extend `ServiceFilter`/`PackageFilter` with `IsActive?`, pass it in the paged query). Make hard-delete the rare path and deactivate the default. `manual_step: nswag-regen`.
- **Size:** M · **Layers:** backend, frontend (admin), i18n. **MANUAL_STEP:** nswag-regen.

---

## MAJOR

### CC-04 — Default currency cannot be changed: `Currency.SetAsDefault()` exists but no command/endpoint/UI calls it
- **Type:** Command/UI missing for an existing domain capability; lifecycle field write-locked. **GAP — needs a user story (Admin).**
- **Evidence:** `Currency.SetAsDefault(bool)` exists (`Currency.cs:39-42`) and `IsDefault` is read in `DeleteCurrency.cs:29,50` (default is delete-protected), `GetCurrencyOverview.cs:18` (sorted first), and surfaced in `CurrencyListItem`/`CurrencyDetails`. But `CreateCurrency.cs` and `UpdateCurrency.cs` never set it, no `SetDefaultCurrency` command exists (grep: `SetAsDefault`/`SetDefault` only implemented for SavedAddresses, `SetDefaultSavedAddress.cs`), and `currency-management.facade.ts` has no set-default action.
- **Impact:** The platform's default currency is frozen at its seed value forever. You can create currencies but never promote one to default, and the default is delete-protected — a dead-end. Inconsistent with the SavedAddresses default pattern that does exist.
- **Fix:** Add `SetDefaultCurrency` command/endpoint (clears the prior default, sets the new one in one transaction) and a "Set as default" action in the currency list, mirroring `SetDefaultSavedAddress`. `manual_step: nswag-regen`.
- **Size:** S · **Layers:** backend, frontend (admin), i18n.

### CC-05 — Currency catalog is effectively cosmetic: prices/format are hardcoded to `'CZK'` across all apps
- **Type:** Endpoint/feature with no real consumer (data not used downstream). **GAP — needs a user story (cross-cutting; raise as question for owner first).**
- **Evidence:** Full Currency CRUD exists, but money formatting is hardcoded `currency: 'CZK'` in 20+ components/facades (e.g. `service-management.facade.ts:88-90`; matches across customer orders, partner invoices, admin reports/packages/pay-config — grep on `'CZK'`). No code reads the default `Currency.Code`/`Symbol` to drive formatting; `ExchangeRate` is never applied to any displayed/charged amount.
- **Impact:** Managing currencies (and CC-04's default) changes nothing user-visible — exchange rates and symbols are inert. For a multi-tenant/multi-country platform this is a real expansion blocker and makes the entire Currencies admin area misleading (operators think they're configuring pricing, but aren't).
- **Fix:** Decide the v1 currency story (owner question): either (a) drive a shared currency-format util from the default `Currency` and apply `ExchangeRate` at the display/charge boundary, or (b) explicitly scope currency to single-CZK for v1 and mark the admin area "display-only". Don't leave a config surface that silently does nothing. Append the policy question to `questions/open.md`.
- **Size:** L · **Layers:** frontend (all 3 apps), backend (expose default currency), shared util, i18n.

### CC-06 — No default-language concept despite a 5-language platform
- **Type:** Missing field/flow (fallback language unspecified). **GAP — needs a user story (Admin) + owner question.**
- **Evidence:** `Language.cs` has only `Code`/`Name` — no `IsDefault` (unlike `Currency`/`Country`). `CreateService`/`UpdateService` validators require a translation for **every** language (`CreateService.cs:67-74`), but nothing designates a fallback language to render when a translation is missing or when a new language is added after services already exist.
- **Impact:** Add a 6th language and every existing service/package immediately violates the "all languages required" rule with no migration path, and there's no defined fallback for partial data. Inconsistent with how Currency/Country model a "primary" row. Risk of blank service names in some locale.
- **Fix:** Owner decision (question to `questions/open.md`): introduce a default/fallback `Language.IsDefault` + a `SetDefaultLanguage` flow, OR formally document that translations are mandatory-for-all and define what happens when a language is added. Align the translation-completeness validation with that decision.
- **Size:** M · **Layers:** backend, db (migration), frontend (admin), i18n. **MANUAL_STEP:** ef-migration, nswag-regen.

### CC-07 — Countries: no `GetPagedCountries` (overview-only) and create/update validators not audited for ISO-code uniqueness
- **Type:** Inconsistency / half-built list flow. **Partial GAP.**
- **Evidence:** `Features/Countries` ships `GetCountryOverview` only — no paged query (contrast Services/Packages/Company/EmailTemplates which all have `GetPaged*`). The admin country list and service-area page load the **entire** catalog and even do one detail call per country to discover `IsServiced` (`service-area-management.facade.ts:64-80`, with a self-aware TODO-shaped comment "If it grows past a few hundred, swap to a dedicated `/admin-countries/serviced-ids` endpoint"). `IsServiced` is not on `CountryListItem`, forcing N+1 detail calls.
- **Impact:** Admin country UI is O(N) round-trips and won't scale; the "serviced-ids" endpoint the code anticipates doesn't exist. Lower business impact today (small catalog) but a known half-measure.
- **Fix:** Add `IsServiced` to `CountryListItem` (kills the N+1) and/or a dedicated `serviced-ids` endpoint; add `GetPagedCountries` for consistency if the list is meant to scale.
- **Size:** S/M · **Layers:** backend, frontend (admin). **MANUAL_STEP:** nswag-regen.

---

## MINOR

### CC-08 — Company info: multi-record CRUD plus a "legacy get-current", but no explicit "active company" selector
- **Type:** Half-built flow / dead-end ambiguity.
- **Evidence:** `AdminCompanyController.cs` exposes paged CRUD **and** `get-current` marked *"Legacy endpoint for backward compatibility"* (lines 91-101). `GetCompanyInfo.cs:21` resolves via `GetActiveCompanyInfoAsync()`. With multi-record CRUD now present, there is no command to choose **which** CompanyInfo is the active one used on invoices/receipts (the active selection appears implicit, not admin-controllable).
- **Impact:** If multiple company records exist (multi-tenant/rebrand), the admin can't deterministically pick the one stamped on fiscal documents — relies on whatever `GetActiveCompanyInfoAsync` infers. Fiscal-document correctness risk.
- **Fix:** Either add an explicit "set active company" command/flag surfaced in the list, or document/enforce single-active invariant and retire the legacy endpoint. Confirm intent with owner.
- **Size:** S · **Layers:** backend, frontend (admin).

### CC-09 — `AdminCodeController` uses bare `[Authorize]`, not a `Permission` policy (auth inconsistency)
- **Type:** Inconsistency between layers (not a functional gap, but a deviation).
- **Evidence:** `AdminCodeController.cs:12` guards `GetOverview` with `[Authorize]` only, while every other catalog-config admin controller uses `[Permission(Policy.CanView...)]` (e.g. `AdminCurrencyController.cs:16`). Codes (enum/code lookups) **is** consumed (`admin-code.effects.ts`, `code.effects.ts`, `catalog.effects.ts`) so not dead — but any authenticated admin can read it regardless of granular permission.
- **Impact:** Minor least-privilege deviation; breaks the uniform policy convention the Reviewer enforces (`conventions.md` "reuse the real types … `Policy.CanXxx`").
- **Fix:** Introduce/reuse a `CanViewCodes` policy (or fold under an existing config-view policy) for consistency, if codes are meant to be permission-gated.
- **Size:** S · **Layers:** backend.

---

## Summary table

| ID | Title | Sev | GAP? | Size | Layers |
|----|-------|-----|------|------|--------|
| CC-01 | Feature Flags fully built backend, zero consumers (no admin UI, no check caller) | Critical | Yes | L | FE/BE/i18n |
| CC-02 | Service/Package hard-delete with no in-use guard | Critical | Yes | M | BE/DB/i18n |
| CC-03 | Service/Package deactivate state filtered-on but unreachable | Critical | Yes | M | BE/FE/i18n |
| CC-04 | Default currency unsettable (`SetAsDefault` never called) | Major | Yes | S | BE/FE/i18n |
| CC-05 | Currency catalog inert — `'CZK'` hardcoded everywhere | Major | Yes | L | FE/BE/util |
| CC-06 | No default/fallback language on a 5-language platform | Major | Yes | M | BE/DB/FE |
| CC-07 | No `GetPagedCountries`; `IsServiced` causes admin N+1 | Major | Partial | S/M | BE/FE |
| CC-08 | Company info multi-record but no explicit active selector | Minor | Partial | S | BE/FE |
| CC-09 | `AdminCodeController` uses `[Authorize]` not `Policy.*` | Minor | No | S | BE |

**Not gaps (verified wired, called out to prevent re-flagging):** Country `IsServiced` / `SetCountryServiced` → consumed by `service-area-management.facade.ts`. Email Templates (`GetEmailTypes`/`Update`/`CreateEmailTemplateTranslation`/`SendTest`) → consumed by `template-management` facades (`email-type-detail.facade.ts:99,133,150`). Codes overview → consumed by admin/partner/customer effects.

**Proposed ticket titles (for PM):**
- CC-01: "Admin Feature Flags management page + wire a real flag consumer"
- CC-02: "Block Service/Package deletion when referenced by orders/packages/pay configs"
- CC-03: "Service/Package activate–deactivate (retire from catalog without hard delete)"
- CC-04: "Set default currency (command + admin action)"
- CC-05: "Drive money formatting from configured default currency (remove hardcoded CZK)"
- CC-06: "Default/fallback language model for catalog translations"
- CC-07: "Add IsServiced to CountryListItem / paged countries to kill admin N+1"
- CC-08: "Explicit active-company selection for fiscal documents"
- CC-09: "Gate AdminCodeController with a Policy permission"

**Key files:** `src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs:15-38`, `Features/Packages/DeletePackage.cs:15-39`, `Features/Services/GetServiceOverview.cs:17-23`, `Features/Services/UpdateService.cs:100-122`, `Features/Currencies/{CreateCurrency,UpdateCurrency,DeleteCurrency}.cs`, `src/Cleansia.Core.Domain/Internationalization/Currency.cs:39-42`, `Language.cs`, `Country.cs:17-24`, `Features/FeatureFlags/*`, `src/Cleansia.Web.Admin/Controllers/{AdminFeatureFlagController,AdminCodeController,AdminCompanyController}.cs`, `src/Cleansia.App/libs/cleansia-admin-features/{service-management,currency-management,country-management}/...facade.ts`, `src/Cleansia.App/libs/core/services/src/lib/enums/routes.enum.ts:15-34`.


# ===== employees SECURITY S =====

I now have a complete picture. `GetCallerEmployeeIdAsync` derives from the JWT (employee claim, then email lookup) — confirming the IDOR contrast: the safe dashboard endpoints resolve the caller's id server-side, while the three vulnerable ones trust the query string.

One more confirmation needed for the headline finding: that the partner `CanGetCurrentEmployee` permission is granted to ordinary employees (so any partner can hit these endpoints), and that the frontend indeed sends the caller's own id (meaning the server never validates it). The permission gate is identical (`CanGetCurrentEmployee`) on all dashboard endpoints, so any authenticated partner reaching `GetDashboardStats` can also reach `GetOrderAnalytics`. That's sufficient. Let me do a quick final check on the `DownloadEmployeeDocument` admin handler S10/IsActive (it checks IsActive) and confirm `GetMyDocuments` BlobUrl is a raw path not a credentialed URL (minor S4). I have enough. Let me write the report.

Based on my read of the actual code, here is the findings report.

---

# Security Audit — Employees / EmployeeDocuments / Dashboard (S1–S10)

Scope read: Admin/Partner/Mobile.Partner controllers + every command/query/handler/validator/DTO/mapper in `Features/Employees`, `Features/EmployeeDocuments`, `Features/Dashboard`. Tenancy filter in `CleansiaDbContext` and `OrderAccessService` confirmed.

Overall the self-service write path is solid: every `Update*` command and every `*MyDocument*` command derives the caller from the JWT (`IUserSessionProvider.GetUserEmail()`) and checks `employee.Id == command.EmployeeId` (or `document.EmployeeId == employee.Id`) in the validator. `Employee` and `EmployeeDocument` both implement `ITenantEntity` and are auto-scoped by the global filter (`CleansiaDbContext.cs:115+`). DTOs/mappers leak no `TenantId`/hash/Stripe id. The problems are concentrated in the **Dashboard analytics endpoints** and a few smaller gaps.

---

## SEC-EMP-01 — Any partner can read any other partner's order/time/productivity analytics (IDOR) — CRITICAL
- Type: Broken access control (S1 + S3 horizontal privilege escalation)
- Files:
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetOrderAnalytics.cs:18-32` (`Query.EmployeeId` required; handler calls `orderRepository.GetEmployeeOrdersByDateRangeAsync(request.EmployeeId, ...)` with no ownership/role check)
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetTimeAnalytics.cs:18-30` (same pattern, `GetCompletedOrdersByDateRangeAsync(request.EmployeeId,...)`)
  - `src/Cleansia.Core.AppServices/Features/Dashboard/GetProductivityMetrics.cs:19-46` (same; and `CalculatePersonalBestsAsync` at `:108-115` reads that employee's **invoices** via `GetByEmployeeAndDateRangeAsync(employeeId,...)`)
  - Exposed by `Cleansia.Web.Partner/Controllers/DashboardController.cs:55-86` and `Cleansia.Web.Mobile.Partner/Controllers/DashboardController.cs:65-96`, all gated only by `[Permission(Policy.CanGetCurrentEmployee)]` — the everyday partner permission.
- Concrete risk: `EmployeeId` is a `required` **query-string** parameter that the handler trusts verbatim. There is no validator and no `GetCallerEmployeeIdAsync` call (confirmed: only `GetOrderAnalytics/GetTimeAnalytics/GetProductivityMetrics` reference these queries; none reference the access service). Any authenticated partner can call `GET /api/Dashboard/GetOrderAnalytics?EmployeeId=<victimId>&...` (employee ids are visible in order DTOs) and read another cleaner's completed/cancelled order history, service mix, on-time rates, productivity, personal-best months, and — through ProductivityMetrics' invoice path — historical earnings totals. This is the textbook S3 failure the rules call out by name.
- Why it slipped through: the sibling endpoints `GetDashboardStats.cs:36-48` and `GetEarningsAnalytics.cs:29-48` do it correctly — they role-gate (`role == Administrator` ⇒ trust `query.EmployeeId`, else `orderAccessService.GetCallerEmployeeIdAsync()`). These three were never converted to that pattern. The web/mobile clients always send the caller's own id, so it never surfaced in normal use.
- Long-term-correct fix: make the three handlers ignore any client `EmployeeId` for non-admins and resolve it from `IOrderAccessService.GetCallerEmployeeIdAsync()` exactly like `GetDashboardStats`/`GetEarningsAnalytics`; for admin callers keep the role-gated `query.EmployeeId`. Drop `required` from the query field (it becomes server-enriched). Add a shared validator or guard so a non-admin passing a foreign id gets NotFound. Cover with a "partner-cannot-read-foreign-analytics" test.
- Size: S. Layers: backend (+ nswag-regen manual_step since `EmployeeId` stops being required on the DTO). Functional GAP: no — it's a regression-style hole, but the fix should be tracked as a security ticket/story.

## SEC-EMP-02 — Side-effecting partner endpoints (file upload + profile writes) are not rate-limited — MAJOR
- Type: Missing rate limit (S5)
- Files: `Cleansia.Web.Partner/Controllers/EmployeeController.cs` (`SaveMyDocuments` :51, `UpdateEmployee` :40) and `Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs` (`SaveMyDocuments` :110, plus all `Update*` :39-108). Confirmed only `AuthController` and `GdprController` carry `[EnableRateLimiting("auth")]` in both partner hosts.
- Concrete risk: `SaveMyDocuments` accepts a `List<DocumentToSave>` of base64 file payloads and writes each to blob storage (`SaveMyDocuments.cs:112-184`) with no per-user throttle. A single authenticated cleaner can drive unbounded blob writes / storage cost and DB rows (auto-versioning creates a new `EmployeeDocument` per call). `UpdateEmployee` similarly runs geocoding + blob uploads per call.
- Fix: add a narrow per-user limiter (the rules' "mutations that cost money or send email" class) — e.g. `[EnableRateLimiting("uploads")]` on `SaveMyDocuments` and a modest per-user window on the profile writes. Also enforce a max document count / total payload size per request in `SaveMyDocuments.Validator`.
- Size: S. Layers: backend (host config + controller attributes). Functional GAP: no.

## SEC-EMP-03 — `SaveMyDocuments` document write is non-idempotent — MAJOR
- Type: Idempotency on a doublable side-effect (S7)
- File: `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs:112-184`
- Concrete risk: each call uploads to a GUID-suffixed blob path (`{employeeId}_{type}_{timestamp}_{guid}`) and adds a new `EmployeeDocument` row, auto-incrementing the version when a filename already exists. A double-tap, mobile retry, or pipeline retry produces duplicate blobs and a spurious new "version" of the same file — admins then review near-identical duplicates, and storage grows. There is no request/content de-dup check (contrast the S7 reference patterns that check a ledger/transaction id before acting).
- Fix: de-duplicate by (employeeId, filename, content hash) within a short window, or require a client-supplied idempotency key, before creating a new version. At minimum skip creating a new version when the latest active version has an identical content hash.
- Size: M. Layers: backend (+ domain helper for content hash). Functional GAP: partial — versioning exists but lacks a dedupe guard.

## SEC-EMP-04 — Admin can re-approve / re-reject an already-decided document (state-machine / idempotency gap) — MINOR
- Type: Missing state guard (S7-adjacent)
- Files: `EmployeeDocuments/ApproveDocument.cs:30-34` and `RejectDocument.cs:30-34` — validators only check existence; the handlers call `document.Approve(...)` / `document.Reject(...)` unconditionally.
- Concrete risk: an already-Approved document can be re-approved (re-stamping `ReviewedByUserId`/`ReviewedAt`/notes) or flipped Approved→Rejected with no guard, overwriting the original audit trail. Note `ApproveEmployee.cs:25-32` and `RejectEmployee.cs:22-29` *do* guard against re-decision — documents are inconsistent with that established pattern.
- Fix: add a validator rule requiring `document.Status == Pending` (mirroring `EmployeeAlreadyApproved`) before approve/reject; introduce `Document.AlreadyReviewed` error keys (all 5 locales).
- Size: S. Layers: backend (+ i18n manual_step). Functional GAP: no.

## SEC-EMP-05 — Admin upload-on-behalf and admin delete-document commands are built but unreachable — MAJOR (functional GAP)
- Type: Half-built feature; authorization-relevant because admins currently cannot manage cleaner documents server-side
- Files: `EmployeeDocuments/UploadEmployeeDocument.cs`, `EmployeeDocuments/UploadNewDocumentVersion.cs`, `EmployeeDocuments/DeleteDocument.cs` — confirmed **not** referenced by any controller (`AdminEmployeeDocumentController` exposes only get-paged/approve/reject/versions/download).
- Concrete risk: not an exploit, but a security-relevant gap — admins have no way to remove a fraudulent/illegal document a cleaner uploaded (only the cleaner can, via `DeleteMyDocument`, and only if it isn't Approved). Also `DeleteDocument` (admin) has **no** ownership/status guard in its validator; if it is later wired up as-is it's fine for admins but must not be exposed on a partner host.
- Fix: decide product intent. If admin document management is in scope, wire these to `AdminEmployeeDocumentController` under `CanApproveEmployeeDocument`/a new `CanManageEmployeeDocument` policy with a proper file pipeline; otherwise delete the dead commands. Flag which host before exposing `DeleteDocument`.
- Size: M. Layers: backend + admin frontend (+ nswag-regen). Functional GAP: yes — needs a user story ("Admin manages cleaner documents").

## SEC-EMP-06 — Admin can reassign a cleaner's `WorkCountryId` only at approval; no post-approval admin update path verified — MINOR (functional GAP, note)
- Type: Coverage note around `AdminUpdateEmployee`/`AdminUpdateEmployeeAvailability`
- Files: `AdminEmployeeController.cs:80-91` (`UpdateEmployee` enriches `EmployeeId` from route — correct S1), `:67-78` (availability). These are admin-permission-gated and operate cross-employee by design (no ownership check needed). No S1–S4 defect found.
- Concrete risk: none security-critical; flagged only so the reviewer/PM knows `AdminUpdateEmployee` was checked and is clean (route id overrides body id at `:88`, so a body `EmployeeId` cannot redirect the write).
- Size: n/a. Included for completeness.

## SEC-EMP-07 — `GetMyDocuments` returns raw blob path as `BlobUrl` — MINOR
- Type: DTO field semantics / potential S4 (info leak / broken link)
- File: `EmployeeDocuments/GetMyDocuments.cs:74` — `BlobUrl = d.FilePath` with comment "Will be converted to full URL by blob service," but no conversion happens in the handler.
- Concrete risk: the client receives the internal storage virtual path (container layout, `{employeeId}/...`) rather than a time-limited SAS URL. It exposes internal storage structure and, if the container were ever mis-permissioned to public, the path is a ready-made key. Low severity because the path alone isn't a credential and the real download goes through `DownloadMyDocument` (which is ownership-checked). Note `SaveMyDocuments.cs:136` *does* return a real `GetBlobUri` url — inconsistent.
- Fix: either resolve `FilePath` to a short-lived SAS URL in the handler, or drop `BlobUrl` from the DTO and have clients always use the `DownloadMyDocument` endpoint.
- Size: S. Layers: backend. Functional GAP: no.

---

## Items explicitly checked and PASSING
- S1/S3 self-writes: `UpdateEmployee`, `UpdatePersonalInfo`, `UpdateIdentificationInfo`, `UpdateAddressInfo`, `UpdateBankDetails`, `UpdateEmergencyContact`, `UpdateAvailability` — all enforce `AllowedToUpdateEmployee` (JWT-email ⇒ owned employee) in the validator. `DeleteMyDocument`/`DownloadMyDocument`/`SaveMyDocuments` enforce `document.EmployeeId == caller's employee.Id`.
- S2: every controller method in all 4 controllers carries a `[Permission(...)]` attribute.
- S4: `EmployeeItem`, `EmployeeListItem`, `AdminEmployeeDetail`, `AdminEmployeeListItem`, `EmployeeDocumentItem`, dashboard DTOs — no `TenantId`, password hash, or Stripe id. IBAN/PassportId/VAT appear only on self (own data) and admin DTOs (privileged) — acceptable per S4's documented-intent carve-out.
- S6: no `ILogger`/`Log*` calls anywhere in the three feature folders — no PII logging.
- S8: `Employee` and `EmployeeDocument` implement `ITenantEntity`; global filter auto-applied via convention in `CleansiaDbContext.cs:115+`. No `FromSqlRaw`/`IgnoreQueryFilters` in these features.
- S10: `GetMyDocuments` requests `includeInactive:false` (`:63`); `DownloadMyDocument`/`DownloadEmployeeDocument` validators require `IsActive:true`; `GetPagedEmployees` honors `Filter.IsActive`; `GetEmployeeDetail` intentionally shows all rows to admins (correct per S10).

## Priority order
1. SEC-EMP-01 (critical, IDOR analytics leak) — block any Dashboard-touching change until fixed.
2. SEC-EMP-02 (rate-limit on file upload + writes).
3. SEC-EMP-03 (upload idempotency).
4. SEC-EMP-05 (admin doc-management gap — needs a story).
5. SEC-EMP-04, SEC-EMP-07, SEC-EMP-06 (minor).

Relevant files (all absolute):
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Dashboard\GetOrderAnalytics.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Dashboard\GetTimeAnalytics.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Dashboard\GetProductivityMetrics.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Dashboard\GetDashboardStats.cs` / `GetEarningsAnalytics.cs` (correct reference pattern)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Partner\Controllers\DashboardController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Mobile.Partner\Controllers\DashboardController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\EmployeeDocuments\SaveMyDocuments.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\EmployeeDocuments\ApproveDocument.cs` / `RejectDocument.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\EmployeeDocuments\UploadEmployeeDocument.cs` / `UploadNewDocumentVersion.cs` / `DeleteDocument.cs` (unreachable)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\EmployeeDocuments\GetMyDocuments.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\OrderAccessService.cs`


# ===== AppHost/Aspire wiring, secrets, CORS, host exposure =====

Confirmed. CSRF is `Enabled: false` everywhere including all three Production files, and nothing overrides it. The cookie-based web hosts (Admin/Partner/Customer) register CSRF opt-out paths and `UseCsrfValidation()` middleware, but the middleware no-ops because the flag is off — so cookie-auth SPAs ship with zero CSRF protection in production.

I have a comprehensive picture. Let me compile the findings report.

## SECURITY AUDIT — Blind-Spot Pass: AppHost/Aspire, Secrets, CORS, Host Exposure, Rate-Limiter Wiring

Scope: `src/Cleansia.AppHost/Program.cs`, all 5 web-host `Program.cs`/`Startup.cs`, `Cleansia.Config` (base startup, CSRF, JWT), `Cleansia.ServiceDefaults`, per-host `appsettings*.json`, and the policy/permission wiring that decides which audience can hit which mutation. The repo now has **6** hosts (Admin 5001, Partner 5000, Customer 5003, Mobile.Partner 5002, Mobile.Customer 5004) — CLAUDE.md is stale (lists 4).

Findings are ranked by impact. IDs are stable (`BSP-` = blind-spot pass).

---

### BSP-1 — Entire payroll/pay-config/pay-period permission family falls through to "any authenticated user" (privilege escalation + doubled financial side-effect)
- Severity: **Critical** | Type: AuthZ / privilege escalation / financial | GAP: No (regression to fix)
- Files: `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:204-205` (the `GetValueOrDefault(permission, PhysicalPolicy.Authenticated)` fallback) and the `Map` dictionary lines 6-202, which **contain no entry** for any of: `CanViewPagedInvoices`, `CanViewPeriodPays`, `CanCalculateOrderPay`, `CanGenerateInvoice`, `CanApproveInvoice`, `CanMarkInvoicePaid`, `CanCancelInvoice`, `CanClosePayPeriod`, `CanViewPayPeriods`, `CanViewPayPeriod`, `CanCreatePayPeriod`, `CanUpdatePayPeriod`, `CanOpenPayPeriod`, `CanDeletePayPeriod`, `CanViewPayConfigs`, `CanViewPayConfig`, `CanCreatePayConfig`, `CanUpdatePayConfig`, `CanDeletePayConfig`.
- Mechanism: `PermissionAttribute` (`src/Cleansia.Web.Partner/Attributes/PermissionAttribute.cs:7`) resolves the policy via `permission.ToPhysicalPolicy()`. Because these permissions are unmapped, every one resolves to `PhysicalPolicy.Authenticated`, which `AddUserAuthorization` registers as `RequireAuthenticatedUser()` only (`src/Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:203-204`). `Policy.cs:74-97` comments claim `// Admin`, but the comments are not enforced anywhere.
- Impact: On the **Partner host (5000)**, where ordinary cleaners legitimately authenticate (Employee profile, `cleansia.partner` audience), any authenticated employee can call `PayConfigController` (`CreatePayConfig`/`UpdatePayConfig`/`DeletePayConfig` — set their own pay rates), `PayPeriodController` (`OpenPayPeriod`/`ClosePayPeriod`/`DeletePayPeriod`), and `EmployeePayrollController` (`GenerateInvoice`/`ApproveInvoice`/`MarkInvoicePaid`/`CancelInvoice`). The handlers do **no** internal role/ownership check: `MarkInvoicePaid.Handler` (`MarkInvoicePaid.cs:56-62`), `ApproveInvoice.Handler` (`ApproveInvoice.cs:55-62`), and `GenerateInvoice.Handler` (`GenerateInvoice.cs:73-105`) all trust the attribute and take an arbitrary `EmployeeId` from the body. A cleaner can therefore self-set pay rates, self-generate an invoice for any employee id, self-approve it (the approver email stamped is their own — `ApproveInvoice.cs:58-59`), and mark it paid — a complete, audited-to-the-attacker payroll fraud chain.
- Long-term fix: add explicit `Map` entries (`AdminOnly` for all create/update/delete/approve/markpaid/close; `EmployeeOrAdmin` + handler-level self-scoping for read-own); change the `ToPhysicalPolicy` fallback to **fail closed** (return a never-satisfiable policy or throw at startup for unmapped permissions) so a forgotten mapping is a 403/boot failure, not an open door. Add a startup assertion that every `Policy.*` constant has a `Map` entry. Add defense-in-depth role checks in the mutating handlers.
- Size: M | Layers: backend (AppServices auth, payroll handlers), config

### BSP-2 — Cross-employee payroll/PII read leak on Mobile.Partner via the same unmapped fallback
- Severity: **High** | Type: AuthZ / DTO leak (S3/S4) | GAP: No
- Files: `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeePayrollController.cs:16-55` (`GetPagedInvoices`, `GetInvoiceById`, `DownloadInvoice`, all `[Permission(Policy.CanViewPagedInvoices)]`), same root cause as BSP-1.
- Impact: `CanViewPagedInvoices` is unmapped → `Authenticated`. `GetPagedInvoices.Handler` paging is not employee-scoped at the attribute level. `GetInvoiceById` is saved by an in-handler role/owner check (`GetInvoiceById.cs:57-66`), but `GetPagedInvoices` must be verified to self-scope; if it lists all employees' invoices, any mobile partner user reads every employee's pay totals and PII. Confirm `GetPagedInvoices`/`DownloadInvoice` scope to caller's `EmployeeId`.
- Long-term fix: covered by BSP-1's mapping fix plus per-handler self-scoping; until then these are an unfiltered cross-tenant/cross-employee read.
- Size: S | Layers: backend

### BSP-3 — CSRF protection disabled in production on all three cookie-auth web hosts
- Severity: **High** | Type: CSRF / session-riding | GAP: No
- Files: `src/Cleansia.Web.Admin/appsettings.Production.json:24-26`, `src/Cleansia.Web.Partner/appsettings.Production.json:24-26`, `src/Cleansia.Web.Customer/appsettings.Production.json:24-26` — all `"Csrf": { "Enabled": false }`. No environment/user-secret override exists; Development files don't set it either (`src/Cleansia.Web.Customer/appsettings.Development.json` has no `Csrf` block).
- Mechanism: Admin/Partner/Customer hosts authenticate via HttpOnly cookies (`admin_token`/`partner_token`/`customer_token`, set in each `Startup.cs` and read in the JWT `OnMessageReceived` cookie fallback). They register CSRF opt-out paths and `app.UseCsrfValidation()` (`Startup.cs` of each). But `CsrfValidationMiddleware.InvokeAsync` short-circuits when `!_options.Enabled` (`src/Cleansia.Config/Authentication/CsrfValidationMiddleware.cs:48`), so with cookie auth + CORS `AllowCredentials()`, every state-changing endpoint is reachable cross-site with the victim's ambient cookie. CORS limits *script-readable* responses to the fixed origin list, but does not block the side-effecting request itself (simple/`POST` form-style requests fire regardless).
- Long-term fix: set `Csrf:Enabled=true` in all three Production files (owner manual step — do not edit prod secrets, but `Enabled` is a non-secret flag the owner must flip), provision `Csrf:Secret` via secrets, and add an integration test asserting a state-changing cookie-auth request without `X-CSRF-Token` returns 403 in a production-like config.
- Size: S (config) + verification | Layers: config, backend; **flag as owner `manual_steps`**

### BSP-4 — Rate limiters are global (no per-user/per-IP partition) — brute-force and abuse throttle is trivially bypassed and self-DoS-prone
- Severity: **High** | Type: Rate-limiting wiring (S5) | GAP: No
- Files: `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:76-92` — both `AddFixedWindowLimiter("auth", …)` (10/min) and `AddFixedWindowLimiter("interactive", …)` (60/min) are plain fixed-window limiters with **no partition key**. A bare `AddFixedWindowLimiter` is a single process-wide counter shared by all callers.
- Impact: The "auth" window is meant as a brute-force defense (login/register/forgot-password/refresh, and the side-effecting `CreateOrder`/`CreatePaymentIntent`). With one global 10/min bucket: (a) an attacker brute-forcing one account consumes the bucket for the whole host, locking out all legitimate users (DoS), and (b) the per-account brute-force ceiling is meaningless — the limit isn't per identity. The intended S5 semantics ("10 req/min/partition") are documented in `security-rules.md` but not implemented.
- Long-term fix: replace with `PartitionedRateLimiter` partitioned by client IP for anonymous auth routes and by `userId` claim for authenticated mutations (e.g. `RateLimitPartition.GetFixedWindowLimiter(partitionKey: …)`). Add `QueueProcessingOrder`/`429` retry-after. Keep the webhook unlimited (it already is).
- Size: M | Layers: config (shared startup), backend

### BSP-5 — Swagger UI + OpenAPI exposed on every non-Production environment (Staging)
- Severity: **Medium** | Type: Information exposure | GAP: No
- Files: `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:110-118` — `if (!env.IsProduction()) { UseSwagger(); UseSwaggerUI(...) }`.
- Impact: Any environment that isn't exactly `Production` (Staging, QA, Demo, or a mis-set `ASPNETCORE_ENVIRONMENT`) publishes the full API surface and schemas — including the admin host's endpoints — unauthenticated. If staging is internet-reachable, this hands an attacker the complete endpoint/DTO map (and pairs with BSP-1/BSP-3 for direct exploitation). The single-point dependency on the env string is fragile: a misconfigured prod env var silently exposes Swagger.
- Long-term fix: gate Swagger on an explicit allow-list (`Development` only) or behind authentication, and add a startup guard that refuses to serve Swagger when `CorsOrigins` contains a public `cleansia.cz` origin.
- Size: S | Layers: config

### BSP-6 — `ToPhysicalPolicy` fail-open default is a systemic latent hole beyond payroll
- Severity: **Medium** (structural; the concrete instances are BSP-1/BSP-2) | Type: AuthZ design | GAP: No
- Files: `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:205`.
- Impact: Any future `[Permission(Policy.CanXxx)]` added without a `Map` entry silently becomes "any authenticated user" on whichever host exposes it. This is exactly how BSP-1 happened. It will recur. (Note the Admin host masks the symptom only because its `cleansia.admin` audience is held only by Administrators — a thin accidental defense, not a designed one.)
- Long-term fix: same as BSP-1's structural half — fail closed + startup completeness assertion. Tracked separately because it warrants a guardrail/test even after BSP-1's data is fixed.
- Size: S | Layers: backend

### BSP-7 — Admin host does not register `CustomerOnly` policy; shared `PolicyBuilder` maps several permissions to it
- Severity: **Low/Medium** | Type: Config correctness / latent 500 | GAP: No
- Files: Admin `AddUserAuthorization` (`src/Cleansia.Web.Admin/Extensions/ServiceExtensions.cs:200-231`) registers only `Authenticated`, `EmployeeOrAdmin`, `AdminOnly`, `OwnerOrElevated` — not `CustomerOnly`. The shared `PolicyBuilder.Map` resolves `CanCancelOrder`, `CanSubmitOrderReview`, `CanManageMembership`, etc. to `CustomerOnly`.
- Impact: If any admin controller ever references a `CustomerOnly`-mapped permission, the authorization middleware throws "No policy found" (500), not a clean 403. Today it's latent (admin controllers don't use those permissions), but it's a sharp edge created by sharing one global map across hosts with divergent registered policy sets.
- Long-term fix: register the full `PhysicalPolicy` set on every host (or host-scope the map). Add a startup check that every value in `PolicyBuilder.Map` is a policy registered on that host.
- Size: S | Layers: backend (per-host startup)

### BSP-8 — JWT signing key is shared across all audiences; only the `aud` claim separates trust domains
- Severity: **Low** (currently sound) | Type: Auth design note | GAP: No
- Files: each host's `AddJwt` (`ValidAudience = JwtAudiences.Admin/Partner/Customer/Mobile`) with a common `JwtSettings:Secret` and `ValidIssuer = "cleansia"`. Audience binding is enforced both at validation and at issuance (`PartnerLogin`/`AdminLogin`/`RefreshToken` stamp `hostAudience.Audience`, and `RefreshToken.cs:61-66` re-checks `RequiredAudience`).
- Impact: Isolation holds today. The risk is structural: a single leaked `JwtSettings:Secret` forges tokens for *every* audience including Admin. With one shared HMAC key, audience is the only wall. Acceptable, but worth documenting and, longer-term, splitting the admin signing key from the customer/partner key so an admin-token forge requires a separately-held secret.
- Long-term fix: separate signing key (or asymmetric keys) for the Admin audience; document the audience-isolation invariant.
- Size: M (key rotation = owner-only) | Layers: config; **escalate key changes to owner**

### BSP-9 — Anonymous `Order/LookupBatch` returns tenant-scoped order data with a batch shape
- Severity: **Low/Medium** (needs handler verification) | Type: S3/anonymous-data-exposure | GAP: Possibly
- Files: `src/Cleansia.Web.Customer/Controllers/OrderController.cs:29-37` (`[AllowAnonymous] LookupOrderBatch`). Single `Lookup` requires `orderNumber`+`email` (a shared-secret pair, OK). The batch variant must enforce the same per-item secret and a length cap; otherwise it is an enumeration surface that bypasses the global tenant filter (anonymous = no tenant claim, per `security-rules.md` S3). Outside the strict AppHost/CORS scope, flagged for the order-subsystem audit to verify `LookupOrderBatch.Query` validates each `(orderNumber,email)` pair and caps batch size.
- Size: S | Layers: backend

---

### Passes (verified clean in this pass)
- **Secrets**: no real secrets committed — all sensitive values are `SET_VIA_USER_SECRETS`/`SET_VIA_SECRETS` placeholders in `appsettings*.json` across all hosts (JWT, Stripe, SendGrid, CSRF, connection strings). SendGrid template IDs and the `it@cleansia.cz` from-address are non-secret. (S-config PASS.)
- **CORS**: each host uses a fixed `CorsOrigins` allow-list with `AllowCredentials()` (incompatible with `AllowAnyOrigin`, correctly avoided) — `CleansiaStartupBase.cs:52-67`. Prod origins are the correct per-host domains. PASS.
- **Audience separation**: per-host `ValidAudience` is distinct and enforced at both issuance and refresh. Mobile.Customer correctly uses `JwtAudiences.Customer` (matches AppHost comment); Mobile.Partner uses `JwtAudiences.Mobile`. PASS.
- **Stripe webhook placement/exposure**: `[AllowAnonymous]`, raw-body signature read, CSRF opt-out, and **not** rate-limited (so Stripe retries aren't throttled) — `Customer/PaymentController.cs:43-58`. PASS.
- **Mobile hosts**: bearer-only, no cookie fallback, no CSRF surface — correct (`Mobile.Partner/Extensions/ServiceExtensions.cs`). PASS.
- **Migrations/seeding**: auto-migrate + raw-SQL seed only run when `IsDevelopment()`; prod migrations are pipeline-applied (`CleansiaStartupBase.cs:148-166`). PASS.
- **Sentry**: `SendDefaultPii = false` (`ServiceDefaults/Extensions.cs:85`). PASS.

### Cross-cutting note
CLAUDE.md and the Aspire `Program.cs` comment are **stale/contradictory** vs. the code (4 hosts claimed; 6 exist; AppHost comment about customer-mobile audience). Not a vulnerability, but it misled the prior audit — recommend a docs ticket.

---

### Proposed tickets (one per actionable finding)
1. **BSP-1 (Critical):** "Map all payroll/pay-config/pay-period permissions and make `ToPhysicalPolicy` fail closed" — blocks merge of anything touching those controllers until fixed.
2. **BSP-2 (High):** "Self-scope Mobile.Partner invoice list/download to the calling employee."
3. **BSP-3 (High):** "Enable CSRF in production for Admin/Partner/Customer hosts (+ secret provisioning)" — owner `manual_steps`.
4. **BSP-4 (High):** "Partition the `auth`/`interactive` rate limiters by IP/userId."
5. **BSP-5 (Medium):** "Restrict Swagger to Development-only (or auth-gate it)."
6. **BSP-6 (Medium):** "Add startup assertion: every `Policy.*` has a `PolicyBuilder.Map` entry; fail boot otherwise."
7. **BSP-7 (Low/Med):** "Register full `PhysicalPolicy` set on every host; validate map coverage per host."
8. **BSP-8 (Low):** "Separate Admin JWT signing key from customer/partner; document audience-isolation invariant" — key change owner-only.
9. **BSP-9 (Low/Med):** "Verify `Order/LookupBatch` enforces per-item secret + batch cap" — route to order-subsystem audit.

**Top risk to block on:** BSP-1. It is reachable today by any partner-app cleaner, requires no special tooling, hits real money (invoices/pay rates), and the handlers provide zero defense-in-depth. Do not merge any change that adds a `[Permission]` to the Partner host until the fail-open default in `PolicyBuilder.cs:205` is closed.

Key files: `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\PolicyBuilder.cs`, `...\Authentication\Policy.cs`, `...\Authentication\PhysicalPolicy.cs`, `...\Features\EmployeePayroll\MarkInvoicePaid.cs`, `...\ApproveInvoice.cs`, `...\GenerateInvoice.cs`, `...\Cleansia.Config\Abstractions\CleansiaStartupBase.cs`, `...\Cleansia.Config\Authentication\CsrfValidationMiddleware.cs`, each host's `Startup.cs` / `Extensions\ServiceExtensions.cs`, all `appsettings.Production.json`, `...\Cleansia.AppHost\Program.cs`, `...\Cleansia.ServiceDefaults\Extensions.cs`.


# ===== loyalty-growth SECURITY S =====

I have all the information I need. The `ValidateReferral` anonymous path uses `userRepository.GetByIdAsync` (tenant-filtered, not the IgnoreTenant variant) but with no tenant claim the filter resolves to `TenantId == null` only — meaning in single-tenant mode it works, and in multi-tenant mode it returns null first name (degraded, not a leak). Only first name is exposed, which is documented-acceptable. Not a finding.

Let me compile the findings report. I have a thorough, evidence-based picture of all 10 dimensions across the loyalty-growth domain.

## Security Audit — Loyalty-Growth Domain (Loyalty, Referrals, PromoCodes, Memberships, Marketing)

Scope swept: 5 customer controllers (web + mobile), 5 admin controllers, all handlers/commands/queries in `Features/{Loyalty,Referrals,PromoCodes,Memberships,Marketing,Codes}`, the 3 coordinating services (`LoyaltyService`, `ReferralService`, `PromoCodeService`), the Stripe subscription webhook, and the EF entity configs + repositories. The Stripe order/subscription webhook is signature-verified and idempotency-gated via `ProcessedStripeEvent` (S7 solid there); customer userId is consistently derived from `IUserSessionProvider.GetUserId()` (S1 clean across the domain); every endpoint carries a `[Permission]`/`[AllowAnonymous]` (S2 clean); code-uniqueness indexes are correctly `(TenantId, Code)` and all entities implement `ITenantEntity` (S8 largely clean). The issues below are real and ranked by impact.

---

### LG-SEC-01 — Single-use promo codes can be redeemed past the per-user cap via a concurrency race
- Severity: critical | Type: S7 (idempotency / non-atomic side-effect) | Size: M
- Files: `src/Cleansia.Core.AppServices/Services/PromoCodeService.cs:122-124` (and `:49-51`); `src/Cleansia.Infra.Database/EntityConfigurations/PromoCodeRedemptionEntityConfiguration.cs:54-55`
- Concrete risk: `ApplyAsync` enforces the per-user cap with a read-then-write (`CountForUserAndCodeAsync(userId, codeId) >= MaxRedemptionsPerUser`) and the backing index `HasIndex(r => new { r.PromoCodeId, r.UserId })` is **not unique**. Two orders placed near-simultaneously by the same user (double-submit, or two devices) both pass the count check before either commits, so a `MaxRedemptionsPerUser = 1` code is redeemed twice — each granting a real discount on a real paid order. The only unique guard is on `OrderId` (`:59-60`), which stops the *same* order double-applying but not two different orders. `GlobalMaxRedemptions` (`PromoCodeService.cs:170-172` via `CurrentRedemptionsCount`) has the same read-then-increment race and no DB ceiling.
- Long-term-correct fix: add a unique index `(TenantId, PromoCodeId, UserId)` per redemption-slot (or `(PromoCodeId, UserId, slotOrdinal)` when `MaxRedemptionsPerUser > 1`) and let the `DbUpdateException` map to `PerUserLimitReached`; for the global cap, do a conditional `UPDATE ... WHERE CurrentRedemptionsCount < GlobalMaxRedemptions` and treat 0 rows affected as the limit being hit. Manual step: `ef-migration`.
- GAP: no — hardening of an implemented flow.

### LG-SEC-02 — Mobile direct-subscribe path creates a real Stripe subscription with no idempotency key, so a double-tap double-charges
- Severity: critical | Type: S7 | Size: M
- File: `src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs:79-94`
- Concrete risk: in the `PaymentMethodConfirmed == true` branch a **fresh** `Guid.NewGuid()` attempt id is generated per call (`:84`) and passed to `stripeClient.CreateSubscriptionAsync`, by design so re-subscribes aren't replayed. But there is no application-level guard against two in-flight requests for the same user: the only protection is `GetActiveForUserAsync(...) != null` at `:57`, evaluated before the Stripe call and before commit. Two concurrent `Subscribe` calls (double tap on the mobile PaymentSheet confirm) both see "no active membership", both call Stripe with different idempotency keys, and create **two** subscriptions and two `UserMembership` rows — the customer is billed twice. `UserMembership.StripeSubscriptionId` is unique but the two subs have different ids, so the DB index doesn't catch it.
- Long-term-correct fix: gate the confirmed-subscribe path on a per-user idempotency token supplied by the client (reuse it as the Stripe idempotency key) or take a short-lived per-user advisory lock / unique "pending subscription" row so concurrent confirms collapse to one. The `subscription.created` webhook already reconciles by `StripeSubscriptionId`, so the durable path should be: create via Checkout/webhook only, and make the mobile direct-create idempotent on a client token.
- GAP: no — implemented but unsafe.

### LG-SEC-03 — `CreateMembershipSubscription.Response` leaks the raw Stripe customer id to the client
- Severity: major | Type: S4 (DTO leak) | Size: S
- Files: `src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs:17-21, 103, 113`
- Concrete risk: the response record exposes `StripeCustomerId` (`cus_...`) to both the web and mobile customer apps. S4 explicitly lists "Stripe customer/subscription ids" as must-not-leak. A Stripe customer id is a stable cross-session identifier and an attack-surface enabler (it scopes ephemeral keys); it has no client-side use here beyond what the SetupIntent client secret + ephemeral key already provide. `MembershipId` (an internal ULID) is also returned but is lower-risk.
- Long-term-correct fix: drop `StripeCustomerId` from the response; the mobile PaymentSheet only needs `SetupIntentClientSecret` + `EphemeralKey`. Manual step: `nswag-regen` (DTO contract change — removing a field is breaking for stale generated clients, so coordinate per S9).
- GAP: no.

### LG-SEC-04 — Stripe customer id logged at Information level on every first-time subscribe/checkout
- Severity: major | Type: S6 (PII/payment detail in logs) | Size: S
- Files: `src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs:74-76`; `src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipCheckoutSession.cs:71-73`
- Concrete risk: `logger.LogInformation("Created Stripe customer {StripeCustomerId} for user {UserId} ...", stripeCustomerId, user.Id)`. S6 names "payment/Stripe detail" as forbidden above Debug. The `cus_...` id lands in Information-level logs/log-sink for every new subscriber. Lower-impact than email but still a payment-system identifier in shared logs.
- Long-term-correct fix: log `user.Id` only at Information; move the Stripe id to `LogDebug`, or omit it. The `StripeSubscriptionId` logged at `CreateMembershipSubscription.cs:97`, `SwapMembershipPlan.cs:73`, and `StripeSubscriptionWebhookHandler.cs:67` is the same class of leak — sweep all of them.
- GAP: no.

### LG-SEC-05 — `GetMembershipPlans` and customer loyalty tier reads are `ITenantEntity`-scoped but served on an `[AllowAnonymous]` route → silently empty in multi-tenant mode (S8/S3 correctness, with an over-broad-read footgun)
- Severity: major | Type: S8 / S3 (anonymous + tenant filter interaction) | Size: M
- Files: `src/Cleansia.Web.Customer/Controllers/MembershipController.cs:58-65` and mobile `:58-65`; `GetMembershipPlans.cs:42`; entity `MembershipPlan : ... ITenantEntity`
- Concrete risk: `MembershipPlan` implements `ITenantEntity`, so the global filter applies. On the `[AllowAnonymous]` `GetPlans` route there is no tenant claim, so `GetCurrentTenantId()` is null and the filter collapses to `TenantId == null`. In any multi-tenant deployment the anonymous marketing page therefore returns **only** null-tenant plans (wrong/empty), while the authenticated subscribe flow sees the correct tenant's plans — an inconsistency that will surface as "plans missing on the public page". The inverse footgun: if a future plan row is seeded with `TenantId == null` as a "shared" plan, it becomes visible to **every** tenant's anonymous page. Per S3, anonymous routes must not serve tenant-scoped data except behind a shared secret; membership plans are arguably platform-config and the safer model is to not make them tenant-scoped (or to resolve tenant from the host/subdomain before the query).
- Long-term-correct fix: decide explicitly — either resolve the tenant from the request host for the anonymous plans route (set a tenant override before the query), or treat `MembershipPlan`/`LoyaltyTierConfig` as true platform config (not `ITenantEntity`) and document why. Don't leave plan visibility dependent on whether a JWT happens to be present.
- GAP: partial — needs a product decision (story-worthy): "Tenant resolution for anonymous catalog/plan endpoints".

### LG-SEC-06 — Admin grant/revoke of loyalty points is fully non-idempotent and unbounded — a double-click or retry double-grants real points
- Severity: major | Type: S7 | Size: M
- Files: `src/Cleansia.Core.AppServices/Services/LoyaltyService.cs:181-211` (`GrantPointsManuallyAsync`) and `:213-247` (`RevokePointsManuallyAsync`); controller `src/Cleansia.Web.Admin/Controllers/AdminLoyaltyController.cs:15-41`
- Concrete risk: the idempotency guard in `GrantPointsManuallyAsync` only fires when `orderId` is non-null (`:199-207`); admin manual grants pass `orderId: null` (`GrantPointsManually.cs:58`), so the guard is skipped *by design* and the code comment even says "those are intentional duplicates by definition." But the admin endpoint has **no client-supplied idempotency key and no rate-limit (S5)** — so a double-submitted "grant 50,000 points" request, a proxy retry, or a Stripe-style network retry grants the points twice. Points are economically meaningful (they drive tier discounts via `ResolveTierDiscountForOrderAsync`), so this is a financial side-effect doubling. Same shape on revoke.
- Long-term-correct fix: require a client-generated `requestId` on the grant/revoke command, persist it on the `LoyaltyTransaction`, and unique-index it so a retry collapses; add `[EnableRateLimiting]` with a narrow per-admin window (S5). Manual step: `ef-migration` for the idempotency-key column.
- GAP: no — implemented but unsafe.

### LG-SEC-07 — Side-effecting admin/membership mutations have no rate limiting
- Severity: major | Type: S5 | Size: S
- Files: `AdminLoyaltyController.cs:15,29` (grant/revoke); `AdminMarketingController.cs:19-31` (`send-sitewide-promo` — fan-out push to the entire user base); `MembershipController.cs:15,27,46,67` (Subscribe/Cancel/CreateCheckoutSession/SwapPlan, all of which call Stripe)
- Concrete risk: S5 requires a per-user/narrow limit on mutations that cost money or send messages. `send-sitewide-promo` enqueues a push to every `Promo=true` user — a repeated trigger spams the entire base and burns FCM quota. The membership mutations each hit Stripe (customer creation, subscription create/swap, checkout session); only `PromoCode/Validate` and `Referral/Validate` carry `[EnableRateLimiting("auth")]`. The Stripe calls are the expensive ones and are unthrottled.
- Long-term-correct fix: add `[EnableRateLimiting]` with appropriate windows — a strict singleton-ish guard on `send-sitewide-promo`, per-user windows on the membership Stripe-touching endpoints and on grant/revoke.
- GAP: no.

### LG-SEC-08 — Admin promo-redemption and referral-by-user views expose full counterparty emails; confirm this is gated to a PII-cleared admin role, not generic admin
- Severity: minor | Type: S4 (intentional but broad PII exposure) | Size: S
- Files: `PromoCodeRedemptionListItem.cs:11` + mapper `GetPromoCodeRedemptions.cs:39`; `AdminReferralListItem.cs:13-15` + mapper `GetReferralsByUser.cs:59-61`; also raw `UserId` echoed in `GetUserLoyaltyAccount.Response.UserId` and the redemption/referral DTOs
- Concrete risk: these admin DTOs return full `UserEmail` / `ReferrerEmail` / `ReferredEmail` plus raw `UserId`s. S4 permits non-self email for documented ops/support intent (the DTO comments claim this), so it is not a leak to the customer surface — but the controllers gate only on `CanViewPromoCodes` / `CanViewReferrals`, and the audit should confirm those policies are bound to a support/ops role with PII clearance rather than every admin token. The cross-tenant angle is covered (rows are `ITenantEntity`, so an admin only sees their tenant's redemptions), so this is a policy-scoping check, not a tenancy bug.
- Long-term-correct fix: confirm in `PolicyBuilder` that `CanViewPromoCodes`/`CanViewReferrals` map to a PII-authorized role; if generic admins shouldn't see raw emails, mask to first-name + masked email like the customer-facing referral list already does (`GetMyReferrals` exposes only `ReferredFirstName`).
- GAP: no — verification + possible masking.

### LG-SEC-09 — `SendSitewidePromo` fan-out enqueue is not idempotent / not de-duplicated
- Severity: minor | Type: S7 | Size: M
- File: `src/Cleansia.Core.AppServices/Features/Marketing/SendSitewidePromo.cs:67-91`
- Concrete risk: the command enqueues exactly one fan-out message and returns success; there is no campaign id, no dedupe, and (per LG-SEC-07) no rate limit. A retried/double-submitted admin action enqueues a second full fan-out, double-pushing a marketing notification to the entire user base. Lower severity because it's "spam" not "money," but it is a doublable mass side-effect.
- Long-term-correct fix: assign a `campaignId` (client-supplied or derived), persist it, and have the fan-out function skip an already-dispatched campaign; combine with the LG-SEC-07 rate limit.
- GAP: partial — a campaign-entity model is half-built (no persisted campaign record at all), story-worthy: "Persisted marketing campaign with dedupe + audit".

---

### Net assessment
- Clean: S1 (userId always from `IUserSessionProvider`), S2 (all endpoints attributed), S8 code-uniqueness `(TenantId, Code)` and `ITenantEntity` coverage, S9 (migrations correctly flagged as owner-only in entity config comments), S10 (admin promo list filters `IsActive`; customer paths read live accounts).
- The two critical items (LG-SEC-01 promo per-user cap race, LG-SEC-02 mobile double-subscribe) are the ship-blockers — both let a real money side-effect double under ordinary double-submit conditions. LG-SEC-03/04 (Stripe id leak + log) and LG-SEC-06 (non-idempotent manual grant) are the next tier.
- Recommend blocking merge of anything touching `PromoCodeService.ApplyAsync` or `CreateMembershipSubscription` until LG-SEC-01 and LG-SEC-02 have a unique-index / idempotency-key fix and I re-verify.

Key files for the developer:
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\PromoCodeService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Database\EntityConfigurations\PromoCodeRedemptionEntityConfiguration.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Memberships\CreateMembershipSubscription.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Memberships\CreateMembershipCheckoutSession.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\LoyaltyService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Admin\Controllers\AdminLoyaltyController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Admin\Controllers\AdminMarketingController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Marketing\SendSitewidePromo.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\MembershipController.cs`


# ===== catalog-config PERFORMANCE =====

This is fine (validator does `ExistsAsync`, handler does `GetByIdAsync` — one extra query, standard pattern across the codebase). I have enough to write a thorough findings report.

Let me note the validator double-query pattern is endemic (validator `ExistsAsync` + handler `GetByIdAsync` = 2 round-trips for every command). I'll include it as a systemic minor finding since it appears in every command in this domain.

---

# Performance & Cost Audit — Catalog-Config Domain

Scope: `Features/{Services, Packages, Currencies, Countries, Languages, Company, Codes, FeatureFlags, EmailTemplates}` + admin/customer UIs. Rules cited: `conventions.md` ("CancellationToken propagation", "production-ready long-term bar"), `patterns-backend.md` (paged-query recipe A6: `.AsNoTracking()` + project in `.Select`; "include ONLY what the mapper reads"). Audit only — no code changed.

## Ranked findings

### PERF-CAT-01 — No caching layer for near-static catalog reads (highest cost lever)
- **Severity:** critical · **Type:** missing-infrastructure / cost · **Size:** L · **Layers:** backend + infra
- **Where:** `Cleansia.Web.Customer/Controllers/ServiceController.cs:14-21`, `PackageController.cs:14-21`, `CountryController`, `CurrencyController`, `LanguageController`; backed by `GetServiceOverview.cs`, `GetPackageOverview.cs`, `GetCountryOverview.cs`/`GetServicedCountries.cs`, `GetCurrencyOverview.cs`, `GetLanguageOverview.cs`; and `AppConfigurationProvider.cs:26-56`. Confirmed zero caching exists anywhere in the solution (no `AddOutputCache`/`IMemoryCache`/`HybridCache`/`IDistributedCache` match).
- **Impact:** These overview endpoints are `[AllowAnonymous]` and are called on **every** customer booking-wizard render (`customer-stores/catalog.effects.ts` → `serviceClient.getOverview()` / `packageClient.getOverview()`), including server-side on each SSR render. Services/packages/countries/currencies/languages change a few times a year but hit Postgres on every page view. This is the single largest recurring DB/CPU cost in the domain at scale.
- **Fix (long-term):** Introduce `HybridCache` (.NET 9+/10) or `IMemoryCache` with a short TTL (e.g. 60-300s) keyed by tenant for the overview/serviced/feature-flag reads; invalidate on the matching create/update/delete commands. This is an Architect/ADR decision (new cross-cutting abstraction) — raise as a ticket. **This is a functional GAP (missing infrastructure) → needs a user story.**

### PERF-CAT-02 — `GetPagedPackages` projects an entity instance-method, forcing client-side eval over un-included navigations
- **Severity:** critical · **Type:** N+1 / wrong-result / over-fetch · **Size:** M · **Layers:** backend
- **Where:** `Features/Packages/GetPagedPackages.cs:33-37` → `.GetPagedSort(...).AsNoTracking().Select(package => package.MapToDto()).ToListAsync()`; `Mappers/PackageMappers.cs:8-18` reads `package.IncludedServices.Select(ps => ps.Service.Name)` and `ps.Service.Translations`.
- **Impact:** `MapToDto` is a compiled C# method (not an `Expression`), and it reads `Package.IncludedServices` — a computed property (`Package.cs:24`: `_includedServices.ToList().AsReadOnly()`) plus a JSON value-converted `Translations`. EF Core 10 cannot translate this projection, so it materializes whole `Package` rows then runs the mapper in memory. But `GetPagedSort` (base) does **not** Include `IncludedServices`/`Service` (only `PackageRepository.GetByIdAsync` overrides that). With no lazy-loading proxies (`UseLazyLoadingProxies` is not registered), `IncludedServices` comes back **empty** for every row. Net: every paged-packages call serializes an empty `IncludedServices` array (silent wrong data) and over-fetches full entity columns instead of the ~5 the list needs. Violates `patterns-backend.md` A6 ("project in `.Select`", "include ONLY what the mapper reads").
- **Fix:** Either (a) Include `IncludedServices.Service` in this handler and accept the over-fetch is intentional, or (b) build a dedicated DTO projection that EF can translate (don't call the instance mapper inside `IQueryable.Select`). Note the admin package list (`package-management.models.ts`) renders only name/description/price — so `IncludedServices` shouldn't be in `PackageListItem` for this list at all; trim the DTO. **Half-built/wrong-result → user story.**

### PERF-CAT-03 — `GetEmailTypes` loads the entire EmailTemplateTranslations table to aggregate in memory
- **Severity:** major · **Type:** over-fetch / in-memory aggregation · **Size:** M · **Layers:** backend
- **Where:** `Features/EmailTemplates/GetEmailTypes.cs:28-45` → `repository.GetAll().Include(e => e.Language).ToListAsync()` then `GroupBy`/`Count`/`Distinct`/`Max` in C#.
- **Impact:** Materializes every template translation row (5 types × up to 5 languages × N keys × tenants) plus its `Language` on every admin email-templates landing. As template count grows this is unbounded fetch + CPU for what should be a single `GROUP BY`. Also missing `AsNoTracking()` (entities tracked needlessly on a read path).
- **Fix:** Push the grouping/counting/distinct/max into the `IQueryable` (`GroupBy(t => t.EmailType).Select(g => new {...})`), select only `Language.Code`, add `AsNoTracking()`. The "missing email types" backfill stays in memory over the small grouped result.

### PERF-CAT-04 — Feature-flag resolution does up to 3 sequential uncached round-trips per check
- **Severity:** major · **Type:** N round-trips / cost · **Size:** M · **Layers:** backend + infra
- **Where:** `Infra.Database/AppConfigurationProvider.cs:26-56` (`IsFeatureEnabledAsync`), invoked by `Features/FeatureFlags/CheckFeatureFlag.cs`.
- **Impact:** Each flag check fires up to 3 awaited `FirstOrDefaultAsync` queries (tenant → country → global), with no caching. Feature flags are read-mostly config; if checked on request hot paths this is 1-3 queries per check every time. Index `(Name, Scope, ScopeValue)` exists (`FeatureFlagEntityConfiguration.cs:27`), so the queries are cheap individually — the cost is volume + no cache + sequential awaits.
- **Fix:** Cache flags (folds into PERF-CAT-01's cache). Optionally collapse the 3 lookups into one query (`WHERE Name = @n AND ((Scope='tenant' AND ScopeValue=@t) OR (Scope='country' AND ScopeValue=@c) OR Scope='global')`) ordered by scope-precedence, picking the most specific in memory — one round trip instead of three.

### PERF-CAT-05 — Read paths missing `AsNoTracking()` (overview + repo reads + detail-by-id)
- **Severity:** major · **Type:** missing-AsNoTracking / allocation · **Size:** S · **Layers:** backend
- **Where:** `GetServiceOverview.cs:20-23`, `GetPackageOverview.cs:20-24`, `GetCurrencyOverview.cs:17-21`, `GetLanguageOverview.cs:18-22`, `GetServiceCategories.cs:23-26`, `GetCountryOverview.cs` (via repo); repo reads `CountryRepository.GetServicedAsync` (`:36-42`), `CompanyInfoRepository.GetActiveCompanyInfoAsync` (`:9-12`), `EmailTemplateTranslationRepository.GetByEmailTypeAsync` (`:22-29`) and `GetByEmailTypeAndLanguage` (`:16-19`); detail handlers `GetServiceById.cs:33`, `GetPackageById.cs:33`, `GetCurrencyById.cs:34` (all via `GetByIdAsync` which uses the tracked `GetQueryable()`).
- **Impact:** Every read-only catalog query builds the EF change-tracker snapshot for entities that are never mutated — wasted CPU/allocations on the busiest (public, SSR) endpoints. Violates `patterns-backend.md` A6 / repository guidance ("Use `.AsNoTracking()` on read paths").
- **Fix:** Add `.AsNoTracking()` to these read queries (and a dedicated no-tracking read path for detail-by-id queries, since `GetByIdAsync` is shared with command handlers that need tracking — don't change the base method; add `.AsNoTracking()` in the read handlers or a `GetByIdReadOnlyAsync`).

### PERF-CAT-06 — Paged list handlers project via instance mapper → full-entity over-fetch instead of column projection
- **Severity:** major · **Type:** over-fetch · **Size:** M · **Layers:** backend
- **Where:** `GetPagedServices.cs:33-37` (`.Include(Category).ToList()` then maps in memory), `GetPagedCompanyInfo.cs:35-40` (`.Include(Country).Select(x => x.MapToListItem())`), `GetPagedEmailTemplates.cs:35-40` (`.Include(Language).Select(x => x.MapToListItem())`). All three call a compiled instance mapper, forcing the projection client-side after fetching full entity rows.
- **Impact:** Each list row pulls every column of the entity (and the included nav's full row) when the list DTO needs ~5-9 fields. On admin grids this is moderate; combined with the tracking issue it's avoidable bandwidth + memory. Note `GetServiceListItem` also drags the full JSON `Translations` blob and full `Category` row per service.
- **Fix:** Replace `.Select(x => x.MapToListItem())` with an inline EF-translatable projection to the list DTO (`.Select(x => new XxxListItem(x.Id, x.Name, ... , x.Country.Name))`). Keeps the query server-side and column-trimmed. (Where `Translations` JSON is genuinely needed by the list, keep it; the admin grids inspected don't render it — candidate to drop from the list DTO.)

### PERF-CAT-07 — `GetServicedCountries` materializes then maps in memory (no `AsNoTracking`, returns un-paged full set)
- **Severity:** minor · **Type:** over-fetch / allocation · **Size:** S · **Layers:** backend
- **Where:** `Features/Countries/GetServicedCountries.cs:21-23` + `CountryRepository.GetServicedAsync:36-42`.
- **Impact:** Customer/partner-facing picker; tracked entities materialized, `Translations` JSON pulled per country, then `.MapToDto()` in memory. Small N (countries) so low absolute cost, but it's a public path and folds naturally into PERF-CAT-01's cache + the `AsNoTracking` fix.
- **Fix:** `AsNoTracking()` + project in the repo query; cache.

### PERF-CAT-08 — Catalog tables lack indexes for default sort / tenant scoping
- **Severity:** minor · **Type:** missing-index · **Size:** S · **Layers:** db (coordinate with DB Master) · **manual_step: ef-migration**
- **Where:** `ServiceEntityConfiguration.cs`, `PackageEntityConfiguration.cs`, `CurrencyEntityConfiguration.cs`, `LanguageEntityConfiguration.cs`, `CountryEntityConfiguration.cs` — none declare an index on `Name` (the default `OrderBy`/sort field) or a `(TenantId, Name)` composite for tenant-scoped lists. (CompanyInfo and EmailTemplateTranslation do have appropriate indexes; FeatureFlag does too.)
- **Impact:** Low today (small tables), but the paged lists sort by `Name` and these are `ITenantEntity` — at multi-tenant scale a `(TenantId, Name)` index avoids a sort/seq-scan. The `Name.ToLower().Contains(...)` search filter (`ServiceSpecification.cs:28`, `PackageSpecification.cs:18`) is a leading-wildcard `LIKE` and is **inherently unindexable** — flag for DB Master to consider `pg_trgm` GIN if search volume warrants, otherwise accept.
- **Fix:** Add `(TenantId, Name)` indexes to the tenant-scoped catalog entities; evaluate trigram index for search. DB Master decision.

### PERF-CAT-09 — Systemic: every command does validator `ExistsAsync` + handler `GetByIdAsync` (2 round-trips)
- **Severity:** minor · **Type:** redundant round-trip · **Size:** M (systemic) · **Layers:** backend
- **Where:** pattern across the domain, e.g. `SetCountryServiced.cs:27 + :36`, `GetServiceById.cs:23 + :33`, `GetPackageById.cs`, `GetCurrencyById.cs`, every Update/Delete in Services/Packages/Currencies/Countries/Languages/Company.
- **Impact:** Two DB hits (a `COUNT/EXISTS` then a `SELECT` of the same row) for a single logical operation. This is the established codebase idiom (validator existence-check, handler fetch-and-guard per `patterns-backend.md`), so it is **not** a per-feature defect — but it's a real, repeated extra round-trip on every write. Low per-call cost; large aggregate.
- **Fix:** Not a quick win and not a deviation to fix ad hoc — this is an Architect-level pattern question (e.g. let the handler's null-guard own existence and drop the validator `ExistsAsync`). Raise as a cross-cutting ticket, do not change per-feature.

## Frontend / mobile

- **Frontend:** Clean. The admin catalog list components (`service-management`, `package-management`, etc.) use `ChangeDetectionStrategy.OnPush` + signals + facades, server-side lazy paging via the shared `cleansia-table` (which is OnPush and has `trackByFn` keyed on `item.id`, `cleansia-table.component.ts:369`). No template logic hot spots, no eager heavy imports found in these features. Customer catalog is loaded once per session via NgRx (`catalog.effects.ts`). **No perf findings.** (Non-perf note for the Reviewer/i18n: `service-management.facade.ts:85-91` hardcodes `'CZK'`/`'en-GB'` in `formatCurrency` — correctness/i18n, out of scope here.)
- **Mobile:** Catalog-config (services/packages/currencies/countries/languages/company/codes/feature-flags/email-templates) is **admin-only** — no Android surface in `:partner-app`/`:customer-app` for these features. No recomposition findings in scope.

## Summary table

| ID | Sev | Title | Size | GAP? |
|---|---|---|---|---|
| PERF-CAT-01 | critical | No caching for near-static public catalog reads (SSR hot path) | L | yes |
| PERF-CAT-02 | critical | `GetPagedPackages` client-eval over un-included navs (empty IncludedServices + over-fetch) | M | yes |
| PERF-CAT-03 | major | `GetEmailTypes` loads whole table to aggregate in memory | M | no |
| PERF-CAT-04 | major | Feature-flag check: 3 uncached sequential round-trips | M | partial (cache) |
| PERF-CAT-05 | major | Missing `AsNoTracking()` across read paths | S | no |
| PERF-CAT-06 | major | Paged lists over-fetch via instance mapper instead of column projection | M | no |
| PERF-CAT-07 | minor | `GetServicedCountries` in-memory map, no AsNoTracking | S | no |
| PERF-CAT-08 | minor | Missing `(TenantId, Name)` indexes on catalog tables | S | no (ef-migration) |
| PERF-CAT-09 | minor | Systemic validator-Exists + handler-GetById double round-trip | M | no (architect) |

**Key files:** `src/Cleansia.Core.AppServices/Features/Packages/GetPagedPackages.cs`, `src/Cleansia.Core.AppServices/Mappers/PackageMappers.cs`, `src/Cleansia.Core.Domain/Packages/Package.cs`, `src/Cleansia.Core.AppServices/Features/EmailTemplates/GetEmailTypes.cs`, `src/Cleansia.Infra.Database/AppConfigurationProvider.cs`, `src/Cleansia.Core.AppServices/Features/Services/GetServiceOverview.cs`, `src/Cleansia.Core.AppServices/Features/Packages/GetPackageOverview.cs`, `src/Cleansia.Web.Customer/Controllers/ServiceController.cs`, `src/Cleansia.Web.Customer/Controllers/PackageController.cs`, `src/Cleansia.Infra.Database/BaseRepository.cs`, `src/Cleansia.Infra.Database/Repositories/{CountryRepository,CompanyInfoRepository,EmailTemplateTranslationRepository}.cs`.


# ===== Stripe webhooks (BOTH handlers) security and idempotency =====

I now have a complete picture. The web Checkout flow only creates the Stripe Checkout Session (no local row); the local `UserMembership` row is created exclusively by the webhook's `ProvisionFromCreatedEventAsync`, which does NOT check `GetActiveForUserAsync`. This confirms the duplicate-membership gap. I have everything needed for the report.

---

# Stripe Webhooks Security & Idempotency Audit — Findings

**Scope:** Both webhook paths end to end — `HandlePaymentNotification.cs`, `StripeSubscriptionWebhookHandler.cs`, the 3 `PaymentController.Webhook` endpoints (Partner/Customer/Mobile), `StripeClient.cs`, `ProcessedStripeEvent` dedup, `UnitOfWorkPipelineBehavior`, `AzureStorageQueueClient`.

## Headline verdict on the blind-spot question

**The subscription handler is NOT a forged-event hole.** `ConstructEvent` (signature verification) genuinely appears only in `HandlePaymentNotification.cs`, but `StripeSubscriptionWebhookHandler.HandleAsync` is unreachable except via `HandlePaymentNotification.Handler` line 166 — and that handler verifies the signature at line 124 *before* dispatching to the subscription path. All three controllers (`Cleansia.Web.Partner`, `Cleansia.Web.Customer`, `Cleansia.Web.Mobile.Customer`) route through the same verified command. There is no Functions-based or alternate entry point (grep of `Cleansia.Functions` for webhook/`IStripeSubscriptionWebhookHandler` = none). So **S2 passes** for both paths. The real defects are idempotency ordering, a missing invariant check, and rate limiting — below, ranked by impact.

---

### SEC-W1 — Receipt + push side-effects fire BEFORE the idempotency stamp commits (non-transactional outbox)
- **Severity:** High
- **Type:** Idempotency / double side-effect (S7)
- **Location:** `HandlePaymentNotification.cs:241-257` (queue sends inside `HandleCompletedSession`), `UnitOfWorkPipelineBehavior.cs:19-20` (commit happens *after* the handler returns), `AzureStorageQueueClient.cs:14-27` (immediate, non-transactional dispatch)
- **Impact:** The handler enqueues `GenerateReceipt` and the `OrderConfirmed` push *during* handler execution. The `ProcessedStripeEvent` stamp and the order state change only persist later, in the pipeline's `CommitAsync`. The code comment at lines 152-159 asserts "side effects fire at most once" and "the stamp is committed atomically with the rest of the handler's work" — but the queue send is **not** part of the EF transaction. If `CommitAsync` throws (e.g. the parallel-retry `DbUpdateException` the design deliberately relies on, a transient PG error, or a `CancellationToken` trip), the receipt/push messages are already on the wire and the stamp rolled back. Stripe retries, the handler re-runs (stamp absent), and enqueues a **second** receipt-generation + a second push. Result: duplicate customer receipts (a financial document) and duplicate "order confirmed" notifications per retry. The whole point of the `ProcessedStripeEvent` table is defeated for the side-effects that actually matter.
- **Long-term fix:** Make queue dispatch transactional with the DB write — a transactional-outbox row written in the same EF transaction and drained after commit, or at minimum move `queueClient.SendAsync` to *after* `CommitAsync` (post-commit dispatch) and make the receipt generator itself idempotent (it should no-op if a receipt for the order already exists). Correct the misleading comment at lines 136-159.
- **Size:** M · **Layers:** AppServices, Infra.Queue, Functions (receipt generator idempotency) · **Functional GAP:** Partial — needs a story for transactional-outbox or post-commit dispatch.

### SEC-W2 — Webhook auto-provision can create a second active membership (one-active invariant bypassed)
- **Severity:** High
- **Type:** Idempotency / double financial side-effect + data-integrity (S7)
- **Location:** `StripeSubscriptionWebhookHandler.cs:102-167` (`ProvisionFromCreatedEventAsync` — no active-membership check before `UserMembership.Create`), contrast `CreateMembershipCheckoutSession.cs:54-59` (the request path *does* check `GetActiveForUserAsync`)
- **Impact:** The web Checkout flow creates only the Stripe Session; the local `UserMembership` row is created **exclusively** by the `customer.subscription.created` webhook. That provisioning path never calls `GetActiveForUserAsync`, while `UserMembership` itself documents "one user can have at most one active membership… enforced in handler code, not by a unique index" (`UserMembership.cs:13-15, 86-88`). A user who already has an active membership and opens a second Checkout (the request-side guard only blocks the *Checkout-session creation* call, not Stripe-side reality — they can subscribe via a stale tab, dashboard, or two near-simultaneous checkouts) gets a **second active row** with no guard. Benefit usage tracking, renewal reminders, and the pricing pipeline all assume a single active row; duplicates double-grant benefits and create reconciliation drift against Stripe. There is also no unique index as a backstop (`UserMembership.cs:14`).
- **Long-term fix:** In `ProvisionFromCreatedEventAsync`, call `GetActiveForUserAsync(userId)` before `Create`; if one exists, reconcile/log instead of inserting. Add a filtered unique index on `(TenantId, UserId)` where `Status = Active` as the database backstop.
- **Size:** S–M · **Layers:** AppServices, Infra.Database (migration) · **Functional GAP:** Yes — story for membership-provision dedup + DB constraint. Flag `manual_step: ef-migration`.

### SEC-W3 — Webhook endpoints are not rate-limited (S5)
- **Severity:** Medium
- **Type:** Rate limiting on side-effecting endpoint (S5)
- **Location:** `Cleansia.Web.Partner/Controllers/PaymentController.cs:13-17`, `Cleansia.Web.Customer/Controllers/PaymentController.cs:43-47`, `Cleansia.Web.Mobile.Customer/Controllers/PaymentController.cs:43-47` — all three `Webhook` actions are `[AllowAnonymous]` with **no** `[EnableRateLimiting]`
- **Impact:** The `/api/Payment/webhook` route is anonymous and unthrottled on every API host. An attacker who cannot forge a valid signature still gets an unbounded, free DB round-trip per request: read body → `EztractOrderId`/`ConstructEvent` attempt → on most paths an order/membership repo lookup before rejection. The validator additionally calls `EventUtility.ConstructEvent` up to **twice more** (`HandlePaymentNotification.cs:48, 62`) plus the handler's third construct at line 124 — three signature computations per request — amplifying CPU cost. This is a cheap unauthenticated DoS amplifier on the most sensitive endpoint in the system. Note S5 in `security-rules.md` explicitly lists side-effecting mutations as requiring a limit; webhooks qualify.
- **Long-term fix:** Add a dedicated rate-limit window keyed by source IP (Stripe publishes its egress ranges) on the webhook route, and deduplicate the triple `ConstructEvent` so the payload is parsed/verified exactly once and passed through the command.
- **Size:** S · **Layers:** Web (3 hosts), AppServices (validator refactor) · **Functional GAP:** No — hardening.

### SEC-W4 — Signature failures are swallowed in the validator, masking the real reason and burning work
- **Severity:** Low–Medium
- **Type:** Error handling / robustness (supports S5/S7)
- **Location:** `HandlePaymentNotification.cs:44-58` (`NotificationIsHandled` catches `StripeException` and returns `false`), lines 60-82 (`OrderExistsAsync` re-runs `ConstructEvent` with **no** try/catch)
- **Impact:** The validator's `When(NotificationIsHandled)` constructs the event; on bad signature it returns `false` and skips the order check — fine. But `OrderExistsAsync` (gated by the same `When`) calls `ConstructEvent` again with no guard. The gating means it's only reached when the first construct succeeded, so it won't throw in practice — but the logic is duplicated and fragile: any future reorder of the rules surfaces an unhandled `StripeException` as a 500 (which Stripe then *retries*, amplifying load). More broadly, a forged/invalid payload still pays for two full `ConstructEvent` HMAC computations in the validator before the handler's third one returns the clean `InvalidSignature` 400. Verification should happen once.
- **Long-term fix:** Verify the signature exactly once at the controller or handler boundary, hand the parsed `Event` to the validator/handler, and drop the validator's reconstruction entirely.
- **Size:** S · **Layers:** AppServices, Web · **Functional GAP:** No.

### SEC-W5 — `IgnoreQueryFilters()` cross-tenant lookups are correct but tenant override is set from attacker-influenceable metadata
- **Severity:** Low (informational / defense-in-depth)
- **Type:** Tenant isolation (S8)
- **Location:** `StripeSubscriptionWebhookHandler.cs:45-48, 132-143`; `UserMembershipRepository.cs:24-33`; `HandlePaymentNotification.cs:184-196` + `OrderRepository.GetByIdIgnoringTenantAsync`
- **Impact:** The webhook legitimately bypasses the tenant filter (`IgnoreQueryFilters()`) because Stripe events aren't tenant-scoped — this is correctly commented and acceptable per S8's "justifying comment" rule. The tenant override is then derived from the resolved **server-side** row's `TenantId` (order/membership/user), not from the payload, which is the right source. The only residual risk: in `ProvisionFromCreatedEventAsync` the `UserId`/`MembershipPlanCode` come from **Stripe subscription metadata** (`StripeSubscriptionWebhookHandler.cs:122-123`), set originally by us in `CreateMembershipCheckoutSessionAsync` — but for a `customer.subscription.created` event created directly in the Stripe Dashboard, that metadata is operator-controlled and the handler will provision a row for *any* `UserId` it names, inheriting that user's tenant (line 140-142). Low risk (requires Stripe account access), but the provisioning trusts metadata to pick the owning user. Worth a note, not a blocker.
- **Long-term fix:** Keep as-is, but document that dashboard-created subscriptions are an accepted trust boundary; the SEC-W2 active-membership check also bounds the blast radius.
- **Size:** XS · **Layers:** AppServices · **Functional GAP:** No.

### SEC-W6 — Secret handling: single shared `WebhookSecret`, plaintext config binding (informational)
- **Severity:** Low (informational)
- **Type:** Secret handling (escalate to owner — do not rotate)
- **Location:** `StripeConfig.cs:8-13` (`SecretKey`/`WebhookSecret` bound from `IConfiguration` section "Stripe"), used at `HandlePaymentNotification.cs:49,63,125` and throughout `StripeClient.cs`
- **Impact:** One `WebhookSecret` verifies all event families (orders + subscriptions) across all 3 API hosts and a single `SecretKey` is instantiated inline in every `StripeClient` method via `new global::Stripe.StripeClient(config.SecretKey)`. No per-host or per-endpoint secret separation, and secrets are read from plain config binding (fine if backed by Key Vault/env in deployment, a risk if appsettings.json). Not a code bug; flagging for the owner to confirm secrets are injected from a vault and not committed. **Per my constraints I am not rotating or touching config — escalating to owner.**
- **Long-term fix:** Owner to confirm vault-backed config; consider distinct webhook signing secrets per Stripe endpoint if the hosts register separate Stripe webhook endpoints.
- **Size:** XS (verification) · **Layers:** Config/Deploy · **Functional GAP:** No.

---

## What PASSES (so the developer doesn't "fix" non-issues)
- **S2** — every webhook action has `[AllowAnonymous]`; signature verification is the auth mechanism. Both paths verified before any side effect.
- **S7 dedup core** — `ProcessedStripeEvent` has a real UNIQUE index (`ProcessedStripeEventEntityConfiguration.cs:37-38`), the existence check + stamp-before-side-effects ordering for the **DB** state is sound; terminal-state guards exist in `HandleCompletedSession`/`HandleExpiredSession` (lines 232, 267). The gap is only the **queue** side-effects (SEC-W1).
- **S1** — no `userId` trusted from request body on the webhook path; identity comes from Stripe metadata resolved against server rows.
- **S4** — webhook returns a bare event/subscription id string, no DTO leak.
- **StripeClient idempotency keys** — every Stripe write call uses a deliberate `IdempotencyKey` (`StripeClient.cs:41,72,95,131,208,247,264,311`) with sound amount/attempt scoping.

## Priority order for remediation
1. **SEC-W1** (duplicate receipts/pushes on retry — defeats the dedup table's purpose for the side-effects that matter)
2. **SEC-W2** (duplicate active memberships — double benefit grant, no DB backstop)
3. **SEC-W3** (unauthenticated unthrottled webhook DoS amplifier)
4. **SEC-W4** (triple signature verification + masked failure → retry storms)
5. **SEC-W5 / SEC-W6** (defense-in-depth + owner secret confirmation)

**Key files (absolute):**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Payments\HandlePaymentNotification.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\StripeSubscriptionWebhookHandler.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Behaviors\UnitOfWorkPipelineBehavior.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Azure.Storage.Queues\AzureStorageQueueClient.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Database\Repositories\UserMembershipRepository.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Partner\Controllers\PaymentController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\PaymentController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Mobile.Customer\Controllers\PaymentController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Common\Configuration\StripeConfig.cs`

Audit-only — no code changed. SEC-W6 escalated to owner (secrets not touched).


# ===== disputes-addresses SECURITY S =====

`ResolveDispute.RefundAmount` is recorded on the entity only — no Stripe refund is actually issued (it's a half-built feature: an admin enters a refund amount, but nothing pays it out). That's a functional GAP, not a doublable money side-effect, so S7 is informational here. The dispute resolution does not move money — consistent with the prior audit's note that the dispute lifecycle is largely unreachable/half-built.

I have enough to write the findings. Let me do one quick confirmation that `GetPagedDisputes` for a non-admin properly nulls `CustomerEmail`/`CustomerName` filter but still returns DTOs containing the customer's own email (self — acceptable), and that the partner host's `CanViewDisputeList` = CustomerOnly means partners can't reach it at all. The partner DisputeController declares Create/View with `CanCreateDispute`=CustomerOnly etc., so on the Partner host those are effectively dead (partners are Employees, excluded by CustomerOnly) — a functional inconsistency but not a leak. I have a complete picture.

---

# Security Audit: Disputes + Addresses + SavedAddresses (S1–S10)

Scope read: `Features/Disputes/*` (7 handlers + 4 DTOs + filter + mapper + domain `Dispute`), `Features/SavedAddresses/*` (6 handlers + DTO), `Features/Addresses/AddressDto`, repositories, `PolicyBuilder`, and all 5 controllers (Customer/Mobile.Customer/Partner dispute + Customer/Mobile.Customer saved-address), plus customer disputes facade.

Ranked by impact.

---

## SEC-DSP-01 — CRITICAL — Any authenticated user can inject staff messages into ANY dispute (privilege escalation + ownership bypass)
- **Type:** S1 / S2 / S3 (broken access control)
- **Where:** `PolicyBuilder.cs:76` maps `CanRespondToDispute` → `PhysicalPolicy.Authenticated`; exploited in `AddDisputeMessage.cs:50` and `:65`. Exposed by `Cleansia.Web.Customer/Controllers/DisputeController.cs:52-62` and `Cleansia.Web.Mobile.Customer/Controllers/DisputeController.cs:52-62`.
- **Concrete risk:** `AddMessage` is gated only by `Authenticated` (any logged-in customer or partner, any role). The handler trusts `IsStaffMessage` from the request body, and the ownership check at line 50 is `if (!request.IsStaffMessage && dispute.UserId != userId)` — so when a caller sends `IsStaffMessage: true`, the ownership check is **completely skipped**. A non-admin can therefore: (a) post a message into **any dispute id that exists** regardless of owner (cross-customer), and (b) have that message render as an official **staff** reply (the DTO exposes `IsStaffMessage`, line `DisputeMessageDto.cs:8`, and the customer UI badges staff messages). Worse, posting a staff message also fires a **push notification to the victim customer** (`AddDisputeMessage.cs:65-78`), so an attacker can spam arbitrary customers with fake "support" replies. `CanRespondToDispute` is comment-documented as "Admin only" in `Policy.cs:103` — the physical mapping contradicts the intent.
- **Impact:** Impersonation of support, tampering with other customers' dispute threads, push-spam to arbitrary users.
- **Fix (long-term correct):** Two-part. (1) Split the policy: customer-side reply uses a customer-scoped policy, and `IsStaffMessage` must **never** come from the body — derive it server-side from the caller's role/host (only the Admin host with `AdminOnly` may set `isStaff: true`). Remove `IsStaffMessage` from `AddDisputeMessage.Command`. (2) Fix the ownership guard so it runs **unconditionally** for non-staff actors (and load the dispute through an ownership-aware check returning `NotFound`, mirroring S3). The current `dispute` is also dereferenced before a null check (`GetDisputeWithDetailsAsync` can return null) — NRE on a bad id.
- **Size:** M · **Layers:** backend (policy, handler, command, controllers ×2) + manual_step nswag-regen (DTO/command shape change).
- **Functional gap?** No — broken existing behavior.

## SEC-DSP-02 — CRITICAL — CreateDispute does not verify the order belongs to the caller
- **Type:** S3 (resource-by-id ownership)
- **Where:** `CreateDispute.cs:18-23` (validator) + `CreateDispute.cs:54-72` (handler). Order check is only `orderRepository.ExistsAsync` (inherited id-only existence from `BaseRepository`, confirmed `IOrderRepository` adds no override).
- **Concrete risk:** A customer can open a dispute against **any order id in their tenant**, including orders owned by other customers (`Order.UserId` at `Order.cs:114` is never compared to the caller). The created `Dispute.UserId` is set to the *attacker's* id, so it then appears in the attacker's own list/detail and they can attach messages/evidence to it. This is the entry point that an admin later sees and may act on (resolve/refund) — i.e. it seeds a fraudulent dispute referencing a stranger's order, and leaks the victim order's `DisplayOrderNumber` back via `DisputeDetails`. Tenant boundary still holds (order lookup is tenant-filtered), but cross-customer within a tenant does not.
- **Fix:** In the handler (not just validator, per S3), load the order via an ownership-aware repo call and return `NotFound` (`Order.NotFound`) when `order is null || order.UserId != userId`. Do it in the handler so it holds regardless of host.
- **Size:** S · **Layers:** backend (validator/handler).
- **Functional gap?** No — missing guard on existing endpoint.

## SEC-DSP-03 — MAJOR — Dispute DTOs leak the customer's email on the list/detail endpoints
- **Type:** S4 (DTO leak)
- **Where:** `DisputeDetails.cs:10` and `DisputeListItem.cs:10` both carry `CustomerEmail`; populated in `DisputeMappers.cs:19,35` from `dispute.User.Email`. Served to the Customer host via `CanViewDispute`/`CanViewDisputeList` (`CustomerOnly`).
- **Concrete risk:** For the customer-facing endpoints the caller is always the dispute owner (after SEC-DSP-02 is fixed), so it's their *own* email — borderline acceptable. But the same DTO is the contract for the **admin** path and the partner controller, and `CustomerEmail`/`CustomerName` of a non-self user is exactly the S4-prohibited PII. The list item carrying full email by default (vs. on-demand detail) is the larger exposure: any future reuse of `DisputeListItem` on a non-owner surface leaks PII for every dispute in a page. The filter already recognizes this risk — `GetPagedDisputes.cs:37` strips `CustomerEmail`/`CustomerName` from the *filter inputs* for non-admins, but the *response* still includes the email field.
- **Fix:** Remove `CustomerEmail` (and reconsider `CustomerName`) from the shared `DisputeListItem`; expose customer PII only on the admin detail DTO, and only there. Keep the customer-self DTO email-free (the customer knows their own email).
- **Size:** S · **Layers:** backend (DTO + mapper) + manual_step nswag-regen.
- **Functional gap?** No.

## SEC-DSP-04 — MAJOR — Dispute + SavedAddress side-effecting mutations have no rate limit
- **Type:** S5
- **Where:** No `[EnableRateLimiting]` on any method in `Cleansia.Web.Customer/Controllers/DisputeController.cs`, `Cleansia.Web.Mobile.Customer/Controllers/DisputeController.cs`, or either `SavedAddressController.cs`. The codebase has `auth`/`interactive` windows and applies them to order mutations (`OrderController.cs:40,51`).
- **Concrete risk:** `UploadEvidence` accepts a 10 MB blob per call (`UploadDisputeEvidence.cs:14`) and writes to blob storage with no per-user throttle — a cheap storage/cost-amplification and DoS vector. `CreateDispute` and `AddMessage` are unthrottled and `AddMessage` (with SEC-DSP-01) fans out push notifications, making it a spam amplifier. `Add`/`Update` saved-address create `Address` rows unthrottled (storage bloat). All are "side-effecting mutations" that the S5 rule says must get a narrower per-user limit.
- **Fix:** Add `[EnableRateLimiting("interactive")]` (or a narrower per-user window) to the dispute create/message/evidence and saved-address add/update/delete/set-default endpoints on both customer hosts.
- **Size:** S · **Layers:** backend (controllers ×4).
- **Functional gap?** No.

## SEC-DSP-05 — MAJOR — UploadDisputeEvidence stores the raw client-supplied filename and trusts client Content-Type
- **Type:** S4-adjacent / file-upload hardening (no dedicated rule number, cite S5/S4 + upload hygiene)
- **Where:** `UploadDisputeEvidence.cs:94-104`: `Path.GetExtension(command.FileName)` is concatenated into the blob name, and `command.ContentType` (the browser-declared type) is stored as blob metadata and re-served. `DisputeEvidenceDto.FilePath` (`DisputeEvidenceDto.cs:6`) returns the **internal blob path** to the client.
- **Concrete risk:** (1) The allow-list checks only the *declared* `ContentType` string, not the actual bytes — an attacker can upload an HTML/SVG/script payload labeled `image/png`; when the SAS URL is opened in a browser tab the stored `Content-Type` metadata is honored and the content can execute in the blob host's origin (stored-XSS / content-sniffing). (2) `FileName` is used to derive the extension and is echoed back verbatim with no sanitization (path/ংinjection into the blob name via crafted extension). (3) `FilePath` leaking the raw container path in the DTO is unnecessary surface (S4 "leaks internal structure"); the `BlobUrl` SAS is the only field the client needs.
- **Fix:** Validate magic bytes against the allow-list (not the declared type); store a server-normalized content-type; generate the blob name from a server GUID with a sanitized/whitelisted extension only; serve blobs with `Content-Disposition: attachment` + a restrictive `Content-Type`; drop `FilePath` from `DisputeEvidenceDto`.
- **Size:** M · **Layers:** backend (handler, validator, DTO, mapper) + manual_step nswag-regen.
- **Functional gap?** No (hardening of existing feature).

## SEC-DSP-06 — MAJOR — Dispute resolution records a RefundAmount but issues no refund (half-built money path)
- **Type:** Functional GAP (S7-adjacent — the doublable side effect simply does not exist yet)
- **Where:** `ResolveDispute.cs:53-57` → `Dispute.Resolve` (`Dispute.cs:82-90`) only sets `RefundAmount`/`Status=Resolved`. No consumer of `Dispute.RefundAmount` exists anywhere (confirmed: only DTOs/filters reference it). `StripeDisputeId`/`LinkStripeDispute` (`Dispute.cs:104`) is dead — never called.
- **Concrete risk:** An admin "resolves with refund €X" and the customer is never paid; or, when the refund is later wired in, it will need idempotency (S7) that isn't designed. Today it is a silent correctness/financial gap and a UX lie. Also `UpdateStatus`/`Resolve` can move a dispute to `Resolved` with no state-machine guard (any status → any status), and re-resolving overwrites the prior refund amount.
- **Fix:** Design the resolution→refund path as an idempotent command keyed on the dispute id / a ledger entry (mirror `LoyaltyService`/`ReferralService` patterns), gate the status transitions, and wire `StripeDisputeId` for the inbound Stripe-dispute webhook. Until then, the admin UI must not present "refund" as if it pays out.
- **Size:** L · **Layers:** backend (handler, domain, Stripe client, webhook) + admin UI.
- **Functional gap?** YES — needs a user story (and is consistent with the prior audit's "dispute lifecycle largely unreachable").

## SEC-DSP-07 — MINOR — Dispute resolve/status endpoints are admin-policied but mounted on the Partner host where they're unreachable; customer-policied dispute endpoints are duplicated on the Partner host (dead/inconsistent surface)
- **Type:** Consistency / latent-authorization (S2 hygiene)
- **Where:** `Cleansia.Web.Partner/Controllers/DisputeController.cs:66-88` exposes `Resolve` (`CanResolveDispute`=AdminOnly) and `UpdateStatus` (`CanUpdateDisputeStatus`=AdminOnly) on the **Partner** API; lines 18-64 duplicate `Create`/`GetById`/`GetPaged`/`AddMessage` with `CustomerOnly`/`Authenticated` policies. Partners are `Employee`-profile, so `AdminOnly` and `CustomerOnly` both exclude them — these routes are effectively dead.
- **Concrete risk:** Not currently exploitable, but it's a misleading authorization surface: the admin-only dispute mutations live on the partner host with no admin role present, and the only `Authenticated`-gated route on this host (`AddMessage`) is the SEC-DSP-01 hole — meaning a **partner** (Employee) *can* hit `AddMessage` on the Partner host and exploit SEC-DSP-01 from there too. If an Admin role is ever added to the Partner host's token issuer, Resolve/UpdateStatus silently become live with no further review.
- **Fix:** Move admin dispute operations to the Admin host (or behind the admin API), and remove the dead partner duplicates. Re-confirm `AddMessage` is removed/locked here once SEC-DSP-01 is fixed.
- **Size:** S · **Layers:** backend (controller placement/policy).
- **Functional gap?** Partial — admin dispute management UI/host wiring is the real missing piece (ties to SEC-DSP-06).

## SEC-DSP-08 — MINOR — DisputeMessageDto exposes raw AuthorId
- **Type:** S4
- **Where:** `DisputeMessageDto.cs:5` (`AuthorId`), set in `DisputeMappers.cs:58`.
- **Concrete risk:** For staff messages, `AuthorId` is an admin/employee user id surfaced to the customer client. It's an opaque id (not directly PII), but S4 says don't ship other users' ids; combined with any other endpoint that maps id→identity it aids enumeration. `AuthorName` already covers the display need.
- **Fix:** Drop `AuthorId` from the customer-facing DTO (or replace with a coarse `AuthorRole` flag); keep `IsStaffMessage` for badging.
- **Size:** S · **Layers:** backend (DTO + mapper) + manual_step nswag-regen.
- **Functional gap?** No.

---

## Items checked and PASSING (no action)
- **S1 (SavedAddresses):** `userId` derived from `IUserSessionProvider` in every handler; no userId from body. PASS.
- **S3 (SavedAddresses Update/Delete/SetDefault):** ownership enforced in validators via `BeOwnedByCallerAsync` (`UpdateSavedAddress.cs:104`, `DeleteSavedAddress.cs:40`, `SetDefaultSavedAddress.cs:40`). PASS. (Note: ownership is in the *validator* not the handler — acceptable here because all three hosts route through the same validator, but the S3 rule prefers handler/domain; minor, not filed.)
- **S3 (UploadDisputeEvidence):** handler checks `dispute.UserId != userId` (`UploadDisputeEvidence.cs:88`). PASS.
- **S3 (GetDisputeDetails / GetPagedDisputes):** non-admin scoped to `dispute.UserId == caller` (`GetDisputeDetails.cs:32-41`, `GetPagedDisputes.cs:32-38`). PASS.
- **S4 (SavedAddressDto):** no UserId/TenantId/email/Stripe ids. PASS.
- **S8 (tenancy):** `Dispute`, `SavedAddress`, `Address` all implement `ITenantEntity`; repos read via `Context.Set<T>()`/`GetDbSet()` so the global filter applies; no `FromSqlRaw`/`IgnoreQueryFilters` in these paths. PASS.
- **S6 (logging):** no PII logging found in any of these handlers; the push-notification payload carries only `disputeId`/`userId` (`AddDisputeMessage.cs:71-76`). PASS.
- **S10 (IsActive):** `SavedAddress`/`Dispute` use no soft-delete semantics here (delete is a hard `Remove`); not applicable. PASS.

## Top-3 to block a merge on
1. **SEC-DSP-01** (staff-message injection / ownership bypass) — critical, exploitable today from both customer hosts.
2. **SEC-DSP-02** (CreateDispute cross-customer order) — critical, trivial to exploit.
3. **SEC-DSP-04 + SEC-DSP-05** (no rate limit + unsafe evidence upload) — the blob-upload abuse + stored-content vector.

## Relevant file paths
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\PolicyBuilder.cs` (line 76)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\AddDisputeMessage.cs` (lines 34-78)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\CreateDispute.cs` (lines 16-72)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\UploadDisputeEvidence.cs` (lines 94-121)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\ResolveDispute.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\DTOs\DisputeListItem.cs`, `DisputeDetails.cs`, `DisputeMessageDto.cs`, `DisputeEvidenceDto.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Mappers\DisputeMappers.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\DisputeController.cs`, `...\Cleansia.Web.Mobile.Customer\Controllers\DisputeController.cs`, `...\Cleansia.Web.Partner\Controllers\DisputeController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\SavedAddressController.cs`, `...\Cleansia.Web.Mobile.Customer\Controllers\SavedAddressController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Disputes\Dispute.cs` (lines 82-108)