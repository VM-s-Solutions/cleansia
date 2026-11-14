# Booking and pricing: end-to-end cases

How a customer or a guest gets a price and a booking, from `docs/flows/booking-and-pricing.md`. Ids `E2E-BOOK-NNN`; format and DEV facts in `e2e/README.md`.

Reading notes for every case below:

- "The quote" is `POST api/Order/Quote` (anonymous on both the customer host and the customer mobile host). "Book" is `POST api/Order/CreateOrder`: anonymous on the customer web host, session required on the customer mobile host. Money is read from the quote's own lines, never typed in, because the DEV catalogue prices are data (read them from the quote). `S` means the quote's lines subtotal in the order currency before any surcharge or discount.
- "Day-ahead slot" means tomorrow at 10:00 in the market's zone (Prague for CZ). It is always more than 4 h away and inside 08:00 to 19:45, so it carries no express surcharge.
- Every refusal is read from the response's error key. A refused booking must create no order, charge nothing, reserve no express waiver and take no credit; the case says so where it can be seen.
- The Customer role signs in as the run's customer; the Guest role has no session. Web means the customer web app (`cleansia.app`), iOS and Android mean the customer apps.

## Market, catalogue and quote

### E2E-BOOK-001 — The quote prices a plain selection on the server
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-path : the quote returns the total, the surcharges, the duration and the crew for a selection.
- **Given** a signed-in customer and the catalogue of the default market (CZ, CZK). **When** the customer quotes one service, 1 room, 1 bathroom, Normal level, a day-ahead slot, no promo. **Then** 200 with `totalPrice` equal to the service's base plus its per-room price times rooms plus bathrooms; `dirtinessLevel` Normal and `dirtinessSurchargeAmount` 0; no express surcharge; `estimatedDurationMinutes` and `requiredEmployees` (at least 1); `creditBalance` and `creditMaxShareOfOrder` present. The web summary rail, the iOS and the Android confirm step show the same total as the API.
- **DEV:** creates nothing. Run on the mobile host too (same route, same figures).

