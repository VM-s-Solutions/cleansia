# ADR-NNNN draft ("a client price display SPLITS the server's composed total") — Challenger pass

**Mode:** challenger. Distinct instance from the author; did not write the draft, the catalog edit, or
the living doc. Target: `agents/archive/2026-08/adr-deliberation/drafts/NNNN-client-price-display-splits-the-server-total.md`
@ `fe6db0ca`, its catalog edit (`agents/knowledge/patterns-mobile.md:1269-1322`) and its living doc
(`agents/architecture/decisions/client-price-display.md`).

**Gate 0: REFUTED by default.** Every claim below cites a `file:line` I opened in the working tree on
2026-08-05. No Bash — nothing was compiled or executed; the arithmetic below is decimal algebra over
source I read, and every claim of the form "test X asserts N" is a quotation, not an inference. No git
write, no ADR edit, nothing outside `agents/archive/2026-08/adr-deliberation/challenges/`.

---

## Headline

**The draft ratifies a formula that is still wrong, on the same population, in the same direction of
error, against the same server.** The magnitude dropped from ~20 % of the total to 20 % of the
*discount*, which is why it survived — but the sentence the draft ships as the *repair*
(`patterns-mobile.md:1290-1293`: *"subtracting it off the gross total reproduces the server's own
composition exactly"*) is **arithmetically false**, and the two `T1-CI` suites the draft names as its
enforcers **assert the wrong number as the expected value**.

The server does not compose `total = gross − discount`. It composes
`total = (raw − discount) × (1 + rate)` — one form, in exactly two places, both read:
`QuoteOrder.cs:215-216` and `OrderFactory.cs:103-108`. The two differ by `rate × discount`.

Three blocking findings, three amendments, two limbs I attacked and could not break.

| | Finding | Blocking |
|---|---|---|
| **CH-P1** | The ratified `total` is not the charged total on a discounted express booking; the server already ships the correct field and no client reads it | **Yes** |
| **CH-P2** | Both named `T1-CI` enforcers, and the web spec, encode the defect as the expected value. The gate cannot go red on the thing it names | **Yes** |
| **CH-P3** | The replacement catalog sentence is false in the same clause as the one it replaced | **Yes** |
| CH-P4 | D2, applied honestly, condemns D1 — and the draft never notices | amendment |
| CH-P5 | "Applies to: web" is asserted in the ADR and **denied in the catalog edit it ships** | amendment |
| CH-P6 | The discriminator is decidable and correct — but three clients send three different client-computed money bases **to the server**, and the rule is silent on that direction | amendment |
| CH-P7 | The `Enforced by:` labels name real, red-able gates. The scope gap they state is not the one that matters | amendment |

**Citation sampling.** I spot-checked eleven of the draft's own citations. Ten are exact
(`OrderPricingCalculator.cs:82`; `QuoteOrder.cs:163-164`; `BookingPricing.swift:46-63`;
`BookingPricing.kt:50-65`; `order-pricing.facade.ts:98-101` — `preSurchargeSubtotal` is at `:101`,
`displayedTotalPrice` at `:108-110`, so the range is off by a few lines and the *claim* holds;
`BookingSheetView.swift:175`; `ConfirmStep.swift:30-31`; `ConfirmStep.kt:100`;
`BookingBottomSheet.kt:554`; `ios-ci.yml:189-196`; `android-ci.yml:79`). Citation hygiene is good. The
problems below are not citation drift — they are conclusions drawn from citations that are individually
correct and collectively incomplete: **the draft quotes `QuoteOrder.cs:163-164` and never reads
`QuoteOrder.cs:215-220`, fifty lines further down in the same method.**

---

## CH-P1 — The ratified `total` is not the total the customer is charged. On an express booking with any discount, all three clients display `0.20 × discount` too much — and the server already sends the right number. **BLOCKING**

**The hole.** D1 fixes the total row as

```
total = max(quote.totalPrice - discount, 0)
```

and the living doc §3 defends it: *"subtracting the discount off the gross total reproduces the
server's composition exactly."* It does not. The server's composition, read end to end:

```csharp
// OrderPricingCalculator.cs:79,82
expressSurchargeAmount = chargeSubtotal * BookingPolicy.ExpressSurchargeRate;   // raw * 0.20
totalPrice             = chargeSubtotal + expressSurchargeAmount;              // raw * 1.20

// QuoteOrder.cs:163-164  (the two lines the draft quotes)
var grossSubtotal = result.TotalPrice;
var rawSubtotal   = grossSubtotal - result.ExpressSurchargeAmount;             // == raw

// QuoteOrder.cs:215-216  (the two lines the draft does NOT quote)
var finalPrice = BookingPolicy.ApplyExpressSurcharge(
    rawSubtotal - resolution.TotalAmount, result.ExpressSurchargeApplied);     // (raw - d) * 1.20

// BookingPolicy.cs:188-189
=> surchargeApplies ? discountedSubtotal * (1 + ExpressSurchargeRate) : discountedSubtotal;
```

and `OrderFactory.cs:103-108` freezes the identical expression onto `Order.TotalPrice`, which is what
`CreatePaymentIntent.cs:106` charges.

```
server / charged :  (raw − d) × 1.20  =  1.20·raw − 1.20·d
client / D1      :   1.20·raw − d
delta            :   0.20 × d          ← the client always shows MORE
```

**Worked, with the repository's own numbers.** `Cleansia.Tests/Features/Orders/QuoteOrderExpressSurchargeDiscountBaseTests.cs`
arranges base 1000, express slot, surcharge 200, Plus 10 % → `d = 100`, and asserts at `:112-121`:

```csharp
var factoryTotal = (BaseSubtotal - BaseSubtotal * PlusPercentage / 100m)
    * (1 + BookingPolicy.ExpressSurchargeRate);          // 900 * 1.2 == 1080
Assert.Equal(factoryTotal, result.Value.FinalPriceAfterDiscount);
```

For that same booking D1 renders `1200 − 100 = 1100`. **The customer is charged 1080 and shown 1100.**
At the LOY-003 12 % cap on a 5 000 CZK subtotal (`OrderFactory.cs:55`), `d = 600` and the gap is
**120 CZK**.

**The discrepancy is visible inside one session.** `OrderMappers.cs:182-183` returns `order.TotalPrice`
on the order-detail DTO and `EmailService.cs:93` prints it in the confirmation mail. The confirm sheet
says 1100; the order the customer opens ten seconds later says 1080. That is the *identical*
user-observable failure the draft's §Context describes ("the price shown was never the price charged"),
reduced in size and left in place.

**The server already ships the answer, twice.** `QuoteOrder.Response` (`QuoteOrder.cs:45-46`) carries
**`FinalPriceAfterDiscount`** — the field whose XML doc at `:32-34` says, in terms:

> *"`FinalPriceAfterDiscount` is the display price after the best-of-three (tier vs membership)
> discount, **computed the way `OrderFactory` persists it**: discount off the pre-surcharge subtotal,
> surcharge on top."*

— and **`OriginalSubtotal`** (`= finalPrice + discount`, `:221`), from which the same number is
recoverable. Android's own DTO decodes it and documents it:

```kotlin
// customer-app/…/core/booking/BookingDtos.kt:33-40
/** RAW pre-discount subtotal — must be sent unchanged on Create so the
 *  backend's PriceMatchesAsync validator passes. Use [finalPriceAfterDiscount]
 *  for what the user sees. */
val totalPrice: Double,
/** Display price after best-of (tier, membership) discount. Promo isn't included here. */
val finalPriceAfterDiscount: Double = 0.0,
```

`BookingPriceSummary.resolve` (`BookingPricing.kt:50-65`) ignores it. Web's generated client carries it
(`customer-client.ts:11951`) and no wizard file reads it. iOS's `BookingQuote`
(`BookingCodeStates.swift:4-16`) does not even **decode** it — the one client-side field the server
provides for exactly this row was dropped on the floor at the DTO boundary.

**Why it matters more than the 20 CZK.** The draft's entire claim to authority is that it replaced a
*re-derivation* with a *split*. `total = gross − d` is not a split of anything the server sent: no
server field equals it, and it is only *correct* under a composition order the client chose. The draft's
own diagnosis applies to itself verbatim — *"mirroring a server formula is not reproducing a server
number"* — except here the client is not even mirroring the formula, it is mirroring **half** of it (the
subtraction) and dropping the other half (the gross-up).

