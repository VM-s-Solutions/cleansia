# Client price display — living decision doc

> **Status: the shape below is IMPLEMENTED on all three clients and pinned by CI on two of them.**
> The immutable record is `agents/archive/2026-08/adr-deliberation/drafts/NNNN-client-price-display-splits-the-server-total.md`
> (**`proposed`** — number not yet allocated; a defense panel of distinct instances is owed before it
> can be `accepted`, per `../../process/deliberation.md`).
> **Catalog:** `../../knowledge/patterns-mobile.md` → *"The client SPLITS the server's total; it never
> evaluates a rate"* (carries the `Enforced by:` label).
> **Related:** `membership-benefits.md` (ADR-0035, the express waiver this surfaced through),
> `../../analysts/` booking/pricing notes.

---

## 1. The question this settles

A booking screen must show a **breakdown** — subtotal, express surcharge, discount, total — while the
server owns the price. Two ways to get a breakdown onto a screen:

- **Re-derive it**: the client re-runs the server's formula from the inputs it has.
- **Split it**: the server sends a composed total plus the components; the client recovers the rows by
  addition/subtraction and evaluates nothing.

Cleansia chose **split**. Everything below is why, and what the choice costs.

## 2. The current shape

`QuoteOrder` returns a total that **already contains** the express surcharge:

```
# server, OrderPricingCalculator.cs:82
totalPrice = chargeSubtotal + expressSurchargeAmount

# server, QuoteOrder.cs:163-164 — every discount is derived from the RAW subtotal
rawSubtotal = totalPrice - expressSurchargeAmount
```

Each client therefore holds an **output**, not an input, and derives its rows by subtraction in exactly
one place per app:

```
subtotal         = quote.totalPrice - quote.expressSurchargeAmount
expressSurcharge = quote.expressSurchargeAmount
total            = max(quote.totalPrice - discount, 0)
expressLine      = waived ? Waived : applied ? Charged : NotExpress
```

| Client | The one resolver | Call sites |
|---|---|---|
| iOS | `BookingPriceSummary.resolve` — `CleansiaCustomer/Sources/Features/Booking/BookingPricing.swift:46-63` | `BookingSheetView.swift:175`, `Confirm/ConfirmStep.swift:30-31` |
| Android | `BookingPriceSummary.resolve` — `customer-app/…/features/booking/BookingPricing.kt:50-65` | `ConfirmStep.kt:100`, `BookingBottomSheet.kt:554` |
| Web (customer) | `OrderPricingFacade` computed signals — `libs/cleansia-customer-features/order-wizard/…/order-pricing.facade.ts:98-101` | the wizard summary + sticky bar |

**Three fields, three jobs, none interchangeable.** `expressSurchargeApplied` is `false` for *both* a
waived slot and a slot that was never express, so the waived row rides its own
`expressSurchargeWaivedByMembership` field and **outranks** `applied` when the client resolves the line.
`expressSurchargeAmount` is the money; nothing else is.

**The one client-side percentage that survives** is `BookingPricing.requiresExpressSurcharge` — the
2–4 h lead band that decides which *slots the grid may label* express **before any quote for that slot
exists**. It touches no money and no money row reads it. It is a labelling heuristic; the server's
`BookingPolicy.RequiresExpressSurcharge` remains the only authority on whether a surcharge is charged.

## 3. Why re-deriving was rejected — the evidence, not the preference

This is not a preference: **the re-derive form shipped and was wrong on both mobile clients.**

Both clients mirrored `CreateOrder.Handler`'s *ordering* — apply the discount first, then +20 % express
on the discounted subtotal — on the stated ground that mirroring the server's ordering makes the shown
total equal the charged total. It does not, because the value the client is applying 20 % to is
`totalPrice`, which the server had **already** added 20 % to. An express booking displayed ~20 % above
what the order was created with, on both platforms, agreeing with each other and with neither the
server nor the charge.

