# ADR-0004 — Fiscal receipt idempotency boundary: claim-before-register, gapless sequence, and the safe held-receipt residual

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-03
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | functions | cross-cutting (fiscal/regulatory)
- **Extends:** ADR-0002 (D2.2 C6/F4 residual, D3.3 fiscal carve-out, D3.4 reconciliation)
- **Ticket:** T-0119 (F4) · **Follow-ups:** FISCAL-SEQ (**T-0220**), FISCAL-RECON (D3.4 / existing T-0122, amended by C-B), FISCAL-AUTH-IDEMP (**T-0221**)

> **Ticket-id note (orchestrator):** the lead draft referenced the split-offs as "T-0123/T-0124", which
> COLLIDE with existing tickets (T-0123 = PROD-CONFIG, T-0124 = PERF-IDA-01). The split-offs are filed at
> the real next-free ids: **FISCAL-SEQ = T-0220**, **FISCAL-AUTH-IDEMP = T-0221**. FISCAL-RECON's C-B
> amendment lands on the existing **T-0122**.

> **Why its own ADR and not an ADR-0002 addendum.** ADR-0002 froze the *generic* side-effect dispatch
> contract (post-commit dispatch, idempotent consumers, poison floor). The fiscal half of F4 is a
> distinct, regulation-bearing decision: it adds an *ordering inversion on an irreversible external
> effect*, names a *gapless-monotonic-atomic sequence allocator* as a hard go-live gate, and defines
> *per-provider go-live gates* for DE TSE / AT RKSV / ES VeriFactu. It carries three follow-up tickets
> and two compliance go-live gates. That is an ADR, not a footnote. It **extends** ADR-0002 D2.2/D3.3/
> D3.4 and is immutable once accepted — change it by superseding.

---

## Context

ADR-0002 D2.2 (closed by **T-0118 / F2**) made the receipt *email* re-send idempotent: the receipt
row is committed (`GenerateReceiptHandler.cs:125`, or the blocking-hold commit at `:109`) **before**
`SendOrderReceiptEmailAsync` (`:130`), and a redelivery short-circuits on `order.Receipt is not null`
(`:85`). The **fiscal half remained open**, and it is the regulatory one.

**The fiscal window (verified).** `ReceiptService.GenerateReceiptAsync`
(`src/Cleansia.Core.AppServices/Services/ReceiptService.cs:27`) does ALL of the following **before** the
handler commits the row:
- `:49` allocate the sequence via `receiptRepository.GetNextSequenceForYearAsync`;
- `:55-56` `OrderReceipt.Create` + `receiptRepository.Add(receipt)` (staged, NOT committed);
- `:79 → :130-132` `HandleFiscalAsync` → the **external** tax-authority call `fiscalService.RegisterReceiptAsync`;
- `:89-93` generate PDF + upload to blob.

The handler only commits at `GenerateReceiptHandler.cs:125` (or the hold path `:109`). So a crash in
`[handler :92, handler :125)` leaves **no committed receipt row** → a redelivery sees
`order.Receipt is null` (`:85`) → **re-runs `GenerateReceiptAsync`** → a **second fiscal sequence
consumed and a second authority registration**. Azure Storage queues are at-least-once
(`host.json maxDequeueCount: 5`), so this is a live path; the `order.Receipt is not null` guard does
**not** catch it because the row was never committed. For BlockingOnline regimes (DE TSE, AT RKSV, ES
VeriFactu) a double-consumed sequence or a double authority registration is a **compliance incident**,
not a data nit.

**The sequence allocator is independently fragile.** `GetNextSequenceForYearAsync`
(`OrderReceiptRepository.cs:35-47`) returns `COUNT(*) of receipts this year + 1`. It is:
- **non-atomic / race-prone** — two concurrent generations both `COUNT = N`, both get `N+1` → duplicate
  `ReceiptNumber`; the unique `IX_OrderReceipts_ReceiptNumber` (`OrderReceiptEntityConfiguration.cs:39-41`)
  throws PG 23505 on the second commit (caught today only as a crash), and a *redelivery* gets a
  *different* number so that index does **not** dedup the redelivery case;
- **gappy / count-basis-shifting** — a rolled-back or deleted receipt shifts the count; `COUNT(*)+1`
  cannot guarantee the gapless, monotonic, per-issuer sequence DE/AT/ES legally require.