**Scope, stated so this is not overclaimed.** The error is zero when `d = 0`, zero when the slot is not
express, and zero when the surcharge is waived (`expressSurchargeAmount == 0` ⇒ `total = raw − d`,
which is correct). It is non-zero on exactly the population the draft exists to protect: **a charged
express slot held by a customer with a tier, Plus or promo discount.**

**What I want changed.**

1. `total` reads **`quote.finalPriceAfterDiscount`** for the server-discount case. iOS must add the
   field to `BookingQuote` + `init(from:)` (`BookingCodeStates.swift:5-16`, `:46-…`); Android and web
   already have it.
2. The **promo case has no server field** and cannot be produced by adding or subtracting the fields the
   quote sends — see CH-P4. Until a server field exists, D1 must say so rather than paper it with a
   subtraction that is wrong in the same way.
3. The living doc §3's *"reproduces the server's composition exactly"* and the catalog's copy of it are
   deleted, not softened (CH-P3).

*Blocking?* **Yes.** The ADR's stated purpose is that the shown price is the charged price. It is not,
on the population it names, and the correction is one field on three clients.

---

## CH-P2 — Both `T1-CI` enforcers named by D6, and the web spec named by the living doc, assert the WRONG number. The gate is green *because* it encodes the defect. **BLOCKING**

