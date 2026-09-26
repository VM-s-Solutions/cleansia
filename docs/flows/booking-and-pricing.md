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
  V-->>API: refuse cash unless signed in AND one cleaner is required
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

## Cash is for a signed-in customer's one-cleaner job {#cash-eligibility}

A booking may be paid in cash only when the caller is signed in **and** the server's crew for the
selection is exactly one (`BookingPolicy.AllowsCash`, owner ruling 2026-09-24); a guest, or a booking
whose duration needs two cleaners or more, pays by card. The crew is not something the command carries:
the validator reads it off the same calculator run that re-priced the order
(`OrderDuration.RequiredEmployees`, one cleaner per started 120 minutes), so a forged request cannot
make a two-cleaner job cash-eligible. The rule sits in the price chain after the price match and ahead
of the promo rules, and a refusal is `order.cash_not_available` before any side effect — no express
waiver reserved, no credit debited, no referral accepted, nothing dispatched. `OrderFactory` refuses
the same combination as a backstop for callers that never run the validator.

**What the clients do.** The quote already returns `requiredEmployees` for the selection on screen.
The customer web wizard and the Android and iOS booking flows combine it with the live sign-in state:
cash is offered only when both allow it; otherwise it is disabled with the reason — cash needs a
signed-in customer, this booking needs N cleaners, or (while no quote describes the current selection)
it is confirmed once the price is ready. The mobile booking flows run inside a signed-in session, so
only the web ever shows the first reason. A cash choice that stops being allowed — a sign-out, a bigger
selection — is **taken away, not switched to card**: the payment choice is cleared and the customer is
told to choose again. Both mobile apps re-check cash against the quote the booking is submitted with
and clear it there rather than send it. If the server still refuses, the web and iOS clear the choice
(the web wizard returns to the payment step); Android shows the refusal and leaves the choice for the
customer to change.
→ [Business rules — paying in cash](/product/business-rules#cash)

## The booking leaves a row, and so does a refused one

`CreateOrder` is marked `customer.order.create` ([ADR-0062](/decisions/adr-0062)), so the same commit
that creates the order writes a `CustomerActionAudit` row carrying what the server priced and showed:
the price breakdown, the discounts and the express state, the line items by id, the cleaning time and
lead time, the cancellation policy figures as shown (with this customer's free window and oops
window), the terms tick and the terms version in force, the client it came from, the IP and device — never the name,
the address text or the instructions. A guest booking writes the same row with no user; it is the
case the row exists for, and it is reachable later only by the order, never by a person.

A refused booking is a row too, written outside the transaction that was rolled back: a missing
terms tick is `consent.terms_not_accepted` (judged first, ahead of the price chain), the wrong quoted
total is `order.total_price.not_match`, an express waiver whose quota ran out is its own key, and
cash the rule does not allow is `order.cash_not_available`.
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

## The order is stamped with the contract for work it is booked under {#work-contract-stamp}

`OrderFactory` — the one production writer of an order, reached by `CreateOrder` and by the recurring
materialiser alike — resolves the customer-audience **`WorkContract`** document in force for the
**address's market** on the booking day and stamps it on the order (`Orders.WorkContractDocumentId`),
beside the currency and for the same reason the pay-coverage gate sits there: an order no contract can
form on must not be booked. Nothing in force is an `InvalidOperationException`, not a booking with a
blank; the seed lands the text at every host start (and, on a fresh Development database, once more
in the boot that migrates it), so the case is a deploy with a future-dated-only folder, and the seed
test is the guard. The stamp is set once: a new version of the text applies to orders booked from its
date and changes nothing on an order already booked. The wizard's confirm step names the contract
(*"By confirming the order you conclude a contract for work with the cleaner on these terms"* →
`/work-contract`) as an information line, not a second tick — the customer's half is the terms
consent plus the stamped document. What the cleaner accepts against that stamp, and when, is the take's
story → [Offerability and the take](/flows/offerability-and-take#the-take-carries-the-acceptance),
[ADR-0068](/decisions/adr-0068) D1.

## The order records the language it was booked in {#booking-language}

`CreateOrder` carries the customer's `language` — one of the seeded codes, `en` when a client sends
none — and `OrderFactory` stores it on the order as `Order.LanguageCode`. Every client sends what the
customer is reading. The web sends its UI language. The Android and iOS customer apps send the
language the app is displaying: the one chosen in the app's language setting, else the first of the
device's languages the app supports, else English.

The order's documents read it first. The receipt is written in the order's language, then the
account's preferred language, then whatever its producer passed — so a guest, who has no account to
read, gets a receipt in the language they booked in. A recurring occurrence has no booking request of
its own: its `LanguageCode` is null and its receipt follows the account's preference.
→ [What the receipt says](/flows/payment-and-fiscal#what-the-receipt-says)

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

The server accepts at most **eight rooms and four bathrooms**. The same upper bounds apply to
booking, quote, Plus-savings quote and recurring-template creation or update, with
`order.size_exceeds_maximum` when either is exceeded. Existing lower-bound rules are unchanged.

Web, Android, and iOS offer starts every 15 minutes from 08:00 through 19:45. The two-hour minimum
lead time and the express window still apply to the exact selected instant, including its minutes.
The picker range and grid are not additional API restrictions today; enforcing them on API callers
awaits an owner decision.

## Edge cases

| Case | What happens |
|---|---|
| The quoted price no longer matches | Refused. The client re-quotes. |
| **The quote included an express waiver, and the monthly quota ran out in between** | Refused with its **own** error rather than a generic mismatch — this is the one pricing input that can legitimately change between two runs of a fixed command, and the customer is told exactly that. |
| Under 2 h lead time | Refused outright. Not priced higher — refused. |
| 2–4 h lead time | Accepted with a **+20 %** express surcharge, unless a Plus waiver applies. |
| Booked span over 24 h | Refused. See [why that bound exists](/product/business-rules#maximum-booked-duration-24-h-and-it-is-not-about-calendars). |
| A package **and** a service the package includes | Charged twice, performed twice, takes twice as long. Owner ruling — not a bug, and not to be de-duplicated. |
| Guest, no account | Allowed, by card. The order is keyed on the email address, and the customer later finds it via order lookup. The audit row has no user; an admin reaches it from the order's history. |
| Cash from a guest, or on a booking that needs two cleaners or more | Refused, `order.cash_not_available`, before anything is reserved, debited or dispatched. 120 booked minutes is one cleaner; 121 is two. |
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

**A cash schedule must stay a one-cleaner job** ([the cash rule](/product/business-rules#cash)). A
template always belongs to an account, so only the crew can fail it: `CreateRecurringBooking` and
`UpdateRecurringBooking` refuse cash (`order.cash_not_available`) when the selection, judged on the live
catalogue by the same duration sum the factory staffs occurrences with, needs more than one cleaner.
A cash template that needs more — authored before the rule, or grown by a catalogue change — is
**never switched to card and never charged**:

- **The materialiser skips it.** It creates no occurrence and logs a warning; the template stays active,
  and its materialisation marker is left where it was, so it books again — from the occurrences still
  ahead — as soon as it is eligible.
- **The list says so.** `GetMyRecurringBookings` returns `requiresPaymentMethodChange: true` on it. The
  web, Android and iOS lists badge it *Needs a change* and explain the fix beside an edit action; the web
  list also stops promising a next visit, and Android's Home schedules section badges it too and lists it
  first.
- **The fix is an edit.** An update replaces the selection and the payment type together, so the
  customer moves the schedule to card or to a selection one cleaner can do; the edit clears the
  materialisation marker and the schedule books again.
- **An occurrence whose own crew is more than one cleaner cannot be confirmed as cash.**
  `ConfirmRecurringOrder` judges the occurrence's stored `RequiredEmployees`, not the live template, and
  refuses such a cash occurrence (one materialised before the rule) with `order.cash_not_available` —
  it is neither confirmed as cash nor switched to card. The customer cancels it, free while nobody has
  taken it, and corrects the template; left alone, it is retracted an hour before the slot like any
  unconfirmed occurrence. A one-cleaner cash occurrence created before a catalogue change grew its
  template is still confirmable as cash.

The recurring wizard on the web, Android and iOS applies the same rule while the customer authors or
edits: cash is offered only when the quote for the selection says one cleaner, and a cash choice that
stops being allowed is cleared rather than switched. Every recurring wizard starts a new schedule on
card.

The materialiser calculates a raw subtotal without a cleaning date; `OrderFactory` then applies
the express surcharge once, from that occurrence's date and lead time. Recurring templates carry no
extras, and this path reserves no monthly express waiver. The undated pricing call does not mean
that a short-notice occurrence is exempt from the surcharge.

The `Monthly` frequency currently adds 30 days and then advances to the selected weekday, normally
an interval of 35 days. Calendar-month semantics, including short months, await an owner decision;
"every 30 days" would not describe the current algorithm either.

> The materialiser decides "did I already spawn this occurrence?" with an unlocked read, and **the
> answer is enforced by a unique index** — `IX_Orders_RecurringTemplateId_CleaningDateTime`, on the
> template plus the exact occurrence instant, filtered to spawned orders. The read is the fast path; the
> index is the arbiter, and it speaks at commit.
>
> Until 2026-08-15 there was no index, and what actually prevented a duplicate charge was that Azure
> Functions timer triggers hold a singleton lease — **a guarantee in the hosting model rather than the
> schema**, so moving the sweep to another scheduler or fanning it out would have reintroduced duplicate
> billing silently. The lease still holds; it is no longer the only thing holding.

## Guest order lookup {#guest-order-lookup}

**A guest proves a booking with one per-order access token, and it arrives only by e-mail.** The
token is 256 bits of URL-safe randomness, stored as a SHA-256 digest and never persisted in the
clear; the anonymous endpoints hash what the caller sent and resolve the booking by that digest
alone — across operating companies, so a guest who booked under one operator finds the order without
knowing which operator that was ([ADR-0051](/decisions/adr-0051)'s bypass-and-re-pin cell, with the
hash as the pin). A token that matches nothing, an expired or revoked one, and a booking that belongs
to an account all answer the same `order.not_found`, so the read is never an oracle for which
bookings exist.

| Route (customer web + customer mobile) | Takes | Result |
|---|---|---|
| `POST api/Order/Lookup` | `accessToken` | The booking, in the projection a guest is allowed to see |
| `GET api/Order/Lookup?token=…` | the same token on the query string | The route the e-mail link lands on |
| `POST api/Order/LookupBatch` | up to **10** tokens | The bookings this browser still holds a token for; an unmatched token simply yields no row, so the response never says which token was wrong |

Both reads sit in the `interactive` rate-limit window, and the request logger suppresses
`accessToken` the way it suppresses a password.
→ [Rate-limit policy](/domain/roles/rate-limit-policy)

The guest projection carries no address and no crew: the token opens the **booking**, not the
household. It does carry the `confirmationCode`, now purely as the short human reference printed on
the booking — nothing authenticates on it, and it is served on the guest's own order only.

::: warning It replaced a triple that was never a secret
The old key was **display order number + e-mail + confirmation code**. The display number is
sequential, the e-mail is not private, and the confirmation code was served on the order detail to
**every cleaner assigned to the job** — so a cleaner held the whole key to their own customer's
booking, including the cancellation that charges that customer the 25 % / 50 % tier. Re-keying closed
the class rather than one leak; the code has left every DTO a cleaner can reach.
:::

### Where a token comes from, and how long it lives {#guest-access-token}

**Every message that offers a guest a link mints its own token**, so one booking accumulates several
live rows and none of them supersedes another. Superseding is what a single-channel design needs and
this is not one: a guest who opened *"your cleaner is on the way"* would land on a page that can open
nothing, and the checkout success page could not read back the booking it had just taken payment for.
N live tokens are no weaker than one — each is 256 bits, resolved by its own hash, and scoped to the
single booking it was minted for.

| Minted by | How the guest receives it |
|---|---|
| `CreateOrder` | `guestAccessToken` on the checkout response — guest bookings only, `null` when the booking names an account |
| The receipt e-mail | the *view your booking* button |
| "A cleaner has taken your job" · "we're on our way" · "all done" | the same button on each status e-mail |
| The cancellation e-mail | the same button |

The link is `{clientDomain}/track-order?orderNumber=…&email=…&token=…`; only the `token` opens
anything. A call site that sends a status e-mail **without** minting one ships a button that dead-ends
on the "the link is in your e-mail" panel, which is why `GuestTrackLinkTests` walks the tree for
senders rather than testing each handler behaviourally.

A token dies **30 days after the cleaning** — long enough to cover the refund window and a question
about the receipt afterwards, short enough that a mailbox read years later is not a live key to
somebody's home. **`CancelGuestOrder` revokes every existing live token on the booking at once**, because there is
nothing left to do with them — with one deliberate exception, the cancellation e-mail itself
([below](#guest-cancellation)). An account booking mints none at all: its owner signs in instead.
Erasing an ended guest booking also revokes its live tokens in the same database commit as its
personal data is anonymised. Live guest bookings excluded from erasure keep their tokens. The weekly
retention sweep deletes expired or revoked token rows; it does not extend their lifetime.
→ [GDPR, retention and audit](/flows/gdpr-and-audit#retention)

## Guest cancellation {#guest-cancellation}

A guest previews a cancellation and submits it with **the same access token**, and no account. Both
operations require a guest booking (`UserId` null); an account-owned booking is refused with the same
`order.not_found` answer as an unknown token. The booking’s operator is resolved from that proven
order, so the guest need not choose its market and an unrelated browsing or account market cannot
redirect the cancellation.

The preview shows the standard cancellation tier, fee and policy refund in the order’s currency.
The cancellation recalculates those figures at the time it is submitted, with the same notice,
cleaner-assignment and oops-window rules as the signed-in path, and records `CancelledBy.Customer`.
A guest has no Plus entitlement, so their oops window is the standard 15 minutes, and the preview's
`oopsWindowMinutes` says 15. An order already cancelled, completed or under way cannot be
cancelled again. → [Cancellation rules](/product/business-rules#cancellation)

The two anonymous routes are available on the customer web and customer mobile API hosts, both in
the `auth` rate-limit window. Each request body carries the access token and nothing else that
proves anything:

| Route | Result |
|---|---|
| `POST api/Order/GuestCancellationPreview` | The same fee-preview response shape as the signed-in API, under the standard guest policy |
| `POST api/Order/CancelGuest` | The cancellation response: policy fee/refund, whether a refund succeeded, and its actual amount when available |

A cancellation e-mail goes to the **persisted booking address**, with a refund line only for a
successfully issued refund and its actual amount. A deleted or anonymised destination receives
nothing. The guest gets no account, feed or push; assigned cleaners still receive their notice. The
act is recorded as `customer.order.cancel` with no customer user id, even when a session accompanies
the guest's token. → [The customer trail](/flows/gdpr-and-audit#customer-trail)

**`CancelGuestOrder` revokes every existing live key, and the cancellation e-mail then carries a new one.** Those are
the same decision rather than opposite ones: every token the guest already held is retired at the
cancel, and the last message the booking will ever send carries the only one that still opens it, so
the customer can read what they were refunded. It is minted and committed *before* the send, so a
crash after it cannot leave an e-mailed token with no row behind it, and it expires on the same
schedule as any other — 30 days past the cleaning.

**Shipped on every client.** The web track page, the Android customer app and the iOS customer app
all draw the preview (tier, fee, refund estimate) before asking for confirmation, and all three key
it on the access token. → [Order tracking](/customer-app/order-tracking)