The generalizable failure: **mirroring a server *formula* is not reproducing a server *number*.** The
formula's input was the raw subtotal; what the client holds is the formula's output. Any "the client
mirrors backend ordering" sentence should be read as a question — *which end of the pipeline did the
value I hold come from?*

Two properties of the split form that the re-derive form cannot have:

1. **It cannot drift when a rate changes.** `BookingPolicy.ExpressSurchargeRate` can move without a
   client release, because no client knows it.
2. **It dissolves the pre-/post-discount base question instead of answering it.** With no rate applied
   there is no base to choose. The server derives every discount from the same pre-surcharge subtotal,
   so subtracting the discount off the gross total reproduces the server's composition exactly — and
   the clients agree with *the server*, not merely with each other. Cross-client agreement was the weak
   test that let the defect ship: both clients were consistent and both were wrong.

## 4. The trade-off space (what "split" costs)

| Option | Breakdown fidelity | Failure mode | Verdict |
|---|---|---|---|
| **A. Client re-derives from inputs** | Full | Silent divergence from the charge on every rate/ordering change; two sources of truth for one number | **Rejected — shipped and defective** |
| **B. Client splits the composed total** *(chosen)* | Limited to the components the server sends | A row the server has no field for cannot be shown | **Chosen** |
| **C. Server returns a fully itemized breakdown DTO** (an ordered list of labelled lines) | Full, server-owned | Wire/versioning cost; label localization becomes a server concern or a key contract; every client rewrites its summary UI | **Rejected for now — B is the same guarantee at a fraction of the cost** |

**B's real cost, stated plainly:** the breakdown a client can show is bounded by the amount fields on
the quote. That is deliberate — it converts "we want a new row" from a client task into a **server
contract change**, which is the point. The standing rule that falls out of it:

> A new money line is a **new amount field on the quote**, never a rate in the client. If a row cannot
> be produced by adding or subtracting fields the server sent, the fix is a server field.

C stays on the table and becomes the right answer the moment the number of amount fields makes the
split unreadable, or a country needs line items the CZ/SK shape does not have (a per-country breakdown
belongs in `CountryConfiguration`-driven server output, never in client branching). B → C is additive:
the split resolver is the natural consumer of an itemized DTO.

## 5. Enforcement — and what it does not cover

| Enforcer | Stack | Tier | Covers |
|---|---|---|---|
| `BookingPriceSummaryTests` (`CleansiaCustomer/Tests/BookingPricingTests.swift:46-125`) | iOS, `ios-ci.yml:189-196` | `T1-CI` | the resolver's arithmetic, the waived-outranks-charged order, the discount floor at 0 |
| `BookingPriceSummaryTest` (`customer-app/src/test/…/BookingPriceSummaryTest.kt`) | Android, `android-ci.yml:79` | `T1-CI` | the same, **plus** the base-rejecting case |
| `order-pricing.facade.spec.ts`, `order-wizard.facade.spec.ts` | Web, `frontend-ci.yml:85-86` (affected) | `T1-CI` | the web split |

The Android suite carries the assertion that kills the *class* rather than an instance: a quote whose
surcharge is 20 % of **neither** the pre-discount nor the post-discount subtotal, so only reading the
server's field reproduces it. One assertion rejecting both candidate bases.

**The gap, recorded rather than papered over:** these suites pin the **resolver**. They do not detect a
*second* computer of money appearing beside it — a view that inlines `total * 0.2` in a new row is
green. Today every money row on all three clients goes through the one resolver, so the baseline is
zero; the residual risk is a future call site, not a present one. If it recurs, the mechanizable form is
a source-scan guard over the booking feature directory (the `MembershipExpressClaimTests` `#filePath`
idiom on iOS; a `check-consistency.mjs` rule is `T2-ADVISORY` and cannot read Swift at all).

## 6. Open

- **P-1** — decide whether the tip/VAT lines that a future country needs arrive as amount fields (B) or
  force the move to C. Not urgent; nothing needs them today.
- The ADR is `proposed`; a panel of distinct instances (author ≠ challengers ≠ lead) is owed before it
  is `accepted`, and it must be given a collision-checked number at that time.