**The hole.** D6 attaches `Enforced by:` to `BookingPriceSummaryTests` (iOS) and `BookingPriceSummaryTest`
(Android). Both exist, both run in CI, both go red if you mutate the resolver — I verified all three
(§Found sound). What neither can do is fail on the arithmetic the draft is about, because each one
**asserts the client's number and calls it the server's**:

```kotlin
// customer-app/src/test/…/BookingPriceSummaryTest.kt:32-47
/**
 * The old shape returned 1320 here — it treated the gross 1200 as a pre-surcharge base, discounted
 * it to 1100 and added 20 % of that again. The server charges 1100.       ← FALSE
 */
@Test
fun `an express total is the server total less the discount, never a rate on top`() {
    val summary = BookingPriceSummary.resolve(
        quote(totalPrice = 1200.0, surchargeApplied = true, surcharge = 200.0), discount = 100.0)
    …
    assertEquals(1100.0, summary.total, 0.001)                              ← server charges 1080
}
```

Same inputs, same repository, opposite expected value:

| Suite | File:line | Inputs | Asserts |
|---|---|---|---|
| Backend (`Cleansia.Tests`) | `QuoteOrderExpressSurchargeDiscountBaseTests.cs:112-121` | base 1000, surcharge 200, Plus 10 % | `FinalPriceAfterDiscount == 1080` |
| Android (`T1-CI`, cited by D6) | `BookingPriceSummaryTest.kt:36-47` | total 1200, surcharge 200, discount 100 | `total == 1100` |
| Web (`T1-CI`, cited by living doc §5) | `order-pricing.facade.spec.ts:118-127` | total 1200, surcharge 200, discount 100 | `displayedTotalPrice() == 1100` |
| iOS (`T1-CI`, cited by D6) | `BookingPricingTests.swift:113-119` | total 1200, surcharge 200, discount 300 | `total == 900` (server: 840) |

Three green client suites, one green backend suite, and they disagree by construction.

