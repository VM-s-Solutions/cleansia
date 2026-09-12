# Booking and pricing

A customer picks services, a date and a payment method, and an order exists. Guests can do this — no
account required.

## The path

```mermaid
sequenceDiagram
  autonumber
  participant C as Customer
  participant API as Customer API
  participant V as CreateOrder.Validator
  participant P as OrderPricingCalculator
  participant F as OrderFactory
  participant S as Stripe

  C->>API: POST /Order/Quote
  API->>P: price the selection
  P-->>C: total, express surcharge, waiver state

  C->>API: POST /Order/CreateOrder (incl. the quoted total)
  API->>V: validate
  V->>P: RE-price, server-side
  V-->>API: refuse if the totals disagree
  API->>F: create
  F-->>API: New + PaymentStatus.Pending
  alt Card
    API->>S: create checkout session for order.TotalPrice
    S-->>C: payment page
  else Cash
    API-->>C: booked; nothing to pay now
  end
```

## The price is never taken from the client

`CreateOrder.Command` carries a `TotalPrice`, and it is **a confirmation, not an input**. The validator
re-prices the whole selection server-side and refuses on disagreement. The amount that reaches Stripe
is `ToMinorUnits(order.TotalPrice)` read from the persisted, server-computed value — the client cannot
influence it at any point, which is why the payment webhook does not need to reconcile the amount.

## Responsive quote previews

The home calculator requests its quote immediately. Booking groups rapid selection changes into a
200 ms pause before requesting the latest price. A changed selection cancels the previous pending
request immediately; clearing the selection clears the quote. An identical quote already in flight
is shared with checkout rather than requested again. The previous price stays visible while an
updated quote loads, and checkout uses a quote matching the current selection.

`OrderPricingCalculator` returns the estimated duration with its pricing snapshot, using the same
selected services and package contents. `QuoteOrder` reuses that duration instead of loading the
catalogue a second time. Pricing, discounts, currency conversion, and create-time validation retain
their existing rules.

## Room selection and start times

While selecting services, room and bathroom counts are editable above the sticky desktop summary.
On smaller screens those controls appear before the packages and services, so they are visible
without scrolling through the catalogue. Both layouts edit the same selection.

Web, Android, and iOS offer starts every 15 minutes from 08:00 through 19:45. The two-hour minimum
lead time and the express window still apply to the exact selected instant, including its minutes.

## Edge cases

| Case | What happens |
|---|---|
| The quoted price no longer matches | Refused. The client re-quotes. |
| **The quote included an express waiver, and the monthly quota ran out in between** | Refused with its **own** error rather than a generic mismatch — this is the one pricing input that can legitimately change between two runs of a fixed command, and the customer is told exactly that. |
| Under 2 h lead time | Refused outright. Not priced higher — refused. |
| 2–4 h lead time | Accepted with a **+20 %** express surcharge, unless a Plus waiver applies. |
| Booked span over 24 h | Refused. See [why that bound exists](/product/business-rules#maximum-booked-duration-24-h-and-it-is-not-about-calendars). |
| A package **and** a service the package includes | Charged twice, performed twice, takes twice as long. Owner ruling — not a bug, and not to be de-duplicated. |
| Guest, no account | Allowed. The order is keyed on the email address, and the customer later finds it via order lookup. |

## Recurring bookings

A template materialises occurrences up to 7 days ahead. A materialised occurrence stays unconfirmed
until the customer confirms it, so *"pending for over an hour"* is its **normal** state, not an
abandoned checkout — which is why the stale-checkout sweep explicitly excludes rows with a
`RecurringTemplateId`. A separate sweep retracts unconfirmed occurrences an hour before the slot.

> The materialiser decides "did I already spawn this occurrence?" with an unlocked read, and **the
> answer is enforced by a unique index** — `IX_Orders_RecurringTemplateId_CleaningDateTime`, on the
> template plus the exact occurrence instant, filtered to spawned orders. The read is the fast path; the
> index is the arbiter, and it speaks at commit.
>
> Until 2026-08-15 there was no index, and what actually prevented a duplicate charge was that Azure
> Functions timer triggers hold a singleton lease — **a guarantee in the hosting model rather than the
> schema**, so moving the sweep to another scheduler or fanning it out would have reintroduced duplicate
> billing silently. The lease still holds; it is no longer the only thing holding.

## Guest order lookup

`GET /Order/Lookup` takes an order number and an email and is anonymous. It is not enumerable: the
number is `ORD-` plus 8 random hex characters (32 bits, not sequential), the email must match, and the
endpoint is rate-limited. The batch variant is capped at 10 items and keyed on the internal GUID
rather than the human-typed number, so it is strictly narrower than the single lookup it builds on.
