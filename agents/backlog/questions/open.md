# Open Questions — escalation inbox

Any agent appends a question here when it needs an owner decision. The PM surfaces `blocking: yes`
entries at the next checkpoint. When the owner answers, the entry moves to `answered.md` and the
decision is locked into the relevant artifact (ADR / story / charter) so it's never re-asked.

Format:

```
### Q-NNNN — [blocking: yes|no] <short title>
- Raised by: <agent> (<ticket id>)
- Date: YYYY-MM-DD
- Question: <the precise decision needed>
- Why it matters: <the lasting consequence of getting it wrong>
- Default taken (if non-blocking): <the defensible assumption proceeded with>
- Answer: _(owner fills in)_
```

---

_(Q-0001…Q-0005 and Q-RATELIMIT-01/02/03 all answered 2026-06-01; see `answered.md`. Key outcomes:
staff dispute replies = Admin-only (ADR-0001); prod proxy = 1 hop, `ForwardLimit=1`, rate-limit cleared
for prod (ADR-0003); Wave 0 ships rate-limit per-IP-only with BSP-4b as a fast-follow.)_

---

## Wave-1 planning questions (2026-06-05) — see `status/sprint-3.md`

> **All four Wave-1 planning questions (Q-W1-1…Q-W1-4) were answered by the owner on 2026-06-05 and
> moved to `answered.md`.** Outcomes: Q-W1-1 — Wave 0 is CLOSED (T-0230 reconciled to `done`; does NOT
> gate Wave 1). Q-W1-2 — both L-splits authorized (T-0142 → T-0152/T-0153/T-0154; T-0143 →
> T-0155/T-0156/T-0157/T-0158; architect owns the ADR-0002 D1.3 decision in T-0155). Q-W1-3 — BLIND-2
> filed into Wave 1 as **T-0159**. Q-W1-4 — author ADR-REFUND (T-0140) now in Batch 1A.

_No open Wave-1 *planning* questions remain._

---

## Refund money-path questions (2026-06-06) — raised by ADR-0006 (T-0140)

### Q-REFUND-01 — [blocking: yes — for DE/AT/ES go-live only; NOT for CZ/SK/PL launch] Per-country corrective fiscal document on refund/cancellation of a registered sale
- Raised by: architect (T-0140 / ADR-0006 D6)
- Date: 2026-06-06
- Question: When a refund or cancellation reverses a **fiscally-registered** sale, does each
  **BlockingOnline** country (DE TSE / AT RKSV / ES VeriFactu) legally require a **corrective fiscal
  document** (cancellation/credit registration with the tax authority), and in what form? CZ/SK/PL today
  are `None`/`AsyncBackground` with no gapless/corrective requirement, so this does not block their launch.
- Why it matters: A refund that silently skips a legally-required corrective registration is a
  **compliance incident** in a BlockingOnline regime — the same class ADR-0004 guards for receipts. The
  refund seam (ADR-0006) carries a `Refund.ReceiptId` link and is ready to ride ADR-0004's existing fiscal
  retry/reconciliation machinery; only the per-country **rule** is missing.
- Default taken (non-blocking for CZ/SK/PL): the `Refund` row records the corrective obligation but does
  not register a corrective document (no live BlockingOnline country). The TC-FISCAL-CORRECTIVE test is
  gated on this answer.
- Gating: **bound to ADR-0004's existing DE/AT/ES go-live gate** — must be answered + implemented before
  any BlockingOnline country goes live in production.
