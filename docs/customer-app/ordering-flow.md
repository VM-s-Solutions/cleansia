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

The customer picks a cleaning date and time slot.

**Date selection:**
- Minimum date: today (if time slots remain) or tomorrow
- Uses PrimeNG `DatePicker`

**Time selection:**
- 30-minute slots from 07:00 to 20:00
- If today is selected, past time slots are filtered out
- Default: `09:00`
- If the selected time becomes unavailable (e.g., date changes to today), it auto-resets to the first available slot

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

Price is computed reactively via a `computed()` signal in the facade:

```typescript
totalPrice = computed(() => {
  let total = 0;
  // Services: basePrice + perRoomPrice * (rooms + bathrooms)
  for (const id of data.selectedServiceIds) {
    const svc = allServices.find(s => s.id === id);
    if (svc) {
      total += svc.basePrice + svc.perRoomPrice * (data.rooms + data.bathrooms);
    }
  }
  // Packages: flat price
  for (const id of data.selectedPackageIds) {
    const pkg = allPackages.find(p => p.id === id);
    if (pkg) {
      total += pkg.price;
    }
  }
  return total;
});
```

::: info Pricing Formula
**Service price** = `basePrice + perRoomPrice * (rooms + bathrooms)`

**Package price** = flat `price` (not affected by rooms/bathrooms)

**Total** = sum of all selected service prices + sum of all selected package prices
:::

Prices are formatted in CZK using `Intl.NumberFormat('cs-CZ')`.

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
