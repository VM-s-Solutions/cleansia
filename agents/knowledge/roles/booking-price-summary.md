# Role — `BookingPriceSummary` (client-side money resolver, iOS + Android) — CRC card

> **✅ BUILT on both mobile clients** — iOS
> `src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/BookingPricing.swift:34-64`, Android
> `src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/booking/BookingPricing.kt:41-67`.
> Web's equivalent is `OrderPricingFacade`'s computed signals
> (`libs/cleansia-customer-features/order-wizard/…/order-pricing.facade.ts:98-101`).
> **Decision:** `agents/archive/2026-08/adr-deliberation/drafts/NNNN-client-price-display-splits-the-server-total.md`
> (`proposed`, number not allocated). **Living doc:** `agents/architecture/decisions/client-price-display.md`.
> **Catalog:** `agents/knowledge/patterns-mobile.md` → *"The client SPLITS the server's total…"*.
>
> This card exists because the defect it prevents was caused by a **missing "does NOT know"** — the old
> shape knew the express surcharge rate, and knowing it was the bug.

## Responsibility (one sentence)

Turn **one server quote plus one already-resolved discount amount** into the exact set of money rows a
booking screen draws — subtotal, express surcharge, express line state, total — **by splitting the
server's figures and evaluating nothing.**

## Collaborators

- **The quote** (`BookingQuote` / `QuoteOrderResponse`) — `totalPrice`, `expressSurchargeAmount`,
  `expressSurchargeApplied`, `expressSurchargeWaivedByMembership`, `currencyCode`. **All amounts, no
  rates.** `totalPrice` already contains the surcharge (server: `OrderPricingCalculator.cs:82`).
- **`discount: Double`** — passed in, never computed here. Today that is
  `BookingViewModel.effectiveDiscount` (`BookingViewModel.swift:96-99`) = `max(tierDiscountAmount +
  membershipDiscountAmount, promoState.discount)`, all three of which are **server amounts**. This role
  neither knows nor cares how the best-of was chosen.
- **Its consumers** — the sticky price bar and the confirm summary, on both clients
  (iOS `BookingSheetView.swift:175`, `Confirm/ConfirmStep.swift:30-31`; Android `ConfirmStep.kt:100`,
  `BookingBottomSheet.kt:554`). Both read the **same** instance-shape so the two cannot disagree.

## Does NOT know

- **Any rate.** Not the 20 % express surcharge, not a tier or membership percentage, not VAT. If a rate
  literal appears in this type, the defect is back. The whole rule: *a new money line is a new **amount
  field** on the quote, never a rate here.*
- **How the server composed the total.** It knows only that `totalPrice` includes the surcharge, which
  is why `subtotal` is a **subtraction** and never a re-derivation.
- **Whether this slot is express.** `BookingPricing.requiresExpressSurcharge` labels *slots in the grid*
  before a quote exists; this type reads the server's verdict fields instead. The two must never be
  crossed — one is presentation, the other is money.
- **Which discount won.** Best-of-three is LOY-003, server-side; the caller hands over one number.
- **The currency symbol or formatting.** `BookingPricing.formatTotal` / `formatOrderPrice` own that; this
  type returns raw amounts (the same "keys + raw amounts, the view owns currency" split as
  `CancellationFeeCallout`).
- **Anything about the order being submitted.** It is a pure function of (quote, discount). No VM state,
  no network, no side effect.

## Invariants a reviewer checks

1. `subtotal == totalPrice - expressSurchargeAmount`. Never a percentage of anything.
2. `expressSurcharge` is the server's field **verbatim** — never `subtotal * rate`, never inferred.
3. `expressLine`: **`waived` outranks `charged`.** `expressSurchargeApplied == false` is equally true for
   a waived slot and a slot that was never express, so the waived row rides
   `expressSurchargeWaivedByMembership` and never `amount == 0`.
4. `total == max(totalPrice - discount, 0)` — the floor is real; a promo larger than the order must not
   render a negative.
5. **No quote → zeros, not a guess.** A confident wrong number is worse than an empty row (the T-0527
   rule).
6. **One call site per screen.** A view that does its own arithmetic on `quote.totalPrice` is the
   deviation, and it is the one thing the guard suites do **not** catch.

## Enforcement

`BookingPriceSummaryTests` (iOS, `CleansiaCustomer/Tests/BookingPricingTests.swift:46-125`,
`ios-ci.yml:189-196`) and `BookingPriceSummaryTest` (Android,
`customer-app/src/test/…/BookingPriceSummaryTest.kt`, `android-ci.yml:79`) — **`T1-CI`**. The Android
suite carries the case that kills the class rather than an instance: a surcharge that is 20 % of
**neither** candidate base, so only reading the server field reproduces it (`:56-65`).

**Scope boundary:** both suites pin *this type*. Invariant 6 has **no** mechanical enforcer; its baseline
is zero today (all six call sites enumerated above). If a second computer of money ever appears, that is
the moment to add a source-scan guard, not before.

## Watch-list

- If the quote grows to five or six amount fields, the split stops being readable and the right move is a
  **server-itemized breakdown DTO** (ADR alternative A2) consumed *by this same type* — it is additive,
  not a rewrite.
- A per-country receipt shape belongs in `CountryConfiguration`-driven **server output**. Do not let a
  country difference arrive here as a branch.
- iOS and Android carry two copies of this type by construction (no shared code across the platforms).
  Do **not** hoist the iOS one into `CleansiaCore`: the partner app has no booking sheet, so there is no
  second consumer, and a customer money type in the shared package invites one.
