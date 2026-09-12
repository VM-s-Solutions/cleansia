# Orders

The Order API manages the full lifecycle of cleaning orders: creation, assignment, execution, photo documentation, and completion.

::: info Source Files
- Partner controller: `src/Cleansia.Web.Partner/Controllers/OrderController.cs`
- Mobile controllers: `src/Cleansia.Web.Mobile.Partner/`, `src/Cleansia.Web.Mobile.Customer/`
- Customer controller: `src/Cleansia.Web.Customer/Controllers/OrderController.cs`
- Command/query handlers: `src/Cleansia.Core.AppServices/Features/Orders/`
- Offerability rule: `src/Cleansia.Core.Domain/Orders/OrderAvailability.cs` (ADR-0037)
- Policies: `src/Cleansia.Core.AppServices/Authentication/Policy.cs`
:::

## Order Lifecycle

An order carries **two independent state axes**. `OrderStatus` answers *how far the work has got*;
`PaymentStatus` + `PaymentType` answer *where the money is*. Clients that read only the first will
get the wrong answer for card orders.

```
OrderStatus     [New] --------> [Confirmed] -> [OnTheWay] -> [InProgress] -> [Completed] -> [Receipt]
                   \_______________|______________|_____________|__________> [Cancelled]

PaymentStatus   [Pending] -> [Paid] | [Failed] | [Refunded] | [PartiallyRefunded] | [Disputed]
```

### Order Statuses

| Status | Value | Description |
|---|---|---|
| `New` | `0` | Initial status. **Every** order starts here — cash and card alike |
| `Pending` | `1` | **Dead. Nothing writes it** (ADR-0037 D5) — see below |
| `Confirmed` | `2` | A cleaner took the order, or the Stripe webhook settled a card payment, or the customer confirmed a recurring cash occurrence, or an admin overrode the status |
| `OnTheWay` | `3` | Cleaner is en route to the address |
| `InProgress` | `4` | Cleaner started work |
| `Completed` | `5` | Cleaner finished and submitted completion |
| `Cancelled` | `6` | Order was cancelled |

::: danger `OrderStatus.Pending` has no writer — do not wait for it
The old version of this page described `Pending` as *"card payment initiated, waiting for the Stripe
webhook"*. That state is real, but it is tracked on the **payment** axis, not the status axis
(ADR-0037 D5). A card order awaiting its webhook is:

```json
{ "status": 0, "paymentType": 2, "paymentStatus": 1 }   // New + Card + Pending
```

and once the webhook lands:

```json
{ "status": 2, "paymentType": 2, "paymentStatus": 2 }   // Confirmed + Card + Paid
```

The value stays in the enum because it is on the wire to three generated clients and legacy rows may
hold it. **Keep tolerating it in the conservative direction** — treat a `Pending` order as live, never
as offerable. `AdminOverrideOrderStatus` explicitly refuses it as a target status.
:::

::: warning `Confirmed` does not mean "a cleaner is on this job"
It is written by four paths and only one of them involves a cleaner. To ask whether a cleaner has
actually been pulled onto the job, read the assignment rows (`assignedEmployees`), not the status.
:::

### Payment Statuses

| Status | Value |
|---|---|
| `Pending` | `1` |
| `Paid` | `2` |
| `Failed` | `3` |
| `Refunded` | `4` |
| `Disputed` | `5` |
| `PartiallyRefunded` | `6` |

### Offerability — which orders a cleaner may be offered and may take

`OrderAvailability` is the single rule (ADR-0037). Every surface reads it; none re-derives it:

```csharp
(CurrentStatus == Confirmed || (CurrentStatus == New && PaymentType == Cash))
&& (PaymentStatus == Paid  || (PaymentType == Cash && RecurringTemplateId == null))
```

A plain status list cannot express it. `New` is offerable **only for cash** — on a one-off cash order
the take *is* the confirmation. `Confirmed` is offerable only once nothing scheduled can still retract
the order: the two production retractors are `CleanupStalePendingOrders` (15-min timer; card +
`PaymentStatus.Pending` + non-recurring) and `AutoCancelStaleRecurringOrders` (hourly; recurring +
`PaymentStatus.Pending`), and the money term above is the union of the negations of their WHERE
clauses.

