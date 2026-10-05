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

  C->>API: POST /Order/Quote (selection, size, dirtiness level)
  API->>P: price the selection
  P-->>C: total, dirtiness and express surcharges, waiver state, duration and crew

  C->>API: POST /Order/CreateOrder (incl. the level and the quoted total)
  API->>V: validate
  V->>P: RE-price, server-side
  V-->>API: refuse if the totals disagree
  V-->>API: refuse cash unless signed in AND one cleaner is required
  API->>F: create
  F-->>API: New + PaymentStatus.Pending
  alt Card
    API->>API: take the customer's credit (same currency, at most 70 %)
    API->>S: create checkout session for order.AmountDueOnCard
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
credit and membership usage stay with the account; card payments use the one Stripe account the
platform configures, the operating company's own (decision 49). Owner-pinned reads keep the booking and its receipt visible in the customer’s order history
regardless of the browsing market. → [ADR-0061 D6](/decisions/adr-0061#d6-tenant-country-and-currency-agree-by-construction-and-two-validators-refuse-the-cases-that-could-break-it)

## The price is never taken from the client

`CreateOrder.Command` carries a `TotalPrice`, and it is **a confirmation, not an input**. The validator
re-prices the whole selection server-side and refuses on disagreement. The amount that reaches Stripe
is `ToMinorUnits(order.AmountDueOnCard)` — the total less the credit `CreateOrder` took for it — read
from the persisted, server-computed values. The client cannot influence it at any point, which is why
the payment webhook does not need to reconcile the amount. The quote carries the customer's credit
balance and the share (`creditBalance`, `creditMaxShareOfOrder`) so the wizard can say *To pay by card*
before Stripe does. Every client shows that split only for a card payment, because `CreateOrder` takes
credit only from a card booking (`TakeCreditForOrderAsync`); with cash and a balance held it says credit
applies to card payments only. → [Business rules — customer credit](/product/business-rules#credit)

## The customer says how dirty the home is {#dirtiness-level}

Owner rulings 2026-09-28. Right after the services, every booking flow — the customer web wizard, and
Android and iOS — asks **how clean the home is**, as a required step of its own: *Normal*,
*Increased +15 %* or *Heavy +30 %*, each with the owner's description of the home it fits (when it was
last cleaned, and what the cleaner will find on the floor, in the bathroom and in the kitchen), and the
hint to pick the higher level when unsure, so the cleaner has enough time. Nothing is preselected, and
on the web the step comes before the address. The web's home calculator offers the same three levels
and states the surcharge inside its price. Copy is in five locales; the rates in it are pinned to
`BookingPolicy` by the parity checker.

The level rides every request that describes the job, and the server answers from it:

| At the chosen level the server… | |
|---|---|
| prices | the whole basket plus the level's rate, before the discounts and under express → [the price](/product/business-rules#dirtiness-price) |
| times | the service minutes, plus minutes per room, × (1 + rate), rounded up |
| crews | one cleaner per started 120 minutes of that — so `requiredEmployees` can grow, and cash can fall away |
| pays | each seat its share of the job's pay, raised by the rate after the clamp → [cleaner pay](/product/business-rules#cleaner-pay) |

The quote and the Plus preview carry it and the quote echoes it with the surcharge
(`dirtinessLevel`, `dirtinessSurchargeAmount`), so the clients re-read cash from the crew the new quote
gives: picking *Increased* on a 120-minute job makes it a two-cleaner job, and a cash choice is taken
away with its reason. The web's booking sends the level its resubmitted total was quoted at, so the
price match compares like with like; the summary rail itemises the stored surcharge under the level and
the review restates it. The web's Plus preferred-cleaner roster is asked about the job being composed —
rooms, bathrooms and the picked level (Normal until one is picked) — so a cleaner free only for a
Normal-length job is not offered and then refused by the hold. After booking, the customer's order
detail on every client names the level and itemises its surcharge.

A request with no level books at **Normal** — the server's default for old clients and API callers;
no shipped client books without one. An unknown value is `common.invalid_enum_value`. The level is stored on
the order and fixed there: the on-site top-up that would change it is approved in principle and not
built. → [Business rules — the dirtiness level](/product/business-rules#dirtiness),
[ADR-0069](/decisions/adr-0069)

## Cash is for a signed-in customer's one-cleaner job {#cash-eligibility}

A booking may be paid in cash only when the caller is signed in **and** the server's crew for the
selection is exactly one (`BookingPolicy.AllowsCash`, owner ruling 2026-09-24); a guest, or a booking
whose duration needs two cleaners or more, pays by card. The crew is not something the command carries:
the validator reads it off the same calculator run that re-priced the order
(`OrderDuration.RequiredEmployees`, one cleaner per started 120 minutes of a duration that already
includes the [dirtiness level](#dirtiness-level) and the home's size), so a forged request cannot make
a two-cleaner job cash-eligible. The rule sits in the price chain after the price match and ahead
of the promo rules, and a refusal is `order.cash_not_available` before any side effect — no express
waiver reserved, no credit debited, no referral accepted, nothing dispatched. `OrderFactory` refuses
the same combination as a backstop for callers that never run the validator.

**What the clients do.** The quote already returns `requiredEmployees` for the selection on screen. The
customer web wizard and the Android and iOS booking flows combine it with the live sign-in state: cash is
offered only when both allow it; otherwise it is disabled with the reason — cash needs a signed-in
customer, this booking needs N cleaners, or (while no quote describes the current selection) it is
confirmed once the price is ready. The mobile booking flows run inside a signed-in session, so only the
web ever shows the first reason. A cash choice that stops being allowed — a sign-out, a bigger selection,
a higher dirtiness level — is **taken away, not switched to card**: the payment choice is cleared and the
customer is told to choose again. Both mobile apps re-check cash against the quote the booking is
submitted with and clear it there rather than send it. If the server still refuses, the web and iOS clear
the choice (the web wizard returns to the payment step); Android shows the refusal and leaves the choice
for the customer to change. → [Business rules — paying in cash](/product/business-rules#cash)

## The booking leaves a row, and so does a refused one

`CreateOrder` is marked `customer.order.create` ([ADR-0062](/decisions/adr-0062)), so the same commit
that creates the order writes a `CustomerActionAudit` row carrying what the server priced and showed: the
price breakdown, the dirtiness level and its surcharge, the discounts and the express state, the line
items by id, the cleaning time and lead time, the cancellation policy figures as shown (with this
customer's free window and oops window), the terms tick and the terms version in force, the client it
came from, the IP and device — never the name, the address text or the instructions. A guest booking
writes the same row with no user; it is the case the row exists for, and it is reachable later only by
the order, never by a person.

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

## The terms tick is required, unless the account already accepted the texts in force

A booking asserts `termsAccepted: true` or is refused — **unless** the signed-in customer's terms and
privacy consents both **cover the texts in force** for the booking's market: granted, not withdrawn,
and pointing at the very document in force (owner rulings 2026-09-14 and 2026-09-28). That customer
sees no box and sends nothing; a guest has no account to hold a consent on and always asserts it; a
customer whose consent was withdrawn, or who accepted an older version, is asked again — so a new terms
version is accepted before the next booking, and bookings already made keep theirs. The tick
short-circuits the consent read, so a customer re-consenting at checkout is never refused for a row the
server has not written yet. The web wizard, the Android and the iOS customer apps show the sentence on
the review step when the consents on record (`coversCurrentVersion` on the consent read) do not cover
the texts in force, or there is no account, gate the confirmation on it, and send the tick only when the
box was shown and ticked. With the tick, a signed-in customer's two consent rows move to the texts in
force, with the IP and device, and the booking row records the versions **actually accepted**
([ADR-0063](/decisions/adr-0063)); without it, the versions the customer's rows hold. **Creating a
recurring schedule is gated the same way** (`CreateRecurringBooking`). The web, Android and iOS
schedule forms ask for the tick on a new schedule by the same rule, never on an edit. The web and iOS
forms have done so since 2026-10-04; until then a customer behind on the terms was refused there with
no box to tick. On the web and iOS a refusal with `consent.terms_not_accepted` shows the box, unticked,
instead of a generic error. **So is confirming a recurring occurrence**
(`ConfirmRecurringOrder`, owner ruling 2026-10-03). It uses the same method as `CreateOrder`
(`CustomerLegalConsents.AssertedOrCoverTextsInForceAsync`), applied to the market of the occurrence's
address, and is refused with the same key. Until then it was not gated, on the reasoning that the
template had been accepted. But an occurrence is created up to a week ahead, under whatever version
is in force on that day → [Recurring bookings](#recurring-bookings).
→ [Business rules — what is recorded about a customer](/product/business-rules#customer-record)

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

**So do the order's status e-mails, since 2026-10-01.** *A cleaner has taken your job* (`TakeOrder`),
*started* (`StartOrder`) and *all done* (`CompleteOrder`) used to read the account's preferred
language alone, so a customer whose stored preference was stale — a Google or Apple sign-up is stamped
`en` — got a Czech confirmation followed by English status e-mails. They now resolve the order's
language, then the account's preference, then English, the chain the booking confirmation already
used: **one order, one language.** A booking language with no e-mail copy falls back to English, as
the confirmation does. A customer who changes the app's language after booking keeps getting that
order's e-mails in the language it was booked in.

## Responsive quote previews

The home calculator requests its quote immediately. Booking groups rapid selection changes into a
200 ms pause before requesting the latest price. A changed selection cancels the previous pending
request immediately; clearing the selection clears the quote. An identical quote already in flight
is shared with checkout rather than requested again. The previous price stays visible while an
updated quote loads, and checkout uses a quote matching the current selection.

`OrderPricingCalculator` returns the estimated duration with its pricing snapshot, using the same
selected services and package contents, the same room and bathroom count and the same dirtiness
level. `QuoteOrder` reuses that duration instead of loading the catalogue a second time. Pricing,
discounts and create-time validation retain their existing rules; nothing converts between currencies.

## Room selection and start times

While selecting services, room and bathroom counts are editable above the sticky desktop summary.
On smaller screens those controls appear before the packages and services, so they are visible
without scrolling through the catalogue. Both layouts edit the same selection.

The server accepts at most **eight rooms and four bathrooms**. The same upper bounds apply to
booking, quote, Plus-savings quote and recurring-template creation or update, with
`order.size_exceeds_maximum` when either is exceeded. A booking with a negative count is refused
with `validation.must_be_positive`, the quote's key.

**The size lengthens the job as well as pricing it.** Every room and bathroom adds each service's
minutes per room to the booked time, the same count the per-room price multiplies — and so, through
the crew, it can decide cash. Those minutes are **0 on every service** until the real durations are
typed into the admin catalogue, so for now the size moves the price and not the time.
→ [Business rules — crew size](/product/business-rules#crew-size)

**No client offers a size the server refuses, and the mobile apps say where it stops.** Every picker
stops at 8 rooms and 4 bathrooms. On Android and iOS both stepper buttons stop at the bounds: the plus
greys at the cap, and the minus greys at the floor, 1 on a one-off booking and 0 on a schedule. Both
flows state the limit, *Up to 8 rooms and 4 bathrooms* (`booking_size_limit_caption`, rendered from
the two constants rather than written into the translation). The steppers used to stop at the cap
without saying why, and the one-off minus looked live at 1 (owner ruling 2026-10-01):

| Picker | Rooms | Bathrooms |
|---|---|---|
| One-off booking — web, Android, iOS | 1 – 8 | 1 – 4 |
| Recurring schedule — web | 1 – 8 | 1 – 4 |
| Recurring schedule — Android, iOS | 0 – 8 | 0 – 4 |

**The caption sits on the *Your home* title's row, at its trailing end** (owner remark 2026-10-04,
since 2026-10-05). It is on the title's first baseline, in the same small secondary style it had, and
the steppers follow directly under the row, so the card is a line shorter. Where the title and the
caption do not fit on one line, as on a 320pt (320dp) phone in Ukrainian or Russian or at a large
text size, the caption takes its own line directly under the title, leading-aligned, and never goes
back below the steppers. VoiceOver and TalkBack read the title, then the caption. Both apps lay it out
in a `SizeLimitTitleRow`, the same in both flows: on iOS a `ViewThatFits` that tries the one-line row
first, on Android a `Layout` that keeps the caption beside the title while the two fit with at least
12dp between them (`sizeCaptionFitsBesideTitle`). The schedule form's size section had no title, only
its *Rooms* and *Bathrooms* labels, so it now opens with a *Your home* title row carrying the caption
(the booking's `booking_your_home` string), with the two counters under it. Until then the caption was
a line of its own under the steppers in both flows. `PropertySizeTests` (iOS) and `PropertySizeTest`
(Android) pin that each flow states the caps once, on its title's row, above the steppers, and the
fit rule.

**On the one-off booking's size row, the *Your home* title sits above the two steppers** on Android
and iOS (since 2026-10-02). The steppers used to sit beside the title, which left no room once
Ukrainian counted bathrooms as *ванна кімната* (owner ruling 2026-10-02). On a 360dp phone the two
Ukrainian counters alone needed 306dp of the row's 292dp, and on a 320pt iPhone they ran into each
other. Each counter's label now takes the width its pill leaves after both buttons, and a longer label
wraps onto a second line between words. **Since 2026-10-03 the two steppers split the row equally**
(owner remark 2026-10-03). Each takes half of the width from the title's leading edge to the card's
trailing edge, less an 8pt (8dp) gap, the same gap as on Home's quick-size card. Both keep one height
when a label wraps, and each label is centred between its minus and plus. They used to be as wide as
their labels, so *3 rooms* and *1 bath* sat at different widths and left a ragged gap at the
card's edge, out of line with the title and the caption. The schedule's size section keeps steppers as
wide as their labels, on both platforms. Home's *How big is your home?* card gives its labels up to two
lines too, and then shrinks them to 80% if a word is still too wide. On Android the cleaner's job-board
scope chips wrap onto a second line (`FlowRow`), as iOS's `ChipFlow` already did.

On Android and iOS, a rebooking, or a schedule started from a past order, that was larger starts at the
cap. The web's two prefills (`prefillFromRebook`, `prefillFromOrder`) pass the stored size through
unclamped: a larger past order leaves no size chip selected, and submitting it is refused with
`order.size_exceeds_maximum`. Editing an existing schedule does not clamp what it stored; the server
already refuses anything larger. Android
(`PropertySize.kt`) and iOS (`PropertySize.swift`) each hold the two caps once, and a test on each
reads them against `BookingPolicy.MaxRooms` and `MaxBathrooms` in `BookingPolicy.cs`. The
booking-policy parity gate (`check-booking-policy-parity.mjs`) holds every client to the same two
numbers. It reads both constants, the last chip of the web's two size pickers, the refusal copy
(`order.size_exceeds_maximum`) in the web, Android and iOS catalogs in all five locales, which must
state exactly 8 and 4, and the caption, which must carry its two placeholders and no figure. The
caption's noun forms in Czech, Slovak, Ukrainian and Russian agree with 8 and 4, so the gate also fails
when the policy moves and asks for them to be re-read.

**Starts are 08:00 – 19:45 in 15-minute steps, in the market's time zone, at most 60 days ahead — and
the server enforces it** (owner ruling 2026-09-28). `CreateOrder` and `QuoteOrder` refuse a start off the
grid, outside the window or past the horizon as `order.cleaning_date.outside_booking_window`, reading the
wall clock in the service address's market zone, never the device's; the future and lead-time rules keep
their own keys and run first. Web, Android and iOS offer exactly those starts: the web's one-off
calendar, its next-month arrow and the home calculator's date picker stop at the 60-day horizon, and a
start the server refuses holds the wizard on the time step with its reason. The two-hour minimum lead
time and the express window still apply to the exact selected instant, including its minutes.
→ [Business rules — the start-time window](/product/business-rules#start-time-window)

## Edge cases

| Case | What happens |
|---|---|
| The quoted price no longer matches | Refused. The client re-quotes. |
| **The quote included an express waiver, and the monthly quota ran out in between** | Refused with its **own** error rather than a generic mismatch — this is the one pricing input that can legitimately change between two runs of a fixed command, and the customer is told exactly that. |
| Under 2 h lead time | Refused outright. Not priced higher — refused. |
| A start off the 15-minute grid, before 08:00 or after 19:45 in the market's zone, or more than 60 days ahead | Refused on the quote and the booking alike, `order.cleaning_date.outside_booking_window`. |
| 2–4 h lead time | Accepted with a **+20 %** express surcharge, unless a Plus waiver applies. |
| A *Heavy* home on an express slot | Both surcharges: +60 % on the basket, then +20 % on that — × 1.92 before discounts. |
| A request that carries no dirtiness level | Priced, timed and booked at *Normal*. No shipped client books without one. |
| An unknown dirtiness level | Refused, `common.invalid_enum_value`, on the quote, the Plus preview, the booking and both recurring commands. |
| A 120-minute selection at *Increased* or *Heavy* | 156 or 192 booked minutes: two cleaners, so card only. |
| Booked span over 24 h | Refused, judged on the booked time with the level and the size in it. See [why that bound exists](/product/business-rules#maximum-booked-duration-24-h-and-it-is-not-about-calendars). |
| A package **and** a service the package includes | Charged twice, performed twice, takes twice as long. Owner ruling — not a bug, and not to be de-duplicated. Every client marks such a service *In your package* and asks before either half is added by hand; two chosen packages that share a service charge it twice the same way, and adding the second asks too; a selection the form is handed is only marked → [Charging a package and a service together](/product/business-rules#charging-a-package-and-a-service-together). |
| A deactivated service or package selected by id, from an out-of-date app or an *Order again* sent before the catalogue loaded | Refused on the quote, the Plus preview, the booking and a new schedule, `order.selected_services.invalid` / `order.selected_package.invalid`, since 2026-10-05. A schedule created before the deactivation keeps booking it, and its occurrences are confirmed and paid from their stored price; its web card shows no price and no message, and editing it removes the entry with a notice. A deactivated service inside a package books with the package → [A deactivated service or package](/product/business-rules#deactivated-catalogue). |
| Guest, no account | Allowed **on the web**, by card. The order is keyed on the email address, and the customer later finds it via order lookup. The audit row has no user; an admin reaches it from the order's history. The customer mobile host's create route requires a session, so an anonymous call there is `401` and creates nothing → [CreateOrder](/api/orders#createorder). |
| Cash from a guest, or on a booking that needs two cleaners or more | Refused, `order.cash_not_available`, before anything is reserved, debited or dispatched. 120 booked minutes is one cleaner; 121 is two. |
| An unknown language code on the booking | Refused (`CreateOrder.Validator` carries the `LanguageValidator`, the `Register` idiom) — the audit row records `language`, and an unrecognised code is not evidence of anything. No shipped client sends one outside the five seeded codes. |
| A recurring occurrence confirmed | One `customer.order.recurring.confirm` row: the order, the template, the price and currency, the dirtiness level, the payment type, the cleaning time and lead time, and — since 2026-10-03 — the terms tick and the terms and privacy versions it was confirmed under. A confirm refused for the terms writes the failure row with `consent.terms_not_accepted`, as a booking does. A schedule created, edited, paused/resumed or deleted writes a `customer.recurring.*` row with the schedule facts — its level among them — before and after. |

## Recurring bookings

A template materialises occurrences up to 7 days ahead, and never one closer than the 2 h floor a
one-off booking gets — so the materialiser no longer creates same-day occurrences that are already past
or express-charged. A template whose time is outside the start-time window is skipped with a warning
and kept as authored; `CreateRecurringBooking` and `UpdateRecurringBooking` refuse such a time
(`order.cleaning_date.outside_booking_window`) and a `StartsOn` more than 60 days ahead. A materialised
occurrence stays unconfirmed until the customer confirms it, so *"pending for over an hour"* is its
**normal** state, not an abandoned checkout — which is why the stale-checkout sweep explicitly excludes
rows with a `RecurringTemplateId`. A separate sweep retracts unconfirmed occurrences an hour before the
slot.

**Confirming an occurrence** (`ConfirmRecurringOrder`, owner ruling 2026-09-28) stamps
`Order.CustomerConfirmedAt` for both tenders — the first stamp wins — and the card path then asks for
the money:

| Tender | What the confirm does | When it is offerable to cleaners |
|---|---|---|
| Cash | nothing more: the occurrence stays `Pending` until the cleaner records the cash, gets the informational booking e-mail, no receipt and no *payment confirmed* push; a second confirm is `order.recurring_already_confirmed` | at the confirm |
| Card, mobile | a PaymentIntent for the PaymentSheet (`clientSecret`) | when the webhook writes `Paid` |
| Card, web | a Stripe Checkout Session (`checkoutUrl`), built like a resumed checkout; the customer web's order detail confirms and redirects | when `checkout.session.completed` writes `Paid` |

A card occurrence begun on the other channel is refused (`InvalidOrderStatusTransition`), so no order
ever has two capturable payment surfaces; a card occurrence can be confirmed again until its payment
settles, and a Checkout Session that expires leaves it confirmable. The confirm refuses a cancelled
occurrence, a paid one, and one closer than 2 h (`order.cleaning_date.below_lead_time`). The
stale-occurrence sweep and the confirm reminders select only occurrences still awaiting the customer
(`Order.AwaitsCustomerConfirmation`: open, `Pending`, and for cash not yet confirmed); the order detail
carries it as `needsConfirmation`, and every client offers the confirm on it. Until the web confirm
existed, every occurrence of a web-only customer was auto-cancelled an hour before its slot.

**The confirm asks for the terms in force, on a booking's rule** (owner ruling 2026-10-03, *ask at
confirm*). An occurrence is created up to 7 days ahead under the version in force that day, and the
customer is asked to accept the version in force at the confirm.

- **The command** carries an optional `termsAccepted`, like `CreateOrder`. Without the tick, a
  customer whose terms and privacy consents do not both cover the texts in force for the occurrence's
  market is refused `consent.terms_not_accepted`
  ([the terms tick](#the-terms-tick-is-required-unless-the-account-already-accepted-the-texts-in-force)).
  The validator reads the order the same way the handler does. An order the handler refuses anyway (not
  the caller's, not an occurrence, no longer awaiting confirmation) passes, so the handler's own answer
  is the one returned.
- **With the tick, both consent rows move to the texts in force** (`CustomerLegalConsents.RecordAsync`,
  the booking's call). This happens after every refusal and under the account's own company, before
  either tender switches to the order's company. The confirmation row records the tick and the versions
  confirmed under.
- **The clients show the tick only when it is needed.** Each reads the customer's consents once, and
  only for an order that `needsConfirmation`. It asks unless both consents are granted, not withdrawn
  and cover the version in force, so a failed read asks too. The box sits above the confirm, uses the
  booking's own copy, and sends `termsAccepted: true` only when it was shown and ticked; otherwise the
  member is not sent. The web order detail (card and cash alike) disables the confirm and shows the
  booking's reason until the box is ticked. Android disables the button the same way. iOS keeps the
  box hidden and the button disabled until the consents are read, so the box never flashes over an
  account that holds them.
- **A refusal over the terms shows the box at once** on Android and iOS (since 2026-10-04). The
  consent read is judged for the default market and the confirm for the occurrence's own, so an account
  the read called covered can still be refused `consent.terms_not_accepted`. Until then the box stayed
  hidden until the screen was reopened. The box now appears at once, unticked, and stays for the
  screen's life, and the next confirm sends the tick. The apps do not read the consents again, because
  that read would still say covered and hide the box. On iOS, a refresh triggered by a push reads the
  consents as opening the screen does, so an occurrence that first arrives by a push no longer leaves
  the confirm disabled with no box.
- **An occurrence is not paid before it is confirmed** (since 2026-10-04). `CreatePaymentIntent`
  refuses an occurrence with no `CustomerConfirmedAt` as `order.invalid_status_transition`, so a
  hand-made call for a PaymentSheet intent cannot skip the confirm's terms check. A confirmed
  occurrence, including a retry after a failed payment, pays as before. The apps confirm first and
  then take the sheet's intent. The web confirm opens its own Checkout Session, and
  `ResumeOrderCheckout` already refused every occurrence.
- **An occurrence nobody confirms** is still retracted an hour before its slot, with no fee.

**A schedule's weekday and time are the market's wall-clock time** (since 2026-09-28; they used to be
read as UTC, so a Prague 10:00 schedule ran at 11:00 in winter and 12:00 in summer). The materialiser
loads the saved address first and takes its zone from the address country's
`CountryConfiguration.TimeZoneId`, else the default market's, else UTC. It walks the calendar in that
zone — the weekday, the step and the resume marker are all local dates — and converts each local date
plus `TimeOfDay` to UTC on its own, so 10:00 stays 10:00 across the clock change:

| Case | What the occurrence gets |
|---|---|
| A time the spring change skips (02:30 on that Sunday in Prague) | moved forward by the gap — 03:30 |
| A time the autumn change repeats | the later, standard-time instant |

The duplicate guard is unchanged and still compares exact UTC instants. A template whose saved address
is missing now stops before it can move its resume marker.
The template's DTO carries the market zone the materialiser walks (`RecurringBookingTemplateDto.timeZoneId`,
filled by create, update and the schedule list), and the web schedule card reads its *next visit* in that
zone, so the date lands on the weekday beside it from any reader's zone.

**An edit cannot move the start past the end.** The server refuses a start on or after the stored end
date (`recurring_booking.ends_on_before_start`), and no edit form has an end-date editor, so on the
web, Android and iOS the start-date picker of an edited schedule stops at the day before its end date.
A schedule with no end date, and every new one, is open after today. On iOS the range collapses to
its first day rather than crossing when the end date is less than a day after the earliest start.

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
catalogue by the same duration sum the factory staffs occurrences with — at the template's size and
dirtiness level — needs more than one cleaner. A cash template that needs more — authored before the
rule, or grown by a catalogue change such as longer service minutes or minutes per room — is
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

**A schedule has its own dirtiness level** (owner ruling 2026-09-28) — by the descriptions the customer
reads, a home cleaned once a month is *Increased*. `CreateRecurringBooking` and `UpdateRecurringBooking`
carry `dirtinessLevel`: Normal when a client sends none, editable, refused as an unknown value
(`common.invalid_enum_value`); the template DTO returns it. Every occurrence is priced, timed, crewed and
paid at it — the materialiser hands it to `OrderFactory`, which stores it on the occurrence — and the
cash check above, the list's `requiresPaymentMethodChange` flag and the schedule's audit facts read it.
The web, Android and iOS schedule forms ask for it on a new schedule with the booking wizard's three
levels and copy (on the web none is chosen and a save without one names it as missing) and keep the
schedule's own level on an edit. The web form's quote prices at the chosen level (Normal until one is
picked), so the price and the cash crew follow it, and each schedule card is quoted at its own level. →
[Business rules — a schedule carries its level](/product/business-rules#dirtiness-recurring)

The materialiser calculates a raw subtotal — at the template's dirtiness level — without a cleaning date;
`OrderFactory` then applies the express surcharge once, from that occurrence's date and lead time.
Recurring templates carry no extras, and this path reserves no monthly express waiver. The undated
pricing call does not mean that a short-notice occurrence is exempt from the surcharge.

**`Monthly` is the nth weekday** (owner ruling 2026-09-28): the ordinal is read off the first chosen
weekday on or after `StartsOn`, in the market's calendar — the 2nd Thursday stays the 2nd Thursday — and a
schedule that began on a 5th weekday takes the **last** one in months that have no 5th. Twelve visits a
year; it used to add 30 days and move to the weekday, about ten. **Every cadence is counted from
`StartsOn`**, and `LastMaterializedFor` is only a resume pointer, so an edit — which clears it — keeps a
monthly schedule on its weekday and a fortnightly one on its own weeks. The web's *next visit* walks the
same rule and skips a visit under two hours away. → [Business rules — Cleansia Plus](/product/business-rules#cleansia-plus)

**The favourite cleaner can be chosen when the schedule is created**, not only kept on an edit. The web,
Android and iOS schedule forms offer the cleaners who have served the customer (`GetMyServingCleaners`,
asked with no slot, so it costs no availability query) and send the choice as `preferredEmployeeId`;
it can be changed or cleared on an edit, and on the web a refusal on create offers saving without them.

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

**The lookup is web-only; the mobile host still serves it** (owner ruling 2026-10-01). The guest's
link opens the web `/track-order`, and neither the Android nor the iOS customer app has a guest screen
any more: *Find a guest booking*, where the guest pasted the link into the app, is gone, and no guest
path is on either app's anonymous allow-list. That reverses the 2026-09-28 meeting default that the
apps keep the lookup because it creates nothing. The customer mobile host keeps all six guest routes
for now — `Lookup` (POST and GET), `LookupBatch`, and the three in
[Guest cancellation](#guest-cancellation) — because an app build installed before the change still
calls `Lookup`, `GuestCancellationPreview` and `CancelGuest`. They answer as they do on the web host
until a follow-up removes them (T-0800, once no supported build calls them). → [The anonymous allow-list](/mobile-app/api-integration#the-anonymous-allow-list)

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
somebody's home. **Every cancellation the guest or the platform makes revokes every existing live
token on the booking at once** — the guest's own, an administrator's, the company wind-down, the
unfilled-slot sweep and the stale-checkout sweep — because there is nothing left to do with them, with
one deliberate exception: the cancellation e-mail itself ([below](#guest-cancellation)). That includes the webhook's cancellation
when Stripe reports the checkout expired or the payment cancelled (`checkout.session.expired`,
`payment_intent.canceled`), which since 2026-09-28 cancels through the same writer and sends the same
e-mail. It is rare — no expiry is set on the Checkout Session, so Stripe's own comes long after the
stale-checkout sweep's hour. An account booking mints none at all: its owner signs in instead.
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

The preview shows the standard cancellation tier, fee and policy refund in the order’s currency —
**0** refund on a booking that took no payment. The cancellation recalculates those figures at the time
it is submitted, with the same notice, cleaner-assignment and oops-window rules as the signed-in path
and the fee figures frozen on the order when it was booked,
and records `CancelledBy.Customer`. A guest has no Plus entitlement, so their oops window is 60 minutes
on their **first booking** — the first on that e-mail or phone, guest or account — and the standard 15
otherwise; the preview's `oopsWindowMinutes` says which. An order already cancelled, completed or under
way cannot be cancelled; nor can one whose booked start has passed with a cleaner on the job
(`order.start_passed_cannot_cancel`), which the guest reports as a no-show instead.
→ [Cancellation rules](/product/business-rules#cancellation)

The three anonymous routes are available on the customer web and customer mobile API hosts, all in
the `auth` rate-limit window. Each request body carries the access token and nothing else that
proves anything:

| Route | Result |
|---|---|
| `POST api/Order/GuestCancellationPreview` | The same fee-preview response shape as the signed-in API, under the standard guest policy |
| `POST api/Order/CancelGuest` | The cancellation response: policy fee/refund, whether a refund succeeded, and its actual amount when available |
| `POST api/Order/ReportGuestNoShow` | After the booked start, when the assigned cleaner has not started: tells the company's administrators once (`admin.order.cleaner_not_started`) and moves no money — an administrator confirms the no-show. Refused before the start (`order.start_time_not_reached`) and once the job is under way (`order.cleaner_already_started`); on a booking nobody took it raises nothing, because the unfilled sweep cancels and refunds that one → [When the cleaner no-shows](/product/business-rules#when-the-cleaner-cancels-or-no-shows) |

A cancellation e-mail goes to the **persisted booking address**, with a refund line only for a
successfully issued refund and its actual amount. A deleted or anonymised destination receives
nothing. The guest gets no account, feed or push; assigned cleaners still receive their notice. The
act is recorded as `customer.order.cancel` with no customer user id, even when a session accompanies
the guest's token. → [The customer trail](/flows/gdpr-and-audit#customer-trail)

**The cancel revokes every existing live key, and the cancellation e-mail then carries a new one.** Those are
the same decision rather than opposite ones: every token the guest already held is retired at the
cancel, and the last message the booking will ever send carries the only one that still opens it, so
the customer can read what they were refunded. It is minted and committed *before* the send, so a
crash after it cannot leave an e-mailed token with no row behind it, and it expires on the same
schedule as any other — 30 days past the cleaning.

**A guest is told when the platform cancels, too.** An administrator's cancel, the company wind-down,
the unfilled-slot sweep (`CancelUnfilledOrders`) and the stale-checkout sweep
(`CleanupStalePendingOrders`) stage the same e-mail as the guest's own cancel, through one helper
(`GuestCancellationEmail`), in the booking's language, with the same revoke-then-mint rule. It adds
two lines the guest's own cancel does not need:

| Line | What it says |
|---|---|
| Why | Keyed on the reason **code**, never on an administrator's free text: *no cleaner was available* (`order.cancelled.no_cleaner_available`), *the payment wasn't completed* (`order.cancelled.payment_not_completed`), otherwise *we had to cancel this booking* |
| Money | *Refund issued: {amount}* only with the amount a refund actually returned; *Nothing was charged* when the card was never charged (payment `Pending` or `Failed`); *Your refund is being processed* when the booking is still paid, so its refund was attempted and did not come back; otherwise nothing — a requested figure is never printed |

The texts are in the e-mail's own defaults, in all five languages.

**Shipped on every client.** The web track page, the Android customer app and the iOS customer app
all draw the preview (tier, fee, refund estimate) before asking for confirmation, and all three key
it on the access token. → [Order tracking](/customer-app/order-tracking)
