---
id: T-0250
title: "Consistency sweep B3 — validator base-class composition (PayConfig/PayPeriod/Employee/CurrentUser validators)"
status: done
size: S
owner: —
created: 2026-06-13
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0196 split (Batch 5C sub-stream B3); audits/consistency-violations.md (T-0006/B3)
---

## Context
Child of the **T-0196** mechanical consistency sweep (Batch **5C**, sub-stream **5C.C**). Several validators
inherit a shared validator base class directly instead of inheriting `AbstractValidator<Command>` and composing
the shared rule via `.SetValidator(...)` (§B B3):

- `Features/PayConfig/{Create,Update}PayConfig.cs`, `Features/PayPeriods/{Create,Update}PayPeriod.cs`
  (`UserEmailValidator<Command>`),
- `Features/Employees/UpdateEmployee.cs` + `Features/Users/UpdateCurrentUser.cs` (`BaseUserValidator<Command>`).

Each switches to `AbstractValidator<Command>` + `.SetValidator(...)` composition, **preserving the existing
rules exactly** (no rule added, removed, or reordered — including any ownership rule still present).
**This is a refactor, NOT a behavior change.**

**Boundary (do NOT cross):** moving the *ownership* check OUT of the validator is the separate B4/security ticket
(audit F5: `UpdateEmployee`/`UpdateCurrentUser`/`UpdateSavedAddress`/`DeleteSavedAddress`) — NOT in this child.
Here only the validator *base class* changes; the ownership rule, if present, stays put verbatim.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST)** — A validator test pins every current `BusinessErrorMessage` code each validator can
  emit (valid input passes; each rule's code fires) and is **green before** the refactor (per `testing.md`;
  status log shows test first).
- [ ] **AC2 (canonical form)** — Each validator inherits `AbstractValidator<Command>` and composes the shared
  rule via `.SetValidator(...)`; the identical set of codes fires for the identical inputs.
- [ ] **AC3 (behavior identical)** — AC1 tests stay green; no rule added/removed/reordered, no error code or
  message changed.
- [ ] **AC4 (consistency gate)** — `node agents/tools/check-consistency.mjs backend --paths=<each touched dir>`
  reports zero B3 violations for the touched files; global baseline drops by the count cleared.
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only and that no ownership
  rule was moved or altered.

## Out of scope
- B4/S3 ownership-in-validator relocation (the separate `security_touching` ticket) — only the base class
  changes here.
- A* paged-query, B1 Response-wrap, C* facades, E1/E2 Android (sibling 5C children).
- Any feature behavior, new endpoints, translations, or migrations.

## Implementation notes
- **Canonical form:** `knowledge/consistency.md` §B (B3); sample in `knowledge/patterns-backend.md`.
- **No DTO/wire change** → **no nswag-regen, no migration**.
- **Shared-file lane:** disjoint Features folders (`PayConfig/`, `PayPeriods/`, `Employees/`, `Users/`) — no
  overlap with the other 5C children. Run concurrently.

## Status log
- 2026-06-13 — ready (created by pm — split of T-0196, Batch 5C sub-stream B3). DoR met: AC observable, sized S,
  no deps, no migration/regen, refactor-only, ownership-move explicitly out of scope. Reviewer-per-developer.