The rule is enforced **at the take**, not only in the list — see [TakeOrder](#takeorder-validations).

## Endpoints

### CreateOrder <Badge type="info" text="Customer + Customer Mobile" />

Creates a new cleaning order with payment. **Only the two customer-facing hosts expose it** —
`Cleansia.Web.Customer` and `Cleansia.Web.Mobile.Customer`. The partner hosts do not have a
create-order route; a cleaner takes existing orders, they do not book them.

```
POST /api/Order/CreateOrder
```

**Auth:** Anonymous (guest booking supported; an authenticated caller gets loyalty/membership pricing)

**Request body:**

```json
{
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com",
  "customerPhone": "+420123456789",

  // Exactly ONE of customerAddress / savedAddressId — supplying both or neither is rejected
  // with `order.address_exactly_one_required`.
  "customerAddress": {
    "street": "Vinohradska 12",
    "city": "Prague",
    "zipCode": "12000",
    "countryId": "country-id",
    "state": null
  },
  "savedAddressId": null,

  "selectedPackageIds": ["pkg-1"],
  "selectedServiceIds": ["svc-1", "svc-2"],
  "rooms": 3,
  "bathrooms": 1,
  "extras": { "ironing": true, "windowCleaning": false },
  "cleaningDate": "2026-04-15T10:00:00Z",
  "paymentType": 1,
  "currencyId": "currency-id",
  "totalPrice": 1500.00,
  "language": "en",

  // All optional; omit for old-client behaviour
  "promoCode": null,
  "referralCode": null,
  "preferredEmployeeId": null,     // a customer REQUEST, not an assignment (ADR-0036)
  "specialInstructions": null,     // free text, max 2000
  "accessInstructions": null       // free text, max 2000
}
```

`currencyId` — optional. The order's currency is the **service address's country's** currency
(owner ruling 2026-09-12); null lets the server derive it, and a value must equal it — send back the
`currencyId` the quote returned for the same country — or create fails as `currency.invalid` before any
pricing. The server re-prices in that currency and compares against `totalPrice`, so a total quoted
in another market fails as `order.total_price.not_match`.

| `paymentType` | Value | Behavior |
|---------------|-------|----------|
| `Cash` | `1` | Receipt queued. The order stays `New` + `PaymentStatus.Pending` and becomes offerable immediately; the cleaner's take is what writes `Confirmed` |
| `Card` | `2` | Web: a Stripe Checkout Session is created. Mobile: no session — the client drives a PaymentSheet against the PaymentIntent. Either way the order stays `New` + `PaymentStatus.Pending` and is **not** offerable until the webhook writes `Paid`; the status stays `New` until a cleaner takes it (ADR-0057) |

::: warning A cash order is not auto-confirmed at creation
`OrderPaymentDispatcher` queues a receipt for cash and nothing else — it writes neither
`OrderStatus.Confirmed` nor `PaymentStatus.Paid`. The only cash path that auto-confirms is
`ConfirmRecurringOrder`, for a recurring occurrence the customer confirms.
:::

::: info Duration cap
The booked estimate (the sum of the selected services' and packages' `estimatedTime`) may not exceed
`BookingPolicy.MaxBookableOrderSpanHours` = **24 h**. `CreateOrder.Validator` rejects above it with a
business error; `OrderFactory` throws as a backstop for callers that skip the validator (the recurring
materializer). The cap also bounds crew size, since `requiredEmployees = ceil(estimatedTime / 120)`.
:::

**Response:**

```json
{
  "id": "order-id",
  "confirmationCode": "ABC123",
  "stripeSessionId": "https://checkout.stripe.com/..." 
}
```

`stripeSessionId` is `null` for cash payments and a Stripe checkout URL for card payments.

::: warning Price validation is a chain, and the order of its rules is load-bearing
`CreateOrder.Validator` runs one `Cascade.Stop` chain over the whole command, so only the **first**
failure is reported:

| Rule | Error key |
|---|---|
| A named `currencyId` equals the service address's country's currency; omitted/null = that currency | `currency.invalid` |
| The address country's currency is offerable — switched on AND priced (`ICurrencyRepository.IsOfferableAsync`) | `currency.invalid` |
| At least one service or package | `order.empty` |
| Booked estimate ≤ `MaxBookableOrderSpanHours` (24 h) | `order.span_exceeds_maximum` |
| A membership express waiver the client assumed is still available | `membership.express_waiver.no_longer_available` |
| Server-recalculated price equals the submitted `totalPrice` | `order.total_price.not_match` |
| A `promoCode`, if any, is sent by a signed-in customer | `promo.requires_account` — error code `PromoCode` |
| The `promoCode`, if any, would be honoured — previewed again in the address currency on the pre-surcharge subtotal | the preview's own reason: `promo.currency_mismatch`, `promo.expired`, `promo.global_limit_reached`, `promo.per_user_limit_reached`, `promo.below_minimum_order_amount`, `promo.not_found`, `promo.inactive`, `promo.not_yet_valid` — error code `PromoCode` |

The two currency rules head the chain, and sit in this chain rather than in a rule of their own,
because the calculator throws on a currency it cannot price in: a separate rule would not stop the two
price rules from running it, and a 400 would become a 500. Separately from this chain, a selected
service or package with no price row in the address country's currency fails as
`order.selected_services.invalid` / `order.selected_package.invalid`.

The two promo rules are last and they **refuse the booking** rather than silently dropping the code: a
customer who applied a code and was shown a discounted price must not be charged the full price
because the code bound to another currency, expired, or hit its cap between apply and submit. A
redemption is recorded against a user, so an anonymous caller who names a code is refused
(`promo.requires_account`) rather than having the code dropped and the full price charged; the honour
rule then previews the code for the signed-in customer, and the handler applies the discount from the
same preview the validator accepted, so the two cannot disagree.

`preferredEmployeeId`, when set, runs its own `Cascade.Stop` chain after these: the caller holds an
active, paid Plus membership (`order.preferred_employee.membership_required`), then the named cleaner is
**eligible** — a completed order together **and** paid in the order's currency, the service address's
country's (`order.preferred_employee.not_eligible`, one key for both terms). A cleaner paid in another
currency does not see the order on their board and cannot take it, so a hold on them could only lapse.
`ChoosePreferredCleaner` runs the same two rules against the existing order's currency; the recurring
template commands run the eligibility rule against the saved address's country's currency.

The waiver rule sits **before** the price rule deliberately: a Plus member who used up their last
free express upgrade between quoting and submitting would otherwise get
`order.total_price.not_match`, which every client renders as a generic "the price changed" — the one
sentence that cannot explain what actually happened.

The server recalculation is not a bare sum. `OrderFactory.ResolveLoy003Discount` resolves discounts
as follows, then `BookingPolicy.ApplyExpressSurcharge` grosses the **discounted** subtotal up:

1. **Cleansia Plus + loyalty tier are additive**, capped at **12 %** of the raw subtotal
   (`MaxCombinedDiscountFraction`). When the cap bites, both are pro-rated down proportionally so
   each source's share stays visible on the receipt instead of one being zeroed.
2. **A promo code replaces the combined amount if it is larger** — never stacks on top. When it
   loses, the order is persisted with `PromoCodeId` and `PromoDiscountAmount` null and the
   redemption is *not* recorded, so a one-shot code is not burned for a discount the customer never
   received.
3. The express surcharge (+20 %) is applied **after** the discount, and only when the booking is in
   the 2–4 h lead window and no membership waiver was reserved.

`QuoteOrder` runs the same ordering, which is why the wizard's quote and the receipted saving cannot
drift apart.

Separate lead-time rules run first on `cleaningDate`: `order.cleaning_date.future`, then
`order.cleaning_date.below_lead_time` (under 2 h lead).
:::

---

### Quote <Badge type="info" text="Customer + Customer Mobile" />

Prices a prospective booking **server-side**. Clients never compute a total themselves.

```
POST /api/Order/Quote
```

**Auth:** Anonymous (rate-limited: `interactive` policy)

**Request body:**

```json
{
  "selectedServiceIds": ["svc-1"],
  "selectedPackageIds": [],
  "rooms": 3,
  "bathrooms": 1,
  "selectedExtraSlugs": ["inside-oven"],
  "countryId": "country-id",
  "currencyId": null,
  "cleaningDate": "2026-04-15T10:00:00Z"
}
```

`cleaningDate` is optional — omit it on the wizard's first step, before a slot is chosen, and the
express-surcharge check is skipped.

`countryId` — optional; the service address's country once the wizard has one. The quote is priced
in that country's currency (owner ruling 2026-09-12: the market is the booking's, not the customer's);
a country the platform does not service is refused as `country.not_serviced`. `currencyId` — optional;
an explicit currency, which wins over `countryId` — it exists so a client can re-quote in exactly the
currency it was first quoted in, not so it can choose one, and on create it is checked against the
address. With neither the quote is in the platform default, which is what the wizard's first step and
the home page's quick quote get. Whichever way it resolves, the currency must be one the platform can
quote in — switched on and carrying at least one catalogue price row — or the quote is refused as
`currency.invalid`. The response's `currencyId` / `currencyCode` say which one was used. Prices are
authored per currency and nothing converts, so a selected service or package with no price row in
that currency is refused as `order.selected_services.invalid` / `order.selected_package.invalid`; an
extra without one is dropped from the extras subtotal. `QuotePlusSavings` takes the same two fields
and resolves them the same way.

**Response:**

```json
{
  "totalPrice": 1500.00,
  "finalPriceAfterDiscount": 1350.00,
  "originalSubtotal": 1500.00,
  "appliedDiscountSource": "Membership",
  "tierDiscountAmount": null,
  "tierDiscountMinOrderAmount": null,
  "membershipDiscountAmount": 150.00,
  "servicesSubtotal": 1200.00,
  "packagesSubtotal": 0.00,
  "extrasSubtotal": 50.00,
  "expressSurchargeApplied": false,
  "expressSurchargeAmount": 0.00,
  "expressSurchargeWaivedByMembership": true,
  "expressUpgradesRemaining": 1,
  "currencyId": "currency-id",
  "currencyCode": "CZK"
}
```

| Field | Meaning |
|---|---|
| `totalPrice` | The **undiscounted** total including any express surcharge. This is the value `CreateOrder` validates against — submit it unchanged as `totalPrice` |
| `finalPriceAfterDiscount` | What the customer pays: discount off the pre-surcharge subtotal, surcharge on top |
| `appliedDiscountSource` | `None` (0), `Tier` (1), `Membership` (2), `Promo` (3), `Combined` (4). Plus and tier are additive, so `Combined` is reachable; `Promo` is not produced by this endpoint |
| `expressSurchargeWaivedByMembership` | Disambiguates `expressSurchargeApplied: false`. Without it, "waived" and "not an express slot at all" look identical |
| `expressUpgradesRemaining` | Waivers left **this calendar month, before this booking** — server-computed. Null when the caller has no membership. A client that counts its own orders disagrees with the server the first time a cancellation releases a slot |
| `tierDiscountMinOrderAmount` | The tier-discount floor the quote judged the order against, so a client can state the same rule. Null when no floor applied — the floor is a platform-default-currency number and is enforced only on an order in that currency |
| `currencyId` / `currencyCode` | The currency the quote was priced in — the one named on the request, else the request's `countryId`'s, else (no country named) the platform default. A named country without a configured currency throws rather than defaulting. There is no exchange rate on the wire; nothing converts |

Promo codes are **not** priced here — they are entered at checkout and applied at create time.

---

### QuotePlusSavings <Badge type="info" text="Customer" />

What this basket would cost with a Cleansia Plus plan the caller does not have — the wizard's Plus
step's "you would save X on today's order". Server-side for the same reason the quote is: the 12 %
combined cap and the un-grossing of the express surcharge are not representable in a client.

```
POST /api/Order/QuotePlusSavings
```

**Auth:** Anonymous (rate-limited: `interactive` policy)

**Request body:**

```json
{
  "selectedServiceIds": ["svc-1"],
  "selectedPackageIds": [],
  "rooms": 3,
  "bathrooms": 1,
  "planCode": "PLUS_MONTHLY",
  "selectedExtraSlugs": ["inside-oven"],
  "countryId": "country-id",
  "currencyId": null,
  "cleaningDate": "2026-04-15T10:00:00Z"
}
```

It prices the same basket the quote does, so it runs the **same rules with the same keys**, and the
validator refuses before the calculator can throw:

| Rule | Error key |
|---|---|
| Every selected service exists **and** has a price row in the resolved currency | `order.selected_services.invalid` |
| Every selected package exists **and** has a price row in the resolved currency | `order.selected_package.invalid` |
| `countryId`, if named, is a serviced country | `country.not_serviced` |
| The resolved currency (named `currencyId`, else the country's, else the platform default) is offerable | `currency.invalid` |
| Booked estimate ≤ `MaxBookableOrderSpanHours` (24 h) | `order.span_exceeds_maximum` |

The span cap is the one `QuoteOrder` and `CreateOrder` draw (ADR-0039 D3.4): a preview must not show
savings on a basket the booking will refuse. An empty selection still previews, as it still quotes.

**Response:**

```json
{
  "wouldSaveAmount": 75.00,
  "wouldPayTotal": 1425.00,
  "currentTotal": 1500.00,
  "currencyCode": "CZK",
  "planCode": "PLUS_MONTHLY"
}
```

`wouldSaveAmount` is the **discount only** — the value of a waived express surcharge is not added to
it.

---

### Catalogue overviews <Badge type="info" text="Customer + Customer Mobile" />

The three lists the booking wizard is built from. They live on their own controllers, not on
`Order`, but they are documented here because they take the same `countryId` the quote does and
answer in the same currency.

```
GET /api/Service/GetOverview?countryId=country-id
GET /api/Package/GetOverview?countryId=country-id
GET /api/Extra/GetOverview?countryId=country-id
```

**Auth:** Anonymous

`countryId` — optional; the service address's country once the wizard has one. A country the platform
does not serve (unknown id, or `Country.IsServiced` false) has **no catalogue**: the overview answers an
empty list, never the default catalogue and never an error, before any currency is resolved. A
serviced country's overview is priced in that country's currency and **withholds** any entry that
has no price row in it or no platform-wide pay config in it — the same two gates the quote enforces, applied before the customer can pick the
entry. Without `countryId` the overview is in the platform default. A named country the seed has not
configured with a real currency does **not** fall through to the default — the resolver throws (owner
ruling 2026-09-12; a serviced country without a currency is a deploy defect, never a market), and the
overview runs no serviced check of its own; the quote is where an unserviced country is refused as
`country.not_serviced`. Each `ServiceListItem` / `PackageListItem` / `ExtraListItem` carries `currencyCode`, so a
surface labels the prices it was sent rather than a currency it assumed.

**Response** (service overview; the others are the same shape with one `price`):

```json
[
  {
    "id": "svc-1",
    "name": "Standard clean",
    "description": "...",
    "category": { "id": "cat-1", "name": "Home" },
    "basePrice": 900.00,
    "perRoomPrice": 150.00,
    "translations": { "cs": { "name": "Standardní úklid", "description": "..." } },
    "currencyCode": "CZK"
  }
]
```

A wizard that learned the country at the address step, after the customer picked from the default
catalogue, re-reads the overview with the country and prunes any selection that is no longer offered;
otherwise the next quote refuses the selection as `order.selected_services.invalid`.

The Partner host's `Service/GetOverview` and `Package/GetOverview` take no `countryId` and answer in
the platform default. `GET /api/Currency/GetOverview` (anonymous, Customer + Customer Mobile) lists
the currencies with `isDefault`, for a surface that has no country yet to learn what the default is.

---

### PromoCode/Validate <Badge type="info" text="Customer + Customer Mobile" />

The checkout preview of a promo code. It answers the question `CreateOrder` will ask again, so it
must be asked in the same currency.

```
POST /api/PromoCode/Validate
```

**Auth:** `CanRedeemPromoCode` (Customer)

**Request body:**

```json
{
  "code": "SAVE10",
  "orderSubtotal": 1500.00,
  "currencyId": "currency-id"
}
```

`orderSubtotal` is the quote's **pre-surcharge** subtotal — the base a discount is judged on.
`currencyId` — optional; the quote's `currencyId`, which is the address country's currency. Null
resolves to the platform default, which is only right for a quote that had no country. A code with a
minimum is bound to one currency (its own, or the platform default when it names none), and on a
subtotal in any other it answers `CurrencyMismatch` before the minimum is compared.

**Response:**

```json
{ "isValid": false, "discountAmount": null, "errorCode": "CurrencyMismatch" }
```

`errorCode` is the enum name (`NotFound`, `Inactive`, `Expired`, `NotYetValid`, `GlobalLimitReached`,
`PerUserLimitReached`, `BelowMinimumOrderAmount`, `CurrencyMismatch`), null when valid. The same eight
reasons refuse the booking on create as `promo.*` keys — a client that previewed in the wrong currency
sees the mismatch at submit instead of a silently dropped discount.
→ [Money constants](/product/business-rules#money-constants)

---

### GetPaged

Returns a paginated list of orders.

```
GET /api/Order/GetPaged?page=1&pageSize=10
```

**Auth:** `CanViewPagedOrder` (Admin, Employee) or `CanViewPagedUserOrder` (Customer -- own orders)

**Response:**

```json
{
  "items": [
    {
      "id": "order-id",
      "customerName": "Jane Doe",
      "cleaningDate": "2026-04-15T10:00:00Z",
      "status": "Confirmed",
      "totalPrice": 1500.00,
      "hasReview": false
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 10
}
```

**Currency.** `OrderFilter.CurrencyId` (query parameter `currencyId`) pins the page to one currency;
no existence check, an unknown id is an empty page. Without it a sort on `totalPrice` is served
*within* currency — the server leads the sort with `currencyId`, so rows arrive grouped by currency and
ordered by price inside each group, because 150 EUR does not file below 3 000 CZK. `currencyId` is
also accepted as a sort field. For a **non-admin caller** the list is additionally scoped to the
currency the cleaner is paid in (their work country's): an order in another currency is not on their
board at all, unless they are already assigned to it. → [Business rules](/product/business-rules#cleaner-currency)

**`hasReview`** exists so a client can decide whether to ask for a review **without fetching the order
detail**. The mobile apps raise the completion prompt off the list, and before this flag the only way to
know a review already existed was a second round trip per order — which meant the prompt either fired
late or fired again for a customer who had already answered.

---

### GetById

Returns full details of a single order.

```
GET /api/Order/GetById?id=order-id
```

**Auth:** `CanViewOrderDetail` (Authenticated -- all roles)

**Response:** `OrderItem` object with full order details, address, services, packages, status history.

---

### Lookup <Badge type="info" text="Customer API only" />

Looks up an order by order number and email (for anonymous tracking).

```
GET /api/Order/Lookup?orderNumber=CLN-2026-001&email=jane@example.com
```

**Auth:** Anonymous (rate-limited: 10 requests/minute per IP)

---

### LookupBatch <Badge type="info" text="Customer API only" />

Looks up multiple orders at once.

```
POST /api/Order/LookupBatch
```

**Auth:** Anonymous (rate-limited: 10 requests/minute per IP)

**Request body:**

```json
{
  "lookups": [
    { "orderNumber": "CLN-2026-001", "email": "jane@example.com" },
    { "orderNumber": "CLN-2026-002", "email": "jane@example.com" }
  ]
}
```

---

### TakeOrder

Employee accepts/claims an order.

```
POST /api/Order/TakeOrder
```

**Auth:** `CanTakeOrder` (Employee)

**Request body:**

```json
{
  "orderId": "order-id"
}
```

**Response:** `TakeOrder.Response` with updated order state.

#### TakeOrder Validations

`TakeOrder.Validator` is **one ordered `Cascade.Stop` chain**, so exactly one error comes back — the
first that fails. The order of the rules is deliberate: a cancelled order with a free seat must say
*the job is gone*, not *the job is full*.

| # | Rule | Error key |
|---|---|---|
| 1 | `orderId` present | `common.required` |
| 2 | Order exists, **is not held from this caller** (ADR-0036), **and is in the currency the caller is paid in** — or the caller is already on it (`OrderVisibility.OpenTo`) | `order.not_found` |
| 3 | Not cancelled | `order.already_cancelled` |
| 4 | Not completed | `order.already_completed` |
| 5 | **Offerable** — the ADR-0037 rule, both axes | `order.not_takeable` |
| 6 | A seat is free (`assignedEmployees.Count < maxEmployees`) | `order.no_available_spots` |
| 7 | Caller resolves to an employee | `employee.not_found` |
| 8 | Employee has an address on file | `employee.profile_incomplete` |
| 9 | `ContractStatus == Approved` | `employee.not_approved` |
| 10 | Not already assigned to this order | `order.employee_already_assigned` |
| 11 | Weekly cap, **only if an admin set one** on this cleaner (`Employee.WeeklyOrderLimit`; null = unlimited, the default) | `order.weekly_limit_reached` |
| 12 | No scheduling overlap with the employee's live commitments | `order.time_conflict` |

::: info The preferred-cleaner hold and the currency are folded into the existence check
Rules 2 and 5 are separate questions. Until `preferredHoldUntilUtc`, the order's **first seat** is
offered to `preferredEmployeeId` alone; a held order answers `order.not_found`, identical to a missing
one, so the fact that some other cleaner was named cannot be inferred from the refusal. The hold
releases when the deadline passes or as soon as any cleaner is assigned. `preferredEmployeeId` is
never returned on a partner-facing DTO. The currency term answers the same way for the same reason: an
order in a currency the caller is not paid in was never on their board, so from their side it does not
exist (owner ruling 2026-09-12 — a cleaner is paid in the currency of the country they work in).
:::

The employee is always derived server-side from the caller, never taken from the request body. On
failure the response is a `400` RFC 7807 Problem Details; clients resolve `errors[0]` under the
`api.*` i18n namespace.

---

### StartOrder

Employee starts working on the order (begins the timer).

```
POST /api/Order/StartOrder
```

**Auth:** `CanStartOrder` (Employee)

**Request body:**

```json
{
  "orderId": "order-id"
}
```

**Refused when the job is more than 60 minutes away** — `order.too_early_to_start`. The same gate is on
`NotifyOnTheWay`, because both write to the customer's lock screen. Late is never blocked. The check is
the **last** rule on the command, so a cleaner who is not assigned learns only that, and nothing about
when the job is scheduled. → [business rules](/product/business-rules#start-grace-window)

---

### CompleteOrder

Employee marks the order as completed.

```
POST /api/Order/CompleteOrder
```

**Auth:** `CanCompleteOrder` (Employee)

**Request body:**

```json
{
  "orderId": "order-id"
}
```

---

## Photo Endpoints

### UploadPhoto

Uploads a single photo for an order (before or after cleaning).

```
POST /api/Order/UploadPhoto
```

**Auth:** `CanUploadOrderPhoto` (Employee)

**Request body:**

```json
{
  "orderId": "order-id",
  "base64Image": "data:image/jpeg;base64,...",
  "category": "Before",
  "fileName": "kitchen.jpg"
}
```

---

### SavePhotos

Batch-saves multiple photos for an order.

```
POST /api/Order/SavePhotos
```

**Auth:** `CanUploadOrderPhoto` (Employee)

---

### GetPhotos

Retrieves all photos for an order.

```
GET /api/Order/GetPhotos?orderId=order-id
```

**Auth:** `CanViewOrderPhotos` (Authenticated -- all roles)

**Response:** Photo URLs are returned as **SAS URLs** (Azure Blob Storage Shared Access Signatures) with a **1-hour expiry**. Clients must handle URL refresh if photos are displayed for extended periods.

---

### DeletePhoto

Deletes a specific order photo.

```
DELETE /api/Order/DeletePhoto?photoId=photo-id
```

**Auth:** `CanDeleteOrderPhoto` (Employee)

---

## Notes and Issues

### AddNote

Adds a note to an order (visible to admins and the assigned employee).

```
POST /api/Order/AddNote
```

**Auth:** `CanAddOrderNote` (Employee)

**Request body:**

```json
{
  "orderId": "order-id",
  "note": "Customer requested extra attention to kitchen floor."
}
```

---

### ReportIssue

Reports a problem encountered during the cleaning.

```
POST /api/Order/ReportIssue
```

**Auth:** `CanReportOrderIssue` (Employee)

**Request body:**

```json
{
  "orderId": "order-id",
  "issue": "Lock on back door was broken, could not access balcony."
}
```

---

## Review and Receipt

### SubmitReview <Badge type="info" text="Customer API only" />

Customer submits a review after order completion.

```
POST /api/Order/SubmitReview
```

**Auth:** `CanSubmitOrderReview` (Customer)

**Request body:**

```json
{
  "orderId": "order-id",
  "rating": 5,
  "comment": "Excellent service!",
  "tags": [1, 3, 4]
}
```

**`tags`** is optional and carries **integers**, not strings — the enum's number *is* the wire contract,
so a value may never be renumbered once shipped. Positive tags occupy the 1–10 band and negative tags
11–20, with room left in each so a later insert never shifts a shipped value.

| # | Tag | | # | Tag |
|---|---|---|---|---|
| 1 | On time | | 11 | Arrived late |
| 2 | Thorough | | 12 | Missed areas |
| 3 | Friendly | | 13 | Felt rushed |
| 4 | Careful with belongings | | 14 | Extra not done |
| 5 | Extras done well | | 15 | Instructions not followed |
| 6 | Followed instructions | | 16 | Unprofessional |
| 7 | Great photos | | 17 | Smell or products |
| | | | 18 | Crew smaller than booked |

Polarity must match the rating: **4–5 stars accepts only positive tags, 1–3 stars only negative ones.**
A mismatched, duplicated or unknown tag is **refused**, not silently dropped — a client that sends a tag
the server does not recognise has a bug, and swallowing it would hide the bug while corrupting the
counts. At most **4** tags per review.

| Condition | Error |
|---|---|
| More than 4 tags | `order.review.too_many_tags` |
| Same tag twice | `order.review.duplicate_tag` |
| Value not in the enum | `order.review.unknown_tag` |
| Positive tag on a 1–3 star review, or the reverse | `order.review.tag_rating_mismatch` |
| A review already exists for this order | `order.review.already_exists` |

Tags are stored as `jsonb` on `OrderReview`, which is what makes *"the top three complaints this month"*
a query rather than a text search. The web customer app submits reviews without tags today; the two
mobile apps render the chips.

::: info Rating Recalculation
When a review is submitted, the `SubmitOrderReview` handler recalculates the assigned employee's `AverageRating` across all their reviewed orders. It is a displayed and sortable figure only — it no longer gates anything. Until 2026-08-22 it drove a 3/6/10 weekly order cap, which meant a cleaner with no reviews yet (rating `0`) was capped at three jobs a week; the cap is now a deliberate per-cleaner setting an admin applies, and is unset for everyone by default.
:::

---

### DownloadReceipt

Downloads the order receipt as a PDF file.

```
GET /api/Order/DownloadReceipt?orderId=order-id
```

**Auth:** `CanViewOrderDetail` (Authenticated -- all roles)

**Response:** Binary PDF file (`application/pdf`).

::: tip Receipt Generation
Receipts are generated asynchronously via an Azure Queue message (`GenerateReceipt`) processed by Azure Functions. The PDF is stored in Azure Blob Storage.
:::

## Error Responses

All endpoints return RFC 7807 Problem Details on failure:

| Status | Meaning |
|--------|---------|
| `200` | Success |
| `400` | Validation error |
| `401` | Not authenticated |
| `403` | Insufficient permissions |
