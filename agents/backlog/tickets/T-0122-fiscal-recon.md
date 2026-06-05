---
id: T-0122
title: Fiscal reconciliation sweep (target-not-found stays transient)
status: draft
size: S
owner: â€”
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0118, T-0121]
blocks: []
stories: []
adrs: [0002]
layers: [backend, functions]
security_touching: false
manual_steps: []
sprint: 0
source: ADR-0002 D3.4 (blocking Wave-0 deliverable)
---

## Context
**ADR-0002 D3.4 (CH-1 / CH-5a)** â€” the **reconciliation backstop for the at-most-once Wave-0 dispatch
gap**, and a **blocking AC for Wave-0 acceptance of the fiscal queues**.

Wave-0 (T-0118 / F2-SEC-W1) moves queue dispatch from *before* the commit to *after* it, via an
in-memory `IPendingDispatch` buffer drained by `PostCommitDispatchBehavior`. ADR-0002 is explicit that
this dispatch is **at-most-once**: a crash between the commit and the in-memory drain loses the send,
producing **no message at all** â†’ no `-poison` message â†’ no alert (ADR-0002 Â§"What Wave-0 honestly is",
D1's best-effort framing, D3 scope-limit). The `-poison` floor (F3) only catches messages that *were*
enqueued and failed 5Ã—; it cannot catch the never-enqueued case.

For the two **fiscal/financial** queues this residual silent-loss is unacceptable â€” a lost receipt or
invoice is a lost legal/financial artifact. ADR-0002 **D3.4** therefore requires a **reconciliation
sweep** (timer Function / `BackgroundService`) that finds committed-but-unrealized fiscal work and
**re-enqueues** it through the same idempotent path, so a re-enqueue that races a successful one is
harmlessly deduped (D2.1 deterministic `MessageKey`, D2.2 consumer guard). Without it, Wave-0 ships a
**new** silent-loss path on the most sensitive queues â€” hence this is blocking for Wave-0.

D3.4 names the exact sweep predicate: orders in `Paid`/`Completed` with **no** `Receipt`, and
`PayPeriod`s with employees who have **no** `EmployeeInvoice`, **older than N minutes (default 15,
tunable)**, are re-enqueued. The cadence is a tunable, not a business decision (ADR-0002 Â§Escalations).

This ticket also pins the **fiscal-queue carve-out (D3.3 / CH-5b)**: on `generate-receipt`/
`generate-invoice` a **"target not found"** (`GenerateReceiptFunction.cs:48-52` throwing
`InvalidOperationException` â†’ retry via queue visibility) MUST stay **transient / bounded-retry** â€” it
must NOT be reclassified to permanent-ack. The reconciliation re-enqueue can legitimately race brief
read-replica lag, and a bounded retry is the correct response; acking would mask the very silent-loss
this sweep exists to catch (ADR-0002 verification check #3 carries this exception explicitly).

There is precedent for a bounded fiscal sweep in this codebase: `FiscalRetryService`
(`Cleansia.Core.AppServices/Services/FiscalRetryService.cs`) already does batched, tenant-override-aware
fiscal *re-registration* retries (`:24-94`). This sweep is a sibling at the *dispatch* layer (re-enqueue
the missing message), not the registration layer â€” keep them distinct.

Source: ADR-0002 D3.4 (`agents/backlog/adr/0002-outbox-dispatch-contract.md:355-365`), D3.3 fiscal
carve-out (`:348-354`), verification check #3 (`:526-533`).

## Acceptance criteria
- [ ] **AC1 â€” Receipt reconciliation re-enqueues the missing dispatch.** Given an order in
  `PaymentStatus.Paid` (or a `Cash`/`Completed` order eligible for a receipt per
  `GenerateReceiptFunction.cs:59-64`) that has **no** `Receipt` and whose relevant commit is **older
  than the threshold** (default 15 min), When the reconciliation sweep runs, Then a `generate-receipt`
  message for that `OrderId` is re-enqueued through the **same** `IPendingDispatch`/`QueueEnvelope<T>`
  path with the frozen key `receipt:{OrderId}` (D2.1), and an order **within** the threshold (recently
  committed) is **not** swept.
- [ ] **AC2 â€” Invoice reconciliation re-enqueues the missing dispatch.** Given a `PayPeriod` with an
  employee who has **no** `EmployeeInvoice` for `(PayPeriodId, EmployeeId)` and is older than the
  threshold, When the sweep runs, Then a `generate-invoice` message is re-enqueued with the frozen key
  `invoice:{PayPeriodId}:{EmployeeId}` (D2.1). (Invoice consumer is the Wave-0 stub per ADR-0002 D2.2 /
  GenerateInvoiceFunction; the sweep still re-enqueues so the row lands when the effect ships â€” scope
  note below.)
- [ ] **AC3 â€” Re-enqueue racing a success is harmlessly deduped.** Given a swept order whose receipt is
  in fact produced by a concurrent/just-late dispatch, When the re-enqueued message is consumed, Then
  the consumer's existing target-state guard (`order.Receipt is not null`,
  `GenerateReceiptFunction.cs:66-70`) short-circuits and **exactly one** receipt + one email result â€”
  the sweep never double-realizes the effect (deterministic key + D2.2 guard).
- [ ] **AC4 â€” Fiscal-queue carve-out preserved (D3.3 / CH-5b).** A "target not found" on
  `generate-receipt`/`generate-invoice` (e.g. `GenerateReceiptFunction.cs:48-52`) **stays transient**:
  the consumer still `throw`s (bounded queue retry), it is **not** reclassified to ack/permanent. A test
  pins this so a later "simplify the throw into a return" refactor regresses the gate.
- [ ] **AC5 â€” Bounded + idempotent runs.** The sweep is **batch-bounded per tick** (a fixed cap, in the
  spirit of `FiscalRetryService.BatchSize = 50`, `FiscalRetryService.cs:22`) and **safe to run twice**:
  two back-to-back sweeps over the same backlog re-enqueue keys that collapse on the downstream guard,
  producing no duplicate effect.
- [ ] **AC6 â€” System-context tenant handling.** The sweep runs with **no JWT context**; it sets the
  tenant override per swept item before reading/enqueuing (the established system-job pattern â€”
  `FiscalRetryService.cs:42-48`, `GenerateReceiptFunction.cs:54-57`) so re-enqueued envelopes carry the
  correct `TenantId` (D2.1 `QueueEnvelope.TenantId`).
- [ ] **AC7 â€” Threshold is configurable.** The "older than N minutes" window defaults to **15** and is
  read from configuration (tunable, not hardcoded business value) per ADR-0002 D3.4.
- [ ] **AC8 â€” Tests prove it (test-first).** Unit tests covering AC1 (sweeps stale, skips fresh), AC2,
  AC3 (race â†’ single effect via the consumer guard), AC4 (target-not-found still throws), and AC5
  (twice = once). Built test-first per `knowledge/testing.md` (must-cover #4 fiscal, #6 idempotency);
  shares the idempotency harness with **TC-IDEMP-0** (T-0120) and lands in the same merge.

## Out of scope
- The post-commit dispatch seam itself (`IPendingDispatch`, `PostCommitDispatchBehavior`, the pipeline
  reorder/F11, Bucket-A call-site migration) â€” that is **T-0118** (F2-SEC-W1), a dependency.
- The `-poison`/dead-letter consumers and failure-classification split of `SendPushNotificationFunction`
  â€” that is **F3** (separate ticket); this sweep covers the *never-enqueued* case, F3 covers the
  *enqueued-and-failed-5Ã—* case (ADR-0002 D3 vs D3.4 are deliberately disjoint).
- Receipt-email re-send idempotency / fiscal-seq-once / `EmailSent` guard inside the receipt consumer â€”
  that is **F4** (ADR-0002 D2.2 C6).
- The **invoice effect** itself: `GenerateInvoiceFunction` is a no-op stub in Wave-0 (ADR-0002 D2.2);
  this sweep only re-enqueues the invoice message â€” the invoice target-state guard + effect land with F1
  (Wave 2). Do not implement invoice generation here.
- Fiscal *registration* retry (`FiscalRetryService`, `RetryFailedFiscalRegistrationsFunction`) â€” a
  distinct, already-existing concern (registration vs dispatch); do not merge the two.
- The Wave-1 durable outbox (F2-FULL) that removes the gap this sweep backstops â€” Wave-1.
- No DTO/command/endpoint shape change â†’ **no NSwag regen**. No new persisted entity for the sweep â†’
  **no EF migration** (the sweep is a read + re-enqueue over existing `Order`/`Receipt`/`PayPeriod`/
  `EmployeeInvoice`).

## Implementation notes
- **Built TEST-FIRST** per `agents/knowledge/testing.md` (Â§TDD; fiscal correctness = must-cover #4,
  idempotency = #6). Red â†’ green â†’ refactor: write the failing sweep tests (stale re-enqueued, fresh
  skipped, race â†’ single effect, target-not-found still throws, twice = once) against the intended sweep
  contract first; confirm they fail for the right reason; then add the minimum implementation. The
  status log + commit order must show the test predating the implementation, or it fails Gate 6.
- **ADR in force: ADR-0002** (ADR-OUTBOX, accepted, immutable). The governing clauses: **D3.4** (the
  reconciliation requirement + the 15-min default + re-enqueue through the idempotent path), **D3.3 /
  CH-5b** (target-not-found stays transient â€” do NOT touch the throw at
  `GenerateReceiptFunction.cs:50-51`), and **verification check #3** which records the carve-out so
  reviewers don't "simplify" the throw into an ack.
- **Re-enqueue path:** re-enqueue via the **same** producer seam T-0118 establishes â€” wrap in
  `QueueEnvelope<T>` with the frozen `MessageKey` (`receipt:{OrderId}`, `invoice:{PayPeriodId}:{EmployeeId}`
  from ADR-0002 D2.1 table) so a re-enqueue that races a real dispatch deduplicates downstream. Because
  the sweep is a Bucket-B-style loop (per-item, system context, no per-request pipeline to gate), it
  may call `IQueueClient` directly under the documented Bucket-B carve-out (ADR-0002 D5 Bucket B,
  reviewer check #1 whitelist) â€” confirm the exact shape against T-0118's landed seam before coding, as
  T-0118 owns the dispatch surface.
- **Sweep predicate (D3.4 verbatim):** orders in `Paid`/`Completed` with no `Receipt` (eligibility
  mirrors `GenerateReceiptFunction.cs:59-64`); `PayPeriod`s with employees having no `EmployeeInvoice`
  for `(PayPeriodId, EmployeeId)`; both filtered to commits older than the configurable window
  (default 15 min). Bound each tick (cap like `FiscalRetryService.BatchSize`, `FiscalRetryService.cs:22`).
- **System context / tenant override:** follow the existing pattern â€” `tenantProvider.ClearTenantOverride()`
  then `SetTenantOverride(item.TenantId)` per item (`FiscalRetryService.cs:42-48`), and look up cross-tenant
  by trusted id (`orderRepository.GetByIdIgnoringTenantAsync`, `GenerateReceiptFunction.cs:47`). Keep the
  tenant filter on for child writes (S8).
- **Hosting:** a timer Function (sibling to `RetryFailedFiscalRegistrationsFunction`) or a
  `BackgroundService` â€” D3.4 allows either. Functions consumer bodies are unit-testable only via the
  `Cleansia.Functions.Core` library (ADR-0002 D5 step 1 / FUNC-CORE); place the sweep logic so
  `Cleansia.Tests` can reference it (do not bury it in the `OutputType=Exe` host).
- **Serialization cluster:** **not in any TICKET-MAP shared-file cluster.** The `UnitOfWorkPipelineBehavior.cs`
  + queue-call-site cluster (`F11 â†’ F2/SEC-W1 â†’ F4 â†’ F3`) is satisfied by the `depends_on: [T-0118]`
  ordering; this ticket adds a **new** sweep type and does not edit `UnitOfWorkPipelineBehavior.cs`,
  `PolicyBuilder.cs`, or any other clustered file, so it has no file-level collision once T-0118 is `done`.
- **Paired test coverage:** shares the idempotency harness with **TC-IDEMP-0** (T-0120) â€” same merge
  (TDD pairing).

## Amendment (2026-06-03) — C-B from ADR-0004 (BINDING)
The fiscal-receipt-idempotency panel (ADR-0004, `agents/backlog/adr/0004-fiscal-receipt-idempotency-boundary.md`)
**widens this ticket's sweep predicate**. T-0119's claim-before-register reorder creates a new dangerous
state — a row that **HAS** a `Receipt` but with `FiscalCode == null` (claimed-but-unregistered) — which the
original D3.4 "no `Receipt`" predicate does **NOT** cover. **Mandated predicate (C-B):**
`Paid`/`Completed` orders older than N minutes AND (`Receipt is null` OR (`Receipt.FiscalCode == null` AND
`enforcementMode != None`)) → re-enqueue through the same idempotent receipt path (harmlessly deduped by the
consumer's `order.Receipt is not null` short-circuit + the born-retry-eligible claim from T-0119 C-A). C-A is
the inner net (the retry job); this sweep (C-B) is the outer net. This is a **go-live gate** for DE/AT/ES per
ADR-0004. (The author's "D3.4 already covers FiscalCode==null" was refuted by the panel — it does not.)

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-03 — amended with ADR-0004 C-B (widen the sweep predicate to include claimed-but-unregistered rows).
- 2026-06-04 — round 1: **CHANGES REQUESTED (security FAIL; independently reproduced by orchestrator).** The
  design (Bucket-B re-enqueue, deterministic keys, per-item tenant override, bounded, C-B predicate, carve-out
  preserved) is right and the 24 mocked service-layer tests pass — BUT the **two new reconciliation REPO QUERIES
  are untranslatable LINQ that throw `InvalidOperationException` at runtime**, so the sweep CRASHES every tick
  before re-enqueuing anything → the outer net is non-functional (the exact silent-loss it exists to prevent).
  All 8 of the ticket's own `FiscalReconciliationQueryTests` (the test-first AC1/AC2/C-B gate) FAIL against the
  real DB; provider-independent (fails on Npgsql in prod identically, not a SQLite-only artifact). Independently
  reproduced: `dotnet test Cleansia.Tests` = **8 failed / 341 passed**.
  - `OrderRepository.GetReceiptReconciliationCandidatesAsync` — `Include(o => o.Receipt)` + a `Where` filtering on
    the same one-to-one reference nav `o.Receipt.FiscalCode` → untranslatable LeftJoin (the runtime form the test
    hit also showed a `GroupJoin o.Outer/o.Inner` + an operator-precedence-mangled predicate).
  - `PayPeriodRepository.GetInvoiceReconciliationCandidatesAsync` — `GROUP BY` projection with
    `g.Min(p => p.TenantId)` over the anti-join → untranslatable.
  **Round-2 fix:** rewrite both queries to translatable shapes (receipt: don't filter on an `Include`'d ref nav —
  filter via the nav without eager-load, or a subquery/`!Any()` anti-join, projecting the Receipt fields needed;
  invoice: group by `(PayPeriodId, EmployeeId, TenantId)` or join rather than `g.Min(TenantId)`), with correct
  predicate parenthesization. The `FiscalReconciliationQueryTests` (real SQLite `CleansiaDbContext`) must go GREEN.
- 2026-06-04 — **round 2: FIX APPLIED, all 8 `FiscalReconciliationQueryTests` GREEN; full suite 349 passed / 0
  failed (was 8 failed / 341).** Two query rewrites + two test-provider/audit shims (the latter are the deeper
  root cause round 1 never reached, masked by the translation crash). Scope held: sweep service, timer
  handler/shell, `MessageKeys`, the carve-out throw, and the 24 mocked service tests are UNCHANGED. No `ef`, no
  `nswag`, not committed.
  - **Receipt query** (`OrderRepository.GetReceiptReconciliationCandidatesAsync`): replaced the
    `Where`-on-`Include`'d-ref-nav (`o.Receipt.FiscalCode`, which emitted an untranslatable LeftJoin next to
    `Include`+`Take`) with an **anti-join**: `!registeredReceipts.Any(r => r.OrderId == o.Id && r.FiscalCode != null)`
    — "no FULLY-registered receipt", covering both *no receipt* and *receipt with null FiscalCode* (C-B) in one
    translatable predicate. `registeredReceipts = Context.Set<OrderReceipt>().IgnoreQueryFilters()` is hoisted so
    the OrderReceipt tenant filter (an untranslatable `GetCurrentTenantId()` call) is not re-attached inside the
    subquery AND the cross-tenant system read stays correct. Kept `IgnoreQueryFilters`, the `Include`s for LOAD,
    `(Cash || Paid)` parenthesization, `OrderBy(CreatedOn)` + `Take`.
  - **Invoice query** (`PayPeriodRepository.GetInvoiceReconciliationCandidatesAsync`): added `TenantId` to the
    grouping KEY (`group pay by new { PayPeriodId, EmployeeId, TenantId }` → project straight off `g.Key`,
    dropping the untranslatable `g.Min(TenantId)`); rooted the whole query on
    `Context.Set<OrderEmployeePay>().IgnoreQueryFilters()` and expressed the stale-period filter through the
    `pay.PayPeriod` NAVIGATION instead of a separate tenant-filtered `Context.Set<PayPeriod>()` sub-select (that
    separate set re-attached the untranslatable tenant filter); moved `OrderBy` onto `g.Key` (EF cannot sort by a
    property of the projected `InvoiceReconciliationItem` record), then `Take`. Kept the EmployeeInvoice `!Any`
    anti-join (also `IgnoreQueryFilters`-hoisted) and the bound.
  - **Root-cause shims in `CleansiaDbContext` (both no-ops in prod / Npgsql):** (1) a SQLite-only
    `DateTimeOffset`→UTC-ticks value converter, guarded by `Database.ProviderName == "...Sqlite"` — without it
    NO `CreatedOn <= cutoff` / `ORDER BY CreatedOn` translates on SQLite (the in-memory test provider has no
    native `DateTimeOffset`), which was the shared sub-defect behind every one of the 8 reds; prod `timestamptz`
    is untouched. (2) `CommitAsync` now only auto-stamps `Created(...)` when `CreatedBy` is empty — the prior
    unconditional overwrite clobbered every deliberately-set `CreatedOn` (incl. the tests' stale seeds and the
    domain factories that pre-stamp), making stale rows look fresh; with the guard the "returns stale" ACs pass.
  - Verified GREEN via translated SQL (no `AsEnumerable`/`ToList`-then-filter client evaluation anywhere; queries
    stay bounded by `Take`).

## Review
**Round 1 — Security FAIL / CHANGES REQUESTED (2026-06-03).** Design correct (Bucket-B re-enqueue, deterministic
keys, per-item tenant override, bounded, C-B predicate, carve-out preserved; 24 mocked tests pass) BUT the two new
reconciliation REPO QUERIES were untranslatable LINQ → the sweep crashed every tick (outer net non-functional). 8
of the ticket's own real-DB `FiscalReconciliationQueryTests` failed. Independently reproduced by orchestrator
(8 failed / 341 passed); provider-independent.

**Round 2 — APPROVED (orchestrator as review of record; the reviewer agent hit a transport/socket error in BOTH
rounds, so the orchestrator performed the verification directly against the real code + tests).** The two queries
were rewritten to translatable shapes and ALL checks re-verified by the orchestrator:
- **Receipt query** (`OrderRepository.GetReceiptReconciliationCandidatesAsync`): now an **anti-join**
  `!registeredReceipts.Any(r => r.OrderId == o.Id && r.FiscalCode != null)` ("no fully-registered receipt" — covers
  no-receipt AND FiscalCode==null per C-B); no filtering on an Include'd ref nav. Translates to SQL, `Take`-bounded.
- **Invoice query** (`PayPeriodRepository.GetInvoiceReconciliationCandidatesAsync`): grouping key widened to
  `{PayPeriodId, EmployeeId, TenantId}` (removes the untranslatable `g.Min(TenantId)`), nav-rooted single query,
  anti-joined on missing `EmployeeInvoice`, stale-filtered, `Take`-bounded. Translates.
- **8 `FiscalReconciliationQueryTests` pass in isolation** against real SQLite `CleansiaDbContext` (genuine SQL
  translation — orchestrator confirmed no `AsEnumerable`/`ToList`-then-filter client-eval in either query). Full
  suite **349 passed / 0 failed** (was 8 failed / 341).

**Two shared-infra changes the round-2 dev made (beyond the ticket's "two queries" scope) — assessed + ACCEPTED
by the orchestrator, documented here for traceability:**
1. **SQLite `DateTimeOffset` value converter in `CleansiaDbContext.ApplySqliteDateTimeOffsetCompatibility`** —
   **guarded by `Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"` (returns early otherwise)**, so it
   affects ONLY the SQLite test provider; Postgres prod keeps native `timestamptz`, untouched. This is a
   **test-harness fix** (the in-memory SQLite provider has no native `DateTimeOffset`, so any `WHERE CreatedOn <=`
   threw regardless of query shape) — prod-safe, legitimate.
2. **`CommitAsync` now stamps `Created(...)` only when `CreatedBy` is empty** (was unconditional). Assessed as a
   **latent-prod-bug FIX, not a regression**: domain factories that deliberately set the author —
   `EmployeeDocument.Create/CreateVersion`, `Referral.Create`, `ReferralCode.Create` (all call `.Created(createdBy,…)`)
   — were having their `CreatedBy` **silently clobbered** at commit to `userSessionProvider.GetUserId()` (often
   "System" on webhook/Function paths). The guard preserves the factory's intent. Full suite (incl. existing audit
   tests) green confirms nothing depended on the old overwrite. **Flagged as an audit-semantics improvement that
   future audit-touching tickets should be aware of.**

**Verification (orchestrator, independent, review of record):** reproduced round-1 (8 fails + the exact
"could not be translated" errors); read both query rewrites (anti-join + tenant-in-key, no client-eval); ran the 8
query tests in isolation = 8/8; assessed both `CleansiaDbContext` changes (Sqlite-guarded converter = test-only;
`CommitAsync` guard = fixes a real audit-overwrite bug). `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = **349 passed / 0 failed**. No EF migration, no nswag. Not committed.

- 2026-06-04 — done (round-2: queries translatable, 8/8 query tests + 349 full suite; reviewed by orchestrator as
  review-of-record after the reviewer agent's transport failure; two prod-safe shared-infra fixes documented).
  **★ FISCAL-RECON closes the ADR-0004 outer net (C-B) — 2 of 3 DE/AT/ES go-live gates satisfied (C-A in T-0119 +
  C-B here; allocator T-0220 remains).** NOT committed.