### E2E-BOOK-002 — A guest can quote without an account
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#edge-cases : a guest gets prices and books by card, no account required.
- **Given** no session. **When** the guest calls the quote for the same selection as E2E-BOOK-001 on the customer host and on the mobile host, and uses the home page quick quote on the web. **Then** both hosts answer 200 with the same `totalPrice` as the signed-in quote (a guest has no tier or Plus discount, so the figure can only be equal or higher than a member's); the quick quote shows the market chip with currency CZK.
- **DEV:** creates nothing.

### E2E-BOOK-003 — The market's countryId decides the currency before there is an address
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#the-market-comes-before-the-address : the market's `countryId` rides the overviews and the quick quote.
- **Given** `Market/GetOverview` (anonymous) listing the markets and flagging the default. **When** the guest calls the service overview and the quote with the default market's `countryId`, then with no `countryId`, then with a `countryId` that is not a listed market. **Then** the first two answer in the default market's currency with identical prices; the third is refused `country.not_serviced`. On the web the default market is preselected and, with one market listed, the chip is a static label, not a control.
- **DEV:** creates nothing. Unsure which markets DEV lists: if `GetOverview` lists a second market, add the same call with its `countryId` and expect its currency.

### E2E-BOOK-004 — The service address wins over the chosen market
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web
- **Rule:** → /product/business-rules#market : from the address step on, the address's country decides the currency.
- **Given** the web with a chosen market and a second serviced market listed. **When** the customer picks an address in the other country at the address step. **Then** the wizard re-reads the catalogue for that country, drops any selection that market does not sell with the notice `catalogue_changed_for_country`, and re-quotes in the address country's currency; a market chosen afterwards leaves the booking alone.
- **DEV:** runs only if `Market/GetOverview` lists two markets with catalogue prices. If DEV lists one, record the case as not run, not failed.

### E2E-BOOK-005 — A package prices and times as authored
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-path : packages are part of the priced selection.
- **Given** an active package with a price row in CZK. **When** the customer quotes the package alone, Normal level. **Then** `totalPrice` equals the package price (plus per-room amounts of its services at the chosen size); the quote's duration is the sum of the package's services.
- **DEV:** creates nothing.

### E2E-BOOK-006 — A package and a service it includes are charged twice
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#charging-a-package-and-a-service-together : owner ruling, never de-duplicated.
- **Given** a package P that includes service X. **When** the customer quotes P alone, X alone, then P and X together. **Then** the third total equals the sum of the first two and its duration is the sum of the two durations. On the web, iOS and Android the X row is marked "In your package" and adding X asks first ("Already in your package", Add again or Cancel); choosing Cancel leaves the selection unchanged.
- **DEV:** creates nothing.

### E2E-BOOK-007 — Extras add to the price, and an extra with no price in the currency is dropped
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#price-stages : extras are authored per currency; one without a row is dropped from the line items, not refused.
- **Given** an active extra with a CZK price row. **When** the customer quotes a service with and without the extra. **Then** the difference equals the extra's price. If an active extra has no CZK row, quoting it returns 200 with no line for it and no error.
- **DEV:** creates nothing. The second half needs an extra with no CZK row; if none exists, run the first half only.

### E2E-BOOK-008 — Rooms and bathrooms move the price by the per-room amount
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dirtiness-price : lines include per-room price times rooms plus bathrooms.
- **Given** a service with a per-room price above 0. **When** the customer quotes it at 1 room and 1 bathroom, then at 3 rooms and 2 bathrooms. **Then** the second total exceeds the first by the per-room price times 3. `estimatedDurationMinutes` is unchanged while the service's minutes per room are 0 (the DEV seed), so the size moves the price and not the time.
- **DEV:** creates nothing.

### E2E-BOOK-009 — The size cap is 8 rooms and 4 bathrooms, on every path
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#room-selection-and-start-times : at most eight rooms and four bathrooms, `order.size_exceeds_maximum`.
- **Given** a valid selection and slot. **When** the customer quotes and books with 8 rooms and 4 bathrooms, then with 9 rooms, then with 5 bathrooms, then with -1 rooms. **Then** 8 and 4 are priced and booked; 9 rooms and 5 bathrooms are refused `order.size_exceeds_maximum` on the quote, the Plus-savings quote and the booking alike; -1 is refused `validation.must_be_positive`. On the web, iOS and Android the stepper stops at 8 and 4 (the plus greys out) and the caption "Up to 8 rooms and 4 bathrooms" is on the Your home row.
- **DEV:** the 8 and 4 booking creates one order; cancel it afterwards through the oops window. The server accepts 0 rooms; the one-off pickers start at 1, so do not assert 0 on the UI.

### E2E-BOOK-010 — A request without a dirtiness level books at Normal
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#dirtiness-level : no level means Normal; no shipped client books without one.
- **Given** a quote body with no `dirtinessLevel`. **When** the customer quotes and books it. **Then** the quote echoes Normal with surcharge 0 and the stored order's level is Normal.
- **DEV:** creates one order if booked; pay by cash or cancel it in the oops window.

### E2E-BOOK-011 — The customer must pick a dirtiness level; Increased costs +15 %, Heavy +30 %
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dirtiness : Normal 0, Increased +15 %, Heavy +30 % on the whole basket, with time and crew following.
- **Given** a fixed selection with subtotal `S` (packages, services and extras) and a day-ahead slot. **When** the customer quotes it at Normal, Increased and Heavy. **Then** `dirtinessSurchargeAmount` is 0, round(S x 0.15, 2) and round(S x 0.30, 2) (half away from zero); `totalPrice` is S plus that surcharge (before discounts); `estimatedDurationMinutes` is the Normal minutes times 1.15 and 1.3, rounded up. The web wizard, iOS and Android each show a required level step right after the services with nothing preselected, and the next button stays disabled until one is chosen. The summary rail itemises the surcharge under the level.
- **DEV:** creates nothing for the quote part; the UI part stops at the review step.

### E2E-BOOK-012 — An unknown dirtiness level is refused everywhere
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#edge-cases : an unknown level is `common.invalid_enum_value`.
- **Given** a valid body. **When** the customer sends `dirtinessLevel` 3 to the quote, the Plus-savings quote and the booking (and to Recurring `Create`). **Then** every call is refused `common.invalid_enum_value`; no order or template is created.
- **DEV:** creates nothing.

### E2E-BOOK-013 — The order stores the level and the surcharge, and shows them
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dirtiness-fixed : the level and its rate are fixed on the order at booking.
- **Given** an Increased booking made in E2E-BOOK-029. **When** the customer opens the order detail and the admin opens the same order. **Then** the customer detail on every client names the level and itemises "Increased dirtiness surcharge" beside the stored amount, with no percentage printed; the admin order detail shows the level and the surcharge amount; the stored rate on the order is the one charged, whatever the constants say later.
- **DEV:** reuses the E2E-BOOK-029 order.

## Lead time, window and horizon

### E2E-BOOK-014 — A start under 2 h away is refused, not priced higher
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#booking-window : under 2 h the booking is refused outright.
- **Given** a valid selection. **When** the customer books (and quotes) a start 90 minutes from now, rounded up to a quarter hour inside 08:00 to 19:45. **Then** the booking is refused `order.cleaning_date.below_lead_time` and creates nothing. On the web the time step does not offer that slot.
- **DEV:** call the API directly: the lead-time rule is judged before the window rule, so the refusal key is the same at any hour, even when the slot is outside 08:00 to 19:45. The web slot list never offers such a slot, so the web half only checks that it is absent.

### E2E-BOOK-015 — A start 2 to 4 h away carries +20 % express
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#booking-window : between 2 and 4 h the booking is accepted with the express surcharge of +20 %.
- **Given** a customer with no Plus membership. **When** the customer quotes and books a start 3 h from now (rounded up to the grid). **Then** the quote's total is the day-ahead total at the same selection plus 20 % of the raw subtotal (`S` plus any dirtiness surcharge, before discounts), the surcharge line is shown on the web, iOS and Android review, the stored order carries `expressSurchargeAmount`, and the booking is accepted.
- **DEV:** creates one order; run only when now plus 3 h falls in 08:00 to 19:45 Prague, that is between 05:00 and 16:45 Prague. Cancel it in its oops window after the checks.

### E2E-BOOK-016 — Heavy on an express slot compounds to x 1.56
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#dirtiness-price : express is measured on top of the dirtiness surcharge, so Heavy and express is x 1.56.
- **Given** the same selection and a 3 h slot. **When** the customer quotes it Heavy. **Then** `totalPrice` equals S x 1.30 x 1.20 before discounts (S x 1.56). Note: the flow page's edge-case table states x 1.92, which is the superseded +60 % rate; the business rule is the reference.
- **DEV:** creates nothing; same clock window as E2E-BOOK-015.

### E2E-BOOK-017 — A start 4 h or more away has no express surcharge
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#booking-window : standard lead time is 4 h.
- **Given** a selection. **When** the customer quotes a start 4 h 15 min from now and a day-ahead slot. **Then** neither carries an express surcharge and both totals are equal.
- **DEV:** creates nothing; the 4 h 15 min slot must fall in 08:00 to 19:45 Prague. Do not test exactly 4 h 00 min: the boundary instant is racy.

### E2E-BOOK-018 — The start must sit on the 15-minute grid
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#start-time-window : the server refuses a start off the 15-minute grid.
- **Given** a day-ahead date. **When** the customer quotes and books 10:07, then 10:15. **Then** 10:07 is refused `order.cleaning_date.outside_booking_window` on both calls; 10:15 is accepted. The web slot list offers quarter-hour steps only.
- **DEV:** the 10:15 booking creates an order; cancel it in the oops window.

### E2E-BOOK-019 — The window is 08:00 to 19:45 in the market's zone
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#start-time-window : first slot 08:00, last 19:45, read in the address market's zone.
- **Given** a day-ahead date. **When** the customer quotes 07:45, 08:00, 19:45 and 20:00 Prague time. **Then** 07:45 and 20:00 are refused `order.cleaning_date.outside_booking_window`; 08:00 and 19:45 are priced. The web, iOS and Android pickers offer 08:00 to 19:45 only.
- **DEV:** creates nothing for the API part.

### E2E-BOOK-020 — The window uses the market's clock, not the device's
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · API
- **Rule:** → /product/business-rules#start-time-window : read in the service address's market zone, never the device's.
- **Given** the web with the browser time zone forced to America/New_York. **When** the customer chooses the 08:00 slot and reaches the review step, and the same instant is quoted through the API. **Then** the slot is shown as 08:00 and the quote accepts the instant 08:00 Prague; a UTC instant that is 08:00 in New York but 14:00 Prague is accepted only as 14:00.
- **DEV:** creates nothing; the Playwright context sets `timezoneId`.

### E2E-BOOK-021 — The horizon is 60 days
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#start-time-window : no further ahead than 60 days.
- **Given** a valid selection. **When** the customer quotes 59 days ahead and 61 days ahead at 10:00. **Then** 59 days is priced and 61 days is refused `order.cleaning_date.outside_booking_window`. The web calendar and its next-month arrow stop at the horizon and the home calculator date picker does the same.
- **DEV:** creates nothing.

### E2E-BOOK-022 — A start in the past is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#start-time-window : the future and lead-time rules keep their own keys and run first.
- **Given** a valid selection. **When** the customer books yesterday 10:00. **Then** the refusal is `order.cleaning_date.future`, not the window key.
- **DEV:** creates nothing.

### E2E-BOOK-023 — A refused slot holds the wizard on the time step
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web
- **Rule:** → /flows/booking-and-pricing#room-selection-and-start-times : a start the server refuses holds the wizard on the time step with its reason.
- **Given** a wizard filled up to the review step with a valid slot, and the server clock moving the slot below the lead time (book a slot at about 2 h 05 min, wait until it is under 2 h, then submit). **When** the customer submits. **Then** the wizard returns to the time step and shows the reason for `order.cleaning_date.below_lead_time`; nothing is created.
- **DEV:** needs a 10-minute wait inside the day window. Not run outside 06:30 to 17:30 Prague.

### E2E-BOOK-024 — The quote tracks the selection while the customer types
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web
- **Rule:** → /flows/booking-and-pricing#responsive-quote-previews : changes are grouped into a 200 ms pause, a changed selection cancels the pending request, clearing the selection clears the quote.
- **Given** the web wizard at the services step. **When** the customer toggles three services within 100 ms of each other, then clears all of them. **Then** only one quote request is sent after the pause (count the `Quote` calls), it matches the final selection, the previous price stays visible while it loads, and clearing the selection removes the price.
- **DEV:** creates nothing; assert on network calls.

### E2E-BOOK-025 — A deactivated service or package is refused by id
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#deactivated-catalogue : selecting a deactivated service or package by id is refused.
- **Given** the Admin deactivates a throwaway service (create one in the suite; do not touch seeded ones). **When** the Customer quotes and books it by id from a stale selection. **Then** both are refused `order.selected_services.invalid`; the same for a throwaway package with `order.selected_package.invalid`. The catalogue overview no longer lists the entries. Re-activating or deleting the throwaway entry cleans up.
- **DEV:** creates and removes catalogue rows; never use a seeded service.

### E2E-BOOK-026 — An empty selection is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#the-path : the validator re-prices the selection; nothing to price is refused.
- **Given** a body with no services, no packages. **When** the customer books it. **Then** the refusal is `order.empty`; nothing is created.
- **DEV:** creates nothing.

### E2E-BOOK-027 — A currency the address does not use is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#price-stages : a `currencyId` the caller names is checked against the address country's currency.
- **Given** a CZ address and a valid selection. **When** the customer books with a `currencyId` of a different currency. **Then** the refusal is `currency.invalid` before anything is priced.
- **DEV:** creates nothing. Needs a second currency row (any seeded currency).

## Booking: card, cash, guest

### E2E-BOOK-028 — A signed-in customer books by card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-path : the order is created `New` with payment `Pending` and Stripe is asked for the amount due on the card.
- **Given** a signed-in customer without credit, a saved CZ address (E2E-BOOK-065) or a looked-up one, a day-ahead slot, terms and early-performance ticks. **When** the customer books by card (web: checkout session redirect; apps: `CreatePaymentIntent` and the payment sheet). **Then** the order exists with status `New`, payment `Pending`, `totalPrice` equal to the quote, `creditAppliedAmount` 0 and `amountDueOnCard` equal to the total; the Stripe session or intent is for exactly `amountDueOnCard` in CZK minor units. Paying it is the card-payment flow, not this case.
- **DEV:** creates one order and one Stripe test session; leave it unpaid for the stale-checkout sweep or continue into the payment flow.

### E2E-BOOK-029 — An Increased booking by card keeps the level on the order
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#dirtiness-level : the level rides the booking and is stored on the order.
- **Given** the selection of E2E-BOOK-011 with the quote at Increased. **When** the customer books it by card resubmitting the Increased total. **Then** the booking is accepted, the order's total equals the Increased quote and the order detail names the level. A submit with the Normal total while the level says Increased is refused `order.total_price.not_match`.
- **DEV:** creates one order.

### E2E-BOOK-030 — The submitted total is a confirmation, never an input
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#the-price-is-never-taken-from-the-client : the validator re-prices and refuses on disagreement.
- **Given** a valid body whose `totalPrice` is lowered by 1 CZK, then raised by 1 CZK. **When** the customer books each. **Then** both are refused `order.total_price.not_match`; no order, no Stripe object. A body with `totalPrice` 0 is refused `order.total_price.positive`.
- **DEV:** creates nothing.

### E2E-BOOK-031 — A signed-in customer books cash with a usable saved card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#cash-eligibility : cash needs a signed-in customer, a usable saved card in the currency and a one-cleaner job.
- **Given** a customer holding an active verified CZK saved card (saved by ticking "Save this card" on a card payment, E2E-BOOK-040) and owing nothing, a one-cleaner selection. **When** the customer books it as cash. **Then** the order exists `New` with payment `Pending`, `creditAppliedAmount` 0, `amountDueOnCard` not charged anywhere; no Stripe session opens; the customer's booking e-mail would carry the cash line (not checkable: see the closing section). The saved card is not charged.
- **DEV:** creates one cash order that counts toward the cap of two (E2E-BOOK-037). Cancel it at the end.

### E2E-BOOK-032 — Cash is refused to a guest
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cash : a guest pays by card.
- **Given** no session. **When** the guest books a one-cleaner selection with `paymentType` cash. **Then** the refusal is `order.cash_not_available` before anything is reserved; on the web the cash option is disabled with the reason "sign in" and the choice is never switched to card by itself.
- **DEV:** creates nothing.

### E2E-BOOK-033 — Cash needs a usable saved card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#card-guarantee : an active, captured, unexpired, same-currency card qualifies; otherwise `order.cash_requires_saved_card`.
- **Given** a signed-in customer with no saved card. **When** the customer books a one-cleaner selection as cash. **Then** the refusal is `order.cash_requires_saved_card`. On the web, iOS and Android the cash option is disabled with the reason "save a usable card in this currency" and no card is chosen for the customer.
- **DEV:** creates nothing.

### E2E-BOOK-034 — A pending, removed or other-currency card does not unlock cash
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#card-guarantee : pending, removed, expired or other-currency cards do not qualify.
- **Given** (a) a card whose setup was started and never completed (`SavedCard/CreateCheckoutSession` abandoned), (b) a card saved and then removed, (c) a card saved on a EUR booking while the booking is CZK. **When** the customer books cash after each. **Then** each is refused `order.cash_requires_saved_card`. `SavedCard/GetMine` lists none of (a) or (b).
- **DEV:** (c) needs a second serviced market; if DEV lists one market, run (a) and (b) only. Expired cards cannot be made on Stripe test mode in the past; not run.

### E2E-BOOK-035 — A job that needs two cleaners cannot be paid in cash
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cash : the server's required crew must be exactly one; 120 booked minutes is one cleaner, 121 is two.
- **Given** the Admin sets a throwaway service's estimated time to 121 minutes (a throwaway service created for the suite, priced in CZK), and the customer holds a usable card. **When** the customer quotes that service and then books it as cash. **Then** the quote returns `requiredEmployees` 2, the booking is refused `order.cash_not_available` and nothing is reserved or debited; booked as card it is accepted and the order stores 2 required cleaners. With the service at 120 minutes the cash booking is accepted. On the web, iOS and Android cash is disabled with the reason "this booking needs 2 cleaners" and a cash choice made before the selection grew is cleared, not switched to card.
- **DEV:** the seeded services are 2 minutes each, so the crew boundary is reachable only through an Admin edit of a throwaway service; edit it back and delete it afterwards. Unsure of the admin form field name: it is the service's estimated time.

### E2E-BOOK-036 — The dirtiness level can take cash away
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#dirtiness-level : a 120-minute job at Increased is 156 minutes, two cleaners, card only.
- **Given** the throwaway service at 120 minutes (E2E-BOOK-035) and a customer with a usable card. **When** the customer quotes it at Normal, then Increased, then Heavy and books each as cash. **Then** Normal returns `requiredEmployees` 1 and books; Increased (156 minutes) and Heavy (192 minutes) return 2 and are refused `order.cash_not_available`. The clients re-read the quote and clear the cash choice when the level changes.
- **DEV:** same throwaway service; the Normal cash booking creates an order, cancel it.

### E2E-BOOK-037 — At most two cash bookings can be open
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#card-guarantee : a third cash booking not yet paid is refused `order.cash_open_bookings_limit_reached`; card stays open.
- **Given** a customer with a usable card who owes nothing. **When** the customer books cash twice, then a third time, then books the third by card, then cancels one cash booking (free in its oops window) and books cash again. **Then** the third cash booking is refused `order.cash_open_bookings_limit_reached`; the card booking is accepted; after the cancel the next cash booking is accepted. Card bookings never count.
- **DEV:** creates up to four orders; cancel all of them at the end. Cancelling inside 15 minutes (60 on a first booking) is free.

### E2E-BOOK-038 — A customer who owes books nothing
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#owes-nothing : an open receivable refuses every new booking, cash or card, `order.unpaid_receivable`.
- **Given** a customer with an open receivable (created by the lockout or cash-not-paid path of the cancellation flow and execution flow) and a valid selection. **When** the customer books cash, then books card, then asks for the receivable list. **Then** both bookings are refused `order.unpaid_receivable`, before anything is reserved, debited or dispatched; the web returns to the payment step listing what is owed with Pay now; the apps open a dialog to Profile, Payments. After the receivable is paid through its pay link or written off by the Admin, both bookings are accepted. A guest booking under the same e-mail is accepted.
- **DEV:** needs a receivable, which needs a completed cash booking that the cleaner reports unpaid (execution flow) or a confirmed lockout; build this case after those cases of the run succeed.

### E2E-BOOK-039 — A guest books by card on the web
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#edge-cases : a guest books by card; the order is keyed on the e-mail and found later through the lookup.
- **Given** no session, a day-ahead slot, contact details with an `@example.com` e-mail, a looked-up CZ address, terms and early-performance ticks. **When** the guest completes the wizard and pays by card at Stripe test checkout (4242 4242 4242 4242). **Then** the order exists with no user, status `New`; the checkout response carried `guestAccessToken`; the success page shows the booking through that token; the order does not appear in any account's My orders until an account with that e-mail signs in (E2E-BOOK-062). The Admin finds it in the order list by e-mail.
- **DEV:** creates one guest order and keeps the token for E2E-BOOK-056 onward.

### E2E-BOOK-040 — The "save this card" tick keeps a card only when ticked
- **See** E2E-PAY-022 and E2E-PAY-023: the same assertion, kept once in the file whose flow page owns the rule.

### E2E-BOOK-041 — A guest cannot save a card
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#save-card : a guest's `saveCard: true` is refused.
- **Given** no session. **When** the guest books with `saveCard` true. **Then** the refusal is `saved_card.requires_account`; the web never shows the tick to a guest.
- **DEV:** creates nothing.

### E2E-BOOK-042 — A saved card can be listed and removed, and only by its owner
- **See** E2E-PAY-029: the same assertion, kept once in the file whose flow page owns the rule.

### E2E-BOOK-043 — Credit is shown on the quote and applies to card bookings only
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-price-is-never-taken-from-the-client : the quote carries the balance and the share so the wizard can say what the card will be asked for.
- **Given** a customer holding credit (the Admin issues it; see the loyalty cases). **When** the customer quotes a selection in that currency and opens the payment step. **Then** the quote carries `creditBalance` and `creditMaxShareOfOrder` (0.7); picking card shows "To pay by card" with the credit taken off, picking cash shows that credit applies to card payments only and no credit line.
- **DEV:** the spending itself is E2E-LOYAL-021.

### E2E-BOOK-044 — A promo code needs an account, and replaces the combined discount when larger
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS
- **Rule:** → /product/business-rules#money-constants : an anonymous booking naming a promo code is refused `promo.requires_account`; a code with a minimum is bound to one currency.
- **Given** an Admin-created percent promo code with no minimum. **When** a guest books with the code, then the signed-in customer quotes and books with it. **Then** the guest is refused `promo.requires_account` (the booking is refused, not silently undiscounted); the customer's quote shows the promo discount, `Validate` agrees, and the order's `promoDiscountAmount` is stored. A code that has expired or hit its cap is refused `promo.expired` or `promo.global_limit_reached` at the booking, never dropped.
- **DEV:** creates a promo code through the admin API; deactivate it afterwards. Unsure whether the Android booking UI has a promo field (the API client has it); do not assert on Android.

### E2E-BOOK-045 — Tier, Plus and promo discounts are capped at 12 % together
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#discount-cap : tier and Plus add and are capped at 12 % of the raw subtotal; a larger promo replaces them.
- **Given** a customer at the top tier with Plus active (12 % plus 5 %), a selection above the tier floor. **When** the customer quotes it. **Then** the combined tier and Plus discount is 12 % of the raw subtotal (both pro-rated down so their sum is 12 %), shown as separate lines; adding a promo of 20 % replaces both with 20 %. The stored order satisfies lines + dirtiness + express - (tier + Plus + promo) = total.
- **DEV:** needs a top-tier account (an Admin grant, see the loyalty cases) and a Plus membership; build after those.

## Terms, consent and the booking record

### E2E-BOOK-046 — The terms tick is required for a guest, and a refusal is recorded
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#the-terms-tick-is-required-unless-the-account-already-accepted-the-texts-in-force : a booking asserts `termsAccepted: true` or is refused.
- **Given** no session and an otherwise valid body. **When** the guest books without `termsAccepted`. **Then** the refusal is `consent.terms_not_accepted` (judged before the price chain, so it also wins over a wrong total); on the web the confirm button is disabled until the box is ticked. The Admin's customer audit list shows a refused `customer.order.create` row with that key and the caller's IP.
- **DEV:** creates a failure audit row, no order.

### E2E-BOOK-047 — A customer whose consents cover the texts in force is not asked
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-terms-tick-is-required-unless-the-account-already-accepted-the-texts-in-force : the box shows only when the terms and privacy consents do not cover the texts in force.
- **Given** a customer who accepted the current terms and privacy versions at registration. **When** the customer books with no `termsAccepted`, then withdraws the consent (profile, consents; or the API) and books again with none. **Then** the first is accepted (and the review step shows no terms box); after the withdrawal the booking is refused `consent.terms_not_accepted` and the review step shows the unticked box. A booking sent with `termsAccepted: true` moves both consent rows to the version in force and the order records the accepted versions.
- **DEV:** creates up to two orders; cancel them.

### E2E-BOOK-048 — The early-performance request is asked on every booking
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#early-performance : every booking and new schedule refuses without `earlyPerformanceRequested: true`, even when consents cover the terms.
- **Given** a customer whose consents cover the current terms, and a guest. **When** each books without `earlyPerformanceRequested`. **Then** both are refused `consent.early_performance_not_requested`; with it true both are accepted and the order stores the wording's version (`early-performance-draft-2026-09-29` or the version then in force), the time, host, IP and device. The web wizard, iOS and Android show the tick next to the terms and do not send the booking without it.
- **DEV:** creates two orders when accepted; cancel them.

### E2E-BOOK-049 — Contact details are validated
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#the-path : the validator refuses a malformed booking before any side effect.
- **Given** a valid guest body. **When** the guest books with, one at a time: a 1-character name, a name over 50 characters, an e-mail without `@`, an e-mail over 50 characters, an empty phone, a street of 4 characters, a postcode of 2 characters. **Then** each is refused with `common.min_length`, `common.max_length`, `email.invalid_format` or `common.required` on the field named; nothing is created. The web wizard blocks the step with the field error before calling the server.
- **DEV:** creates nothing.

### E2E-BOOK-050 — A booking names exactly one address
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#the-path : the validator refuses a booking with both a saved address and a new one, or with neither.
- **Given** a customer with a saved address. **When** the customer books with both `savedAddressId` and `customerAddress`, then with neither. **Then** both are refused `order.address_exactly_one_required`.
- **DEV:** creates nothing.

### E2E-BOOK-051 — A signed-in booking uses the account e-mail
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#booking-contact-e-mail : a signed-in booking stores the e-mail of the session even if the request names another.
- **Given** a signed-in customer. **When** the customer books with `customerEmail` set to another address. **Then** the stored order's contact e-mail is the account's; on the web the field is shown read-only with the account e-mail.
- **DEV:** creates one order; cancel it.

### E2E-BOOK-052 — The order records the language it was booked in
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#booking-language : the order stores the customer's language; an unknown code is refused.
- **Given** the web in Czech. **When** the customer books, and the API receives `language` `xx`. **Then** the order's language is `cs` (read from the order detail); the unknown code is refused `language.not_supported` (the `LanguageValidator`) and creates nothing. Receipts and e-mails in that language are not checkable here.
- **DEV:** creates one order; cancel it. Unsure of the exact key the validator returns for the unknown code: the constant `language.not_supported` is the best match.

### E2E-BOOK-053 — Every booking leaves an audit row the Admin can read
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#the-booking-leaves-a-row-and-so-does-a-refused-one : `customer.order.create` records what the server priced and showed.
- **Given** a booking made in the run (card, signed-in) and a guest booking (E2E-BOOK-039). **When** the Admin opens the customer audit for each. **Then** the signed-in row carries the price breakdown, level and surcharge, discounts, express state, line item ids, duration, cancellation figures, the terms tick and version, the client, IP and device, and no name, address text or instructions; the guest row has no user and is reached through the order's history.
- **DEV:** read-only.

### E2E-BOOK-054 — The customer mobile host does not book for an anonymous caller
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#edge-cases : the mobile host's create route requires a session, an anonymous call is 401 and creates nothing.
- **Given** no session. **When** the guest posts a valid body to the mobile host's `CreateOrder`. **Then** 401; no order exists for the e-mail afterwards.
- **DEV:** creates nothing.

### E2E-BOOK-055 — A preferred cleaner at booking is a Plus benefit
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#preferred-cleaner : naming a favourite cleaner needs a current membership.
- **Given** a customer with no membership and a cleaner who has completed an order for the customer. **When** the customer books naming that cleaner. **Then** the refusal is `order.preferred_employee.membership_required`; with an entitled membership and a cleaner who never served the customer it is `order.preferred_employee.not_eligible`.
- **DEV:** needs a completed order by a cleaner (execution flow) and a Plus membership for the second half. Unsure how the iOS and Android booking flows expose the picker; run on API and Web.

## Guest bookings: lookup, listing and cancellation

### E2E-BOOK-056 — The guest token opens the booking in its redacted projection
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#guest-order-lookup : one per-order access token opens the booking; the projection has no address and no crew.
- **Given** the guest booking of E2E-BOOK-039 and its token. **When** the guest calls `POST api/Order/Lookup` with the token, `GET api/Order/Lookup?token=`, and opens `/track-order?...&token=` on the web. **Then** all three return the booking with status, slot, price and `confirmationCode`, and no address, no crew, no cleaner name; the web page shows the same.
- **DEV:** read-only.

### E2E-BOOK-057 — A wrong, revoked or account token all answer order.not_found
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#guest-order-lookup : a token that matches nothing, an expired or revoked one, and an account booking answer the same `order.not_found`.
- **Given** a random 43-character token and the token of a booking that belongs to an account (use the customer's order id as a bogus token, and the access token of a cancelled guest booking). **When** the guest looks each up. **Then** all answer `order.not_found` with the same body and no hint which case it was.
- **DEV:** the revoked case needs E2E-BOOK-060 first.

### E2E-BOOK-058 — LookupBatch answers per token without saying which was wrong
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#guest-order-lookup : up to 10 tokens; an unmatched token yields no row.
- **Given** one valid token and two invalid ones. **When** the guest posts the three to `LookupBatch`. **Then** the response holds exactly one row; the response does not say which tokens failed. Posting 11 tokens answers 200 with an empty list, not an error (the handler returns no rows past the limit of 10).
- **DEV:** read-only.

### E2E-BOOK-059 — A guest previews and cancels with the same token
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#guest-cancellation : the preview shows the standard tier, fee and refund; the cancel records `CancelledBy.Customer`.
- **Given** the paid guest booking of E2E-BOOK-039, inside its oops window (a guest's first booking has 60 minutes). **When** the guest posts `GuestCancellationPreview`, then `CancelGuest`. **Then** the preview shows `oopsWindowMinutes` 60, fee 0 and, for a paid booking, the full refund; the cancel returns the cancellation response with the refund attempt; the order is `Cancelled`, cancelled by customer; the Admin sees the cancellation. A booking that took no payment shows refund 0.
- **DEV:** the refund of a paid test booking goes to Stripe test mode and succeeds; the 5126 card makes it fail (cancellation flow). Run inside 60 minutes of booking or the fee tier applies instead.

### E2E-BOOK-060 — A guest cancel revokes every token
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#guest-access-token : every cancellation revokes every live token; the cancellation e-mail alone carries a new one.
- **Given** the guest order cancelled in E2E-BOOK-059. **When** the guest looks it up with the original token. **Then** `order.not_found`. The new token inside the cancellation e-mail cannot be read on DEV (closing section).
- **DEV:** follows E2E-BOOK-059.

### E2E-BOOK-061 — The guest routes refuse an account's booking
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#guest-cancellation : both operations require a guest booking; an account booking answers like an unknown token.
- **Given** a signed-in customer's order and a guessed token. **When** the guest calls `GuestCancellationPreview` and `CancelGuest` with any token. **Then** `order.not_found`; the account's order is untouched.
- **DEV:** creates nothing.

### E2E-BOOK-062 — A guest booking is listed in My orders after sign-up with the same e-mail
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#listed-guest-bookings : the confirmed session lists null-user bookings made with its e-mail, labelled and redacted.
- **Given** the guest booking of E2E-BOOK-039 and then a customer account registered and confirmed with the same e-mail (confirmed by the DEV script). **When** the customer opens My orders and the booking. **Then** the booking is listed and labelled as a guest booking; its detail is the redacted one with guest-term cancellation; the account has no review, dispute or reward actions on it and earns nothing for it.
- **DEV:** needs the account created after the guest booking with the same `@example.com` address.

### E2E-BOOK-063 — The guest no-show report is refused before the start
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#guest-cancellation : `ReportGuestNoShow` is refused before the start (`order.start_time_not_reached`) and once the job is under way (`order.cleaner_already_started`).
- **Given** a guest booking with a cleaner assigned. **When** the guest reports a no-show before the slot, and again after the cleaner started it. **Then** the first is `order.start_time_not_reached`, the second `order.cleaner_already_started`. After the slot has passed with nobody started, the report notifies the company's administrators once (`admin.order.cleaner_not_started` in the admin feed) and moves no money.
- **DEV:** the third half needs the slot to pass: the earliest slot is 2 hours ahead, so it is a two-hour wait; run it last.

### E2E-BOOK-064 — The mobile host keeps the guest routes for old builds, the apps have no guest screen
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/booking-and-pricing#guest-order-lookup : the lookup is web-only; the mobile host still serves it for builds installed before the change.
- **Given** the guest token of E2E-BOOK-039. **When** the guest calls `Lookup`, `GuestCancellationPreview` on the mobile host, and an agent opens the iOS and Android customer apps signed out. **Then** the routes answer as on the web host; neither app offers "Find a guest booking" or any guest screen.
- **DEV:** read-only; run before E2E-BOOK-059 cancels the booking, or use a second guest order.

## Saved addresses

### E2E-BOOK-065 — A customer saves an address and books with it
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-path : a booking names a saved address or a new one; the order carries an address snapshot.
- **Given** a signed-in customer with no saved address. **When** the customer adds a CZ address with a label (`SavedAddress/Add`, or "Save this address for next time" in the wizard), then books with its id. **Then** `GetMine` lists it with the label and as default if it is the first; the order's address snapshot equals it; the quote for that address is in CZK. Web: `/saved-addresses`; iOS and Android: the address manager.
- **DEV:** creates one saved address and one order.

### E2E-BOOK-066 — Saved address rules
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#the-path : labels, duplicates, ownership and default handling for saved addresses.
- **Given** a customer with one saved address and a second customer. **When** the first customer adds the same address again, adds one with an empty label, sets the new one as default, updates it, deletes the old default; the second customer tries to update and delete the first's address. **Then** the duplicate is `address.already_exists`, the empty label `address.label_required`, the other customer's attempts `address.not_owned_by_user`; exactly one default exists after each step and deleting the default leaves a valid state. Unsure which key an unknown id returns: record it.
- **DEV:** creates and deletes saved addresses.

### E2E-BOOK-067 — An address in an unserviced city is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#serviced-city : a booking is taken only in a serviced city of its market; the server refuses `city.not_serviced`.
- **Given** a city with no active service-city row (an Admin deactivates a throwaway city, or use one the seed does not list). **When** the customer books at an address there. **Then** the booking is refused `city.not_serviced`; the web booking step cannot be left on that address and says the city is not covered; the apps show a notice and let the address be saved. A Prague address is accepted by its postcode (starting with 1), any other by its city.
- **DEV:** use an address the seed does not list; do not deactivate seeded cities.

### E2E-BOOK-068 — A country that is not a market is refused
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#the-booking-leaves-a-row-and-so-does-a-refused-one : a country that is not a market is `country.not_serviced`, refused before the operator is resolved.
- **Given** no session and a valid body with an address whose country is seeded but not serviced. **When** the guest books. **Then** `country.not_serviced`; no order; the failure sink writes no audit row (nothing exists to stamp it with).
- **DEV:** creates nothing. Needs a seeded country with no operating company; unsure which.

## Recurring bookings

### E2E-BOOK-069 — Recurring schedules are a Plus benefit
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cleansia-plus : authoring a standing booking needs a current membership.
- **Given** a customer without Plus. **When** the customer posts `RecurringBooking/Create`. **Then** `recurring_booking.membership_required`. On iOS and Android a past-due or paused member sees no Create and no Edit and the text "Recurring paused".
- **DEV:** creates nothing. The past-due state cannot be produced (closing section).

### E2E-BOOK-070 — A Plus member creates, lists, pauses and deletes a schedule
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : a template materialises occurrences up to 7 days ahead; create, list, pause, resume, delete.
- **Given** a customer with an active or trialing Plus membership, a saved address, a weekly template on Thursdays at 10:00 starting within the horizon. **When** the customer creates it (terms and early-performance ticks sent), lists it, pauses it with `SetActive`, resumes it, edits it and deletes it. **Then** `GetMine` shows the template with its next visit in the market's zone and the level; pausing stops new occurrences; deleting removes it; occurrences already created stay. A `customer.recurring.*` audit row exists for each act.
- **DEV:** occurrences appear only after the materialiser's tick; read the occurrences after the next tick or note the case "created, awaiting materialiser".

### E2E-BOOK-071 — Creating or editing a schedule is validated
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : the create and update commands refuse a bad time, a bad start, a bad size and a missing consent.
- **Given** a Plus member and a valid body. **When** the customer sends, one at a time: `timeOfDay` 07:30, `timeOfDay` 10:07, `startsOn` 61 days ahead, a start on or after the end date on an update, no services and no packages, 9 rooms, `dirtinessLevel` 3, no `termsAccepted` when the consents do not cover, no `earlyPerformanceRequested`, a saved address of another user, a saved address in an unserviced city. **Then** the refusals are, in order, `order.cleaning_date.outside_booking_window` (07:30, 10:07 and the 61-day start), `recurring_booking.ends_on_before_start`, `recurring_booking.no_services_or_packages`, `order.size_exceeds_maximum`, `common.invalid_enum_value`, `consent.terms_not_accepted`, `consent.early_performance_not_requested`, `recurring_booking.not_owned_by_user` or `recurring_booking.saved_address_not_found` (record which), `city.not_serviced`. Nothing is created.
- **DEV:** creates nothing. Updating a schedule asks for neither consent.

### E2E-BOOK-072 — A cash schedule must stay a one-cleaner job
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : a cash template that needs more than one cleaner is refused, or flagged and skipped by the materialiser, never switched to card.
- **Given** a Plus member with a usable card and the throwaway 121-minute service (E2E-BOOK-035). **When** the member creates a cash schedule with it, then creates one with a 100-minute service as cash and raises the Dirtiness level to Heavy on an update. **Then** the first create is refused `order.cash_not_available`; the second is accepted, then the update is refused `order.cash_not_available`. A template made eligible and then grown by a catalogue edit has `requiresPaymentMethodChange` true in `GetMine`, the web, Android and iOS lists badge it "Needs a change", and an edit that moves it to card clears the flag.
- **DEV:** needs Admin edits of the throwaway service's minutes; restore them after.

### E2E-BOOK-073 — A schedule has its own dirtiness level
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dirtiness-recurring : the template carries a level; every occurrence is priced, timed, crewed and paid at it.
- **Given** a Plus member. **When** the member creates a schedule at Increased and lists it. **Then** the template DTO returns Increased; the web form starts with none chosen and a save without one names it as missing; the card quotes at the schedule's own level; every occurrence materialised later carries the level and the surcharge.
- **DEV:** occurrence check waits for the materialiser.

### E2E-BOOK-074 — An occurrence is confirmed by the customer, cash or card
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : `ConfirmRecurringOrder` stamps the confirmation; cash needs nothing more, card opens a payment surface.
- **Given** a materialised occurrence at least 3 h away that `needsConfirmation`. **When** the customer confirms a cash occurrence, then confirms it again; and confirms a card occurrence on the web (Checkout Session) and, for another, on iOS or Android (PaymentIntent). **Then** the cash confirm succeeds and a second one is `order.recurring_already_confirmed`; the card web confirm returns `checkoutUrl`, the app confirm returns `clientSecret`; an occurrence begun on the other channel is refused `order.invalid_status_transition`; calling `CreatePaymentIntent` for an unconfirmed occurrence is refused `order.invalid_status_transition`.
- **DEV:** needs occurrences from E2E-BOOK-070; the materialiser tick decides when.

### E2E-BOOK-075 — Confirming an occurrence is refused under 2 h, over a debt, or without the terms
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : the confirm refuses an occurrence closer than 2 h, an owing customer and a missing terms tick.
- **Given** (a) an occurrence under 2 h away, (b) a customer with an open receivable, (c) a customer whose terms consent was withdrawn. **When** each confirms. **Then** (a) `order.cleaning_date.below_lead_time`, (b) `order.unpaid_receivable` before any consent is recorded or Stripe is asked, (c) `consent.terms_not_accepted` and the web, Android and iOS show the unticked box at once. An occurrence nobody confirms is retracted an hour before its slot with no fee.
- **DEV:** each needs a prepared occurrence; the retraction waits for the sweep. Run (c) first, it needs no debt.

### E2E-BOOK-076 — A lapsed membership stops the schedule and a re-subscribe resumes it
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cleansia-plus : the template is kept, occurrences already created run, the schedule resumes on the next tick after a re-subscribe.
- **Given** a Plus member with a schedule and a cancelled-and-ended membership. **When** the materialiser runs, then the customer re-subscribes. **Then** no new occurrence is created while lapsed and the customer is told once (`recurring.paused` in the feed); occurrences already made are untouched; after the re-subscribe the next tick creates occurrences.
- **DEV:** a trial lasts 14 days and a lapse needs it to end; not runnable end to end on DEV (closing section). Cancel inside the trial and assert the feed and flags only.

### E2E-BOOK-077 — The preferred cleaner on a schedule must have served the customer in the same currency
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/booking-and-pricing#recurring-bookings : one key, `order.preferred_employee.not_eligible`, for a cleaner who is not eligible.
- **Given** a Plus member who has had a completed order with cleaner C. **When** the member creates a schedule naming C, then one naming a cleaner who never served them. **Then** the first is accepted and `GetMyServingCleaners` listed C (asked with no slot); the second is refused `order.preferred_employee.not_eligible`.
- **DEV:** needs a completed order by a known cleaner (execution flow).

## Not testable on DEV

- E-mail content and delivery: the booking confirmation with its PDF, the status e-mails in the booking's language, the receipt language, the guest cancellation e-mail carrying a new token, the confirmation reminder. `@example.com` never receives mail and no case can read one. Only the facts stored on the order (language, e-mail address, token revoked) are checked.
- Apple and Google sign-in as a way into a booking; load; rate-limit windows (the cross-cutting cases own those).
- The 24 h booked-span cap (`order.span_exceeds_maximum`): the seeded services are 2 minutes each, so no reachable selection comes near 24 h; it is covered by the backend unit tests.
- `order.country_operator_mismatch` and `tenant.not_found`: DEV has one operating company, so a guest cannot disagree with the operator and no serviced market lacks one.
- Another market and another currency end to end (EUR catalogue, a CZK credit refused on a EUR booking, `catalogue_changed_for_country`): only if `Market/GetOverview` lists a second market on the day; otherwise not run.
- The express-waiver reservation race, the unique-index recurring duplicate guard, and the loyalty or credit write races: concurrency, covered by the backend integration suite on a real Postgres.
- Daylight-saving behaviour of a schedule (a time the spring change skips, the autumn repeat) and the monthly nth-weekday cadence: need the calendar to pass; covered by unit tests.
- The stale-checkout sweep (an unpaid card order cancelled after an hour) and the confirmation retraction an hour before the slot depend on timers; a run may observe them after an hour but they are not part of the booking suite.
- A frozen-for-archive company's books refusing writes, and the company wind-down telling its customers: needs a second company; the company-lifecycle cases own it.