**Why it matters — this is precisely the failure ADR-0032 exists to prevent, one level deeper than the
version ADR-0032 names.** ADR-0032's failure mode is *a label naming a gate that cannot fail*. These
gates **can** fail; what they cannot do is fail on the property the entry claims. D6's own self-challenge
C-1 answers the wrong objection: it defends the *breadth* of the scope ("the suites pin a function, the
entry claims a property of the app") and never asks whether the function they pin is pinned to the
**right value**. A closed-roster enforcer whose roster is complete and whose expected values are wrong is
strictly worse than no enforcer, because it converts an error into a ratified invariant.

**The structural cause, which the entry does not state.** Every fixture in all three suites is
hand-built from literals (`BookingPriceSummaryTest.kt:101-116`, `BookingPricingTests.swift:52-60`,
`order-pricing.facade.spec.ts`'s `expressQuoteResponse`). **No assertion in any of them is derived from
the server's composition**, so no possible edit to `OrderPricingCalculator`, `QuoteOrder` or
`BookingPolicy.ApplyExpressSurcharge` can redden them. The celebrated "base-rejecting case"
(`BookingPriceSummaryTest.kt:55-65`) makes this concrete: it uses `totalPrice = 1150, surcharge = 150`,
a quote **the server cannot emit** (`expressSurchargeAmount` is always `raw × 0.20`, so 1150/150 is
unreachable). The one assertion the entry advertises as killing the class is computed against an
impossible server state.

**What I want changed.**

1. All four expected values corrected, in the same change as the D1 fix. Until then the ADR must not
   cite them as enforcers of anything.
2. At least one assertion per client derived from a **round-trip fixture** — a quote whose
   `totalPrice`/`expressSurchargeAmount`/`finalPriceAfterDiscount` triple is a copy of the backend
   suite's own arranged output, with a comment naming
   `QuoteOrderExpressSurchargeDiscountBaseTests.cs:112-121` as its source. That is the cheapest thing
   that makes a client suite capable of noticing a server composition change.
3. D6's stated scope gains this limb: *"these suites are hermetic — no fixture is derived from the
   server, so they cannot detect a client/server composition disagreement."*
4. **iOS CI is path-scoped** (`ios-ci.yml:11-17`, `paths: src/cleansia_ios/**`), so a change to
   `OrderPricingCalculator.cs` never runs the iOS gate. Android CI runs on every PR
   (`android-ci.yml:9-12`, deliberately not path-scoped) and frontend CI is `nx affected`
   (`frontend-ci.yml:85-86`). Three enforcers, three different trigger surfaces, and the entry claims
   one tier for all of them. Say so.

*Blocking?* **Yes.** A `T1-CI` label on a suite that asserts the defect is a stronger claim than no
label at all, and it is the reason a reviewer following §"How a reviewer verifies compliance" step 5
would conclude the gate is live.

---

## CH-P3 — The catalog sentence shipped as the *repair* is false in the same clause as the sentence it repairs. **BLOCKING**

**The hole.** `patterns-mobile.md:1290-1293`:

> **This dissolves the pre-/post-discount base question rather than answering it.** There is no base to
> choose because no rate is applied. Every discount amount on the quote was computed by the server
> *against the same pre-surcharge subtotal*, **so subtracting it off the gross total reproduces the
> server's own composition exactly** — the clients now agree with the server rather than merely with
> each other.

The premise is true (`QuoteOrder.cs:164`, `:179`, `:192`, `:202` — every discount is resolved on
`rawSubtotal`). **The conclusion does not follow from it and is false.** That the discount was computed
on the raw base says nothing about where the *surcharge* is applied, and the surcharge is applied
multiplicatively **after** the subtraction. `raw(1+r) − d ≠ (raw − d)(1+r)` for any `d ≠ 0, r ≠ 0`.

Android's source comment carries the same false clause verbatim (`BookingPricing.kt:37-39`):
*"so subtracting it from the gross total reproduces the server's own composition; there is no
client-chosen base."*

**Why it matters.** The backend already learned this lesson and wrote it down, three files from the
handler the clients mirror:

```csharp
// src/Cleansia.Core.AppServices/Features/Orders/OrderPromoApplier.cs:55-57
// rawSubtotal is the handler's own pre-discount base; re-grossing order.TotalPrice is wrong on an
// express order, where the surcharge is applied AFTER the discount
// (OrderFactory: ApplyExpressSurcharge(raw - applied)).
```

That comment is the exact refutation of the catalog sentence, in the repository, predating it. The
draft's §Context is built on a close reading of `QuoteOrder.cs:158-164`'s comment about "two different
bases" — and the very existence of two bases is *why* the composition is not a subtraction. The draft
read the warning and drew the opposite conclusion.

And the draft's A4 rejection is now the load-bearing argument against itself: *"a correct implementation
sitting under an incorrect rule is a defect with a timer on it."* Here it is an **incorrect**
implementation sitting under an **incorrect** rule, and the rule is the one written to prevent it.

**What I want changed.** Delete the clause. The honest replacement is the one the code supports:

> Every discount amount on the quote was computed by the server against the pre-surcharge subtotal, and
> the surcharge is then re-applied **on top of the discounted subtotal**
> (`BookingPolicy.ApplyExpressSurcharge`). A client therefore cannot compose the discounted total by
> subtraction at all — it reads `finalPriceAfterDiscount`. This is D2 in its strongest form: the row
> that cannot be produced from the fields you have is a **server field**, and here the server already
> sends it.

*Blocking?* **Yes.** The catalog is the artifact the next developer obeys, and A4's rejection reasoning
applies with full force to the sentence the draft is shipping.

---

## CH-P4 — D2 is right, and it condemns D1. The promo case cannot be produced from the fields the quote sends, and the draft treats that as satisfied rather than as the trigger D2 defines.

**The hole.** D2: *"If a row cannot be produced by adding or subtracting fields the server sent, the fix
is a server field."* Apply it to the total row, honestly:

- **Server-discount case** (tier / membership): producible — `finalPriceAfterDiscount`. D2 satisfied,
  D1 wrong (CH-P1).
- **Promo case**: `QuoteOrder.Handler` passes `promoDiscount: 0m` (`:202`) by design — the comment at
  `:167-171` says the promo is entered at checkout and applied at create time. So `finalPriceAfterDiscount`
  is promo-blind, and the promo-adjusted total is `(raw − promo) × (1 + rate)` — **which requires the
  rate**. It is not producible by any addition or subtraction of quote fields.

So D2's own trigger fires, and the draft's answer is a subtraction. The draft never states that its
chosen shape has a case its own rule declares out of bounds.

**Why it matters.** This is the load-bearing half of the ADR (D2 is described as *"the operative half of
D1"*), and the first feature to test it fails it. The correct resolutions are both cheap and both
architectural:

- `QuoteOrder.Command` accepts an optional validated `PromoCode`, and `FinalPriceAfterDiscount` becomes
  promo-aware — a quote round-trip after the promo dialog, which the clients already do
  (`OrderPricingFacade.refreshQuoteNow`, `BookingViewModel`'s debounced watcher); or
- a second server field, `finalPriceAfterDiscount` **given** a caller-supplied discount amount.

Either is a server contract change, which is exactly what D2 says the answer must be. Choosing one is a
decision, and it belongs in this ADR because D1 is unsound without it.

**What I want changed.** D2 gains the worked case; D1 states plainly which discount kinds it can and
cannot compose; the promo field becomes a named follow-up with a ticket, not an omission.

*Blocking?* No — but it is the difference between a rule with an answer and a rule with an exception
nobody wrote down.

---

## CH-P5 — "Applies to: web" is asserted in the ADR and **denied, in parentheses, in the catalog edit the ADR ships**.

**The hole.** The draft's header says *"Applies to: iOS (customer), Android (customer), web (customer)"*,
D3 names `OrderPricingFacade`'s computed signals as one of the three governed resolvers, and the living
doc §5 lists web suites in the enforcement table at `T1-CI`. The catalog entry — the artifact a reviewer
actually reads — says the opposite:

```
> *Cross-stack note (descriptive — not a rule for the backend or for web): … customer web performs
> the identical split at …/order-pricing.facade.ts:98-101.*
                     (patterns-mobile.md:1306-1310)
```

**"not a rule for … web"**, and the entry lives in `patterns-mobile.md`, which
`patterns-frontend.md` readers have no reason to open. So the decision is *stated* as general, *filed*
as mobile, and *scoped out* of web by its own text.

**Why it matters — and it is not merely bookkeeping.** Web is where the shape has actually diverged
furthest, and the divergence is invisible from the mobile catalog:

- `order-wizard.facade.ts:232-235` re-implements the LOY-003 selection with the doc-comment *"Mirrors
  backend `OrderFactory.ResolveLoy003Discount`"* — a **second** computer of money outside the "one
  resolver", which D3 forbids and the entry's stated scope gap says the enforcers cannot see.
- `order-promo.facade.ts:65-72` sends a client-computed money value **to the server** (CH-P6), on a
  justification that is false at HEAD.

None of that is reachable from a rule filed in `patterns-mobile.md` and disclaimed for web.

**What I want changed.** Either (a) the entry loses the *"not a rule for … web"* parenthesis and a
pointer lands in `patterns-frontend.md`, and web is named in the `Enforced by:` label with
`order-pricing.facade.spec.ts` / `frontend-ci.yml:85-86`; or (b) the ADR's *Applies to* drops web and
D3 stops naming `OrderPricingFacade`. **(a)** is right — the rule *is* general — but the ADR may not
claim generality while the catalog edit disclaims it.

*Blocking?* No — but a decision whose two artifacts contradict each other on scope is not finalized.

---

## CH-P6 — The discriminator survives on the two cases the draft tested. It is **silent** on the one that is actually wrong today: a client-computed money value sent TO the server. Three clients, three different bases, none the server's.

**First, what I could not break — the discriminator itself.** D4's *"does a currency amount depend on
it"* is decidable by a reviewer who did not write it, and it classifies the two live cases correctly:

- `BookingPricing.requiresExpressSurcharge` (`BookingPricing.swift:9-13`, `BookingPricing.kt:23-27`)
  returns `Bool`; its only consumers are slot labels; no money row reads it. **Correctly permitted.**
- the "5 % off" perk label off `membership.discountPercentage` — rendered, never applied. **Correctly
  permitted.**
- the old `finalTotal` — produced a currency amount. **Correctly forbidden.**

I tried to construct a case where the test is ambiguous (a percentage that decides *rounding*; a
percentage that decides *which of two server amounts to show*) and in each the test answers cleanly:
the rounding case is money-dependent and forbidden, the selection case is not and is permitted. **D4 is
SUSTAINED as written.**

**The hole is what D4 does not cover.** Every clause of D1–D4 is about money **rendered**. Nothing
governs money **submitted**. And there, at HEAD, the three clients disagree with each other and all
three disagree with the server:

| Caller | `orderSubtotal` sent to `ValidatePromoCode` | file:line |
|---|---|---|
| iOS | `quote.totalPrice` — gross, pre-discount | `BookingViewModel.swift:278` |
| Android | `response.totalPrice` — gross, pre-discount | `BookingViewModel.kt:244` |
| Web | `displayedTotalPrice()` — **gross minus discount** | `order-promo.facade.ts:69-72` |
| **Server, at create** | **`rawSubtotal` = pre-surcharge, pre-discount** | `CreateOrder.cs:364`, `:399-400` → `OrderPromoApplier.cs:28-29` |

Web's choice carries a justification that is **false at HEAD**:

```ts
// order-promo.facade.ts:65-68
// Validate against the price the user is actually charged — backend's
// CreateOrder.Handler resolves promo discounts against `finalTotalPrice`
// (post-express-surcharge), so a bare-subtotal validation could fail a
// min-order threshold that would otherwise pass on the real charge.
```

`CreateOrder.Handler` does no such thing: `rawSubtotal` at `:364` is *pre*-surcharge, and it is what
both `PreviewAsync` (`:399-400`) and `ApplyAsync` (`:446`) receive. `OrderPromoApplier.cs:55-57`
explicitly warns against the base this comment claims the backend uses.

**Consequence, worked.** raw 1000, express, tier discount 100. Web validates a 10 %-off promo against
`displayedTotalPrice = 1100` → previews **110**. The server at create previews against `raw = 1000` →
**100**, then `ResolveLoy003Discount(0, 100, 100, 1000)` takes `promo > combined` as *false* and applies
the tier pair. The wizard showed a 110 saving and a total of `1200 − 110 = 1090`; the order is created
at `(1000 − 100) × 1.2 = 1080`. Two independent errors compounding on one screen. Min-order thresholds
(`promo.below_minimum_order_amount`) are decided on the same mismatched base, so a promo can validate
green in the dialog and be silently dropped at submit.

**What I want changed.** D2 gains a submit-side limb, and it is the same sentence in the other
direction:

> A money value the client **sends** is a value the server gave it, unmodified. If the server needs a
> base the client does not hold, the server derives it — the client never computes one to submit.

Then `orderSubtotal` is either dropped from `ValidatePromoCode` (the server has the selection and can
re-price) or filled from a server field on all three clients. The false comment at
`order-promo.facade.ts:65-68` is deleted in the same change.

*Blocking?* No, but it is the live instance of the class D1 claims to have closed, and the draft's
"how a reviewer verifies" has no step that would find it.

---

## CH-P7 — The `Enforced by:` labels are mechanically honest. The scope statement is honest about the wrong gap.

I verified the label end to end, because ADR-0032 makes this the specific thing a challenger owes:

- `BookingPriceSummaryTests` → `CleansiaCustomer/Tests/BookingPricingTests.swift:62-124`. The target is
  real: `project.yml:157-169` declares `CleansiaCustomerTests` with `sources: [Tests]`, and the
  `CleansiaCustomer` scheme (`:200-209`) lists it under `test.targets`. `ios-ci.yml:191-196` runs
  `xcodebuild -scheme CleansiaCustomer … build test`. **Exists, runs, can go red.**
- `BookingPriceSummaryTest` → `customer-app/src/test/…/BookingPriceSummaryTest.kt`, run by
  `:customer-app:testDebugUnitTest` at `android-ci.yml:79`, on **every** PR (`:9-12`). **Exists, runs,
  can go red.**
- Web: `order-pricing.facade.spec.ts:96-160` under `nx affected -t test` at `frontend-ci.yml:85-86`.
  **Exists, runs, can go red** — but is cited only in the living doc, not in the label (CH-P5).

So `T1-CI` is the right token on the mechanism. What is wrong is the *stated* gap. The entry states one
("they do not catch a second computer of money"), and that one is real — `effectiveDiscount` on all
three clients (`BookingViewModel.swift:96-99`, `BookingViewModel.kt:216-221`,
`order-wizard.facade.ts:232-235`) is already such a computer, and the entry blesses it at `:1302-1304`
as *"compliant: it selects among server amounts"*. I accept that ruling for the mobile pair, where the
inputs are quote fields. I do **not** accept it for web, where the promo input is a number the client
obtained by validating against a base it invented (CH-P6) — "selects among server amounts" is false
there.

The two gaps that are **not** stated and that would each have caught CH-P1:

1. **Hermetic fixtures.** No expected value in any suite is derived from the server (CH-P2).
2. **Trigger asymmetry.** iOS's gate never runs on a backend change (`ios-ci.yml:11-17`).

**What I want changed.** Both added to the entry's scope paragraph, and the "compliant" blessing of
`effectiveDiscount` narrowed to the mobile pair with the web instance called out.

*Blocking?* No.

---

## Found sound — what I attacked and could not break

Stated explicitly, because silence is not assent and because the draft names three specific places an
independent challenger should start. **Two of the three are clean, and I say so.**

- **D5 (three fields, three jobs).** `expressSurchargeWaivedByMembership` is a real wire field
  (`QuoteOrder.cs:59-65`), all three clients rank `waived` above `applied`
  (`BookingPricing.swift:50-56`, `BookingPricing.kt:54-58`, `order-pricing.facade.ts:84-98`), and no
  client derives the waiver from `amount == 0`. **Sustained.** The waived branch is also the one case
  where D1's total is arithmetically right, for the reason D5 gives.
- **`expressUpgradesRemaining` — the author's first nominated attack. It does not land.** iOS reads
  `membership?.expressUpgradesRemaining` (`BookingViewModel.swift:90-92`) with the comment *"Never
  adjusted for the booking being composed"*, and the server-side field is a server count
  (`QuoteOrder.cs:66-71`, `OrderPricingCalculator.cs:95`). No client decrements anything. The only
  residual is that the *same fact* has two server sources (the membership endpoint and the quote), which
  is a staleness nit, not the defect class named. **Sustained.**
- **Quote→submit staleness — the author's second nominated attack. It does not land either.** The
  submit resubmits `TotalPrice` unchanged and `CreateOrder`'s `Cascade.Stop` chain re-prices once
  (`CreateOrder.cs:220-248`), classifying the one input that can move — the waiver — into its own error
  key (`membership.express_waiver.no_longer_available`) ahead of the generic price mismatch. That is a
  better design than the draft gives it credit for. **Sustained.**
- **D3's shape** — "one resolver per screen, a view never does money arithmetic" — is the right shape,
  and it is what makes CH-P1 a **one-line fix in three files** instead of an audit. The shape is sound;
  the expression inside it is wrong.
- **A2 (itemized DTO) and A3 (extra pre-surcharge field)** are correctly rejected on the arguments given.
  A3's "a redundant field is a second source of truth" is right — and note it does **not** apply to
  `finalPriceAfterDiscount`, which is not redundant with anything the client can compute.

**Net.** The author's three self-nominated starting points were the author's own reasoning, and two of
the three are clean. The defect is where the draft was most confident: in the arithmetic it had already
declared settled, fifty lines below the citation it quoted.

---

## Verdict I am asking the lead for

**CH-P1, CH-P2 and CH-P3 block.** They are one defect seen from three sides — the code, its gate, and
its rule — and the fix is a single field on three clients plus four corrected expected values.

The **catalog edit must not be treated as "correct on its own evidence and not waiting on the panel"**
(draft's method box, lines 22-24). It removes a false sentence and installs a different false sentence
in the same paragraph. The *deletion* is right and can stand; the *replacement* at
`patterns-mobile.md:1290-1293` must be corrected before anyone builds on it.

CH-P4 through CH-P7 are amendments I would accept folded into the revision without a second round.
