---
id: T-0244
title: EmployeeInvoice.GenerateVariableSymbol — replace per-process GetHashCode with a deterministic stable hash
status: ready
size: S
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0213]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0213 (TC-6) carried finding — characterization tests pin within-run determinism only
---

## Context
Surfaced (not fixed) by **T-0213** (TC-6 invoice/numbering/pay-period tests). `EmployeeInvoice
.GenerateVariableSymbol` derives its value from `string.GetHashCode()`, which **.NET randomizes per
process by default** (it is stable only *within* a single process run, not across processes/restarts).

Today there is **no live bug**: the variable symbol is computed once at invoice creation and is not
recomputed in a different process expecting the old value. The risk is a **latent fiscal/payment-
reference correctness trap**: if the variable symbol is ever persisted and later **recomputed** in a
different process (a restart, a different host, a reconciliation/regeneration job) expecting equality
with the stored value, the two will silently **mismatch** because the hash basis changed between
processes. A mismatched variable symbol on a payment reference is a hard-to-detect, money-adjacent
correctness defect.

T-0213's `EmployeeInvoiceEntityTests` pin **only within-run determinism** (same inputs → same output
in the same process); they do NOT — and cannot, with the current implementation — assert
cross-invocation/cross-process determinism.

## Acceptance criteria
- [ ] **AC1 (deterministic basis)** — `EmployeeInvoice.GenerateVariableSymbol(employeeId,
  payPeriodId)` produces the **same** output for the same inputs **regardless of process** — replace
  the `string.GetHashCode()` basis with a deterministic stable hash (e.g. a fixed algorithm such as
  FNV-1a / a stable digest over the input bytes, reduced to the required digit width) **or**, if the
  symbol is meant to be persisted-and-never-recomputed, make that contract explicit (persist on
  create, never recompute) and remove the recompute path. The chosen approach is documented at the
  symbol.
- [ ] **AC2 (shape preserved)** — The output keeps its current observable shape (the `D4`+`D6`
  10-character numeric form pinned by T-0213's `EmployeeInvoiceEntityTests`) so existing references
  and the numbering/parity assertions still hold.
- [ ] **AC3 (cross-invocation determinism test)** — Add a test in `Cleansia.Tests` asserting
  cross-invocation determinism (same inputs → identical symbol) in a way that would have caught the
  per-process randomization — e.g. assert against a hard-coded expected value derived from the stable
  algorithm (not against a second in-process call, which the old code already passed). Red against the
  `GetHashCode` basis, green after the fix.
- [ ] **AC4 (no collateral change)** — Invoice numbering (`INV-yyyyMM-XXXXX`), `PaymentReference`, the
  amount/clamp math, and the status transitions are unchanged. No DTO/endpoint/schema change unless a
  persist-and-never-recompute decision (AC1 path b) requires a migration — flag `ef-migration` then.

## Out of scope
- The invoice numbering GUID suffix (separately backed by the DB unique index — not this ticket).
- The fiscal sequence allocator (T-0220) and receipt fiscal codes.
- Any change to how/when invoices are generated (T-0171/T-0180 own that).

## Implementation notes
- Symbol: `EmployeeInvoice.GenerateVariableSymbol`
  (`src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs`).
- T-0213's status log records the watch-out verbatim: "`GenerateVariableSymbol` relies on
  `string.GetHashCode()`, which is randomized per-process by default in .NET — if it were ever
  persisted and recomputed in another process expecting equality, it would not match. Not exercised
  today." T-0213's `EmployeeInvoiceEntityTests` already pin within-run determinism + the 10-digit
  shape — extend/replace with the cross-invocation assertion (AC3).
- If choosing persist-and-never-recompute (AC1 path b), verify no current code path recomputes the
  symbol from stored invoices before committing to that contract.

## Status log
- 2026-06-13 — draft (created by pm; T-0213 carried fiscal-reference finding made a ticket — Wave-5 candidate).
- 2026-06-13 — **ready** (PM, Wave-5 intake / Batch **5B**). Dep T-0213✓ is `done`. DoR met: AC1–AC4
  observable (cross-process determinism via stable hash, 10-digit shape preserved, cross-invocation test
  red-first), S, not security-touching. **Pure-logic / fiscal-reference → strict TDD (test predates
  code) + adversarial-style money review** per the standing money lesson. `manual_steps: []` for the
  default (stable-hash) path; **ef-migration flags ONLY if the dev chooses AC1 path-(b)
  persist-and-never-recompute** — if so, hold at the migration boundary and flag the owner (PM never
  runs it). Edits `EmployeeInvoice.cs` (domain) only — disjoint from other 5B files; parallel rider.)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
