# Order Wizard

The order wizard is a multi-step booking flow implemented in the `@cleansia-customer/order-wizard`
library. It runs in seven steps (`OrderWizardFacade.steps`), numbered here from 0 as `activeStep`
counts them: the services and the size of the home, how dirty it is, the address and contact, the
date and time, the payment, the Cleansia Plus offer, and the review. The step rail above the form
shows them in this order, each as an icon and a label, and announces them to screen readers as
steps 1 to 7 (`aria.step`).

## Architecture

The wizard uses the **Component + Facade** pattern:

- `OrderWizardComponent` -- UI and user interaction
- `OrderWizardFacade` -- Business logic, API calls, state management
- `OrderWizardFormData` -- Type-safe form model

All state is managed via Angular signals (no NgRx for wizard-local state).

## Wizard Steps

| Step | Rail label | What it asks | Continue needs |
|---|---|---|---|
| 0 | Services | packages and services, rooms and bathrooms | a service or a package |
| 1 | Dirtiness | how dirty the home is | a level |
| 2 | Address | the address, the property, the entry note, the contact | a picked (or typed) address in a city that is not refused, and the four contact fields |
| 3 | Date & Time | the day, the arrival time, how the cleaner gets in | a date, and a bookable time inside the booking window |
| 4 | Payment | card or cash, saving the card, a promo code | a payment type that is not refused cash |
| 5 | Cleansia Plus | whether to add Plus, and anything else to book | nothing |
| 6 | Summary | a check of everything, the note for the cleaner, the ticks | the terms tick unless already given, and the early-start tick (*Place order*) |

