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
| `rooms` | `number` | `1` | Minimum 1, increment/decrement buttons |
| `bathrooms` | `number` | `1` | Minimum 1, increment/decrement buttons |

Services and packages support **translations** -- the component reads the user's current locale to display translated names/descriptions.

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

The customer picks a cleaning date and a **1-hour arrival window**. Scheduling is still on a 30-min
grid internally; the window's start is the target start.

**Date selection:**
- Minimum date: today (if time slots remain) or tomorrow
- Uses PrimeNG `DatePicker`

**Time selection** — the constants mirror `BookingPolicy` on the backend and must be kept in sync
(`order-wizard.models.ts:136-147`):

| Constant | Value |
|---|---|
| `WINDOW_DURATION_MINUTES` | 60 |
| `FIRST_WINDOW_HOUR` / `LAST_WINDOW_HOUR` | 8 / 20 (inclusive start, exclusive end → 12 windows) |
| `EXPRESS_LEAD_TIME_HOURS` | 2 — below this, nothing is bookable |
| `STANDARD_LEAD_TIME_HOURS` | 4 — between 2 and 4 h, the slot is bookable **with surcharge** |

Each option is annotated `available` | `express` | `unavailable` by `filterTimeOptionsForToday`.
Only the arrival time is shown ("10:00"), never the window range — a job can run longer than an hour
and "10:00 – 11:00" reads as an end time.

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
| Cash | `PaymentType.Cash` | Pay on delivery |

Default: `PaymentType.Card`

### Step 4: Review & Submit

A summary of the entire order is displayed. The customer can navigate back to any previous step to make changes.

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
- The guest order ID + email is saved via `GuestOrderService`

**Cash payment flow:**
- `customerClient.orderClient.createOrder(command)` is called
- On success, navigates to `/checkout/success?type=cash`
- The guest order ID + email is saved via `GuestOrderService`

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
  paymentType: PaymentType;
  extras: Record<string, boolean>;
  specialInstructions: string;
  entryInstructions: string;
}
```
