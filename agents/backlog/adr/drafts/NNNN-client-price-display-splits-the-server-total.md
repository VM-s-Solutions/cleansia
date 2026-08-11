# ADR-NNNN (DRAFT — number NOT allocated) — A client price display SPLITS the server's composed total; no pricing rate is ever evaluated client-side

- **Status:** `proposed`
- **Date:** 2026-08-05 (drafted)
- **Number:** **not allocated on purpose.** Two architects collided on a number this sprint by both
  grepping `adr/` correctly at the same moment. Highest on disk today is **0042**; I am asking for
  **0043** or whatever the PM has free. The file is renamed on allocation.
- **Applies to:** iOS (customer), Android (customer), web (customer) — and the quote contract they read
- **Consumes:** ADR-0032 (a constraining entry names an enforcer and declares a tier), ADR-0035
  (the metered express waiver whose fields this reads)
- **Catalog edit shipped with it:** `agents/knowledge/patterns-mobile.md` → *"The client SPLITS the
  server's total; it never evaluates a rate"* + the corrected Slice-B clause
- **Living doc:** `agents/architecture/decisions/client-price-display.md`
- **Routing:** routed to the Architect by the iOS lane under `conventions.md` §"Who ratifies a catalog
  edit" **test 2** — the change *replaces a form the existing entry named*, so the inline lane was not
  available. Both required searches were recorded by that lane. Routing accepted; not returned.

> ### ⚠️ Method declaration — this draft has NOT been through a defense panel
> It was written by **one** architect instance. `agents/process/deliberation.md` requires
> author ≠ challengers ≠ lead as **distinct instances** for every architectural decision. §Challenge
> below is an **author-run self-challenge**, which is explicitly weaker: it cannot surface the author's
> blind spots. **This ADR may not be marked `accepted` until an independent challenger round and a lead
> adjudication have run.** The catalog edit it accompanies is nonetheless correct on its own evidence —
> it *removes* a false statement that shipped a defect — and does not wait on the panel.
>
> **No shell in this invocation** (`Read`/`Write`/`Edit`/`Glob`/`Grep` only). Every claim below is from
> reading source at HEAD; nothing was executed. Claims that would change on execution are marked
> **⚠ not run**.

---

## Context

`QuoteOrder` returns one composed price. `OrderPricingCalculator.cs:82` composes it:

```csharp
var totalPrice = chargeSubtotal + expressSurchargeAmount;
```

and `QuoteOrder.cs:163-164` derives the discount base back out of it:

```csharp
var grossSubtotal = result.TotalPrice;
var rawSubtotal   = grossSubtotal - result.ExpressSurchargeAmount;
```

The comment above those lines states the reason the two bases exist and are different: the **gross** is
what the client must resubmit (`CreateOrder.PriceMatchesAsync` compares it against the same calculator
call), while the **discount** is resolved on the raw pre-surcharge subtotal because that is what
`OrderFactory` persists.

**The mobile clients did not read it that way.** Both iOS and Android took the *ordering* of
`CreateOrder.Handler` — discount first, then +20 % express — and re-ran it against `totalPrice`. The
catalog told them to: `patterns-mobile.md` Slice B said, in terms, *"max(tier,promo) discount FIRST,
then +20% express on the discounted subtotal … mirroring `CreateOrder.Handler` ordering so the shown
total == the charged raw subtotal."* The value they applied 20 % to already had 20 % in it. Every
express booking displayed roughly a fifth more than the order was created with, on both platforms.

Two properties made it survive review:

1. **The two clients agreed with each other.** Parity checks — the usual mobile safety net — pass when
   both platforms are wrong in the same way.
2. **The catalog sentence read as settled**, cited a real backend handler, and gave a plausible reason
   ("so the shown total == the charged raw subtotal"). It was the *most* authoritative-looking thing in
   the vicinity of the bug.

## Decision

**D1 — A client renders money by SPLITTING the server's composed total. It evaluates no rate.**
Each money row is produced by adding or subtracting fields the server sent:

