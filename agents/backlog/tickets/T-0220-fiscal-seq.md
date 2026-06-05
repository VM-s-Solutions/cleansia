---
id: T-0220
title: Gapless-monotonic-atomic fiscal sequence allocator (FiscalCounter) — replace COUNT(*)+1
status: draft
size: M
owner: —
created: 2026-06-03
blocks: []
depends_on: [T-0119]
stories: []
adrs: [0004]
layers: [backend, db]
security_touching: true
manual_steps: [ef-migration]
sprint: 2
source: ADR-0004 D-F4.2 (split off T-0119/F4); architect panel 2026-06-03
pairs_with: T-0127
---

## Context
ADR-0004 (`agents/backlog/adr/0004-fiscal-receipt-idempotency-boundary.md`) **D-F4.2** splits the fiscal
**sequence allocator** out of T-0119. The current allocator `GetNextSequenceForYearAsync`
(`src/Cleansia.Infra.Database/Repositories/OrderReceiptRepository.cs:35-47`) is literally
`COUNT(*) of receipts this year + 1` — **non-atomic** (two concurrent generations get the same number →
duplicate `ReceiptNumber`) and **gappy / count-basis-shifting** (a rolled-back or voided receipt shifts
the count). DE TSE / AT RKSV / ES VeriFactu legally require a **gapless, monotonic, per-issuer** fiscal
sequence. T-0119's claim-before-register reorder *creates* committed-but-unregistered rows that
`COUNT(*)+1` mis-counts, so the reorder + `COUNT(*)+1` pairing is **actively unsafe** for any gapless
regime.

It is safe to defer *now* only because every live country is `None`/`AsyncBackground` (CZ/SK/PL) with no
gapless legal requirement today. **This ticket is a HARD GO-LIVE GATE** (ADR-0004): DE/AT/ES MUST NOT be
set to `BlockingOnline`/`BlockingWithOfflineCache` in production until this lands.

## Acceptance criteria
- [ ] **AC1 — Atomic allocation.** A new `FiscalCounter` table keyed `(TenantId, Year[, IssuerScope])`
  with an atomic `UPDATE … SET Value = Value + 1 RETURNING Value` (or a PG `SEQUENCE` per scope; a
  `SELECT … FOR UPDATE` on a counter row is the acceptable simpler variant). N concurrent allocations →
  **N distinct contiguous numbers** (concurrency test).
- [ ] **AC2 — Allocated inside the claim transaction.** The number is allocated in the **same transaction
  that commits the T-0119 phase-1 claim**, so a successfully-claimed number is never rolled back relative
  to its row.
- [ ] **AC3 — Issuer-scoped per regime.** Gaplessness is enforced per **TSE / cash register / issuer**
  per regime — NOT merely per `(tenant, year)`. `IssuerScope` is defined explicitly per regime
  (document the mapping). DE TSE counting is **not** assumed year-reset like CZ EET numbering — confirm
  year semantics per regime (AC: a non-year-reset scope does not reset at year boundary).
- [ ] **AC4 — Void / cancellation support.** A reserved-but-never-signed number is a **documented gap**
  (void record), **never re-allocated**. A rolled-back/voided claim does **not** shift the next
  allocation (test).
- [ ] **AC5 — `GetNextSequenceForYearAsync` replaced.** `ReceiptService` allocates via the new mechanism;
  the old `COUNT(*)+1` is removed. No `ReceiptNumber` duplicate possible under concurrency.
- [ ] **AC6 — ef-migration flagged.** The `FiscalCounter` table folds into the owner's Initial regen
  (`manual_step: ef-migration`, owner-only). Tenant-scoped per S8.
- [ ] **AC7 — Tests (test-first):** the FISCAL-SEQ suite from ADR-0004 Verification — N-concurrent →
  N-distinct-contiguous; voided claim doesn't shift; issuer-scoped reset semantics.

## Out of scope
- The claim-before-register reorder + 23505 handling (that is T-0119 / ADR-0004 D-F4.1, this ticket's dep).
- The authority idempotency key (FISCAL-AUTH-IDEMP / T-0221).
- The reconciliation predicate widening (C-B, on T-0122).

## Implementation notes
- Governing ADR: **ADR-0004 D-F4.2** + the go-live gate. Depends on **T-0119** (the reorder that makes the
  allocator the live correctness surface). Serialization: touches `ReceiptService`/`OrderReceiptRepository`
  + a new entity — do not run concurrently with T-0119 or other fiscal tickets.
- TEST-FIRST; security_touching (regulatory). Migration is owner-only.

## Status log
- 2026-06-03 — draft (created by orchestrator from ADR-0004 D-F4.2 split; go-live gate for DE/AT/ES).

## Review
<!-- reviewer / security / optimizer write verdicts here -->