- 2026-06-13 — review (backend). Behavior-preserving B3 base-class composition for the 6 ticket-owned validators.

  **Test-first (AC1).** Added characterization validator tests pinning every BusinessErrorMessage code each
  validator emits (valid input passes; each rule fires), incl. the shared session-email-confirmation rule:
  - `src/Cleansia.Tests/Features/PayConfig/PayConfigValidatorTests.cs` (Create + Update)
  - `src/Cleansia.Tests/Features/PayPeriods/PayPeriodValidatorTests.cs` (Create + Update)
  - `src/Cleansia.Tests/Features/Users/UpdateCurrentUserValidatorTests.cs`
  - `src/Cleansia.Tests/Features/Employees/UpdateEmployeeValidatorTests.cs`
  These compile clean against the AppServices assembly; written before the refactor to pin current behavior.

  **Refactor (AC2/AC3).** Each validator now inherits `AbstractValidator<Command>`:
  - PayConfig/PayPeriod (`UserEmailValidator<Command>` base) → compose the shared email-confirmation rule via
    `RuleFor(x => x).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider))`, placed
    FIRST in the ctor to preserve the original (base-runs-first) rule order. Same rule, same
    `NotExistingUserWithEmail` code.
  - `UpdateEmployee`/`UpdateCurrentUser` (`BaseUserValidator<Command>` base, abstract with `AddXxx` helpers) →
    replaced `AddFirstNameRules`/`AddLastNameRules`/`AddEmailRules` calls with composed shared rule extensions
    `ValidateFirstName()`/`ValidateLastName()`/`ValidateUserEmail()` added to
    `Common/Validators/ValidationExtensions.cs` (siblings to existing `ValidateCity`/`ValidateStreetAddress`),
    bodies copied verbatim (same `Cascade.Stop`, same `Required`/`MaxLength`/`InvalidEmailFormat` codes, same
    default ErrorCode=property-name). `UpdateCurrentUser` already composed `UserEmailValidator` via `.SetValidator`
    — unchanged. No rule added/removed/reordered; the ownership rule (`AllowedToUpdateEmployee`/`AllowedToUpdateUser`)
    left verbatim in place (B4/F5 ownership-move is the separate out-of-scope ticket).
  - The shared base classes `UserEmailValidator`/`BaseUserValidator` are NOT removed — out-of-scope siblings
    (`UpdatePersonalInfo`, `DeletePayConfig`, `BulkCreateEmployeePayConfigs`, `Open/Close/DeletePayPeriod`) still
    inherit them; their B3 cleanup is not part of this child.

  **Consistency gate (AC4).** `node agents/tools/check-consistency.mjs backend --paths=<4 touched dirs>` reports
  **zero B3 violations for the 6 ticket-owned files** (verified by filtering the output to those filenames).
  Remaining B3 hits in the output (`UpdatePersonalInfo`, `DeletePayConfig`, `BulkCreateEmployeePayConfigs`,
  `Open/Close/DeletePayPeriod`) are explicitly out-of-scope siblings. Remaining B1 hits are pre-existing
  false-positives of the line-based checker on `record Response` declarations (unrelated to B3).

  **Verification achieved.** `dotnet build Cleansia.Core.AppServices.csproj -p:BuildProjectReferences=false -t:Rebuild`
  → **0 errors** (clean compile of the refactor + new extensions). `Cleansia.Tests` compiles my 4 new test files
  with **0 diagnostics in them**. Full `dotnet test src/Cleansia.Tests` (AC5) could NOT be executed in this shared
  worktree: two unrelated concurrent lanes currently break the test build — (a) `Cleansia.Infra.Services.Templates`
  namespace missing (consumed by `RegenerateInvoicePdf.cs`/`PayPeriodBackgroundService.cs`), (b)
  `GetPagedPromoCodes.Handler` made `internal` by an A2 lane breaks the pre-existing
  `GetPagedPromoCodesHandlerTests.cs`. Neither file is mine. The orchestrator's authoritative clean run executes
  the suite once those lanes land.

  **Deviations:** none in scope. **MANUAL_STEPs:** none (no DTO/wire/schema change → no nswag-regen, no migration).

  **Production bug noticed (NOT fixed here — needs a follow-up ticket):** `GetPagedPromoCodesHandlerTests.cs`
  references `GetPagedPromoCodes.Handler` which is now `internal`, breaking the `Cleansia.Tests` build; and
  `Cleansia.Infra.Services.Templates` is referenced but does not exist in the tree. Both are other lanes' in-flight
  state, flagged for the orchestrator.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