**Verified facts that shape this decision (re-derived against current code by the panel):**
1. **A UNIQUE index on `OrderReceipts.OrderId` ALREADY EXISTS.** The one-to-one
   `Order.Receipt` (`OrderEntityConfiguration.cs:116-119`) emits `IX_OrderReceipts_OrderId ... unique: true`
   (`Initial.cs:2534-2538`). **Receipt is one-per-order, NOT one-per-(order, language)** — the
   `(OrderId, LanguageId)` index (`Initial.cs:2529-2532`, non-unique) is a redundant secondary index;
   `LanguageId` only records which language the single receipt was rendered in. The earlier
   "no unique constraint prevents two receipts for one order" framing was **false**. **Consequence: the
   DB backstop already ships → no migration is needed for the at-most-once-claim guarantee.**
2. **`RegisterReceiptAsync` / `FiscalReceiptRequest` carry NO idempotency key**
   (`IFiscalService.cs:29-31`, `FiscalReceiptRequest.cs:7-20`). An authority-side idempotency key is a
   future `IFiscalService` contract change, not a Wave-0 lever.
3. **The retry job is blind to the new residual.** `GetDueForRetryAsync` filters
   `FiscalRegistrationFailed == true && FiscalNextRetryAt != null && FiscalNextRetryAt <= utcNow`
   (`OrderReceiptRepository.cs:62-64`). A claimed-but-unregistered row has
   `FiscalRegistrationFailed == false, FiscalCode == null, FiscalNextRetryAt == null` (entity defaults,
   `OrderReceipt.cs:47,73`; `Create` sets neither, `:84-101`) → **invisible to the retry job.**
   `ScheduleImmediateFiscalRetry` (`OrderReceipt.cs:117-120`) sets **only** `FiscalNextRetryAt`, NOT
   `FiscalRegistrationFailed`, so it alone does **not** make a row retry-eligible. Only
   `MarkFiscalRegistrationFailed` (`:133-148`) flips both flags.
4. **Today the fiscal stamp is in the SAME commit as the row** — `HandleFiscalAsync` calls
   `SetFiscalData` on the in-memory entity (`ReceiptService.cs:136`) and the handler's single commit
   (`:125`) persists row + stamp atomically. Splitting the register out from the claim commit
   introduces a *second* commit and a new "registered-but-stamp-not-persisted" window.
5. **The retry path re-registers with the same `ReceiptNumber`** (`ReceiptService.cs:239`) and the
   `FiscalRetryService` only releases the held email for blocking modes (`FiscalRetryService.cs:66`).
6. **No live BlockingOnline country today** — CZ is `None`/`AsyncBackground` (`FiscalEnforcementMode.cs:13,21`);
   DE/AT/ES are `BlockingOnline` (`:31`). This is why the allocator split is *currently* safe and *must*
   close *before* DE/AT/ES go live.

---

## Decision

> **Contract principle.** The durable **claim** = the committed `OrderReceipt` row carrying the
> allocated sequence. It MUST commit **before any irreversible external effect** — before
> `RegisterReceiptAsync` and before the PDF. A redelivery after the claim sees `order.Receipt is not
> null` and never re-burns a sequence or re-registers. The accepted residual is a **rare held /
> claimed-but-not-fully-realized receipt that retry + reconciliation complete — NEVER a double sequence
> and NEVER a double authority registration.**

### D-F4.1 — At-most-once receipt + sequence + authority registration per OrderId

A **combination**, with claim-before-register as the primary mechanism:

**(a) Reserve-then-claim (primary).** Restructure the consumer into three phases:
1. **Assert + reserve + commit the claim.** Keep the in-process fast path (`order.Receipt is not null`,
   `:85`). Then allocate the sequence, `OrderReceipt.Create` + `Add`, **and `CommitAsync` the row now —
   before the authority call and before the PDF.** The receipt is committed in a *fiscal-pending* state
   (`FiscalCode == null`) and is **born retry-eligible** (see D-F4.3 / C-A).
2. **Realize the external effects.** Call `RegisterReceiptAsync`; stamp `SetFiscalData` on success
   (which clears the retry-eligibility) or `MarkFiscalRegistrationFailed` on failure; generate + upload
   the PDF; commit the fiscal-result stamp.
3. **Terminal email** stays exactly where T-0118 put it (blocking-mode holds until signed).

After phase-1 commit, every redelivery short-circuits at `:85` → no second sequence, no second
registration. A crash *inside* phase 1 (before its commit) leaves no row and no external effect yet, so
re-running phase 1 is safe.