```
subtotal         = quote.totalPrice - quote.expressSurchargeAmount
expressSurcharge = quote.expressSurchargeAmount
total            = max(quote.totalPrice - discount, 0)
expressLine      = waived ? Waived : applied ? Charged : NotExpress
```

**D2 — A new money line is a new AMOUNT field on the quote, never a rate in the client.** If a row
cannot be produced by adding or subtracting fields the server sent, the fix is a server field. This is
the operative half of D1: it names what to do *instead*, so the next feature does not rediscover the
rate.

**D3 — The split lives in exactly ONE resolver per client, and every money row on that screen reads
it.** iOS `BookingPriceSummary.resolve`; Android `BookingPriceSummary.resolve`; web
`OrderPricingFacade`'s computed signals. A view never does money arithmetic. This is what makes D1
enforceable at all: a rule over "every view" has no witness, a rule over one function does.

**D4 — A client-side percentage is permitted only where it decides PRESENTATION and no money row reads
it.** `BookingPricing.requiresExpressSurcharge` (the 2–4 h lead band) stays: it labels *slots* in the
grid before any quote for that slot exists. The test that separates the two cases is not "is it a
percentage" but **"does a currency amount depend on it"**.

**D5 — Three server fields, three jobs, none substitutable.** `expressSurchargeApplied == false` is
equally true for a waived slot and for a slot that was never express, so `Waived` rides its own
`expressSurchargeWaivedByMembership` field and **outranks** `applied`. Deriving the waiver from
`amount == 0` reintroduces exactly the conflation ADR-0035 separated.

**D6 — The catalog entry carries `Enforced by:` with the two shipped suites and states its narrower
scope.** `BookingPriceSummaryTests` (iOS, `ios-ci.yml:189-196`) and `BookingPriceSummaryTest` (Android,
`android-ci.yml:79`) — **`T1-CI`**, scope = the one resolver per client. The entry says so, because
they do not catch a *second* computer of money appearing beside the resolver.

## Alternatives considered

**A1 — Client re-derives the breakdown from the inputs (the status quo ante).**
Rejected on evidence, not preference: it shipped and was wrong. It requires the client to hold every
rate the server holds, so a rate change is a client release, and the failure is silent — a wrong number
renders exactly as confidently as a right one. It also forces a question with no good answer (is the
surcharge computed on the pre- or post-discount base?) which D1 dissolves rather than settles.

**A2 — Server returns a fully itemized breakdown DTO** (an ordered list of labelled amount lines).
The strongest alternative, and **not wrong** — it is where this goes if the split ever stops being
readable. Rejected *for now* on cost: it adds a wire contract to version, moves label localization into
either the server or a key contract, and rewrites three summary UIs, to buy a guarantee D1 already has
(no client-side arithmetic on rates). It is **additive** to D1, not a replacement: the one resolver per
client is the natural consumer of an itemized DTO, so choosing D1 today does not foreclose it. The
trigger to revisit: a country whose receipt needs line items the CZ/SK shape does not have — which must
arrive as `CountryConfiguration`-driven **server output**, never as client branching on a country code.

**A3 — Send the pre-surcharge subtotal as its own field and stop subtracting.**
Cheap and tempting; rejected as **strictly weaker than it looks**. It removes one subtraction and adds a
field that can disagree with `totalPrice - expressSurchargeAmount`. The subtraction is not the fragile
part — evaluating a rate was. A redundant field is a second source of truth for one number, which is the
same defect class one layer over.

**A4 — Leave the catalog entry and fix only the code.**
Rejected: the entry is what *caused* the defect, it names a function that no longer exists, and a
correct implementation sitting under an incorrect rule is a defect with a timer on it.

## Consequences

- **The breakdown a client can show is bounded by the amount fields on the quote.** Deliberate: it
  converts "we want a new row" from a client task into a server contract change. That is the cost, and
  it is the point.
