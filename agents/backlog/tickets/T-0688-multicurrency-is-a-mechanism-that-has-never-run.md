---
id: T-0688
title: Multicurrency has never run at a rate other than 1, and the API accepts any currency from any caller
status: todo
size: M
owner: —
created: 2026-09-07
updated: 2026-09-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

> **Researched 2026-09-08 — [`../T-0688-multicurrency-research.md`](../T-0688-multicurrency-research.md).**
> A full read-only pass over the money path, fiscal/legal surface, data model and all five clients, with
> an ordered work plan and eight open decisions. It found four things this ticket does not mention (the
> VAT convention, the cleaner payout invoice, the receipt line items, the loyalty normalisation) and
> confirms the INDEX row's warning about the size. **Re-scoping is the owner's call — this ticket is
> unchanged apart from this pointer.**

## Context

**In plain terms:** the platform was built to handle several currencies, and the machinery is really
there and wired up. But every price in the catalogue is written in CZK, the exchange rate is always
1, and so the conversion code has never actually converted anything. Nobody knows whether it works,
because nothing has ever exercised it.

The repo already says so — `docs/architecture/platform-expandability.md`: *"Real mechanism,
single-currency operation"* — and the one non-CZK unit test says it in its header: *"Every assertion
here is invisible at rate 1, which is exactly why the bug survived: the suite only ever priced in
CZK."*

### The specific holes

1. **An order's currency is chosen by the client.** `CreateOrder` takes `command.CurrencyId` and the
   only validation is that the row exists. Nothing ties it to the address country, the customer or
   the market. All three clients happen to send null today, so every real order is the default — but
   the API will accept any currency id from any authenticated caller. **This one is a defect
   regardless of whether multicurrency ever ships.**
2. **Catalogue prices have no currency at all.** `Service.BasePrice`, `Package.Price` and
   `Extra.Price` carry no `CurrencyId`; they are implicitly base-currency and converted at quote
   time by multiplying by `Currency.ExchangeRate`.
3. **`MembershipPlan.MonthlyPriceCzk` is CZK by field name**, with the canonical price in Stripe.
4. **No order records the rate it was priced at.** An admin editing a rate retroactively changes what
   every historical order, receipt and report says it cost.
5. **Rates are admin-typed** with no feed, no staleness bound, and no rule that the default
   currency's rate is 1.

## Acceptance criteria

- [ ] **AC1** — Given the release decision below, Then the code states it. If the answer is
      single-currency, the API stops accepting a caller-supplied currency and the mechanism is
      documented as dormant rather than left looking supported.
- [ ] **AC2** — Given an order, Then the rate it was priced at cannot change after the fact.
- [ ] **AC3** — Given a customer holding credit in one currency who books in another, Then the
      behaviour is defined on the grant side as well as the spend side. The spend side already
      refuses, deliberately; the grant side does not check.

## Open decisions

**This is the decision the whole ticket hangs on, and it changes the size by an order of magnitude:**

1. **Is multicurrency in scope for THIS release at all?** The honest option is "single-currency at
   launch, mechanism retained" — which is small, and mostly means closing hole 1 and writing the
   limitation down. Shipping real multicurrency is large.
2. If it IS in scope: does the customer choose the currency, or is it derived from the address
   country?
3. Where do exchange rates come from in production, and how stale may one be?
4. Must an order snapshot its rate? (Recommended yes regardless of 1 — it is the difference between
   a receipt being a record and a receipt being a recalculation.)

## Status log

- 2026-09-07 — scoping read before release. The mechanism is real; the exercise is not.