- Research (2026-06-06, Claude — web sources below): **YES, all three BlockingOnline regimes legally
  require a corrective fiscal registration; a refund/cancellation of a registered sale may NOT silently
  skip it.**
  - **DE (KassenSichV/TSE):** a TSE-signed sale can NOT be voided. A *separate* cancellation receipt
    (Stornobeleg, `BON_STORNO=1`, reverse/voided ReceiptCaseFlag) with reversed amounts must be created
    and independently TSE-signed. (fiskaltrust FAQ.)
  - **ES (VeriFactu / RD 1007/2023, mod. RD 254/2025):** records are immutable + chained; a correction
    requires a new chained billing record — either a *registro de anulación* linked to the original, or a
    *factura rectificativa* (own series, references the original), which explicitly covers refunds
    (devoluciones). Each carries its own SHA-256 hash in the chain. (BOE / AEAT FAQ.)
  - **AT (RKSV):** same model as DE — the signed QR receipt chain means a reversal is itself a signed
    "Storno" receipt linked into the chain; it can NOT be an untracked void. (efsta / RKSV overview.)
  - **Implication for ADR-0006/0004:** the refund seam must, for a BlockingOnline country, register a
    corrective fiscal document via the SAME ADR-0004 claim-before-register + retry/reconciliation
    machinery the receipt flow uses (the `Refund.ReceiptId` link is already in place). The exact document
    *type/series* per country (Storno vs rectificativa vs anulación) is an implementation detail of the
    go-live ticket, not a blocker for CZ/SK/PL (`None`/`AsyncBackground`, no corrective requirement).
  - Sources: fiskaltrust DE-cancellation FAQ; marosavat/getrenn VeriFactu 2026 guides; BOE RD 1007/2023
    (BOE-A-2023-24840) + Orden HAC/1177/2024; AEAT "registros de facturación: anulación" FAQ; efsta RKSV
    overview.
- Answer (owner, 2026-06-06): **CONFIRMED.** Adopt the researched requirement — the refund seam MUST
  register a corrective fiscal document per BlockingOnline country (DE Stornobeleg / ES rectificativa or
  anulación / AT Storno) via the ADR-0004 machinery before DE/AT/ES go-live. No change for CZ/SK/PL.
  Locked into the superseding **ADR-0009** `0009-refund-policy.md` D7 (the next free ADR number was 0009,
  not 0007 — 0001-0008 already exist; 0007=soft-delete, 0008=outbox-table). **RESOLVED.**

### Q-REFUND-02 — [blocking: no] Refund-policy windows (time limit / partial rules / who bears the Stripe fee)
- Raised by: architect (T-0140 / ADR-0006 D6 / Alternatives)
- Date: 2026-06-06
- Question: What are the product refund-policy rules — time window in which a refund may be issued,
  partial-refund eligibility, and whether the platform or the customer bears the non-refunded Stripe
  processing fee on a refund?
- Why it matters: These shape the **amount** a caller computes (`RefundRequest.Amount`) and the admin UX
  (AUD-01). Getting it wrong is a money/CX issue, not a correctness one.
- Default taken (non-blocking): ADR-0006 makes the amount a **caller input** and hard-codes **no** policy;
  the cancel path keeps its existing fee computation (`order.Cancel(...)`). The seam supports any answer,
  so Wave-2 implementation proceeds on the default and tightens when the owner answers.
- Owner direction (2026-06-06): (a) refund window is **14 days** — but VERIFY against code first;
  (b) **partial / per-service refunds wanted** (e.g. cleaner skipped one service → refund that service);
  (c) Stripe-fee bearer = **open**, owner wants a recommendation.
- Codebase findings (2026-06-06, Claude — verified):
  - **No 14-day refund window exists.** `BookingPolicy` (Features/Orders/BookingPolicy.cs) only models
    PRE-cleaning **cancellation** (oops-window, 24h free / 4h 25% / <4h 50%). There is no post-completion
    refund concept anywhere.
  - **`CancelOrder` BLOCKS completed/in-progress orders** (returns OrderAlreadyCompleted /
    OrderInProgressCannotCancel) — so today there is NO path to refund a Completed order at all.
  - **Refund is full-order only:** `refundAmount = TotalPrice * (1 - feeRate)`. No partial / per-service
    refund exists. The Stripe call is the inline un-keyed one ADR-0006 migrates onto IRefundService.
  - ⇒ The owner's scenario ("refund 1 service on a completed order") is NEW functionality the current
    code actively prevents — a post-completion partial refund, distinct from cancellation.
