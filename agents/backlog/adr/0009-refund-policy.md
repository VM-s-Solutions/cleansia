# ADR-0009 — Refund policy: the 14-day soft window, share-of-TotalPrice partial allocation, fee-bearer rule, loyalty clawback, and per-included-service package pricing

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-06
- **Supersedes:** ADR-0006 (extends/refines D2 amount, D5 projection, D6 fiscal, D7 state — the ADR-0006 **seam** is unchanged; this ADR fills the **policy** ADR-0006 deliberately deferred to Q-REFUND-02)
- **Superseded by:** —
- **Applies to:** backend | cross-cutting (financial/fiscal) — plus a backend+db+admin-frontend package-pricing epic
- **Extends:** ADR-0004 (the refund corrective-document obligation rides ADR-0004's fiscal retry/reconciliation), ADR-0001 (issuing a refund stays admin-only), ADR-0002 (refund-success notification still on `IPendingDispatch`)
- **Ticket:** T-0140 (ADR-REFUND — same ticket that produced ADR-0006) · **Consumers (Wave-2):** AUD-01a..e (refund build), the new **AUD-02p** package-pricing epic, D-06 (chargeback)

> **Numbering note (read first).** The task brief assumed the next free ADR number was 0007. It is
> **not**: `agents/backlog/adr/` already contains 0001–0008, with **0007 = soft-delete-policy** and
> **0008 = outbox-table-and-drainer**, both `accepted`. The next free number is **0009**, used here. This
> ADR is **ADR-REFUND-POLICY**. ADR-0006 (`0006-refund-dispute-money-path.md`) stays `accepted` and
> immutable; this ADR **supersedes** it for the policy questions ADR-0006 left to the owner (Q-REFUND-02)
> and **confirms** Q-REFUND-01. Cross-reference: ADR-0006 §"Open questions raised" Q-REFUND-01/02 →
> resolved here; ADR-0006 D2/D5/D6/D7 → extended here. Once `accepted` it is immutable — supersede, never
> edit.

---

## Context

ADR-0006 froze the refund **seam** (`IRefundService.IssueRefundAsync`, deterministic `RefundKey`, the
`Refund` projection, chargeback linkage, the fiscal-corrective boundary) and **deliberately hard-coded no
policy** — it made the refunded amount a caller input (`RefundRequest.Amount`) and escalated the policy
questions as **Q-REFUND-01** (per-country fiscal corrective doc) and **Q-REFUND-02** (window / partial
rules / fee bearer). The owner has now answered both (`agents/backlog/questions/open.md`, both RESOLVED
2026-06-06). This ADR locks those answers into an enforceable contract and designs the one change that
moves beyond the panel's v1 model: **per-included-service package pricing** (decision #4 below).

All facts re-verified against current code (2026-06-06):

**1 — There is no post-completion refund path today; the only refund is full-order cancellation.**
`CancelOrder.Handler` (`src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:119`) computes
`refundAmount = order.TotalPrice * (1m - feeRate)` and **blocks** `Completed`/`InProgress` orders
(`CancelOrder.cs:92-103` → `OrderAlreadyCompleted` / `OrderInProgressCannotCancel`). The owner's scenario
("a cleaner skipped one bundled service on a completed order → refund just that service") is **new
functionality the current code actively prevents** — a post-completion **partial** refund, distinct from
cancellation.

**2 — `Order.TotalPrice` is the frozen, discount-and-surcharge-embedded number.** `OrderFactory.cs:91-95`
computes `discountedSubtotal = RawSubtotal − appliedAmount` then, if express applies,
`finalTotalPrice = discountedSubtotal × (1 + ExpressSurchargeRate)`, and persists it via
`Order.Create(..., finalTotalPrice, ...)` (`OrderFactory.cs:108` → `Order.cs:46,281`). **The discount and
the +20% express surcharge are already inside `Order.TotalPrice`.** Any allocation that takes a line's
*share of `Order.TotalPrice`* therefore inherits both pro-rata automatically — re-adding or re-subtracting
discount/surcharge would double-count. This is the load-bearing reason the allocator keys on
`Order.TotalPrice`, not on raw catalog prices.

**3 — VAT is a frozen snapshot on the order.** `Order.AppliedVatRate` / `NetAmount` / `VatAmount`
(`Order.cs:51,56,63`) are stamped at create time (`OrderFactory.cs:150-155`); `AppliedVatRate` is **null
when the company is not a VAT payer** (`Order.cs:63`; `OrderFactory.cs:155` passes `rate=null`). A refund's
VAT portion must be apportioned from `AppliedVatRate`, and is **0 for a non-VAT-payer order**.

**4 — `CompletedAt` is the authoritative completion timestamp (UTC), and is nullable.**
`Order.CompletedAt` (`Order.cs:79`) is set inside `Order.CompleteOrder(...)` (`Order.cs:505`,
`DateTime.UtcNow`). It is **null while the order is open** and can be null on legacy completed rows that
predate the column. The refund window must anchor to it and decide the null case.

**5 — The cancel fee is a penalty, not a cost passthrough.** `CancelOrder.cs:119`'s `(1 − feeRate)` haircut
is the pre-cleaning cancellation penalty from `BookingPolicy.CalculateCancellationFeeRate`
(`BookingPolicy.cs:98-127`: free / 25% / 50% tiers, acceptance- and oops-window-aware). It is **unrelated**
to the Stripe processing fee. This ADR keeps the cancel fee exactly as-is and never conflates the two.

**6 — `PaymentStatus` has no partial state and `RefundReason` does not exist.**
`PaymentStatus` (`src/Cleansia.Core.Domain/Enums/PaymentStatus.cs:7-13`) is
`Pending=1, Paid=2, Failed=3, Refunded=4, Disputed=5` — **no `PartiallyRefunded`**. There is **no
`RefundReason` enum** anywhere (the only `RefundReason*` symbol in the repo is Stripe's own
`RefundReasons.RequestedByCustomer` at `StripeClient.cs:66`, unrelated). ADR-0006 D5 named both as additive
changes; this ADR fixes their exact values.

**7 — Loyalty earn and the existing revoke.** `LoyaltyService.GrantForCompletedOrderAsync`
(`LoyaltyService.cs:46`) earns `floor(order.TotalPrice / 10m)` points, skipping anonymous/legacy
(`UserId == null`, `:41-44`). `RevokeForCancelledOrderAsync` (`LoyaltyService.cs:99-147`) is a
**full mirror**: it walks back the *entire* original earn once and **no-ops on a second call** via the
`(orderId, LoyaltyEarnSource.OrderCancelled)` idempotency guard (`:126-131`). It **cannot** be reused for
partial refunds — a partial revoke needs a per-refund key. The manual paths
`GrantPointsManuallyAsync` / `RevokePointsManuallyAsync` (`LoyaltyService.cs:181-291`) already implement
the required shape: a `requestId` idempotency key with a fast-path read **and** an S7b unique-index flush
(`FlushCollapsingUniqueViolationAsync`, `:303-318`). The partial-revoke method mirrors that.

**8 — A bundled service has no price basis today (the decision-#4 gap).** `Package.Price`
(`src/Cleansia.Core.Domain/Packages/Package.cs:18`) is a **single bundled price**. The included-services
join `PackageService` (`PackageService.cs:1-21`, `BaseEntity`) has **no price column** — only
`PackageId` + `ServiceId`. `Service.BasePrice` / `PerRoomPrice` (`Service.cs:19,21`) are the *standalone*
catalog prices, and the bundle price is intentionally **not** their sum (a bundle is discounted). The order
lines `OrderPackage` / `OrderService` (`OrderPackage.cs:1-21`, `OrderService.cs:1-20`, both `BaseEntity`)
carry **no per-line price snapshot** — they reference the catalog entities directly. **Consequence: a
single service bundled inside a package has no gross to feed the share-of-`TotalPrice` allocator.** The
panel's v1 limited partial refunds to whole-package lines for exactly this reason; the owner overrode that
and funded per-included pricing (decision #4).

This is **one decision — "the refund policy"** — because the window, the partial allocation, the fee
bearer, the loyalty clawback, and the package-pricing basis are inseparable: the allocator needs a
per-line gross (which the package-pricing model supplies), the gross feeds the share-of-`TotalPrice` math,
the share drives both VAT apportionment and the loyalty clawback magnitude, and the window + fee bearer +
`RefundReason` gate *whether and how much* of that allocation is sent. Splitting them would let the
allocator ship without a basis for bundled lines, or the clawback ship without knowing the refunded net.

---

## Decision

> **Policy principle.** A refund's **amount** is computed by the caller as a *share of the frozen
> `Order.TotalPrice`* of the chosen lines, never recomputed from catalog prices and never re-applying
> discount/surcharge math. The **window** (14 calendar days, soft, anchored to `CompletedAt`) and the
> **fee bearer** (platform absorbs on service-fault, deducts only on pure goodwill) are **caller-side**
> `RefundPolicy` rules — the `IRefundService` seam (ADR-0006) still enforces only the refundable **ceiling**
> and idempotency. A partial refund leaves the order `Completed`, moves `PaymentStatus` to
> `PartiallyRefunded`, proportionally claws back loyalty, and — for a bundled service — is made possible by
> a new per-included-service price basis on `Package`.

### D1 — The refund window: 14 calendar days, SOFT, anchored to `CompletedAt` (decision #1)

A new caller-side **`RefundPolicy`** (a pure policy class in `Cleansia.Core.AppServices.Features.Orders`,
sibling to `BookingPolicy` — see Roles) owns the window. It is **NOT enforced inside `IRefundService`**:
per ADR-0006 D2 the seam enforces only the refundable ceiling + idempotency; the window is a product rule
the *caller* (the admin refund command / `ResolveDispute`) checks before calling the seam.

- **Window = 14 calendar days** from `Order.CompletedAt` (UTC), inclusive. `RefundPolicy.IsWithinWindow`
  returns true iff `nowUtc <= order.CompletedAt + 14 days`.
- **SOFT, admin-overridable with a mandatory recorded reason.** When the window is closed, an admin may
  still issue the refund by supplying a non-empty override reason, which is persisted (audit) on the refund
  intent. A window-closed refund **without** a recorded override reason is rejected by the caller. (The
  reason is recorded on the admin refund command / the `Refund` row's audit, not invented as a new column
  here beyond what AUD-01a needs.)
- **Null `CompletedAt` → window CLOSED by default.** A refund on an order with no `CompletedAt` (open, or a
  legacy completed row) is treated as out-of-window and requires the admin override + reason. This is the
  fail-closed default for the missing anchor (fact 4).
- **Chargeback path exempt.** A `Refund` with `Source = Chargeback` (ADR-0006 D5) is the bank pulling funds
  — it is **not** an app-issued refund and is **not** subject to the window. The reconciliation row is
  written regardless of timing (ADR-0006 D4/D5 unchanged).

The window is a `RefundPolicy` decision so mobile/web/admin all reference one constant (the `BookingPolicy`
pattern, `BookingPolicy.cs:5-6` "keep mobile, web, and backend in sync by referencing these numbers").

### D2 — Partial allocation: share of the frozen `Order.TotalPrice` (decision #2)

For a partial refund the caller selects one or more **lines** (an `OrderService`, an `OrderPackage`, or —
after D5 — a single **service bundled inside** an `OrderPackage`). Each chosen line has a `lineGross`
(D5 defines it for a bundled service; for a whole `OrderService`/`OrderPackage` it is the line's own gross
basis). The allocation is a pure function over the frozen `Order.TotalPrice`:

```
lineRefund_i = round( lineGross_i / Σ(lineGross over chosen lines) × Order.TotalPrice , 2 )
```

- **Order.TotalPrice already embeds discount + express surcharge** (fact 2, `OrderFactory.cs:91-95`). The
  ratio inherits both pro-rata. **Do NOT re-add or re-subtract discount/surcharge** — that is the single
  most important non-obvious rule here.
- **Sub-cent residual:** sum the rounded `lineRefund_i`; the **last refunded line absorbs the residual** so
  `Σ lineRefund == round(Σ(chosen grosses)/Σ(all-chosen grosses) × TotalPrice, 2)` exactly (no lost/created
  cent).
- **VAT apportioned by the same ratio**, derived from the order's frozen rate:
  ```
  refundVat_i = round( lineRefund_i × AppliedVatRate / (100 + AppliedVatRate) , 2 )   // 0 when AppliedVatRate is null (non-VAT-payer, fact 3)
  ```
- **Hard ceiling clamp inside `IRefundService`** (ADR-0006 D2, unchanged): the seam clamps the sent amount
  to `refundable(order) = amountCharged − Σ(succeeded refunds)`, reading the `Refund` projection
  (ADR-0006 D5). The caller's share-of-`TotalPrice` math is the *requested* amount; the seam is the
  *authoritative ceiling*. A partial-then-partial-then-full sequence can never exceed the charge.
- **Order stays `Completed`.** A refund is a payment fact, not a lifecycle transition (ADR-0006 D7). Only
  `PaymentStatus` moves: it becomes **`PartiallyRefunded`** while `0 < Σ(succeeded refunds) < amountCharged`,
  and **`Refunded`** once cumulative succeeded refunds `== amountCharged` (ADR-0006 D5's derived-summary
  rule, now with the concrete enum value from D4 below).

The allocator formula is the same one whether the line is a whole service, a whole package, or a single
bundled service — **the only thing D5 changes is how `lineGross` is *derived* for a bundled service**, never
the formula.

### D3 — The Stripe fee bearer: platform absorbs on fault, deducts only on goodwill (decision #3)

Make fault-vs-goodwill **mechanically decidable** by adding a `RefundReason` value, then key the fee rule
off it:

- **Add `RefundReason.ServiceNotRendered`** to the `RefundReason` enum ADR-0006 D1 named
  (`CustomerCancellation | DisputeResolution | AdminDiscretion | ServiceNotRendered`). This is the
  service-fault reason: the cleaner skipped/under-delivered a line.
- **Platform ABSORBS the Stripe fee on service-fault refunds** — the customer receives the **full allocated
  amount** (the D2 `lineRefund`). Fault reasons: **`ServiceNotRendered`** and **`DisputeResolution`** (a
  dispute resolved in the customer's favour is a fault outcome).
- **The platform deducts the non-refunded Stripe fee ONLY on pure goodwill / change-of-mind**, i.e.
  **`AdminDiscretion`**. There the refund the customer receives may be the allocated amount minus the
  processing fee the platform cannot recover from Stripe.
- **`CustomerCancellation`** keeps the **existing** cancel behaviour unchanged: the cancel fee
  (`CancelOrder.cs:119`, `BookingPolicy` tiers) already determines the refund; the Stripe-fee rule does not
  apply to the cancel penalty path (fact 5).
- **The cancel fee and the Stripe fee are distinct and stay distinct.** The cancel fee is a *penalty* on the
  customer (a fraction of `TotalPrice` retained by the platform); the Stripe fee is a *cost passthrough*
  decision (who eats the non-refundable processor fee). They never mix in code or in the receipt.

Mechanical decidability: a reviewer can see fault-vs-goodwill from `Refund.Reason` alone — no free-text
classification. The fee rule is a pure switch on `RefundReason` and lives caller-side (it shapes the
`RefundRequest.Amount` the caller computes), consistent with ADR-0006 D2 ("the caller computes `Amount`").

### D4 — Enum additions (the exact values)

- **`PaymentStatus.PartiallyRefunded = 6`** (additive; preserves the existing `[SwaggerEnumAsInt]` wire
  values `Pending=1 … Disputed=5`). Used by D2; never a breaking read.
- **`RefundReason` enum** (new, `Cleansia.Core.Domain.Enums`): `CustomerCancellation`,
  `DisputeResolution`, `AdminDiscretion`, **`ServiceNotRendered`**. Carried on `RefundRequest.Reason`
  (ADR-0006 D1) and persisted on the `Refund` projection (ADR-0006 D5).

Both are additive enum changes → **`manual_step: ef-migration` (owner-only)** lands with the consumer
ticket that adds the `Refund` entity (AUD-01a). `RefundReason` on the admin command surface →
**`manual_step: nswag-regen`** for the admin client.

### D5 — Per-included-service package pricing (decision #4 — the deliberate long-term schema change)

The owner **overrode** the panel's v1 "whole-package-only" limit: **a single service bundled inside a
Package MUST be independently refundable.** Today that is impossible (fact 8 — a bundled service has no
gross). This ADR designs the price basis.

**The model chosen: a per-included-service WEIGHT on the `PackageService` join, and the bundled
`Package.Price` is allocated across those weights to derive each included service's gross.**

- **Schema delta.** Add a column **`PackageService.PriceWeight` (decimal, non-null, default for backfill
  per below)** to the included-services join (`PackageService.cs`). The weight is a *relative* number, not a
  currency amount: a service's gross **within a given order's package line** is
  ```
  includedServiceGross = round( ps.PriceWeight / Σ(PriceWeight over the package's included services) × <packageLineGross> , 2 )
  ```
  where `<packageLineGross>` is the package line's own gross basis (D5.1). The last included service absorbs
  the sub-cent residual (same rule as D2).
- **Why weight, not an absolute per-included price column.** Three reasons, weighed against the
  alternatives (see Alternatives):
  1. **Single source of truth for the bundle price.** `Package.Price` stays the one authoritative bundle
     price (fact 8). A weight *splits* it; an absolute per-included price column would create a second
     source that can silently disagree with `Package.Price` (Σ included prices ≠ bundle price → which is
     authoritative?). A weight cannot disagree — it is dimensionless and always re-normalised to the bundle
     price actually charged.
  2. **Migration + admin UX cost.** Backfilling a *weight* has a safe, neutral default (equal weights →
     even split). Backfilling absolute prices forces an immediate pricing decision per legacy bundle. The
     admin package-pricing UI sets relative weights (or "this service is worth ~30% of the bundle"), which
     is a smaller, more forgiving form than per-service currency entry that must sum to the bundle price.
  3. **Receipt fidelity.** The weight-derived `includedServiceGross` is computed *from the price actually
     charged* (`Package.Price` flows through `Order.TotalPrice`), so a refunded bundled line always reconciles
     to the order total — there is no rounding drift between a stored absolute price and the charged bundle.
- **How `lineGross` is derived for a bundled service** (closing the fact-8 gap, feeding D2 unchanged):
  ```
  lineGross(bundled service S in package P on order O)
      = includedServiceGross(S within P)                         // P.Price split by PriceWeight (above)
  ```
  and the D2 allocator then takes that `lineGross`'s **share of Order.TotalPrice** exactly as for any other
  line. The package line's own gross basis (`<packageLineGross>` for the split) is the package's catalog
  `Package.Price` (D5.1). Net effect: a bundled service's refund =
  `share_of_package(by weight) × share_of_order(by package-vs-everything-else) × Order.TotalPrice` — both
  ratios are over frozen, charged numbers, so discount + surcharge stay embedded and are **never
  re-introduced** (the D2 invariant holds; D5 only supplies the gross, never any discount math).

- **D5.1 — the package line's gross basis for the *order-level* ratio.** For the outer D2 ratio
  (line-vs-all-chosen-lines over `Order.TotalPrice`), an `OrderService`'s gross basis is its standalone
  catalog price (`Service.BasePrice + PerRoomPrice × (rooms+bathrooms)`, matching
  `OrderPricingCalculator.cs:30`) and an `OrderPackage`'s gross basis is `Package.Price`
  (`OrderPricingCalculator.cs:26`). These are the **pre-discount** catalog grosses used **only as ratio
  weights**; because the ratio is then multiplied by the frozen `Order.TotalPrice`, the discount/surcharge
  embedded in `TotalPrice` is applied pro-rata and correctly — this is the existing, consistent basis the
  quote already sums, not a new pricing path.

- **Backfill of legacy packages.** Existing `PackageService` rows get a **default `PriceWeight` such that
  the split is even** (e.g. weight `1` for every included service → equal shares). **Whether even-split is
  the right default for any specific live bundle, or whether a particular bundle needs a non-even split, is
  a product/pricing call** — see the NEW open question **Q-REFUND-03** below. The *mechanical* backfill
  (even weights) is safe and reversible; the *business* weighting of a specific legacy bundle is the owner's
  to set via the admin UI, post-migration. The data migration ships even weights; no per-bundle business
  weighting is invented here.

- **Scope.** D5 is a **separate epic** (AUD-02p) that the partial-refund build (AUD-01c) **depends on** —
  AUD-01c cannot refund a bundled service until the gross basis exists. It is backend (schema + the gross
  derivation in the allocator) + db (migration) + **admin frontend** (package-pricing weight UX on the
  package form). `manual_step: ef-migration` (the `PriceWeight` column + backfill) and `manual_step:
  nswag-regen` (the package admin DTO gains `PriceWeight`).

### D6 — Loyalty clawback on partial refund: proportional, per-refund-keyed (decision #4-loyalty)

A partial refund proportionally claws back earned loyalty:

- **Revoke `floor(refundNet / 10)` points per refund**, where `refundNet` is the refund's net (ex-VAT)
  amount — symmetric with the earn `floor(order.TotalPrice / 10)` (`LoyaltyService.cs:46`). Using **net**
  keeps the clawback consistent with how value was earned and avoids clawing back on the VAT portion.
- **Cumulative-capped at the original earn.** Σ(revoked across all this order's partial refunds) never
  exceeds the original `OrderCompleted` earn magnitude (`LoyaltyService.cs:118-123,140`). A near-full set of
  partials cannot over-revoke.
- **Anonymous/legacy skipped.** `UserId == null` → no clawback (mirrors the earn skip,
  `LoyaltyService.cs:41-44`, and the revoke skip, `:112-115`).
- **A NEW `ILoyaltyService` partial-revoke method is REQUIRED — the existing
  `RevokeForCancelledOrderAsync` cannot be reused.** That method is a full mirror keyed on
  `(orderId, OrderCancelled)` that **no-ops on a second call** (`LoyaltyService.cs:126-131`) — so a second
  partial refund would silently revoke nothing. The new method takes a **per-refund idempotency key**
  (the `Refund.Id` / `RefundKey`), mirroring `GrantPointsManuallyAsync`/`RevokePointsManuallyAsync`'s
  `requestId` shape (`LoyaltyService.cs:181-291`): fast-path read on the key, then the S7b unique-index
  flush (`FlushCollapsingUniqueViolationAsync`, `:303-318`) so a concurrent double-submit collapses on the
  filtered unique index instead of double-revoking. Suggested signature (frozen here; AUD-01d implements):
  ```csharp
  // Idempotent on refundKey; cumulative-capped at the original OrderCompleted earn; UserId==null → no-op.
  Task RevokeForPartialRefundAsync(string orderId, decimal refundNet, string refundKey, string actorId, CancellationToken ct);
  ```
  Uses a new `LoyaltyEarnSource.OrderPartiallyRefunded` (additive enum value) so it is distinct from the
  full `OrderCancelled` revoke and the per-key idempotency does not collide with the cancel mirror.

### D7 — Fiscal corrective tie-in (Q-REFUND-01 CONFIRMED — extends ADR-0004)

Q-REFUND-01 is **confirmed** by the owner: a refund/cancellation that reverses a **fiscally-registered**
sale MUST register a **corrective fiscal document** per BlockingOnline country (DE Stornobeleg / ES
*rectificativa* or *registro de anulación* / AT Storno) via **ADR-0004's existing claim-before-register +
retry/reconciliation machinery**, before any DE/AT/ES go-live. This ADR changes nothing in ADR-0006 D6 /
ADR-0004 except to **confirm the rule and bind it to ADR-0004's existing DE/AT/ES go-live gate**:

- **No change for CZ/SK/PL** (`None`/`AsyncBackground`, no gapless/corrective requirement —
  `FiscalEnforcementMode.cs:13,21` per ADR-0004 fact 6). The `Refund` row records the corrective
  obligation; nothing is registered.
- **A partial refund corrects a partial amount.** The corrective document (when a BlockingOnline country
  goes live) registers the **refunded amount + apportioned VAT** (D2) against the original `Refund.ReceiptId`
  (ADR-0006 D5) — it is a partial corrective, not a full void. The exact per-country document type/series is
  an implementation detail of the FISCAL go-live ticket (T-0220/T-0221 cluster), not this ADR.
- **Customer-facing completion/cancellation is NEVER blocked by the corrective registration** — the
  ADR-0004 / `docs/architecture/fiscal-compliance.md` invariant is preserved exactly.

This is the only place this ADR touches ADR-0004: it **extends** ADR-0004 by confirming the refund
corrective obligation as in-scope for the same go-live gate, and adds no new fiscal mechanism.

### D8 — Test contract handed to the consumers (test-first, refund math is on the strict red-green list)

The consumer tickets write these **red first** (per `agents/knowledge/testing.md`):

- **TC-REFUND-WINDOW.** `RefundPolicy.IsWithinWindow`: day-14 in-window, day-15 out; null `CompletedAt`
  closed; chargeback exempt; admin override with a recorded reason re-opens, override without a reason is
  rejected.
- **TC-REFUND-ALLOC.** Σ(lineRefund) over chosen lines == round(share × TotalPrice, 2); last line absorbs
  the residual (penny-perfect); discount + surcharge are NOT re-applied (a discounted+express order's
  partial refund reconciles to `TotalPrice`, not to raw catalog prices).
- **TC-REFUND-VAT.** `refundVat = round(lineRefund × rate/(100+rate), 2)` for a VAT-payer order; `0` for a
  null-`AppliedVatRate` (non-VAT-payer) order.
- **TC-REFUND-CEILING.** A partial-then-partial-then-full sequence never exceeds `amountCharged`
  (`IRefundService` clamp, ADR-0006 D2); `PaymentStatus` is `PartiallyRefunded` until cumulative ==
  charged, then `Refunded`.
- **TC-REFUND-FEE.** Fee absorbed on `ServiceNotRendered`/`DisputeResolution` (customer gets full
  allocated amount); fee deducted allowed on `AdminDiscretion`; cancel-fee path unchanged.
- **TC-REFUND-LOYALTY.** `floor(refundNet/10)` revoked per refund; cumulative-capped at original earn; a
  second partial revokes again (NOT a no-op — proving the new keyed method, not the old mirror);
  `UserId==null` skipped; same `refundKey` twice → revokes once (idempotent).
- **TC-PKG-WEIGHT (AUD-02p).** A bundled service's `includedServiceGross` = round(weight-share ×
  `Package.Price`, 2), last included absorbs residual; even-weight backfill → even split; refunding one
  bundled service then the rest of the package never exceeds the package line's share of `TotalPrice`.

---

## Alternatives considered

- **Hard window enforced inside `IRefundService` (not caller-side `RefundPolicy`).** Rejected: ADR-0006 D2
  scopes the seam to the refundable ceiling + idempotency and makes the amount a caller input; baking a
  product window into the seam couples policy to the money primitive and blocks the legitimate
  admin-override + chargeback-exempt cases. The window is a `RefundPolicy` (sibling to `BookingPolicy`).
- **Allocate partial refunds from catalog prices (re-derive each line's price, re-apply discount /
  surcharge).** Rejected: `Order.TotalPrice` already embeds discount + the +20% express surcharge
  (`OrderFactory.cs:91-95`); re-deriving from catalog and re-applying would double-count and drift from what
  the customer actually paid. Share-of-frozen-`TotalPrice` inherits both pro-rata for free (D2, fact 2).
- **Free-text or boolean "is this our fault?" flag for the fee bearer.** Rejected: not mechanically
  decidable and invites inconsistent classification. A typed `RefundReason.ServiceNotRendered` makes the
  fault-vs-goodwill switch a pure, reviewable enum check (D3).
- **Reuse `RevokeForCancelledOrderAsync` for partial clawback.** Rejected (fact 7): it is a one-shot full
  mirror that no-ops on the second call (`LoyaltyService.cs:126-131`) — a second partial would revoke
  nothing. A per-refund-keyed method (mirroring the manual grant/revoke `requestId` + S7b flush) is
  required (D6).
- **Decision #4 alternative A — keep the panel's v1 "whole-package-only" partial refund (do nothing).**
  Rejected by the owner: a customer must be able to get back the one bundled service the cleaner skipped.
- **Decision #4 alternative B — an explicit absolute per-included-service price column on `PackageService`.**
  Considered. Rejected in favour of a weight: an absolute column creates a second source of truth that can
  disagree with `Package.Price` (Σ included ≠ bundle → ambiguity), forces an immediate per-bundle pricing
  decision on backfill, and risks rounding drift between the stored price and the charged bundle on the
  receipt. A weight is dimensionless, always re-normalises to the price actually charged, backfills to a
  safe even-split default, and keeps `Package.Price` the single authoritative bundle price (D5).
- **Decision #4 alternative C — derive the bundled gross from `Service.BasePrice` (the standalone catalog
  price) directly.** Rejected: the standalone prices intentionally do **not** sum to the discounted
  `Package.Price` (fact 8), so a bundled refund would not reconcile to the bundle actually charged. The
  weight splits the *bundle* price, not the standalone prices.
- **Auto-register the fiscal corrective document for all regimes now.** Rejected / deferred exactly as
  ADR-0006 did: the per-country corrective rule is bound to ADR-0004's DE/AT/ES go-live gate; CZ/SK/PL need
  nothing (D7). Confirmed, not implemented, here.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (money-correctness, fiscal-compliance, schema/migration, pragmatic) attacked;
the Lead re-verified every citation against the real code and adjudicated. **Verdict: all challenges
RESOLVED; zero blocking; one NEW owner question (Q-REFUND-03, non-blocking) raised; consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (money) | Allocating from catalog prices would double-count the embedded discount + express surcharge (CRITICAL) | CONCEDE + AFFIRM | D2 share-of-frozen-`TotalPrice`; fact 2 (`OrderFactory.cs:91-95`) |
| CH-2 (money) | Rounding each line independently loses/creates a cent vs `TotalPrice` (MAJOR) | CONCEDE + REVISE | D2 last-line-absorbs-residual; TC-REFUND-ALLOC penny-perfect |
| CH-3 (VAT) | A non-VAT-payer order has null `AppliedVatRate`; the VAT formula divides by it (MAJOR) | CONCEDE + REVISE | D2 `refundVat = 0` when `AppliedVatRate` null; fact 3 (`Order.cs:63`) |
| CH-4 (loyalty) | `RevokeForCancelledOrderAsync` no-ops on the second call → second partial revokes nothing (MAJOR) | CONCEDE + REVISE | D6 new per-refund-keyed method; fact 7 (`LoyaltyService.cs:126-131`); TC-REFUND-LOYALTY proves non-no-op |
| CH-5 (loyalty) | Clawing back on gross (incl. VAT) over-revokes vs the net-basis earn (MODERATE) | CONCEDE + REVISE | D6 `floor(refundNet/10)`, cumulative-capped |
| CH-6 (schema) | An absolute per-included price column can disagree with `Package.Price` and drift on the receipt (MAJOR) | CONCEDE + REVISE | D5 weight model (single source of truth); Alternatives B |
| CH-7 (schema) | Standalone `Service.BasePrice` does not sum to the discounted bundle price → bundled refund won't reconcile (MAJOR) | CONCEDE + AFFIRM | D5 weight splits `Package.Price`, not standalone prices; Alternatives C |
| CH-8 (migration) | Legacy `PackageService` rows have no weight; the migration must not invent business pricing (MAJOR) | DEFEND + ESCALATE | D5 even-weight mechanical backfill; **Q-REFUND-03** for any per-bundle business weighting |
| CH-9 (window) | A null `CompletedAt` (legacy/open) would make the window math throw or pass spuriously (MODERATE) | CONCEDE + REVISE | D1 null → closed-by-default + admin override |
| CH-10 (fee) | Conflating the cancel fee with the Stripe fee would corrupt the cancel path (MAJOR) | DEFEND | D3 the two fees are distinct; cancel path (`CancelOrder.cs:119`) untouched; fact 5 |
| CH-11 (seam) | Enforcing the window inside `IRefundService` would violate ADR-0006 D2's caller-input contract (MODERATE) | DEFEND | D1 window is caller-side `RefundPolicy`; seam still enforces only the ceiling |
| CH-12 (fiscal) | A partial refund of a registered sale still needs a (partial) corrective in BlockingOnline regimes (CRITICAL for DE/AT/ES) | CONCEDE + CONFIRM | D7 partial corrective on `Refund.ReceiptId`; bound to ADR-0004 go-live gate; CZ/SK/PL unaffected |

**Affirmed unchallenged:** the 14-day soft window anchored to `CompletedAt`; chargeback exempt; admin
override with a recorded reason; `PaymentStatus.PartiallyRefunded = 6` additive; `RefundReason` typed
fault-vs-goodwill; the allocator formula identical across line types (only the bundled-gross derivation
differs); the order stays `Completed` (a refund is a payment fact, ADR-0006 D7).

**Lead re-verification (against current code, 2026-06-06):** `CancelOrder.cs:119` cancel-fee haircut +
`:92-103` completed/in-progress block; `OrderFactory.cs:91-95,108` discount-then-surcharge frozen into
`Order.TotalPrice`; `Order.cs:46,63,79,505` `TotalPrice`/`AppliedVatRate`/`CompletedAt`/`CompleteOrder`;
`PaymentStatus.cs:7-13` no `PartiallyRefunded`; no `RefundReason` enum (only `StripeClient.cs:66`);
`Package.cs:18` single `Package.Price`; `PackageService.cs:1-21` no price column; `Service.cs:19,21`
standalone prices; `OrderPackage.cs`/`OrderService.cs` no line price snapshot;
`OrderPricingCalculator.cs:26,30` package/service gross bases; `LoyaltyService.cs:41-46,99-147,181-291,
303-318` earn/full-revoke/manual-keyed-revoke/S7b-flush.

**Escalations to the owner:** **Q-REFUND-03** (NEW, non-blocking) — per-bundle business weighting of legacy
packages (the even-split default is safe to ship; whether any specific live bundle needs a non-even weight
is a pricing call the owner makes via the admin UI after AUD-02p lands). Q-REFUND-01/02 are RESOLVED.

---

## Consequences

**Cheaper / safer:**
- One enforceable refund policy: window, allocation, fee bearer, and clawback are codified, not folklore;
  a reviewer reads `RefundReason` and `RefundPolicy` instead of guessing intent.
- Partial refunds reconcile to the price actually paid (share-of-frozen-`TotalPrice`), so discount +
  surcharge can never be double-counted, and VAT + loyalty derive from the same ratio.
- A bundled service is independently refundable (the owner's long-term win) **without** a second source of
  truth for the bundle price — the weight always re-normalises to `Package.Price`.
- The fiscal corrective obligation extends cleanly to partial refunds on ADR-0004's existing machinery; no
  new fiscal mechanism, CZ/SK/PL unaffected.

**More expensive (new obligations on developers):**
- A new `RefundPolicy` policy class (window + fee-bearer switch), parallel to `BookingPolicy`.
- `PaymentStatus.PartiallyRefunded = 6` + the `RefundReason` enum → **`manual_step: ef-migration`**
  (owner-only) lands with the `Refund` entity (AUD-01a); **`manual_step: nswag-regen`** for the admin
  refund command surface.
- A new `ILoyaltyService.RevokeForPartialRefundAsync` keyed on the refund — the existing cancel mirror is
  NOT reusable; needs its own `LoyaltyEarnSource.OrderPartiallyRefunded` + filtered-unique-index backstop.
- **AUD-02p (package-pricing epic):** `PackageService.PriceWeight` column + even-weight data backfill
  (**`manual_step: ef-migration`**), the bundled-gross derivation in the allocator, and the admin
  package-form weight UX (**`manual_step: nswag-regen`** for the package DTO). **AUD-01c (partial-refund
  build) depends on AUD-02p** — a bundled service cannot be refunded until the gross basis exists.
- Every partial refund must record (window override reason when applicable, the `RefundReason`) for audit.

**Rollout (Wave-2 consumers, each test-first):**
- **AUD-01a..e** (refund build) + **AUD-02p** (package pricing, a dependency of AUD-01c) — see follow-up
  tickets below. **D-06** (chargeback) unchanged from ADR-0006; the window-exempt `Source=Chargeback`
  reconciliation row is honoured here.

---

## How a reviewer verifies compliance

**Mechanical (the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **Window is caller-side, not in the seam.** `RefundPolicy.IsWithinWindow` exists and is called by the
   admin refund command / `ResolveDispute`; `IRefundService` contains **no** window/`CompletedAt` check
   (it enforces only the ceiling + idempotency, ADR-0006 D2). A window check inside the seam is a finding.
2. **Allocation keys on `Order.TotalPrice`, never on catalog prices with re-applied discount/surcharge.**
   The partial-refund amount path multiplies a line-share by `Order.TotalPrice`; any re-computation of
   discount or `ExpressSurchargeRate` inside the refund allocator is a blocking finding (fact 2).
3. **VAT apportionment guards the null rate.** `refundVat` is `0` when `AppliedVatRate` is null; a
   divide-by/`(100+rate)` with no null guard fails TC-REFUND-VAT.
4. **`RefundReason` drives the fee rule.** The fee absorb/deduct decision is a pure switch on
   `RefundReason` (absorb on `ServiceNotRendered`/`DisputeResolution`; deduct allowed only on
   `AdminDiscretion`); the cancel-fee path (`CancelOrder.cs:119`) is untouched and never mixed with the
   Stripe fee.
5. **Partial loyalty clawback uses the NEW keyed method.** Partial refunds call
   `RevokeForPartialRefundAsync` (per-refund key, cumulative-capped, `UserId==null` skip) — **not**
   `RevokeForCancelledOrderAsync`. A second partial that revokes nothing is a TC-REFUND-LOYALTY failure.
6. **Bundled gross derives from the weight-split of `Package.Price`.** `includedServiceGross` =
   weight-share × `Package.Price` (not `Service.BasePrice`); the package DTO carries `PriceWeight`; the
   allocator formula (D2) is unchanged once a bundled service has a gross.
7. **`PaymentStatus` summary is derived.** `PartiallyRefunded` while `0 < Σ succeeded < charged`,
   `Refunded` at equality; the order lifecycle status stays `Completed` (ADR-0006 D7).

**Test contract:** TC-REFUND-WINDOW, TC-REFUND-ALLOC, TC-REFUND-VAT, TC-REFUND-CEILING, TC-REFUND-FEE,
TC-REFUND-LOYALTY, TC-PKG-WEIGHT (D8) — the consumer tickets land them with the code, red first.

---

## Roles affected

Role files in `agents/knowledge/roles/` (these layer on ADR-0006's `refund-service.md` / `refund.md`,
which the refund consumers create):
- **`refund-policy.md`** (new, policy CRC) — `RefundPolicy`: *responsibility:* decide, once for the
  platform, the refund window (14 days, soft, `CompletedAt`-anchored, null→closed, chargeback-exempt,
  admin-overridable with a recorded reason) and the Stripe-fee bearer rule (absorb on fault, deduct only on
  `AdminDiscretion`). *Collaborators:* `Order.CompletedAt`, `RefundReason`. *Does NOT know:* how the Stripe
  refund is sent (that is `IRefundService`, ADR-0006), the refundable ceiling (the seam clamps it), or how
  the line amount is allocated (the caller computes the share, D2).
- **`refund-allocator` (a responsibility of the admin refund command / `ResolveDispute`, not a new
  service)** — *responsibility:* compute each chosen line's `lineRefund` as its share of frozen
  `Order.TotalPrice` (+ apportioned VAT), deriving a bundled service's `lineGross` from the
  `PackageService.PriceWeight` split of `Package.Price`. *Collaborators:* `Order.TotalPrice`/`AppliedVatRate`,
  `OrderService`/`OrderPackage`, `PackageService.PriceWeight`. *Does NOT know:* discount/surcharge math
  (already embedded in `TotalPrice` — must never re-apply it), the window, or the Stripe call.
- **`PackageService` (existing, updated)** — gains `PriceWeight`: *responsibility:* be the included-services
  join AND carry the relative weight by which `Package.Price` is split to give each included service a gross.
  *Does NOT know:* currency amounts (the weight is dimensionless), discount, or order-level totals.
- **`LoyaltyService` (existing, updated)** — gains `RevokeForPartialRefundAsync` (per-refund-keyed,
  cumulative-capped, anon-skip). *Does NOT know:* the refund amount math (it receives `refundNet`).

Catalog edit (same change): `agents/knowledge/patterns-backend.md §B8` / `security-rules.md §S7`
cross-reference ADR-0009 alongside ADR-0006 — a partial refund that re-applies discount/surcharge, skips
the per-refund loyalty key, or enforces the window inside `IRefundService` is a B8/S7/ADR-0009 violation.

---

## Follow-up tickets & manual steps (Wave-2 BUILD — this ADR is the Wave-1 DECISION)

**Refund-build epic — AUD-01 split into AUD-01a..e** (was the panel's single AUD-01):
- **AUD-01a** — `Refund` entity + unique `RefundKey` index + `PaymentStatus.PartiallyRefunded=6` +
  `RefundReason` enum + EF config. `manual_step: ef-migration` (owner). Size M. Layers: backend, db.
  (Lands the ADR-0006 D5 projection + this ADR's D4 enums.)
- **AUD-01b** — `IRefundService` implementation (one seam, ceiling clamp, deterministic `RefundKey`, 23505
  collapse, confirm-then-record) + `IStripeClient` idempotency-key param. `manual_step: nswag-regen` if the
  refund response DTO changes. Size M. Layers: backend, clients. (ADR-0006 D1/D2/D3.)
- **AUD-01c** — admin partial-refund command over the seam (`AdminOnly`, ADR-0001) + the share-of-
  `TotalPrice` allocator (D2) + `RefundPolicy` window/fee (D1/D3) + `PartiallyRefunded` summary. **Depends
  on AUD-01a, AUD-01b, AND AUD-02p** (cannot refund a bundled service without the gross basis).
  `manual_step: nswag-regen` (admin client gains the refund command). Size L. Layers: backend, frontend (admin refund UX).
- **AUD-01d** — `ILoyaltyService.RevokeForPartialRefundAsync` (per-refund key, cumulative cap, anon-skip) +
  `LoyaltyEarnSource.OrderPartiallyRefunded` + filtered-unique-index backstop. `manual_step: ef-migration`
  (the new source / any key index). Size M. Layers: backend, db.
- **AUD-01e** — `ResolveDispute` + `CancelOrder` migrated onto the seam (ADR-0006 D1.1); the window-exempt
  chargeback row (`Source=Chargeback`) honoured. Size M. Layers: backend.

**NEW per-included-service package-pricing epic — AUD-02p** (the AUD-01c dependency, decision #4):
- **AUD-02p** — `PackageService.PriceWeight` column + even-weight data backfill of legacy rows; the
  bundled-gross derivation (weight-split of `Package.Price`) feeding the D2 allocator; admin package-form
  weight UX. **Blocks AUD-01c.** `manual_step: ef-migration` (owner — `PriceWeight` column + backfill),
  `manual_step: nswag-regen` (package admin DTO gains `PriceWeight`). Size L. Layers: backend, db, frontend (admin).
  Dependency edge: **AUD-02p → (blocks) → AUD-01c.**

**Fiscal (unchanged from ADR-0004/0006, confirmed here):** the DE/AT/ES corrective-document work rides the
existing **T-0220 (FISCAL-SEQ)** / **T-0221 (FISCAL-AUTH-IDEMP)** go-live-gate cluster; D7 adds the partial
corrective on `Refund.ReceiptId`. CZ/SK/PL: no fiscal work.

**Owner-only steps flagged:** `ef-migration` (AUD-01a, AUD-01d, AUD-02p), `nswag-regen` (AUD-01b if DTO
changes, AUD-01c, AUD-02p). **New open question:** Q-REFUND-03 (per-bundle legacy weighting — non-blocking;
even-split default ships).
