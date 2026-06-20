---
id: T-0220
title: Gapless-monotonic-atomic fiscal sequence allocator (FiscalCounter) — replace COUNT(*)+1
status: done
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

## MANUAL_STEP: ef-migration (owner-only — do NOT run `dotnet ef`)

A new `FiscalCounters` table folds into the owner's Initial regen. Schema delta:

```
CREATE TABLE "FiscalCounters" (
    "Id"            character varying(26)  NOT NULL,
    "Year"          integer                NOT NULL,
    "IssuerScope"   character varying(100) NOT NULL,
    "Value"         bigint                 NOT NULL,
    "IsActive"      boolean                NOT NULL,
    "TenantId"      character varying(26)  NULL,
    "CreatedBy"     character varying(255) NOT NULL,
    "CreatedOn"     timestamp with time zone NOT NULL,
    "UpdatedBy"     character varying(255) NULL,
    "UpdatedOn"     timestamp with time zone NULL,
    "DeactivatedBy" character varying(255) NULL,
    "DeactivatedOn" timestamp with time zone NULL,
    CONSTRAINT "PK_FiscalCounters" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_FiscalCounters_TenantId" ON "FiscalCounters" ("TenantId");

-- NULLS NOT DISTINCT is load-bearing: the allocator's ON CONFLICT target keys on this index, and a
-- single-tenant (null TenantId) deployment must collapse onto ONE counter row per (Year, IssuerScope).
CREATE UNIQUE INDEX "IX_FiscalCounters_Tenant_Year_IssuerScope"
    ON "FiscalCounters" ("TenantId", "Year", "IssuerScope") NULLS NOT DISTINCT;
```

EF surfaces this from `FiscalCounterEntityConfiguration` (the unique index uses
`.AreNullsDistinct(false)`); just regenerate the Initial migration so the model snapshot matches. The
`Cleansia.IntegrationTests` FISCAL-SEQ suite (`FiscalCounterAllocatorTests`) is RED until this lands
(it fails on `PendingModelChangesWarning`) and goes GREEN once the table is in the applied migration.

## Status log
- 2026-06-03 — draft (created by orchestrator from ADR-0004 D-F4.2 split; go-live gate for DE/AT/ES).
- 2026-06-07 — db: FiscalCounter entity + atomic UPSERT allocator + ReceiptService wiring + FISCAL-SEQ
  integration suite (test-first, RED on the pending ef-migration). COUNT(*)+1 removed. ef-migration
  flagged for owner.

## Review
<!-- reviewer / security / optimizer write verdicts here -->

### Security review — 2026-06-08 — CHANGES_REQUESTED

Audited the FISCAL-SEQ allocator against S1–S10 + S7a/S7b and the regulatory guarantee
(gapless/monotonic/atomic; no duplicate `ReceiptNumber` under concurrency; no double-registration on
redelivery; tenant-scoped per S8).

**What is correct (PASS):**
- **AC1/AC2 (atomicity + same-tx).** `FiscalCounterRepository.AllocateNextAsync` runs
  `INSERT … ON CONFLICT ("TenantId","Year","IssuerScope") DO UPDATE SET "Value"="Value"+1 RETURNING "Value"`
  over `Context.Database`, which shares the connection of the `unitOfWork.BeginTransactionAsync` claim
  transaction in `GenerateReceiptHandler` (lines 106-129). The ON-CONFLICT row lock serializes
  concurrent allocators per scope → N concurrent → N contiguous; a rolled-back claim returns its
  number (counter is bound to the same tx). The integration suite uses real Postgres (Testcontainers),
  not SQLite — correct, since the row-lock/RETURNING semantics cannot be modelled on SQLite.
- **S7/S7b.** Allocation is inside the phase-1 claim tx that commits BEFORE the authority register and
  the PDF; redelivery short-circuits on `order.Receipt is not null`. The 23505-as-already-claimed catch
  flushes at the handler's own commit (correct per S7b).
- **S8 (entity).** `FiscalCounter : Auditable, ITenantEntity`; unique index is
  `(TenantId, Year, IssuerScope)` with `AreNullsDistinct(false)` and the ON-CONFLICT target matches it.
- **S1/S2/S3/S4/S6/S10** — N/A or PASS: queue-context handler (no JWT/DTO surface), raw SQL is fully
  parameterised (`NpgsqlParameter`), no PII logged, no soft-delete read surface.
- **AC6.** Migration correctly absent (owner-only `ef-migration`); suite is RED on
  `PendingModelChangesWarning` as documented.

**BLOCKERS (must fix before merge):**