**(b) DB backstop = the EXISTING unique index, with 23505-as-already-claimed.** Two concurrent
first-deliveries can both pass `:85` and both attempt the phase-1 commit. The existing
`IX_OrderReceipts_OrderId` (unique) makes the loser throw PG **23505**. The consumer MUST **catch
23505 and treat it as already-claimed** (re-read the row, short-circuit / ack) — NOT bubble it as a
crash. Because the allocator is still `COUNT(*)+1` in T-0119, the loser may also collide on the unique
`IX_OrderReceipts_ReceiptNumber`; the 23505 handler MUST treat a violation of **either**
`IX_OrderReceipts_OrderId` **or** `IX_OrderReceipts_ReceiptNumber` as already-claimed-collapse-to-ack,
and throw only on genuine infra faults (D3.3 classification preserved). **No new index, no migration.**

**(c) Authority-side idempotency key — DEFERRED but NAMED (FISCAL-AUTH-IDEMP).** `RegisterReceiptAsync`
has no idempotency field today (fact 2). Until it does, the natural idempotency token is the
`ReceiptNumber` itself; see D-F4.3 / C-D for the go-live gate this creates on the retry path.

**S8 note (explicit, greppable exception).** `IX_OrderReceipts_OrderId` is unique on **`OrderId`
alone**, not `(TenantId, OrderId)`. This is **safe** because `OrderId` is a globally-unique ULID, so no
two tenants can share one, and a null-`TenantId` single-tenant row must still be unique per order
(adding `TenantId` to the key would *weaken* it for null-tenant rows). This is recorded as a **reasoned
S8 exception**, NOT "S8 silently respected" — a reviewer running the S8 tenant-scoped-unique-index grep
WILL flag this index and must find this justification, and MUST NOT "fix" it to `(TenantId, OrderId)`.

### D-F4.2 — The `COUNT(*)+1` allocator: in-scope as a decision, SPLIT in delivery

`COUNT(*)+1` (`OrderReceiptRepository.cs:42-46`) **cannot** be gapless/monotonic/atomic. It is **IN
SCOPE for this fiscal-correctness decision but SPLIT in delivery**:
- **T-0119** does NOT change `GetNextSequenceForYearAsync`. It ships the at-most-once claim + the
  23505-as-already-claimed handling (which is required so the reorder does not regress concurrent
  behavior into a poison loop now that the sequence rides in the phase-1 claim commit).
- **FISCAL-SEQ (proposed T-0123)** replaces the allocator with a **gapless-monotonic-atomic** mechanism:
  a dedicated **`FiscalCounter` table keyed `(TenantId, Year[, IssuerScope])`** with an atomic
  `UPDATE ... SET Value = Value + 1 RETURNING Value` (or a PostgreSQL `SEQUENCE` per scope; a
  `SELECT ... FOR UPDATE` on a counter row is the acceptable simpler variant), **allocated inside the
  same transaction that commits the phase-1 claim** so a successfully-claimed number is never rolled
  back. It MUST: (i) be **issuer-scoped per regime** — DE/AT/ES require gaplessness per TSE / per cash
  register / per issuer, NOT merely per `(tenant, year)`; "IssuerScope" is to be defined explicitly per
  regime; (ii) **not assume an annual reset** (e.g. DE TSE counting is not year-reset like CZ EET
  numbering) — confirm year semantics per regime; (iii) support **void / cancellation records** so a
  reserved-but-never-signed number is a documented gap, never re-allocated.

**FISCAL-SEQ is a HARD PRECONDITION of the claim-before-register reorder for any gapless regime — not a
free-floating "nice to land first."** The reorder *creates* committed-but-unregistered rows with real
`ReceiptNumber`s; under `COUNT(*)+1` those rows shift the count, and if such a row is later deleted/
voided the count shifts again → a later number is skipped or reused. So the **reorder + `COUNT(*)+1`
pairing is actively unsafe the moment a BlockingOnline (or gapless-law AsyncBackground) country goes
live.** It is safe to defer *now* only because every live country is `None`/`AsyncBackground` with no
gapless legal requirement today (fact 6).

### D-F4.3 — Ordering vs the external authority call + retry reconciliation

**Commit the claim (sequence + row) BEFORE the authority call.** This is the only ordering that makes
the irreversible external effect at-most-once. The forbidden ordering (commit the claim *after* a
successful register) is **rejected**: a crash after a successful registration but before the commit
leaves no row, and a redelivery re-registers — the exact F4 incident.

