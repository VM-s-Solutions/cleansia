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

| `paymentType` | Value | Behavior |
|---------------|-------|----------|
| `Cash` | `1` | Receipt queued. The order stays `New` + `PaymentStatus.Pending` and becomes offerable immediately; the cleaner's take is what writes `Confirmed` |
| `Card` | `2` | Web: a Stripe Checkout Session is created. Mobile: no session — the client drives a PaymentSheet against the PaymentIntent. Either way the order stays `New` + `PaymentStatus.Pending` and is **not** offerable until the webhook writes `Paid` + `Confirmed` |

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
| At least one service or package | `order.empty` |
| Booked estimate ≤ `MaxBookableOrderSpanHours` (24 h) | `order.span_exceeds_maximum` |
| A membership express waiver the client assumed is still available | `membership.express_waiver.no_longer_available` |
| Server-recalculated price equals the submitted `totalPrice` | `order.total_price.not_match` |

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
  "currencyId": "currency-id",
  "cleaningDate": "2026-04-15T10:00:00Z"
}
```

`cleaningDate` is optional — omit it on the wizard's first step, before a slot is chosen, and the
express-surcharge check is skipped.

**Response:**

```json
{
  "totalPrice": 1500.00,
  "finalPriceAfterDiscount": 1350.00,
  "originalSubtotal": 1500.00,
  "appliedDiscountSource": "Membership",
  "tierDiscountAmount": null,
  "membershipDiscountAmount": 150.00,
  "servicesSubtotal": 1200.00,
  "packagesSubtotal": 0.00,
  "extrasSubtotal": 50.00,
  "expressSurchargeApplied": false,
  "expressSurchargeAmount": 0.00,
  "expressSurchargeWaivedByMembership": true,
  "expressUpgradesRemaining": 1,
  "currencyId": "currency-id",
  "currencyCode": "CZK",
  "exchangeRate": 1.0
}
```

| Field | Meaning |
|---|---|
| `totalPrice` | The **undiscounted** total including any express surcharge. This is the value `CreateOrder` validates against — submit it unchanged as `totalPrice` |
| `finalPriceAfterDiscount` | What the customer pays: discount off the pre-surcharge subtotal, surcharge on top |
| `appliedDiscountSource` | `None` (0), `Tier` (1), `Membership` (2), `Promo` (3), `Combined` (4). Plus and tier are additive, so `Combined` is reachable; `Promo` is not produced by this endpoint |
| `expressSurchargeWaivedByMembership` | Disambiguates `expressSurchargeApplied: false`. Without it, "waived" and "not an express slot at all" look identical |
| `expressUpgradesRemaining` | Waivers left **this calendar month, before this booking** — server-computed. Null when the caller has no membership. A client that counts its own orders disagrees with the server the first time a cancellation releases a slot |

Promo codes are **not** priced here — they are entered at checkout and applied at create time.

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
      "totalPrice": 1500.00
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 10
}
```

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
| 2 | Order exists **and is not held from this caller** (ADR-0036) | `order.not_found` |
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

::: info The preferred-cleaner hold is folded into the existence check
Rules 2 and 5 are separate questions. Until `preferredHoldUntilUtc`, the order's **first seat** is
offered to `preferredEmployeeId` alone; a held order answers `order.not_found`, identical to a missing
one, so the fact that some other cleaner was named cannot be inferred from the refusal. The hold
releases when the deadline passes or as soon as any cleaner is assigned. `preferredEmployeeId` is
never returned on a partner-facing DTO.
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
  "comment": "Excellent service!"
}
```

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
