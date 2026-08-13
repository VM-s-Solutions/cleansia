---
id: T-0109
title: Gate Take/Start/Complete order on ContractStatus (rejected cleaners cannot work)
status: done
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0100, T-0107]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 0
source: finding EMP-GAP-01
---

## Context

Finding **EMP-GAP-01** (critical / security), `audits/AUDIT-2026-06-01-findings.json` (id `EMP-GAP-01`)
and `audits/AUDIT-2026-06-01-slice-reports.md` (CRITICAL section): **a cleaner the admin explicitly
rejected can still take, start, and complete orders.**

The "approval" gate on order actions is the misnamed rule `HasUploadedDocumentsAsync`, which checks
only `employee?.ContractStatus != ContractStatus.Pending`:

- `TakeOrder.cs:111-118` â€” `HasUploadedDocumentsAsync` â†’ `ContractStatus != Pending`.
- `CompleteOrder.cs:133-140` â€” same body, same check.
- `StartOrder.cs:26-53` â€” has **no** ContractStatus check at all (only assignment + no-in-progress);
  a rejected cleaner who already grabbed an order can start it.

`Employee.Reject()` (`Cleansia.Core.Domain/Users/Employee.cs:243-255`) sets
`ContractStatus = Rejected (5)`, which is `!= Pending (1)`, so the rejected cleaner passes every gate.
`ContractStatus` (`Cleansia.Core.Domain/Enums/ContractStatus.cs`) is `Pending=1, Active=2,
Terminated=3, Approved=4, Rejected=5`. The intended gate is **`== Approved`**, not `!= Pending`. This
defeats the entire vetting workflow and is direct trust/liability exposure.

Authorization contract is **ADR-0001 (ADR-AUTHZ)**; this depends on **T-0100 (BSP-1)** so the fix is
written against the corrected, fail-closed policy map. Scope is deliberately limited to the
order-action gate â€” the dead `Active`/`Terminated` lifecycle is a separate finding (EMP-GAP-02, not in
this ticket).

## Acceptance criteria

- [ ] **AC1** â€” Given an authenticated cleaner whose `ContractStatus == Rejected (5)` and a job with
  an available spot, When they call Take order, Then the command fails with a single business error
  and **no `OrderEmployee` assignment is created** and the order status is unchanged. (Closes the
  `TakeOrder.cs:111-118` hole.)
- [ ] **AC2** â€” Given a `Rejected` cleaner already assigned to a `Confirmed` order, When they call
  Start order, Then the command fails and the order does **not** transition to `InProgress`. (Closes
  the `StartOrder.cs` gap â€” Start currently has no ContractStatus gate at all.)
- [ ] **AC3** â€” Given a `Rejected` cleaner assigned to an `InProgress` order, When they call Complete
  order, Then the command fails and the order does **not** transition to `Completed` (no receipt
  enqueue, no loyalty grant, no pay fan-out). (Closes `CompleteOrder.cs:133-140`.)