This is regime-aligned, not in tension with any authority: DE TSE / AT RKSV / ES VeriFactu all operate
**number-first, then-sign** (the issuer assigns the document number / transaction count; the authority
signs/registers it and returns a signature/QR). No regime requires the *authority* to mint the
sequence. The one rule: **a number allocated but never signed is a documented pending → completed-by-
retry, else a documented void — never re-allocated** (FISCAL-SEQ void support, above).

**Recovery uses the EXISTING retry + reconciliation machinery, with two mandated corrections:**

- **C-A (BLOCKING, in T-0119) — the claim is born retry-eligible in ALL fiscal modes ≠ `None`.** A
  crash between the phase-1 claim commit and the register call leaves
  `FiscalRegistrationFailed == false, FiscalCode == null, FiscalNextRetryAt == null` →
  **invisible to `GetDueForRetryAsync`** (fact 3). The author's "stamp retry-eligibility in the
  blocking-hold branch" fix does NOT work: (i) that branch runs only *after* `GenerateReceiptAsync`
  returns, so a crash *before* the register never reaches it; and (ii) `ScheduleImmediateFiscalRetry`
  alone does not set `FiscalRegistrationFailed`, so the row still fails the filter. **Mandate:** at
  **claim time** (phase 1), for any `enforcementMode != None`, the receipt is committed in a
  retry-eligible pending state — i.e. `FiscalNextRetryAt` is set **and** the row is matchable by the
  retry query — and `SetFiscalData` clears it on success. **This REQUIRES widening `GetDueForRetryAsync`
  to also return `FiscalCode == null && FiscalNextRetryAt != null && FiscalNextRetryAt <= utcNow`
  regardless of the `FiscalRegistrationFailed` flag.** This is a *behavioral change to the retry query*,
  contrary to the author's "no retry-repo change" claim. (The existing partial filtered index
  `IX_OrderReceipts_FiscalNextRetryAt` on `FiscalNextRetryAt IS NOT NULL` already supports the widened
  query — **no migration**.)
- **C-B (BLOCKING for FISCAL-RECON) — widen the D3.4 reconciliation predicate.** ADR-0002 D3.4 sweeps
  `Paid`/`Completed` orders with **no** `Receipt`. Under claim-before-register the dangerous row **has**
  a Receipt with `FiscalCode == null` — which the current D3.4 spec does NOT cover. The author's claim
  that D3.4 already covers "missing receipt OR FiscalCode == null" is **false against the ADR-0002
  text**. **Mandate:** FISCAL-RECON's predicate becomes `Paid`/`Completed` AND (`Receipt is null` OR
  (`Receipt.FiscalCode == null` AND `enforcementMode != None`)) older than N minutes → re-enqueue
  through the same idempotent path (harmlessly deduped by `:85`). C-A is the inner net; C-B is the
  outer net.
- **C-C (registered-but-stamp-not-persisted residual — NAME it; gate it).** The two-commit split (fact
  4) introduces a window where the authority HAS registered but the `FiscalCode` is not yet persisted; a
  redelivery short-circuits on `:85` and the code is never written, leaving the row permanently
  `FiscalCode == null` *despite a successful registration*. Recovery runs `RetryFiscalRegistrationAsync`
  which **re-calls `RegisterReceiptAsync` with the same `ReceiptNumber`** (fact 5). Whether that
  double-registers at the authority is **provider-dependent and unverified** (fact 2 — no idempotency
  contract). For `AsyncBackground` (CZ/SK/PL today) a rare extra registration is tolerable; for
  **BlockingOnline it is the compliance incident.** **Mandate (C-D go-live gate):** before any
  BlockingOnline provider goes live, its `RegisterReceiptAsync` MUST be verified + documented to be
  **idempotent on `ReceiptNumber`** (returns the prior code; does not burn a new authority entry),
  pending the FISCAL-AUTH-IDEMP `IFiscalService` idempotency-key contract change.

---

## Go-live gates (the single most important thing this ADR records)

BlockingOnline countries (DE / AT / ES) **MUST NOT be set to `BlockingOnline` /
`BlockingWithOfflineCache` in production until ALL of the following close:**

1. **FISCAL-SEQ** — the gapless-monotonic-atomic, issuer-scoped, void-supporting allocator has replaced
   `COUNT(*)+1` (D-F4.2). The reorder + `COUNT(*)+1` pairing is unsafe for gapless regimes.
2. **Per-provider register-idempotency on `ReceiptNumber`** verified + documented (C-C / C-D), pending
   FISCAL-AUTH-IDEMP.
3. **FISCAL-RECON covers the claimed-but-unregistered case** (C-B), and the **born-retry-eligible
   claim** (C-A) is live in T-0119.

