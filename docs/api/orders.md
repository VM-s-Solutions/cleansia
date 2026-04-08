# Orders

The Order API manages the full lifecycle of cleaning orders: creation, assignment, execution, photo documentation, and completion.

::: info Source Files
- Partner/Mobile controllers: `src/Cleansia.Web/Controllers/OrderController.cs`, `src/Cleansia.Web.Mobile/Controllers/OrderController.cs`
- Customer controller: `src/Cleansia.Web.Customer/Controllers/OrderController.cs`
- Command/query handlers: `src/Cleansia.Core.AppServices/Features/Orders/`
- Policies: `src/Cleansia.Core.AppServices/Authentication/Policy.cs`
:::

## Order Lifecycle

```
[New] -> [Pending] -> [Confirmed] -> [InProgress] -> [Completed]
            |                                             |
            v                                             v
       [Cancelled]                                   [Receipt]
```

### Order Statuses

| Status | Value | Description |
|---|---|---|
| `New` | `0` | Initial status when an order is created |
| `Pending` | `1` | Card payment initiated, waiting for Stripe webhook |
| `Confirmed` | `2` | Set when a cleaner takes the order (or cash payment immediately confirmed) |
| `InProgress` | `3` | Employee started cleaning |
| `Completed` | `4` | Employee finished and submitted completion |
| `Cancelled` | `5` | Order was cancelled |

## Endpoints

### CreateOrder

Creates a new cleaning order with payment. Available on all API surfaces (Partner, Customer, Mobile).

```
POST /api/Order/CreateOrder
```

**Auth:** Anonymous (Customer API), Authenticated (Partner/Mobile API)

**Request body:**

```json
{
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com",
  "customerPhone": "+420123456789",
  "customerAddress": {
    "street": "Vinohradska 12",
    "city": "Prague",
    "zipCode": "12000",
    "countryId": "country-id",
    "state": null
  },
  "selectedPackageIds": ["pkg-1"],
  "selectedServiceIds": ["svc-1", "svc-2"],
  "rooms": 3,
  "bathrooms": 1,
  "extras": { "ironing": true, "windowCleaning": false },
  "cleaningDate": "2026-04-15T10:00:00Z",
  "paymentType": 0,
  "currencyId": "currency-id",
  "totalPrice": 1500.00,
  "language": "en"
}
```

| `paymentType` | Value | Behavior |
|---------------|-------|----------|
| `Cash` | `0` | Order immediately confirmed, receipt queued |
| `Card` | `1` | Stripe checkout session created, order stays Pending |

**Response:**

```json
{
  "id": "order-id",
  "confirmationCode": "ABC123",
  "stripeSessionId": "https://checkout.stripe.com/..." 
}
```

`stripeSessionId` is `null` for cash payments and a Stripe checkout URL for card payments.

::: warning Price Validation
The backend recalculates the total price from selected services/packages and verifies it matches the submitted `totalPrice`. If they don't match, the request is rejected with a `TotalPriceNotMatch` error.
:::

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

The backend enforces several checks before allowing an employee to take an order:

- **`RequireCompleteProfile` filter** -- The order endpoints use this filter to ensure the employee has a complete profile before accessing order operations
- **Weekly order limit** -- Based on the employee's `AverageRating`:
  - 0 -- 3.5: max 3 orders/week
  - 3.6 -- 4.5: max 6 orders/week
  - 4.6+: max 10 orders/week
- **Time conflict detection** -- Checks for scheduling overlaps with the employee's existing assigned orders
- **Document checks** -- Employee must have approved required documents
- **Profile checks** -- Employee must have a complete profile (`isProfileComplete`)

If any validation fails, a `400` response is returned with a descriptive error code.

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
When a review is submitted, the `SubmitOrderReview` handler recalculates the assigned employee's `AverageRating` across all their reviewed orders. This updated rating affects the employee's weekly order limit for `TakeOrder`.
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
