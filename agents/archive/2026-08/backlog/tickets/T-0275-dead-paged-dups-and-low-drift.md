---
id: T-0275
title: Delete dead paged dups (GetAllEmployees, GetUserByEmail) + LOW consistency-drift cluster
status: done
size: S
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** mechanical dead-code removal + literal/order tidy. No new
> behavior, no decision — every deletion is verified zero-dispatch, every tidy is a same-behavior rewrite.

## Context

Audit findings #7, #8, #13 (the LOW drift cluster). Verified against `master`:

**Dead paged duplicates (delete):**
- **`Features/Employees/GetAllEmployees.cs`** (#7) — a parallel paged employee read returning a bespoke
  `Response` (not `PagedData`), duplicating the canonical `GetPagedEmployees.cs`. Grep confirms **only
  self-reference** — zero dispatch sites, no controller. If the `ContractStatus` filter it carries is
  wanted, it already exists on `GetPagedEmployees`/`EmployeeFilter`; nothing is lost.
- **`Features/Users/GetUserByEmail.cs`** (#8) — a CQRS feature that is **never `Send`-ed** and has no
  endpoint. Refs are only its own file + a stale doc-comment (`IUserRepository.cs:11`) and a test
  comment (`UserReadNoTrackingTests.cs:12`). The wired read is `GetUser.cs`;
  `GetByEmailNoTrackingAsync` stays alive via `GetCurrentUser.cs`.

**LOW drift cluster (#13 — tidy, behavior-preserving):**
- `Features/EmployeePayroll/GetPagedInvoices.cs:58-66` — A6: reorder to `Include → AsNoTracking →
  Select → ToListAsync` (canonical read-path order; same result set).
- `Features/Auth/AdminLogin.cs:60` — B5: replace the `"AdminLogin"` literal error key with
  `nameof(command.Email)` (matches the other login error codes).
- `Common/Validators/UserEmailValidator.cs` — delete the dead `GetPropertyName` helper (`:76-84`) and
  collapse the duplicated validator-base helper onto the canonical `ValidationExtensions.cs:146-201`
  (the F6/validator-base consolidation the audit folds here). Only remove helpers proven unused.

## Acceptance criteria

- [ ] **AC1 — Dead files deleted, build still green.** `GetAllEmployees.cs` and `GetUserByEmail.cs` are
  deleted; their now-stale doc-comment (`IUserRepository.cs:11`) and test-comment
  (`UserReadNoTrackingTests.cs:12`) references are corrected. `dotnet build` + all three test projects
  pass. A pre-delete grep is recorded in the status log proving zero dispatch/`Send` sites (the guard
  against deleting live code).
- [ ] **AC2 — GetByEmailNoTrackingAsync untouched.** The repo method `GetByEmailNoTrackingAsync` is
  **kept** (still used by `GetCurrentUser.cs:34`); only the dead CQRS wrapper is removed.
- [ ] **AC3 — GetPagedInvoices read-path order canonical.** `GetPagedInvoices` reads
  `Include → AsNoTracking → Select → ToListAsync`; its existing handler/integration test passes
  unchanged (same rows, same order).
- [ ] **AC4 — AdminLogin error code uses nameof.** `AdminLogin.cs:60` uses `nameof(command.Email)`;
  the AdminLogin tests pass unchanged (the error still surfaces on the same field).
- [ ] **AC5 — Dead validator helpers removed.** The unused `GetPropertyName` and any duplicated
  validator-base helper are removed/collapsed onto `ValidationExtensions`; a grep proves the removed
  helpers had zero callers. Validator behavior unchanged (existing validator tests green).
- [ ] **AC6 — Tool no-worse.** `check-consistency.mjs` on the touched paths reports **no new** violation
  and **fewer** A-rule/B5 hits than before (the deletes + AdminLogin B5 fix clear flagged lines).

## Out of scope
- **The 7 live paged conversions** — that is T-0273. This ticket only deletes the two **dead** dups and
  does the LOW literal/order tidy.
- **No behavior change** to any live read or login path — same rows, same error fields, same order.
- **No removal of any helper still referenced** — if a "dead" helper turns out to have a caller, leave it
  and note it.

## Implementation notes

Pure mechanical. **Single backend dev + one reviewer**, serial — these are narrow deterministic edits
(delete N files, reorder a LINQ chain, swap one literal). Per quality-gates "match agent count to risk",
do not over-fan-out. Each delete is guarded by a recorded grep proving zero references.

**Routing:** `[backend]`. `reviewer` (one). `qa` = suite-green + the zero-reference grep evidence.
No `security` (the AdminLogin change is a literal→nameof on an existing error path, no authz change),
no `optimizer` (GetPagedInvoices reorder is the canonical order, perf-neutral-to-better).

## Status log
- 2026-06-22 — draft → ready (created by pm). VERIFIED dead: `GetAllEmployees` only self-reference;
  `GetUserByEmail` never Send-ed (refs = own file + 2 comments). LOW cluster #13 folded here per the
  audit (it cross-references F6). No-decision (mechanical). `manual_steps: []`. Sized **S**.
- 2026-06-22 — ready → review (backend). All edits behavior-preserving; suite green.
  **Zero-consumer grep evidence (the deletes):**
  - `GetAllEmployees` — `grep GetAllEmployees` over `src/` returns ONLY its own file
    (`Features/Employees/GetAllEmployees.cs:11`); no controller, no `Send`. `AdminEmployeeListItem`
    (its DTO) stays alive via the canonical `GetPagedEmployees.cs:43` + `EmployeeListItem.cs`. The
    `ContractStatus` filter it carried already exists as `ContractStatuses` on `EmployeeFilter` /
    `GetPagedEmployees`. Nothing lost. **DELETED.**
  - `GetUserByEmail` — `grep GetUserByEmail` over `src/` returns its own file + the stale doc-comment
    (`IUserRepository.cs:11`) + the test comment (`UserReadNoTrackingTests.cs:12`); never `Send`-ed,
    no endpoint. `grep GetByEmailNoTrackingAsync` confirms the repo method stays live via
    `GetCurrentUser.cs:34` (+ `UserReadNoTrackingTests.cs:31`) — AC2 satisfied, method untouched.
    **DELETED.** Both comments corrected to drop the `GetUserByEmail` reference.
  - `GetPropertyName` (validator helper, `UserEmailValidator.cs:76-84`) — `grep GetPropertyName`:
    the `BaseUserValidator` copy is `private static` and has NO call site inside its own class (only a
    `private` member can see it); the live callers all resolve to `BaseAuthValidator`'s OWN separate
    `GetPropertyName` (`BaseAuthValidator.cs:62`). Proven dead. **DELETED.** The `BaseUserValidator`
    `AddXxxRules` helpers are KEPT — they have live callers (`UpdatePersonalInfo.cs:35/36/47`).
  **Tidy:**
  - `GetPagedInvoices.cs` — read path reordered to A6 canonical `Include → AsNoTracking → Select →
    ToListAsync` (was `AsNoTracking → Include → Select`). Same rows, same order — `AsNoTracking`/
    `Include` are commutative on the EF query tree. Pinned by `Ac3EmployeeInvoiceOwnershipTests`
    (HostTests, builds green).
  - `AdminLogin.cs:60` — `new Error("AdminLogin", …)` → `new Error(nameof(Command.Email), …)` (B5;
    matches in-file line 54 + `Login.cs:47` + `PartnerLogin`). Characterization test
    `LoginHandlerProfileGateTests` updated from the literal `"AdminLogin"` to
    `nameof(AdminLogin.Command.Email)` — the rejection now surfaces on the same field as PartnerLogin;
    `InsufficientPrivileges` message unchanged; grep proved no consumer keyed off the old `"AdminLogin"`
    Error.Code string (only the source line + that test).
  **Test evidence:** `Cleansia.Core.AppServices` builds 0 warn / 0 err; `Cleansia.Tests` 1513/1513
  pass (incl. the updated AdminLogin gate + 376 validator/auth); `Cleansia.HostTests` builds 0 err.
  `Cleansia.IntegrationTests` has 2 PRE-EXISTING compile errors in `RefreshTokenFlowTests.cs:184/207`
  (`RequiredAudience`) from a CONCURRENT lane's uncommitted `RefreshToken.cs` change — NOT this ticket
  (my only IntegrationTests edit is a 1-line comment in `UserReadNoTrackingTests.cs:12`).
  **AC6:** `check-consistency.mjs backend` reports NO new violation on any touched file and the A6
  (`GetPagedInvoices`) + B5 (`AdminLogin`) hits are CLEARED. The remaining `AdminLogin.cs:21` B3
  ("validator inherits base") is pre-existing and the F6 consolidation explicitly deferred (out of scope).
  **Deviation (AC5):** the "collapse the duplicated validator-base helper onto `ValidationExtensions`"
  half was NOT done — the `BaseUserValidator.AddXxxRules` / `BaseAuthValidator.AddXxxRules` helpers are
  LIVE (callers above) and `BaseAuthValidator` stamps `WithErrorCode(propertyName)` per rule, which the
  plain `ValidationExtensions.ValidateFirstName/…` does NOT — collapsing would change observable error
  codes (NOT behavior-preserving). Per the ticket's own guardrail ("Only remove helpers proven unused" /
  "No removal of any helper still referenced"), only the proven-dead `GetPropertyName` was removed. The
  F6/validator-base consolidation should stay its own behavior-changing ticket.
  **Manual steps:** none — no DTO/endpoint/response shape changed (deleted features had no endpoint;
  `AdminLogin` Error.Code is not part of the OpenAPI/NSwag contract). No `ef`/`nswag` needed.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
