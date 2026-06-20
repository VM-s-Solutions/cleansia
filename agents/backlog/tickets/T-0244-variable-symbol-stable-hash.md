---
id: T-0244
title: EmployeeInvoice.GenerateVariableSymbol — replace per-process GetHashCode with a deterministic stable hash
status: done
size: S
owner: —
created: 2026-06-13
updated: 2026-06-14
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
- 2026-06-13 — **review** (backend). Chose **AC1 path (a): stable-hash** (no migration; preserves the
  exact compute-on-demand contract — `GenerateInvoice.Handler` does not call `GenerateVariableSymbol`,
  and the two read paths — `IEmployeeInvoiceRepository.GetByVariableSymbolAsync` +
  `EmployeeInvoiceSpecification` — only **query** stored `VariableSymbol`; nothing recomputes-and-compares,
  so persist-and-never-recompute would add a needless migration for no behavior gain).
  **Change (test-first):**
  - `EmployeeInvoice.GenerateVariableSymbol` now derives `empHash`/`periodHash` from a private
    **FNV-1a-32 over the UTF-8 input bytes** (offset 2166136261, prime 16777619) instead of
    `string.GetHashCode()`. Output shape unchanged: `{empHash%10000:D4}{periodHash%1000000:D6}` →
    10 numeric chars. Why documented at the symbol.
  - Test (added FIRST, red-then-green) in `EmployeeInvoiceEntityTests`:
    `GenerateVariableSymbol_Matches_Stable_Hash_Expected_Value` — a `[Theory]` asserting hard-coded,
    **independently-derived** expected values (`emp-1`/`period-1` → `1883454606`, `emp-1`/`period-2`
    → `1883676987`, computed offline from the FNV-1a spec, NOT a second in-process call). The
    existing T-0213 characterization nets (within-run determinism, 10-digit shape, differs-across-periods)
    stay **unchanged and green** as the safety net.
  - **AC3 red proven:** a standalone runner of the OLD `GetHashCode` basis produced `6030367204` in
    one process and `3116299562` in a separate process — neither equals the test's `1883454606`, so the
    test fails against the old basis AND exposes the per-process randomization directly.
  **Test evidence:** `Cleansia.Core.Domain` builds clean (0 err). `dotnet test Cleansia.Tests.csproj
  --filter ~EmployeeInvoiceEntityTests` ran **30/30 Passed** when implementation + tests were freshly
  co-compiled (incl. the 2 new theory cases). A subsequent `--no-build` reran a STALE DLL (from an
  interim stash used to prove red) and a fresh full rebuild is currently blocked by an **unrelated
  concurrent lane** — `Cleansia.Infra.Services/ServiceCollectionExtensions.cs` references a not-yet-added
  `Cleansia.Infra.Services.Templates` namespace (`ITemplateEngine`/`HandlebarsTemplateEngine`),
  CS0234 — outside this ticket's scope; NOT touched. Orchestrator's authoritative clean run will
  re-link once that lane lands.
  **AC status:** AC1 ✓ (stable FNV-1a basis, documented), AC2 ✓ (D4+D6 10-char shape preserved,
  T-0213 shape net green), AC3 ✓ (cross-invocation test with independently-derivable hard-coded value;
  red against old basis demonstrated), AC4 ✓ (numbering / `PaymentReference` / clamp math / status
  transitions untouched; no DTO/endpoint/schema change).
  **Deviations:** none. **MANUAL_STEPs:** none (stable-hash path; no migration, no DTO shape change → no
  nswag-regen). Files: `src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs`,
  `src/Cleansia.Tests/Features/EmployeePayroll/EmployeeInvoiceEntityTests.cs`.
  **Follow-up flag (not fixed here):** the shared tree currently fails a graph build due to the
  Infra.Services Templates lane (CS0234) — informational for the orchestrator, not a defect in this lane.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