- Status: panel (analyst + architect + PM, 2026-06-06) delivered the recommendation; owner answered all
  four residual decisions. Locked into the superseding **ADR-0009** `0009-refund-policy.md` (ADR-0006 stays
  accepted/immutable; the next free ADR number was 0009, not 0007 — 0001-0008 already exist).
- Answer (owner, 2026-06-06) — **RESOLVED**, all four:
  1. **Window = 14 calendar days**, soft, anchored to `Order.CompletedAt`, admin-overridable with a
     recorded reason, null-anchor closed-by-default, chargeback path exempt.
  2. **Stripe fee:** PLATFORM ABSORBS on service-fault refunds (customer gets the full allocated amount);
     deduct ONLY on pure change-of-mind goodwill. Requires the new `RefundReason.ServiceNotRendered`
     value so fault-vs-goodwill is mechanically decidable.
  3. **Loyalty clawback on partial refunds = YES, proportional** (revoke `floor(refundNet/10)` per refund,
     capped at original earn, anonymous/legacy skipped) — needs a NEW `ILoyaltyService` partial-revoke
     method with a per-refund idempotency key (the existing full-mirror revoke no-ops on a second call).
  4. **FUND per-included-service package pricing** (owner override of the panel's v1 whole-package-only
     limit): a single service bundled inside a package MUST be independently refundable. This is the
     deliberate long-term-win schema/pricing change — packages get a per-included-service price basis so
     the share-of-`TotalPrice` allocator can target one bundled line. Larger schema work; sequenced as
     its own epic that the partial-refund build depends on.
  Allocation formula (share of frozen `Order.TotalPrice`), VAT apportionment, refundable ceiling, cash
  guard, dispute linkage, admin-only issuance, `PaymentStatus.PartiallyRefunded` — all panel-settled.
  **Locked into superseding ADR-0009 `0009-refund-policy.md` (2026-06-06).** (Note: the next free ADR
  number was 0009, not 0007 — 0001-0008 already exist.)

### Q-REFUND-03 — [blocking: no] Per-bundle business weighting of legacy packages for per-included-service pricing
- Raised by: architect (T-0140 / ADR-0009 D5)
- Date: 2026-06-06
- Question: ADR-0009 decision #4 adds `PackageService.PriceWeight` so the bundled `Package.Price` is split
  across a package's included services, giving each a gross the partial-refund allocator can target. The
  data migration backfills **even weights** (every included service = equal share) for existing packages.
  For any specific live bundle where the included services are NOT of equal value, what relative weights
  should they carry?
- Why it matters: the even-split default is mechanically safe and reversible, but a refund of one bundled
  service from a bundle whose services are unequally valued would refund the wrong amount until the weights
  are corrected. This is a pricing/product call, not an architecture one.
- Default taken (non-blocking): ship the even-weight backfill in **T-0231** (AUD-02p1, the db+backend split
  child). The owner sets per-bundle weights via the admin package-pricing UI in **T-0232** (AUD-02p2) after
  T-0231 lands; no per-bundle business weighting is invented in the ADR or the migration.
- Wave-2 status (2026-06-07): this is the **only open Wave-2 question** (Q-REFUND-01/02 resolved in ADR-0009).
  It does **not** block starting Wave 2 — T-0231 ships even-split. The owner should, before any DE/AT/ES or
  high-value-bundle refund goes live, either (a) confirm even-split is acceptable for all current bundles, or
  (b) set real weights via T-0232. AUD-02p is now split: weighting capability = T-0232, schema/backfill = T-0231.
- Answer: _(owner fills in — set per-bundle weights via the admin UI in T-0232 post-T-0231, or confirm
  even-split is acceptable for all current bundles)_
