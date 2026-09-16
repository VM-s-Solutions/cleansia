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

## The market comes before the address

Every step of the path above has a currency, and before there is an address it is the **chosen
market's** ([ADR-0058](/decisions/adr-0058)). The client resolves its market once at bootstrap from
the anonymous `Market/GetOverview` read (stored code if listed → the default market → the first
listed) and sends the market's `countryId` on the catalogue overviews, the quick quote and the
wizard's first step, so a customer who chose SK browses the EUR-priced catalogue and is quoted in
EUR before typing an address. From the address step on the **address's country wins** — the wizard
re-reads the overview for it, trims what that market does not sell (`catalogue_changed_for_country`)
and re-quotes; a market chosen afterwards leaves the booking alone. The one thing inside a booking
that follows the market instead of the address is the Plus step (a subscription belongs to the
customer, not to the booking). When the market list could not be loaded no `countryId` is sent
anywhere and the platform default answers. → [Business rules — the market](/product/business-rules#market)

A signed-in customer may book in any serviced market with an active operator, including a market
served by a different company from the account’s. The server resolves the operator from the address
before creating the order and its children; a saved address from the account’s company is copied
into the order’s address snapshot. Receipts, refunds and disputes belong to that operator. Loyalty,
credit and membership usage stay with the account; card payments still use one holding Stripe
account. Owner-pinned reads keep the booking and its receipt visible in the customer’s order history
regardless of the browsing market. → [ADR-0061 D6](/decisions/adr-0061#d6-tenant-country-and-currency-agree-by-construction-and-two-validators-refuse-the-cases-that-could-break-it)

## The price is never taken from the client

`CreateOrder.Command` carries a `TotalPrice`, and it is **a confirmation, not an input**. The validator
re-prices the whole selection server-side and refuses on disagreement. The amount that reaches Stripe
is `ToMinorUnits(order.TotalPrice)` read from the persisted, server-computed value — the client cannot
influence it at any point, which is why the payment webhook does not need to reconcile the amount.

## The booking leaves a row, and so does a refused one

`CreateOrder` is marked `customer.order.create` ([ADR-0062](/decisions/adr-0062)), so the same commit
that creates the order writes a `CustomerActionAudit` row carrying what the server priced and showed:
the price breakdown, the discounts and the express state, the line items by id, the cleaning time and
lead time, the cancellation policy figures as shown (with this customer's free window), the terms
tick and the terms version in force, the client it came from, the IP and device — never the name,
the address text or the instructions. A guest booking writes the same row with no user; it is the
case the row exists for, and it is reachable later only by the order, never by a person.

A refused booking is a row too, written outside the transaction that was rolled back: a missing
terms tick is `consent.terms_not_accepted` (judged first, ahead of the price chain), the wrong quoted
total is `order.total_price.not_match`, and an express waiver whose quota ran out is its own key.
`order.country_operator_mismatch` remains a guest consistency check; a signed-in account belonging
to another operator is no longer a refusal. An anonymous refusal carries the caller’s IP and is
bounded by the same `auth` window as the request. On an anonymous request, two refusals leave no row
because nothing exists yet to stamp them with: a country that is not a market
(`country.not_serviced`) and a market nobody operates (`tenant.not_found`) are refused before the
operator is resolved, and the failure sink logs one warning instead of writing a row with no tenant.
Successful order/dispute acts follow the order’s operator. A refused act does so only after ownership
is proven; a foreign or missing resource probe retains the caller’s account company. The account
company’s admin can read the customer’s trail across operators after proving access to that account.
→ [What is recorded about a customer](/product/business-rules#customer-record)

## The terms tick is required, unless the account already consented

A booking asserts `termsAccepted: true` or is refused — **unless** the signed-in customer's account
already holds both the terms and the privacy consent, granted and not withdrawn (owner ruling
2026-09-14). That customer sees no box on any client and sends nothing; a guest has no account to hold
a consent on and always asserts it; a customer whose consent was withdrawn is asked again, which is the
right answer to a withdrawal. The tick short-circuits the consent read, so a customer re-consenting at
checkout is never refused for a row the server has not written yet. Every client behaves alike: the
web wizard, the Android and the iOS customer apps show the sentence on the review step when the
account lacks a consent (or there is no account), gate the confirmation on it, and send the tick only
when the box was shown and ticked. The booking row records the tick and the version in force — the
effective date of the terms document for the address's market ([ADR-0063](/decisions/adr-0063)) — and
grants **no** consent rows for it (a residual stated in ADR-0062 D4 as amended: in production every
account holds both from registration). Confirming a recurring occurrence is not gated — the template
was accepted.

## Responsive quote previews

The home calculator requests its quote immediately. Booking groups rapid selection changes into a
200 ms pause before requesting the latest price. A changed selection cancels the previous pending
request immediately; clearing the selection clears the quote. An identical quote already in flight
is shared with checkout rather than requested again. The previous price stays visible while an
updated quote loads, and checkout uses a quote matching the current selection.

`OrderPricingCalculator` returns the estimated duration with its pricing snapshot, using the same
selected services and package contents. `QuoteOrder` reuses that duration instead of loading the
catalogue a second time. Pricing, discounts and create-time validation retain their existing rules;
nothing converts between currencies.

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
| Guest, no account | Allowed. The order is keyed on the email address, and the customer later finds it via order lookup. The audit row has no user; an admin reaches it from the order's history. |
| An unknown language code on the booking | Refused (`CreateOrder.Validator` carries the `LanguageValidator`, the `Register` idiom) — the audit row records `language`, and an unrecognised code is not evidence of anything. No shipped client sends one outside the five seeded codes. |
| A recurring occurrence confirmed | One `customer.order.recurring.confirm` row: the order, the template, the price and currency, the payment type, the cleaning time and lead time. A schedule created, edited, paused/resumed or deleted writes a `customer.recurring.*` row with the schedule facts before and after. |

## Recurring bookings

A template materialises occurrences up to 7 days ahead. A materialised occurrence stays unconfirmed
until the customer confirms it, so *"pending for over an hour"* is its **normal** state, not an
abandoned checkout — which is why the stale-checkout sweep explicitly excludes rows with a
`RecurringTemplateId`. A separate sweep retracts unconfirmed occurrences an hour before the slot.

**A schedule is priced and operated in the market of its saved address.** Creation and update stamp
the template with that address’s active operator. The template carries no currency; every occurrence
resolves its operator and price from the saved address’s country again, the same rule as a one-off
booking. The customer keeps access to their own schedules across operators. Two consequences follow. A preferred cleaner named on the template (`CreateRecurringBooking`,
`UpdateRecurringBooking`) must be paid in that currency as well as have a completed order with the
customer — one key, `order.preferred_employee.not_eligible`, for both terms — because a cleaner paid in
another currency would never see an occurrence on their board. And every recurring wizard -- web,
Android and iOS -- reads the catalogue for the country of whichever saved address is chosen, and for
the chosen market before an address is picked, then trims any selected service or package the new list no
longer offers (with a notice to the customer), like the one-off wizard — otherwise the server would refuse the quote as
`order.selected_services.invalid` / `order.selected_package.invalid` for an entry with no price in that
market. → [Business rules — order currency](/product/business-rules#price-stages)

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