1. **[BLOCKER] Cross-issuer-scope `ReceiptNumber` collision silently drops a real sale (AC3 vs AC5;
   S8; regulatory).** The visible number is `RCP-{currentYear}-{seq:D4}`
   (`ReceiptService.cs:64`) and does NOT encode `IssuerScope` or `TenantId`, but the counter is now
   partitioned on `IssuerScope`. Two scopes (e.g. `cz-eet2` and `sk-ekasa`) each start at 1 and both
   mint `RCP-2026-0001`. The GLOBAL unique `IX_OrderReceipts_ReceiptNumber`
   (`OrderReceiptEntityConfiguration.cs:39-41`, unchanged) throws 23505 on the second; the handler's
   catch (`GenerateReceiptHandler.cs:115-128`) classifies ANY 23505 as "already-claimed → ACK" and
   returns with NO receipt for that order. Because the allocation rolled back with the tx, every
   redelivery re-allocates the SAME counter value → SAME number → collides AGAIN → ACKs AGAIN →
   **permanent, silent, poison-free drop of a legitimate receipt** = the "silently unregistered sale"
   ADR-0004 forbids. Issuer-scoping (AC3) is structurally incompatible with a scope-blind unique
   `ReceiptNumber`. Fix: qualify the persisted number by scope (and tenant), e.g.
   `RCP-{scope}-{year}-{seq}`, OR make the receipt-number uniqueness key
   `(TenantId, FiscalProviderKey/Scope, ReceiptNumber)`, AND narrow the handler's 23505 catch so a
   `ReceiptNumber`-index violation is NOT collapsed to "already-claimed-for-this-order" (only the
   `OrderId`-index violation is the genuine redelivery case). Until then AC5 ("no `ReceiptNumber`
   duplicate possible") is not met for the multi-scope/multi-tenant deployments AC3 exists to support.

2. **[BLOCKER] Live CZ path silently switches from annual-reset to never-reset numbering (AC3
   year-semantics; wrong comment).** CZ today is `FiscalEnforcementMode.None`, so
   `ResolveProviderKeyAsync` returns `string.Empty` (`ReceiptService.cs:155-158`) →
   `FiscalSequenceScope.Resolve("", year)` → scope `DEFAULT`, and `ResetsAnnually("")` is FALSE →
   `NoAnnualResetYear (0)`. The DEFAULT scope therefore NEVER resets, yet the visible number still
   embeds `currentYear`. Old `COUNT(*)+1` was effectively annual-reset (counted by `IssuedAt` in-year).
   So on 2027-01-01 the live sequence will read e.g. `RCP-2027-0457`, not `RCP-2027-0001` — a silent,
   user-visible numbering-contract regression with no AC, test, or manual_step. The code comment at
   `ReceiptService.cs:151-152` ("the empty key resolves to the default annually-reset scope — matching
   CZ's current behaviour") is FALSE — DEFAULT is non-reset and this does NOT match prior behaviour.
   Fix: either map the no-fiscal/None CZ path to an annually-reset scope (preserve prior behaviour) or
   make the change explicit with an AC + manual_step; and correct the comment.

3. **[BLOCKER] `AsyncBackground`-with-unregistered-provider yields `"noop"` as the legal issuer scope
   (AC3).** When a country is `AsyncBackground` but its provider service is not registered (CZ today —
   `CzechEet2FiscalService` is DI-registered only when `Enabled`, which is never), the resolver returns
   `NoOpFiscalService.ProviderKey == "noop"` (`NoOpFiscalService.cs:13`,
   `FiscalServiceResolver.cs:33`). `ReceiptService.cs:162` then keys the gapless counter on the scope
   `"noop"` — a meaningless, non-regime issuer identity that also is not in the year-reset set. The
   issuer scope that anchors a LEGALLY gapless sequence must never silently degrade to `"noop"`. Fix:
   when enforcement != None but no real provider resolves, fail closed (or resolve scope from the
   country/regime mapping, not from the no-op fallback's ProviderKey).

**Non-blocking:**
- (nit) `FiscalCounterRepository.AllocateNextAsync` reads `allocated[0]` — safe (DO UPDATE always
  RETURNs one row), but an explicit single-row assert would harden against a future ON-CONFLICT-DO-NOTHING
  edit.

Verdict: **CHANGES_REQUESTED.** Allocator core (atomic, same-tx, gapless-per-scope, void-safe) is
sound and well-tested; the blockers are in how the scoped sequence projects onto the scope-blind
visible `ReceiptNumber` + global unique index, and in scope-resolution fallbacks. These are
regulatory/tenant-isolation correctness issues, not the deferrable gapless-law gate, so they must
close before merge even though DE/AT/ES go-live is separately gated.