This joint gate is the line between "rare held receipt that self-heals" (safe) and "double registration
/ gappy sequence / silently-unregistered sale" (compliance incident).

---

## The accepted residual (the SAFE direction)

A rare receipt that is, on a crash inside the post-claim window, in one of:
- **(a) claimed-but-not-registered** (`FiscalCode == null`) — recovered by the born-retry-eligible
  claim (C-A) → `GetDueForRetryAsync` → `FiscalRetryService.RetryFiscalRegistrationAsync`, and by the
  widened reconciliation sweep (C-B) as the outer net;
- **(b) registered-but-stamp-not-persisted** (authority has it, `FiscalCode == null` locally) —
  recovered by re-registration with the same `ReceiptNumber`, **safe only under the C-C/C-D per-provider
  idempotency gate**;
- **(c) claimed-but-email-not-sent** — the existing T-0118 / D2.2 lost-email residual; held in blocking
  mode and released by the retry job.

It is **NEVER a double sequence and NEVER a double authority registration** (the claim is durable before
the irreversible effect), and — once C-A/C-B land — **never a silently-and-permanently-unregistered
sale.** This is the **at-most-once-after-the-claim** shape, symmetric with ADR-0002 D2.2's accepted
lost-email and at-most-once-push residuals, one regulatory layer deeper. A double authority registration
is a *compliance* incident; a held receipt is an *operational* delay — we trade the compliance risk away
entirely.

---

## How this reconciles with ADR-0002 D2.2 / D3.3 / D3.4 and the existing retry path

- **D2.2 (C6):** unchanged for the email; this ADR adds the *fiscal* claim-before-register on top of the
  already-landed email claim-first. The phase-1 claim commit simply moves *earlier* (before register),
  so the email hold/release contract is undisturbed.
- **D3.3 (fiscal carve-out):** preserved exactly — "target not found" stays transient/throws
  (`GenerateReceiptHandler.cs:63-67`); a malformed body stays acked (`:48-51`). The new 23505 handling
  is an *additional* permanent-already-done classification, not a reclassification of the carve-out.
- **D3.4 (reconciliation):** extended by C-B (predicate widened to include `FiscalCode == null`).
- **Existing retry path (`FiscalRetryService` + `GetDueForRetryAsync`):** reused; the only change is the
  widened `GetDueForRetryAsync` query (C-A) so the born-retry-eligible claim is swept. The
  `HandleFiscalAsync`-never-throws behavior (`ReceiptService.cs:152-160`) already makes
  claimed-but-unregistered a first-class recoverable state — the reorder makes a *crash* land in the
  same state a *fiscal error* already lands in.

---

## Consequences

**Cheaper / safer:**
- The live double-sequence + double-authority-registration window is **closed at every crash point** (not
  merely shrunk): the claim is durable before any irreversible external effect.
- Reuses the existing retry + reconciliation machinery; T-0119 needs **no schema change** (the unique
  backstop already ships; the partial retry index already supports the widened query).

**More expensive (new obligations):**
- The receipt consumer is restructured into reserve-then-claim phases (the claim commit precedes the
  register), with born-retry-eligible claims and a 23505-as-already-claimed catch.
- `GetDueForRetryAsync` gains a `FiscalCode == null && FiscalNextRetryAt <= now` arm (C-A).
- FISCAL-RECON gains the `FiscalCode == null` predicate (C-B).
- **Go-live gates** (above) bind DE/AT/ES to FISCAL-SEQ + per-provider register-idempotency + the C-A/C-B
  recovery before they may be `BlockingOnline` in production.

**Accepted residual:** the rare held / claimed-but-not-fully-realized receipt described above — the SAFE
direction, completed by retry + reconciliation, never a double sequence / double registration / silently
unregistered sale (once C-A/C-B land).

---

## Verification (the gate)

Extend the T-0119 / T-0127 `TC-IDEMP-0` suite in `Cleansia.Tests` against `Cleansia.Functions.Core`:
- **AC-F4.1 (the gate) — twice-invocation → realize once.** Invoke `HandleAsync` twice with the same
  `QueueEnvelope<GenerateReceiptMessage>` (`receipt:{OrderId}`); after the first run commits the claim,
  assert the reserve/generate path **and** `IFiscalService.RegisterReceiptAsync` **and**
  `IEmailService.SendOrderReceiptEmailAsync` are each invoked **exactly once**.