What a step still needs is `OrderWizardFacade.missingReasons(step)`, to which the component adds the
time slot on step 3 and the two ticks on step 6 (`blockingReasons`). Continue, or *Place order* on
the last step, stays on a step that still needs something and lists what, under
`pages.order.missing.heading`. → [Step Navigation](#step-navigation)

### Step 0: Services & Packages

The customer picks packages and/or services and says how big the home is.

**The catalogue is the market's.** The step lists the services and packages of the market the address
will be in: the chosen market until the address step names a country, that country from then on
(`loadCustomerServices(countryId)`, `loadCustomerPackages(countryId)`, and the extras overview). When
the country changes the catalogue is read again, and a pick the new market does not offer is dropped.
The market directory also feeds the address step's country field.

**On screen.** The packages come first, as cards: a tagline, the name, a tick per service the package
includes, the price *per clean* and a *Choose package* chip, with *Most popular* on the popular one.
Then *Or individual services*, a priced list of names and descriptions, each with an *Add* chip. Names
and descriptions are in the current language. The room and bathroom counts are chips, 1 to 8 and 1 to
4: in the summary's side column on a wide window, and at the top of the step at 1100px and below
([The order summary](#order-summary)).

| Field | Type | Default | Validation |
|---|---|---|---|
| `selectedServiceIds` | `string[]` | `[]` | At least one service or package (`pages.order.missing.services`) |
| `selectedPackageIds` | `string[]` | `[]` | (combined with above) |
| `rooms` | `number` | `1` | A chip, 1–8; API maximum 8 |
| `bathrooms` | `number` | `1` | A chip, 1–4; API maximum 4 |

The same upper limits apply to booking, price quotes, Plus-savings previews and recurring-template
creation/update. The API returns `order.size_exceeds_maximum` when either is exceeded. The Android and
iOS steppers stop at the same limits and state them beside the size row's *Your home* title →
[Room selection](/flows/booking-and-pricing#room-selection-and-start-times).

**A service a chosen package includes is marked, and adding the pair by hand asks first.** Under such a
service the step prints *In your package: {package}* (`pages.order.package_overlap.in_package`), or
*In your packages: {package}* (`in_packages`, the same placeholder holding the comma-joined names)
when two or more chosen packages include it, and the service's add button takes that line as its
`aria-describedby` (`svc-in-pack-{id}`). **The Plus step marks its suggestions the same way.** Its
*Anything else?* list (`crossSellServices`: the first three services not chosen on their own) can offer
a service a chosen package already holds; that row carries the same line under its name, and its *Add*
chip takes the line as its `aria-describedby` (`cross-in-pack-{id}`). The schedule form's service picks
are the third site; there the whole pick is the button, and the line is part of its name.
`OrderWizardFacade.toggleService` and
`togglePackage` ask through the shared `DialogService` before a tap adds either half of a twice-booked
pair, with *Cancel* focused. No option puts it there: on PrimeNG 20.4 the shell's `<p-confirmDialog>`
never applies its own `defaultFocus`, so the dialog focuses the first focusable element of the
message, which has none, then of the footer, where *Cancel* comes before the accept button. A PrimeNG
upgrade that starts honouring `defaultFocus` (default `'accept'`) would move focus to *Add again* or
*Add package*. Removing never asks, and the Plus step's suggested *Add* goes through `toggleService`
too. A service two or more chosen packages include (`isInManyPackages`, which picks both the marker and
the question) is asked about under `service_title_many` (*Already in your packages*) with
`service_message_many` (*"books it once more"*), and a package asks when a service
it includes is already in the booking on its own **or** through another chosen package, never
counting itself (`includedServicesAlreadyChosen`, since 2026-10-04). A selection the wizard is handed
(a catalogue link, *Order again*, a parked basket) is written through `updateFormData`, so it is only
marked. Both helpers, `chosenPackagesByService` and `includedServicesAlreadyChosen`, live in
`customer-services` (`package-overlap.ts`) and the schedule form shares them. → [Charging a package and a service together](/product/business-rules#charging-a-package-and-a-service-together)

**A covered row reads as covered at a glance** (since 2026-10-04, the apps' design, with iOS as the
reference). The three sites draw it from four tokens beside the wizard shell's others
(`_wizard-shell.scss`): `--cl-covered-row`, the accent mixed 8 % into `--cl-surface` (16 % in dark
mode); `--cl-covered-line`, the accent at 60 % as a 1.5px border; `--cl-covered-badge`, the accent at
14 % (24 % in dark mode); and `--cl-covered-ink`, `#0c4a6e` (`#e0f2fe` in dark mode). The services
step's row, otherwise a line between dividers, becomes a card with 18px corners
(`cl-wiz__svc--covered`). The Plus step's suggestion card takes the tint and border
(`cl-wiz__cross-row--covered`), and so does a schedule pick that is not picked
(`cl-rec__pick--covered`, `_recurring-bookings.scss`). A picked pick keeps its picked look, a
`--cl-heading` slab, sky-700 in light mode and sky-300 (`#7dd3fc`) in dark. Its name, a package pick's
price and its tick's disc take `--cl-surface`, the card's own ground, which flips with the slab, and
the tick's check stays `--cl-heading`. Measured in Chromium, all four read 5.93:1 in light mode and
10.37:1 in dark. Until 2026-10-05 the name and the disc were white and the price the soft accent, so
in dark mode the name and the check read 1.67:1 and the price 1.00:1, and in light mode the price
read 2.77:1. Hovering never repaints a picked pick's border (`:hover:not(.cl-rec__pick--on)`): the
hover rule outranked the picked one and turned the border pale. On a picked pick the badge sits on
`--cl-surface`, where its words read 9.46:1 in light mode and 15.06:1 in dark. The badge,
`.cl-wiz__svc-in-pack` at all three sites, sits straight under the name and above the description. It
is a `pi-check-circle`, then the words in semibold `--cl-covered-ink`: 14px on the services step,
whose descriptions are 14px, and 13px elsewhere. The check takes the words' ink, as on the apps (since
2026-10-04; it was the accent, 3.1:1 in light mode and 3.7:1 in dark). Measured from the rendered page
in Chromium, the words and the check read 7.20:1 in light mode and 7.02:1 in dark. A
covered row's description moves to `--cl-muted-on-tint`, since `--cl-muted` reads 4.3:1 on the tint.
The *Add* chip, the `aria-describedby` wiring and the copy are unchanged. The `order-wizard` and
`create-recurring-wizard` component specs assert the covered class and the badge at each site.

### Step 1: Dirtiness level

The customer says how dirty the home is (*How clean is your home?*). Three cards, mildest first, come
from `DIRTINESS_LEVELS` (`customer-services`), which the home calculator shares: *Normal* with no
surcharge, then *Increased* and *Heavy* with theirs, +15 % and +30 %, from the rates in
`@cleansia/models`. Each card has a lead and four signs of that level, and a hint above them says to
pick the higher of two levels when unsure. Nothing is preselected: `dirtinessLevel` is `null` until a
card is pressed, and the step cannot be left without one (`pages.order.missing.dirtiness`). The level
moves the price, the booked time and the cleaner's pay by the same rate →
[The dirtiness level](/product/business-rules#dirtiness).

### Step 2: Address & Contact

The customer says where the clean is, what kind of home it is, how the cleaner gets in, and who to
call.

**A signed-in customer** has the name, e-mail and phone filled in from the account where the fields
are still empty, and the default saved address chosen. The account's saved addresses (`SavedAddressStore`,
read from the server) are chips, each with its label over its street line, beside *Enter a new
address*. A saved address is checked only for being non-empty, because the server already validated
it.

**A new address is looked up, not typed.** A country select (the market directory, showing the chosen
market until one is picked here; it decides the currency the booking is priced in) sits above the
address lookup. A pick fills the street, city and postcode and the coordinates, and shows what was
found so it can be checked; until then the step says to pick an address from the list. A city the
service does not cover is said at once (`api.service_area.city_not_serviced`), and the step cannot be
left; the server checks again on submit. *Cannot find the address? Enter it manually* opens street,
city and postcode fields instead (`addressEnteredManually`), which the server geocodes on submit. A
signed-in customer can tick *Save this address for next time* and name it. Beside the address, a map
tile of the picked coordinates (`/api/AddressSearch/map`), and *In range* once the city is confirmed.

**Then the property, the way in and the contact.** *Flat* or *House*: a flat asks for the floor and the
flat number, a house asks for neither and clears them. *How do we get in?* is an optional note of up
to 2000 characters, badged *Only the assigned cleaner sees it*: like the address and the phone, it is
redacted for every cleaner but the one the order belongs to. Last, the four contact fields:

| Field | Validation |
|---|---|
| `customerFirstName` | Required, 2-50 chars |
| `customerLastName` | Required, 2-50 chars |
| `customerEmail` | Required, valid email, max 50 chars |
| `customerPhone` | Required, matches `^[+]?[\d\s()-]{6,20}$` |
| `address.street` | Required, 5-255 chars |
| `address.city` | Required, 2-100 chars |
| `address.zipCode` | Required, matches `^[\d\s-]{3,20}$` |

<!-- The two ids below keep the anchors these steps had as steps 2 and 3, before the dirtiness
     step: code comments on the web, Android and iOS point at them. -->
### Step 3: Date & Time {#step-2-date-time}

The customer picks a cleaning date and an **arrival time in 15-minute increments**, from 08:00
through 19:45. The home calculator, web booking wizard, Android and iOS offer the same times.
The selected minutes are preserved in the booking sent to the API.

**Date selection** — the wizard's own calendar grid, not a PrimeNG `DatePicker`: one month of day
buttons under the weekday names, with previous and next month arrows.
- Earliest day: today, if one of today's slots is still outside the lead time, else tomorrow
- Latest day: the day before the 60-day horizon falls (`lastBookableDay`), so every slot of every
  offered day is inside it
- Days outside that range are drawn disabled, and the arrows offer no month wholly outside it
- A new date keeps the booked time when it is bookable on that date, and otherwise moves it to the
  date's first bookable slot

**Time selection — the part of day first, then the slot.** The step shows three part-of-day buttons,
each labelled with its first and last arrival time:

| Part | Hours | Label |
|---|---|---|
| Morning | 08–12 | 08:00–11:45 |
| Afternoon | 12–16 | 12:00–15:45 |
| Evening | 16–20 | 16:00–19:45 |

Under them is a 4 × 4 grid of the chosen part's sixteen quarter-hour slots, in place of one tall list
of all 48.

- **Choosing a part never changes the booked time.** It only changes which sixteen slots are on
  screen; the booking keeps its time until a slot is pressed. The step opens on the part that holds
  the booked time, and that part carries a dot while another one is being browsed.
- **A part with no bookable slot is disabled** — today's morning, for instance, once every morning
  slot is inside the lead time. A slot inside the lead time is greyed and disabled in the grid.

**The Android and iOS apps group the time the same way** (since 2026-10-03). Their booking's When step
shows the same three parts, with the same hours and labels, over a 4 × 4 grid of the chosen part's
sixteen slots, and follows both rules above. Each app opens on the part that holds the booked time
while it is still bookable, and otherwise on the first part with a bookable slot. A slot inside the
lead time stays in the grid, greyed and disabled, instead of being left out. An express slot carries a
bolt, and a part that shows one adds a legend line under the grid: *Express +20%*, or *Express · no
surcharge* while the customer has a Plus waiver left. Nothing animates when the part changes, and
every part and slot is at least 48pt (iOS) or 48dp (Android) tall. VoiceOver and TalkBack read a part
as *Morning, 6 slots available* and a slot as its time with its express tag. A recurring schedule's
time uses the same picker. A weekly time has no lead time, so all 48 of its slots can be picked. Until
then the apps listed every bookable quarter hour in one column and tagged the first as *Earliest*. On
iOS a schedule's time was a wheel, and on Android a list in two groups split at 17:00. The apps keep
their own slot data and lead-time bands (`timeSlotsFor` on Android, `BookingTimeSlots` on iOS). Only
the three parts come from the web, as `DayPart` in each app.

The daily bounds and lead times mirror `BookingPolicy` on the backend. Web arrival options live in
the shared `booking-window.models.ts`; the three parts are `dayParts` in `OrderWizardComponent`:

| Constant | Value |
|---|---|
| `SLOT_GRID_MINUTES` | 15 → 48 arrival times (mirrors `BookingPolicy.SlotGridMinutes`) |
| `FIRST_WINDOW_HOUR` / `LAST_WINDOW_HOUR` | 8 / 20 (inclusive start, exclusive end) |
| `MAX_BOOKING_HORIZON_DAYS` | 60 — the last offered day is the one before it |
| `EXPRESS_LEAD_TIME_HOURS` | 2 — below this, nothing is bookable |
| `STANDARD_LEAD_TIME_HOURS` | 4 — between 2 and 4 h, the slot is bookable **with surcharge** |

Each option is annotated `available` | `express` | `unavailable` by `filterTimeOptionsForToday`.
A slot shows only its arrival time ("10:00"), never a window range — a job can run longer than an hour
and "10:00 – 11:00" reads as an end time. A part's label is a range of arrival times for the same
reason, not a promise of when the clean ends.

::: tip Express is a slot property, not a member property
The backend's `ExpressWaiverResolver` answers `inExpressWindow` for everyone, guests included, so the
UI can distinguish "express, charged" from "not an express slot at all". A Cleansia Plus plan may
waive the surcharge, metered per **calendar month** — the quote response carries how many waivers
remain.
:::

Under the slots the step says what the chosen time costs: *This slot is express — +20% is added to the
price*, or that Plus waives it. While an express time is on offer it also says how many waivers the
customer has left this calendar month, or that none are left, from the server's count
(`ExpressUpgradesRemaining`), never one the client adjusts.

**How the cleaner gets in.** Below, *Getting in* offers four chips, *I will be home*, *I will hand over
keys*, *Door code* and *Reception* (`accessMode`, stored on the order as its slug). It is optional. The
assigned cleaner reads it with the entry note from step 2, and it is redacted with that note for every
other cleaner.

### Step 4: Payment Method {#step-3-payment-method}

The customer selects between:

| Method | Value | Description |
|---|---|---|
| Card | `PaymentType.Card` | Redirects to Stripe Checkout |
| Cash | `PaymentType.Cash` | Paid to the cleaner at the job — **only** for a signed-in customer whose booking needs one cleaner |

Default: `PaymentType.Card`

**Cash is decided by the server's rule, read on screen.** `OrderWizardFacade.cashEligibility` feeds
`resolveCashEligibility` (`libs/shared/models`, the web copy of `BookingPolicy.AllowsCash`) with the
live sign-in state and the quote's `requiredEmployees` — but only while the cached quote matches the
current selection. The verdict is one of four:

| Verdict | When | What the step shows |
|---|---|---|
| `available` | signed in, and the quote says one cleaner | cash selectable |
| `needs_account` | a guest | cash disabled — *Cash is only for signed-in customers* (`pages.order.cash_needs_account`) |
| `needs_card` | the quote says two cleaners or more, signed in or not | cash disabled — *Cleaners needed for this booking: N* (`pages.order.cash_needs_card`) |
| `pending` | signed in, no quote for this selection yet | cash disabled until the quote lands (`pages.order.cash_pending`) |

A cash choice that becomes refused — a sign-out, a selection that grows to two cleaners — is **taken
away, never switched to card**: `paymentType` becomes `null`, the step says *Cash is no longer
available for this booking…* (`pages.order.cash_cleared`) and the customer chooses again. Submit never
sends refused cash; if the server still answers `order.cash_not_available`, the choice is cleared and
the wizard returns to the payment step. → [Paying in cash](/product/business-rules#cash)

**Saving the card.** A signed-in customer who chooses card is offered an unticked *Save this card for
my next bookings*, followed by the card-guarantee consent sentence of the version the server records
on the card (`SavedCard.ConsentTextVersionInForce`). A guest or a cash booking never sees it.
`CreateOrderCommand.saveCard` is `true` only for an offered, ticked box; ticked, the card is kept
when the payment succeeds and is listed under **Saved cards** on `/profile`, where it can be removed;
unticked, Stripe keeps nothing. → [A saved card guarantees cash](/product/business-rules#card-guarantee)

**When the server refuses cash at submit.** Cash refused because the customer still owes an amount
from an earlier booking (`order.cash_unpaid_receivable`) is taken away as above, and the step shows
the amount owed with a way to pay it (`cleansia-customer-amount-due`); leaving to pay parks the
booking, which is waiting on the next visit → [What a customer owes](/product/business-rules#receivables).
Cash refused for want of a saved card opens the card capture instead.

**A promo code** is for a signed-in customer: a guest is told that codes are tied to an account and
that the booking does not need one. *Apply* asks the server once, never per keystroke. A valid code
reads *{code} applied — you save {amount}*, or, when the customer's tier or Plus discount is larger and
so applies instead, says that the code is valid but not applied. *Remove code* takes it off.
→ [Discounts, and the 12 % cap](/product/business-rules#discount-cap)

### Step 5: Cleansia Plus

The step offers Plus against this basket (*Add Cleansia Plus?*). Every figure on it is the server's.

- **What Plus would save on this order**, from `QuotePlusSavings`, which applies the same 12 % cap and
  express rules as the booking, re-asked whenever the basket changes while the step is on screen. The
  saving is in the basket's currency and a plan's price in the market's (ADR-0058 D4), each labelled
  with its own code. The lead also says whether the first days are free.
- **The plans**, each with its interval and price, its trial and the price after it, *Best value* and the
  saving against monthly where there is one, and *Start free trial* or *Choose plan*. Choosing parks
  the booking and opens the subscribe page on that plan; a guest is sent to sign in first.
- **The perks**, from the first plan the catalogue returns: its discount, its free-cancellation
  window, the longer free-cancellation grace after booking, its monthly express waivers if it has any,
  and recurring bookings.
- **A guest** is told that membership is tied to an account, with *Sign in* and *Register*; the
  booking is parked first, so it is still there afterwards.
- **The way past, with the same weight**: *Not right now, thanks*, with the total the booking will cost
  without Plus and *Continue without Plus*, which goes on to the review.
- **Anything else?** Up to three services not yet chosen on their own (`crossSellServices`), each with
  its price and an *Add* chip, and a link to the full price list. A service a chosen package already
  includes is marked here as on step 0, and its *Add* asks the same question.

**A market that sells no plan skips the step** (`plusUnavailable`, ADR-0059 D3): Continue and Back walk
past it, because a step whose only content is the way past is a dead page. Opened from the step rail,
it says *Plus is not available in your market yet* above the way past.

### Step 6: Review & Submit

*Check your order* restates what was chosen in five cards built from the form (`reviewCards`), each
with *Edit*, which returns to the step that owns it:

| Card | Lines |
|---|---|
| Services | each priced line of the quote, then the rooms and bathrooms |
| Dirtiness | the level |
| Address | the name, the phone and e-mail, the address with the floor and flat |
| Date & Time | the date and time, and how the cleaner gets in |
| Payment and membership | the payment type, an applied promo code, and the Plus plan when the account has one by now |

Under them come the preferred cleaner, a Plus perk (`cleansia-wizard-preferred-cleaner`, re-read on
every entry to this step because its answer depends on the time; → [Preferred cleaner](/product/business-rules#preferred-cleaner)),
and the *Note for the cleaner* (`specialInstructions`, up to 2000 characters). Then the ticks:

- **The terms**, *I agree to the terms of service and the processing of my personal data*, is shown
  only to a guest, an account that has not accepted the texts in force, or one whose consent could not
  be read; an account that has accepted them sees *You accepted our Terms of Service and Privacy
  Policy with your account* instead. When shown, it is required, and it rides `termsAccepted` on the
  create command.
- **The request to start within the withdrawal period** is asked on every booking, whatever the account
  has accepted, and is required (`pages.order.early_performance.early-performance-draft-2026-09-29`)
  → [The request to start within the withdrawal period](/product/business-rules#early-performance).

Last, the step states, unconditionally, signed in or guest, that *by confirming the order you conclude
a contract for the cleaning with the operating company of the market where your home is, under its
terms of service*, linking `/terms`, and that the cleaner carries it out as the company's subcontractor
and is no party to it (`pages.order.contract_notice`). It is an information line, not a tick.
→ [Business rules — the contract for work](/product/business-rules#work-contract)

## The order summary {#order-summary}

The summary is one element, `.cl-wiz__summary`, placed after the step in the document so a screen
reader meets it after the choices that produce it. CSS alone decides where it sits. It shows no price
until a line is priced.

**Desktop, wider than 1100px.** The step is on the left and a side column on the right. On the
services step the room and bathroom counts sit in their own card at the top of that column
(`.cl-wiz__counts-card`), above the summary, and scroll away with the page; only the summary is
sticky, under the navbar. On a short window the price lines scroll inside their own box before the
panel does, so the total, Continue and the free-cancellation note stay on screen.

**At 1100px and below.** The counts move into the services step, under its lead, and the summary
becomes a bar pinned to the bottom of the screen: the total and a chevron on the left, the primary
button — Continue, or Place order on the last step — on the right. The chevron opens the full summary
above the bar, over a scrim; the chevron again, the scrim, Escape or any step change collapses it, so
every step starts with the bar closed. The page reserves the bar's height under the step and under
the site footer, the scroll-to-top button and the DEV pill sit above it, and the navbar's menu sheet
opens over it.

::: info Why the bar is not a second copy of the total
On 2026-09-01 the wizard's bottom price bar was removed because it was a **second copy of the total**:
two copies of one number, which every quote has to keep equal, and one of them covering the form.
That objection still holds, and the bar answers it rather than overriding it. The bar **is** the
summary — the same element, collapsed by CSS to its own total, chevron and button — so the total
exists once in the document at every width; and the page reserves the bar's height, so it covers no
part of the form. `order-wizard.component.spec.ts` pins exactly one `.cl-wiz__summary` and no
`.order-wizard__mobile-price`; a copy of the total must not come back.
:::

## Price Calculation

::: danger The client does not compute the price
`OrderPricingFacade` (`order-pricing.facade.ts`) debounces the pricing-relevant wizard inputs and
calls **`POST /api/Order/Quote`**, then renders the server's totals verbatim. The wizard no longer
sums `basePrice + perRoomPrice * (rooms + bathrooms)` locally — an earlier version of this page
documented that local sum, and reimplementing it client-side produces a total the server will reject
with `order.total_price.not_match` at submit.
:::

The quoted total already folds in everything the server applies, in this order:

1. Raw subtotal over selected services, packages and extras.
2. **Cleansia Plus + loyalty tier discounts, additive**, capped at 12 % of the raw subtotal and
   pro-rated when the cap bites.
3. **A promo code replaces that combined amount if larger** — it never stacks.
4. **Express surcharge (+20 %)** on the *discounted* subtotal, when the chosen slot is 2–4 h out and
   the customer has no membership express-upgrade waiver left this calendar month.

Never re-apply a percentage on top of the quoted number. `QuoteOrder` and `OrderFactory` run the same
ordering precisely so the quote the customer saw and the price they are charged cannot drift.

Prices are formatted using the order's currency code with `Intl.NumberFormat`, locale derived from the
active translation language.

## Order Submission

When the customer clicks submit on the review step:

1. `OrderWizardFacade.submitOrder()` is called
2. The cleaning date and time are combined into a UTC `Date`
3. A `CreateOrderCommand` is built with all form data

**Card payment flow:**
- `customerClient.paymentClient.createOrder(command)` is called
- If a `stripeSessionId` (Stripe Checkout URL) is returned, the browser redirects to Stripe
- The order id and its `guestAccessToken` are saved via `GuestOrderService`

**Cash payment flow:**
- `customerClient.orderClient.createOrder(command)` is called
- On success, navigates to `/checkout/success?type=cash`
- Nothing is saved to `GuestOrderService`: cash is signed-in only
  ([Paying in cash](/product/business-rules#cash)), so a cash response never carries a
  `guestAccessToken`

`guestAccessToken` is present only on a **guest** booking — always a card booking; an account booking returns `null` and
nothing is saved, because its owner signs in to reach it.
→ [The guest access token](/flows/booking-and-pricing#guest-access-token)

## Rebook Flow

Customers can rebook a previous order. The rebook data is passed via `sessionStorage`:

1. From order detail, customer clicks "Rebook"
2. `RebookParams` (services, packages, rooms, bathrooms, address) are stored in `sessionStorage` under `cleansia_rebook_data`
3. User is navigated to `/order?rebook=true`
4. `OrderWizardComponent.ngOnInit()` reads the rebook data
5. `OrderWizardFacade.prefillFromRebook()` maps previous selections to current available services/packages
6. If any previously selected services/packages are no longer available, a warning dialog is shown

```typescript
interface RebookParams {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  selectedServiceNames: string[];
  selectedPackageNames: string[];
  rooms: number;
  bathrooms: number;
  address?: { street, city, zipCode, countryId, state };
}
```

## Step Navigation

The facade provides navigation methods:

- `nextStep()` / `prevStep()` -- One step on or back, walking past the Plus step when the market sells
  no plan (`stepFrom`). The component's Continue calls `nextStep()` only when nothing is missing
  (`blockingReasons()`).
- `goToStep(n)` -- Jump to a step, ungated in both directions. The step rail (completed steps only)
  and the review's *Edit* use it: looking ahead at a step not yet filled in is not a mistake to prevent.
- `submitOrder()` is the last gate. `firstIncompleteStep()` walks every step's `missingReasons` and,
  if one is short, returns to it with its first reason in a snackbar before anything is sent.

Each navigation scrolls to the top of the page (`window.scrollTo({ top: 0, behavior: 'smooth' })`).

## Form Data Model

```typescript
interface OrderWizardFormData {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  dirtinessLevel: DirtinessLevel | null;   // null until the customer picks one
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  customerPhone: string;
  address: AddressDto;
  addressLatitude: number | null;          // from a lookup pick; null for a typed address
  addressLongitude: number | null;
  addressEnteredManually: boolean;
  propertyType: 'flat' | 'house';          // decides whether floor and flat are asked; not sent
  customerFloor: string;
  customerApartment: string;
  accessMode: string;                      // '' or at_home | keys_handover | door_code | reception
  cleaningDate: Date | null;
  cleaningTime: string;
  paymentType: PaymentType | null;         // null once refused cash was taken away
  extras: Record<string, boolean>;
  specialInstructions: string;             // the note for the cleaner, step 6
  entryInstructions: string;               // how we get in, step 2
  promoCode: string;
  preferredEmployeeId: string | null;      // the Plus preferred-cleaner perk
}
```
