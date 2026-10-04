# Order Wizard

The order wizard is a multi-step booking flow implemented in the `@cleansia-customer/order-wizard` library. It guides customers through selecting services, entering address details, choosing a date/time, selecting payment method, and reviewing the order.

## Architecture

The wizard uses the **Component + Facade** pattern:

- `OrderWizardComponent` -- UI and user interaction
- `OrderWizardFacade` -- Business logic, API calls, state management
- `OrderWizardFormData` -- Type-safe form model

All state is managed via Angular signals (no NgRx for wizard-local state).

## Wizard Steps

### Step 0: Services & Packages

The customer selects from available cleaning services and/or packages, and specifies the number of rooms and bathrooms.

**Data loaded on init:**
- Services list (dispatched via `loadCustomerServices()` NgRx action)
- Packages list (dispatched via `loadCustomerPackages()` NgRx action)
- Countries list (for address country dropdown)

**Fields:**

| Field | Type | Default | Validation |
|---|---|---|---|
| `selectedServiceIds` | `string[]` | `[]` | At least one service or package required |
| `selectedPackageIds` | `string[]` | `[]` | (combined with above) |
| `rooms` | `number` | `1` | Minimum 1 in the picker; API maximum 8 |
| `bathrooms` | `number` | `1` | Minimum 1 in the picker; API maximum 4 |

The same upper limits apply to booking, price quotes, Plus-savings previews and recurring-template
creation/update. The API returns `order.size_exceeds_maximum` when either is exceeded. The Android and
iOS steppers stop at the same limits and state them under the size row →
[Room selection](/flows/booking-and-pricing#room-selection-and-start-times).

Services and packages support **translations** -- the component reads the user's current locale to display translated names/descriptions.

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

### Step 1: Address & Contact

The customer enters their delivery address and contact information.

**Authenticated users** get profile data pre-filled (name, email, phone) and can select from saved addresses stored in localStorage.

**Fields:**

| Field | Validation |
|---|---|
| `customerFirstName` | Required, 2-50 chars |
| `customerLastName` | Required, 2-50 chars |
| `customerEmail` | Required, valid email, max 50 chars |
| `customerPhone` | Required, matches `^[+]?[\d\s()-]{6,20}$` |
| `address.street` | Required, 5-255 chars |
| `address.city` | Required, 2-100 chars |
| `address.zipCode` | Required, matches `^[\d\s-]{3,20}$` |

::: tip Saved Addresses
Authenticated users can save addresses to localStorage (`cleansia_saved_addresses`). When selecting a saved address, validation is relaxed to only check non-empty values. New addresses can optionally be saved for future use.
:::

### Step 2: Date & Time

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

### Step 3: Payment Method

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

### Step 4: Review & Submit

A summary of the entire order is displayed. The customer can navigate back to any previous step to make changes.

Beneath the terms block the step states, unconditionally — whether or not the account has consented
before, signed in or guest — that *by confirming the order you conclude a contract for work with the
cleaner on these terms*, linking `/work-contract` (`pages.order.work_contract_notice`). It is an
information line, not a tick: the customer's half of that contract is the terms consent plus the
contract text the server stamps on the order at booking; the cleaner's half is written when they take
the job. → [Business rules — the contract for work](/product/business-rules#work-contract)

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

- `nextStep()` -- Advance to the next step (with validation via `canProceed()`)
- `prevStep()` -- Go back one step
- `goToStep(n)` -- Jump to a specific step

Each navigation scrolls to the top of the page (`window.scrollTo({ top: 0, behavior: 'smooth' })`).

## Form Data Model

```typescript
interface OrderWizardFormData {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  customerPhone: string;
  address: AddressDto;
  cleaningDate: Date | null;
  cleaningTime: string;
  paymentType: PaymentType | null;   // null once refused cash was taken away
  extras: Record<string, boolean>;
  specialInstructions: string;
  entryInstructions: string;
}
```