- **AC-F4.2 — crash-then-redeliver does not re-register.** Crash after `RegisterReceiptAsync` returns
  but after the claim commit → redeliver → `RegisterReceiptAsync` NOT called again (`:85` short-circuit).
  Inverse: crash *before* the claim commit → redeliver → exactly one register total.
- **AC-F4.3 — concurrent first-delivery → 23505 graceful.** Two parallel `HandleAsync` for the same
  OrderId → exactly one row, exactly one register; the loser's 23505 on **either**
  `IX_OrderReceipts_OrderId` **or** `IX_OrderReceipts_ReceiptNumber` is caught and **acked** (no `throw`,
  no poison loop).
- **AC-F4.4 — claimed-but-unregistered is retry-eligible.** Crash between claim-commit and register →
  the committed row is returned by `GetDueForRetryAsync` (born retry-eligible, C-A) → `FiscalRetryService`
  completes it.
- **AC-F4.5 — classification preserved (D3.3).** target-not-found throws (`:63-67`); malformed acks
  (`:48-51`). Unchanged.
- **FISCAL-SEQ suite (separate ticket):** N concurrent allocations → N distinct contiguous numbers; a
  rolled-back/voided claim does not shift the next allocation; issuer-scoped reset semantics.

---

## Disposition of the panel challenges

| # | Challenge (challenger) | Disposition | Where |
|---|---|---|---|
| Correction-1 | Unique OrderId index already exists; receipt one-per-order; no migration for the backstop (author) | ACCEPTED — verified `Initial.cs:2534-2538`, `OrderEntityConfiguration.cs:116-119` | Context fact 1; D-F4.1(b) |
| Correction-2 | No authority idempotency key; option (c) deferred (author) | ACCEPTED — verified `IFiscalService.cs:29`, `FiscalReceiptRequest.cs` | fact 2; D-F4.1(c) / C-D |
| Hole A / Hole 1 | Born-retry-eligible must be at claim time, in ALL modes; `ScheduleImmediateFiscalRetry` alone is dead; `GetDueForRetryAsync` must widen (A + B) | ACCEPTED (BLOCKING, C-A) — author's hold-branch fix REFUTED; retry-query change MANDATED | D-F4.3 / C-A |
| Hole B | AsyncBackground crash-before-register → silent unregistered sale (Challenger A) | ACCEPTED — C-A applies to all modes ≠ None | D-F4.3 / C-A |
| Hole C / Hole 1b | D3.4 predicate only covers missing receipt, not `FiscalCode == null` (A + B) | ACCEPTED (BLOCKING for RECON, C-B) — author's "D3.4 already covers it" REFUTED | D-F4.3 / C-B |
| Hole 2 / C4-C5 | Two-commit split → registered-but-unstamped residual; retry re-registers same ReceiptNumber (Challenger B) | ACCEPTED — residual NAMED; per-provider idempotency made a go-live gate | D-F4.3 / C-C / C-D |
| 23505 catch | Existing `catch(Exception){throw;}` re-throws; must narrow + ack on either unique index (Challenger B) | ACCEPTED — MANDATED | D-F4.1(b); AC-F4.3 |
| S8 exception | Non-tenant-scoped unique index must be a reasoned, greppable exception (Challenger B) | ACCEPTED (TRIMMED) — recorded as explicit exception; do NOT convert to `(TenantId, OrderId)` | D-F4.1 S8 note |
| FISCAL-SEQ dependency | Reorder + `COUNT(*)+1` is actively unsafe for gapless regimes; SEQ is a hard precondition, issuer-scoped, void-supporting (A + B) | ACCEPTED — SEQ elevated from "land first" to a hard go-live gate | D-F4.2; Go-live gates |
| register-then-number | Does claim-before-register violate any regime? (Challenger A Q4) | RESOLVED — NO; regimes are number-first-then-sign; reserved-but-unsigned = documented void | D-F4.3 |

**Author claims REFUTED / corrected:** (1) "no behavior change to the retry repo" — FALSE,
`GetDueForRetryAsync` MUST widen (C-A); (2) "D3.4 already covers `FiscalCode == null`" — FALSE,
predicate MUST widen (C-B); (3) hold-branch retry-stamping as the C3 fix — INSUFFICIENT (crash precedes
the branch; `ScheduleImmediateFiscalRetry` alone is dead); (4) the registered-but-unstamped residual was
OMITTED from the author's residual list — NAMED here (C-C). **Author direction AFFIRMED:**
claim-before-register is the only at-most-once ordering, the unique OrderId index already exists (no
migration for the backstop), option (c) is correctly deferred.