- **A rate change (`BookingPolicy.ExpressSurchargeRate`) needs no client release.** No client knows it.
- **Cross-client agreement stops being the test.** It never was one — the defect was consistent across
  both. The test is agreement with the server's composition, which the Android base-rejecting case
  encodes directly.
- **Two clients now carry a `BookingPriceSummary` type with the same name and the same contract in two
  languages.** Accepted duplication (the mobile parity principle, ADR-0018); the pinning is that both
  suites assert the same arithmetic.

## How a reviewer verifies compliance

1. `grep` the customer booking feature on each client for a numeric rate literal or a `* 0.2` / `* 1.2`
   applied to a currency value. Expect zero outside `requiresExpressSurcharge`'s lead-hour constants.
2. Every money row in a booking view resolves from `BookingPriceSummary` / `OrderPricingFacade` — no
   view-local arithmetic on `quote.totalPrice`.
3. The waived row reads `expressSurchargeWaivedByMembership`, not `expressSurchargeAmount == 0`.
4. A new money line added to a screen is accompanied by a **server field** on the quote, not a client
   computation.
5. Mutation to prove the gate is live: change `subtotal = totalPrice - expressSurchargeAmount` to
   `totalPrice` in either resolver — both suites must go red. **⚠ not run** in this invocation; the
   implementing lane reported it and the assertions are present by reading.

## Challenge (author-run — NOT a panel; an independent round is owed)

**C-1 — "`T1-CI` is overclaimed: the suites pin a function, the entry claims a property of the app."**
Stands as a scope statement, and is answered *in the entry* rather than by weakening the tier: the label
names the suites, declares `T1-CI`, and then says in the entry that the scope is the one resolver and
that a second computer of money is not caught. ADR-0032 permits a closed-roster enforcer *provided the
boundary is visible to the reader*. The baseline for the wider claim is zero today (all six call sites
enumerated), so the residual is a future call site.

**C-2 — "Then it should be `(gate pending: <ticket>)` for the wider claim."**
Rejected. `(gate pending:)` is for a gate that is *specified and ticketed but blocked by a live
violation*. There is no live violation and no specified gate; inventing a ticket to justify a token
would be worse than stating the scope. If a second computer of money ever appears, that is the moment to
file the source-scan guard — and the entry says what it would look like.

**C-3 — "A2 (itemized DTO) was dismissed too fast; it is the actually-correct long-run design."**
Partly conceded, and folded in: A2 is recorded as *not wrong*, additive rather than alternative, with a
named trigger (a country needing different line items) and a note that D1's one-resolver shape is its
natural consumer. The rejection is on **cost today**, not on merit.

**C-4 — "D4 is a loophole: 'presentation only' is what the defective code thought it was doing."**
Sharpened rather than removed. The defective code was not doing presentation — it produced a **currency
amount**. The discriminator in D4 is therefore not intent but consequence: *does a currency amount
depend on this number?* `requiresExpressSurcharge` fails that test cleanly (its output is a slot label),
and the entry states that no money row reads it.

**C-5 — "Both clients ship a duplicated type; why is this not in `CleansiaCore` / a shared module?"**
Not conceded, and out of this ADR's scope: iOS and Android share no code by construction. Within iOS,
`BookingPriceSummary` is customer-app-local because the partner app has no booking sheet — hoisting it
to `CleansiaCore` would put a customer money type in the shared package for no second consumer.

**Not self-challenged, and an independent challenger should start here:** whether the quote's
`expressUpgradesRemaining` display path has the same class of defect (a client adjusting a server count);
whether `CreateOrder`'s submit-time recomputation can make the displayed total stale between quote and
submit in a way the split hides; and whether web's `OrderPricingFacade` has a call site outside the two
specs' reach.

## Verdict

**Not adjudicated.** No lead has ruled; no independent challenger has run. Per `deliberation.md` step 5,
this artifact is **not finalized** and must not be cited as an accepted decision. What *is* settled and
does not wait on the panel: the two `patterns-mobile.md` entries were **false as written**, one of them
demonstrably causal for a shipped defect, and their correction is a repair rather than a decision.