- [ ] **AC4** â€” Given a cleaner with `ContractStatus == Pending (1)` or `Terminated (3)`, When they
  call Take / Start / Complete, Then each is rejected (only `Approved` may act on an order; `Active`
  is out of reach today â€” gate on `Approved` per the finding's fix and EMP-GAP-02).
- [ ] **AC5** â€” Given a cleaner with `ContractStatus == Approved (4)` who otherwise satisfies all
  existing rules (address present, documents/profile, assignment, no time/limit conflict), When they
  call Take / Start / Complete, Then each succeeds exactly as before â€” **no regression** to the
  happy path.
- [ ] **AC6** â€” The gate is a single, honestly-named rule (e.g. `EmployeeIsApprovedAsync`) used
  identically in all three validators, returning its **own** `BusinessErrorMessage` key distinct from
  `EmployeeDocumentsMissing`, with a corresponding `errors.*` translation key added in all 5 locales
  (en, cs, sk, uk, ru). The misleading `documents_missing` no longer doubles as the approval gate.
- [ ] **AC7** â€” Tests prove the holes are closed and the happy path survives: failing cases for
  `Rejected`/`Pending`/`Terminated` on all three commands and a passing case for `Approved`, written
  test-first (red â†’ green). Cases land with this ticket (pairs with **T-0126 / TC-AUTHZ-0**).

## Out of scope

- The dead `ContractStatus.Active` / `.Terminated` lifecycle, `Terminate()`/`Reactivate()` domain
  methods, admin termination/re-instatement commands, endpoints, and UI â€” that is **EMP-GAP-02**
  (size L, separate ADR + stories). This ticket only changes whether a non-`Approved` cleaner may act
  on an order.
- Whether `Active` should count as "can work" â€” deferred to the EMP-GAP-02 lifecycle ADR; until then
  gate strictly on `Approved`.
- Any change to the policy map / `Policy.*` constants (owned by T-0100/BSP-1).
- Admin/CSR order actions, reassignment, and the generalized cancel (AUD-01, Wave 2).

## Implementation notes

- **TEST-FIRST**, per `knowledge/testing.md` â€” these are validator/lifecycle gates (pure logic at the
  contract), which is the **strict red-green-refactor** category. The status log must show "red:
  <test> failing â†’ green", and each AC item maps to a test case; implementation-first fails Gate 6.
  Pairs with **T-0126 (TC-AUTHZ-0)** â€” same merge (TDD pair).
- **Files to change:** `Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs` (replace the
  `HasUploadedDocumentsAsync` body / add an approval rule), `â€¦/StartOrder.cs` (add the approval rule â€”
  currently absent), `â€¦/CompleteOrder.cs` (same approval rule). Add the new key to
  `Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` (existing employee keys live at lines
  104-111, e.g. `EmployeeProfileIncomplete = "employee.profile_incomplete"`); add matching
  `errors.employee.*` strings to the 5 frontend i18n files.
- The correct check is `employee?.ContractStatus == ContractStatus.Approved` (was
  `!= ContractStatus.Pending`). Reuse `IOrderAccessService.GetCallerEmployeeIdAsync` +
  `IEmployeeRepository.GetByIdAsync` already wired in each validator. Keep `CascadeMode.Stop`.
- **Serialization cluster:** these three files are **not** in any shared-file cluster in
  `TICKET-MAP.md`, so this ticket does not serialize against the PolicyBuilder / Startup / UoW / CreateOrder
  clusters. `CreateOrder.cs` is the only Orders file in a cluster (F2 â†’ AUD-06 â†’ TC-4) and is **not**
  touched here. T-0109 may run concurrently with those, but must not run concurrently with any other
  ticket that edits `TakeOrder.cs` / `StartOrder.cs` / `CompleteOrder.cs` (none currently planned in
  Wave 0).
- **Governing ADR:** ADR-0001 (ADR-AUTHZ) â€” fail-closed authorization. `depends_on: T-0100 (BSP-1)`
  so the gate is written against the corrected policy map; do not start until T-0100 is `done`.
- `security_touching: true` â†’ Security gate is mandatory (this is a critical authz-correctness defect).
- No `manual_steps`: no schema change (reads an existing column), no NSwag regen (no DTO/endpoint
  contract change â€” only a new error code string, which surfaces via the existing error envelope).

## Status log
- 2026-06-01 â€” draft (created by pm)
- 2026-06-02 - in_progress (backend) - TDD red->green.
  - Red: added 9 failing validator cases (Take/Start/Complete x Rejected/Pending/Terminated) asserting
    BusinessErrorMessage.EmployeeNotApproved. `dotnet test Cleansia.Tests` (Orders filter) -> Failed: 9,
    Passed: 36. Right reason: result.IsValid == True (the `!= Pending` gate let Rejected/Terminated
    through; StartOrder had no gate at all; Pending tripped the DoesNotContain(EmployeeDocumentsMissing)
    assertion, proving documents_missing was doubling as the approval gate).
  - Green: one honest EmployeeIsApprovedAsync rule (ContractStatus == Approved; caller server-derived,
    empty caller fails closed) used identically in TakeOrder, StartOrder, CompleteOrder, returning the
    new BusinessErrorMessage.EmployeeNotApproved ("employee.not_approved"). In TakeOrder/CompleteOrder it
    REPLACES the misnamed HasUploadedDocumentsAsync (body was purely ContractStatus != Pending - no
    genuine documents check); in StartOrder it ADDS the previously-absent gate. Added
    api.employee.not_approved to all 5 partner locales (en/cs/sk/uk/ru) with real translations. Build:
    0 errors, 74 warnings (all pre-existing, none in changed files). `dotnet test Cleansia.Tests` ->
    Failed: 0, Passed: 193. No nswag, no ef. AC1-AC7 covered.
- 2026-06-02 - done (backend) - implementation complete, full suite green; awaiting review/security gate.

## Review
**Reviewer — APPROVED (2026-06-02).** Verified against the real code: all 3 validators gate on
`ContractStatus == Approved` via the single honestly-named `EmployeeIsApprovedAsync` rule
(`TakeOrder.cs`, `StartOrder.cs` — which had NO gate before — and `CompleteOrder.cs`); the old
`!= ContractStatus.Pending` check is gone everywhere; the approval failure returns the new
`EmployeeNotApproved` (`employee.not_approved`) key, distinct from `EmployeeDocumentsMissing` (no
`EmployeeDocumentsMissing` reference and no `ContractStatus.Active` leakage remain in the Orders folder —
EMP-GAP-02 not introduced). `HasUploadedDocumentsAsync` was purely the ContractStatus gate (no genuine
documents read), so nothing real was dropped. Tests test-first (RED = 9 failed for the right reason),
assert on the constant. No Policy/PolicyBuilder/CreateOrder edits. No nswag, no ef.

**Security — PASS (2026-06-02).** A Rejected/Pending/Terminated cleaner can no longer take, start, or
complete an order (gate `== Approved` in all 3 validators; StartOrder now enforces it). Employee identity
is server-derived (`IOrderAccessService.GetCallerEmployeeIdAsync`), never a client field; empty caller
fails closed. Reject paths short-circuit before the handler — no `OrderEmployee` assignment (Take), no
InProgress transition (Start), no Completed transition / receipt / loyalty / pay (Complete). New error is
a distinct stable key. A test per command proves Rejected fails + Approved passes.

**Verification (orchestrator, independent):** all 3 validators carry exactly one `== ContractStatus.Approved`,
zero `!= ContractStatus.Pending`, zero `EmployeeDocumentsMissing`; `employee.not_approved` present in all 5
partner locales (real translations). `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = 193 passed / 0 failed (was 181; 9 new gate tests). No EF, no nswag. Not committed.

- 2026-06-02 — done (reviewer APPROVED + security PASS; build 0 errors, 193 tests; gate + 5 locales
  independently re-verified by orchestrator). NOT committed.
