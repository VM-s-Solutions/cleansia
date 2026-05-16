# Wave 3 — Recurring Cleanings Implementation Spec

> Pre-decided product shape: **draft + 24h confirm with Pending Order rows**
> (Option B + Path 2). Customer creates a recurring template; the materializer
> creates real `Pending` Order rows 24-48h ahead so cleaners see them in their
> Available tab; customer gets a push to confirm + pay 24h ahead via existing
> Stripe Checkout flow.
>
> Scope estimate: **5-8 focused days**, split into 3 sub-PRs.
> Each sub-PR is independently shippable and testable.

---

## Problem statement

`MaterializeRecurringBookings.Handler` is a stub at
[MaterializeRecurringBookings.cs:68](src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs#L68)
that logs occurrences and returns `OrdersCreated: 0`. No real Orders ever get
created from templates, so:
- Cleaners never see recurring orders in their Available tab (REC-002).
- Customers' "Plus recurring booking" perk is non-functional past the
  template-creation step.

The materializer needs to:
1. Compute occurrences within a 7-day horizon (already implemented).
2. For each occurrence not yet materialized, create a real `Pending` Order
   linked to the template's user, services, packages, and address.
3. Mark `template.LastMaterializedFor` so the next sweep doesn't double-create.
4. (Wave 3.2) Schedule a push 24h ahead reminding the customer to confirm.

The blocking refactor is that **`CreateOrder.Handler`** at
[CreateOrder.cs:231-510](src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs)
is a 532-LOC handler with 19 injected dependencies. The materializer needs to
share most of the order-creation logic but operates without a JWT context, so
extracting an `OrderFactory` is the natural seam. (This also resolves BE-002 —
the "spaghetti CreateOrder" task — by extracting the parts that don't need
JWT/Stripe/snackbar concerns.)

---

## Decided product shape

| Decision | Choice | Rationale |
|---|---|---|
| **Payment model** | Draft + 24h confirm (Option B) | Smallest scope, reuses existing Stripe Checkout, no new failed-charge handling |
| **Materializer output** | Pending Order rows (Path 2) | Matches REC-002 — cleaners see inventory ahead of time |
| **Customer confirm UX** | Push 24h ahead + tap → existing booking confirm screen pre-filled | Reuses Stripe Checkout + booking flow; minimal new UI |
| **Cleaner take semantics** | Pending Orders takeable like any other (current behavior) | TakeOrder validator already accepts Pending status |
| **Cancel before confirm** | Customer can cancel from the push or the order detail | Order Cancel flow already exists |
| **Discount snapshot** | Computed at materialize time, refreshed at confirm time if outdated | Tier/membership status can change between the two timestamps |

---

## Sub-PR breakdown

### Wave 3.1 — OrderFactory + materializer [SHIPPED]

**Goal:** materializer creates real Pending Orders. Cleaner sees them.
No customer-side push or confirm flow yet — admin can manually verify by
querying `Orders WHERE Status = Pending AND TemplateId IS NOT NULL`.

**Shipped:** 2026-05 session. See [post-android-followups.md "Wave 3.1 (shipped)"](post-android-followups.md) for the line-by-line summary. EF migration still owed by owner: `dotnet ef migrations add AddOrderRecurringTemplateId`.

#### Tasks

**TASK-3.1.A — Extract `IOrderFactory` interface**
- File: `src/Cleansia.Core.AppServices/Features/Orders/IOrderFactory.cs` (new)
- Surface:
  ```csharp
  public interface IOrderFactory
  {
      Task<Order> CreateOrderAsync(CreateOrderInput input, CancellationToken ct);
  }

  public record CreateOrderInput(
      string UserId,           // explicit — no JWT lookup inside factory
      string CustomerName,
      string CustomerEmail,
      string CustomerPhone,
      Address Address,
      int Rooms,
      int Bathrooms,
      Dictionary<string, bool> Extras,
      DateTime CleaningDate,
      PaymentType PaymentType,
      string CurrencyId,
      IEnumerable<string> SelectedServiceIds,
      IEnumerable<string> SelectedPackageIds,
      decimal? RawSubtotalOverride,    // null = compute via pricingCalculator
      string? PromoCode,                // null OK
      string? PreferredEmployeeId,
      string? RecurringTemplateId,      // links order back to template
      // …
  );
  ```

**TASK-3.1.B — `OrderFactory` implementation**
- File: `src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs` (new)
- Lifts the address-resolution + currency-lookup + pricing + discount
  best-of-three + status track + VAT + Order entity construction from
  CreateOrder.Handler.
- Does NOT do: JWT lookup, Stripe session creation, queue messages for
  receipt generation, snackbar messages, referral acceptance.
- DOES do: pricing calculation, discount math, Order.Create, AddOrderStatus,
  SetVatBreakdown.

**TASK-3.1.C — Refactor CreateOrder.Handler to use OrderFactory**
- Handler keeps: JWT user resolution, referral acceptance, Stripe session
  creation, queue messages (receipt generate, push dispatch), snackbar
  messaging.
- Handler delegates: address resolution → factory; pricing + discount →
  factory; Order construction → factory.
- Net result: CreateOrder.Handler.Handle drops from ~300 lines to ~120.
- **Risk:** This is the booking flow we just stabilized in Wave 1. Test
  end-to-end on customer mobile + web before merging.

**TASK-3.1.D — Add `Order.RecurringTemplateId` field**
- File: `src/Cleansia.Core.Domain/Orders/Order.cs`
- New nullable string FK column. EF migration needed.
- Order.Create gets new optional param.
- Lets us trace "this Pending order came from template X."

**TASK-3.1.E — `MaterializeRecurringBookings.Handler` wires to factory**
- For each computed occurrence:
  ```csharp
  var input = new CreateOrderInput(
      UserId: template.UserId,
      CustomerName: $"{template.User.FirstName} {template.User.LastName}",
      CustomerEmail: template.User.Email,
      CustomerPhone: template.User.PhoneNumber!,
      Address: savedAddress.Address,  // resolve from template.SavedAddressId
      Rooms: template.Rooms,
      Bathrooms: template.Bathrooms,
      Extras: new(),
      CleaningDate: occurrence,
      PaymentType: template.PaymentType,
      CurrencyId: defaultCurrency.Id,
      SelectedServiceIds: template.SelectedServiceIds,
      SelectedPackageIds: template.SelectedPackageIds,
      RawSubtotalOverride: null,
      PromoCode: null,
      PreferredEmployeeId: null,
      RecurringTemplateId: template.Id);
  var order = await orderFactory.CreateOrderAsync(input, ct);
  template.MarkMaterializedFor(occurrence);
  ```
- Order goes in with `PaymentStatus.Pending` and `OrderStatus.New`.
- Increment `OrdersCreated` counter in the Response.

**TASK-3.1.F — EF migration**
- New column: `Orders.RecurringTemplateId` (nullable FK to RecurringBookingTemplates).
- **Owner-only step.** Generate via `dotnet ef migrations add AddOrderRecurringTemplateId` and apply manually.

**TASK-3.1.G — Verify cleaner-side (REC-002)**
- Pending Orders with no assigned employees should appear in the partner
  Available tab via the existing `GetPagedOrders` filter — check that
  filter doesn't exclude `RecurringTemplateId IS NOT NULL` (it shouldn't,
  but verify).
- Smoke test: create a template via dev seed, run the materializer, check
  partner Available tab shows the spawned order, take it, complete it.

**TASK-3.1.H — Test coverage**
- Unit test: OrderFactory.CreateOrderAsync produces Pending Order with
  correct discount math.
- Integration test (if EF mock infra allows): materializer end-to-end
  creates an Order, marks template, second run is no-op.

**Manual steps:**
- ⚠️ **MANUAL_STEP**: Owner runs `dotnet ef migrations add` + `database update`.
- ⚠️ **MANUAL_STEP**: Owner regenerates NSwag clients if any DTO surface changes.

---

### Wave 3.2 — Push notification + customer reminder UI [SHIPPED]

**Goal:** customer gets a "your cleaning is tomorrow — confirm" push 24h ahead
of each materialized order, deep-linking to the existing booking confirm flow.

**Shipped:** see [post-android-followups.md "Wave 3.2 (shipped)"](post-android-followups.md) for the line-by-line summary. Combined EF migration with 3.1: `AddOrderRecurringFields` covers both `RecurringTemplateId` and `RecurringReminderSentAt`. Picked Path B (second daily timer trigger sweeping the orders table) over Path A (scheduled-future queue messages) — no new infra needed.

#### Tasks

**TASK-3.2.A — `recurring.scheduled` push event**
- Backend: emit `SendPushNotificationMessage` from `MaterializeRecurringBookings.Handler`
  for each materialized occurrence, scheduled to fire at `occurrence - 24h`.
- Today's push pipeline doesn't support scheduled-future delivery — pushes
  fire immediately. Two options:
  - **A.** Add `ScheduledFor: DateTime?` field to `SendPushNotificationMessage`,
    have the dispatcher Function delay or use a separate scheduled-send queue.
  - **B.** Run a second daily job that sweeps Pending recurring Orders 24h
    out and fires the push then. Simpler — no new queue infra.
  - **Recommend B.**

**TASK-3.2.B — Mobile FCM template**
- File: `core/notifications/CleansiaFirebaseMessagingService.kt`
- New entry in `templateFor()` for `recurring.scheduled`:
  ```kotlin
  "recurring.scheduled" -> Triple(
      R.string.notification_recurring_scheduled_title,
      R.string.notification_recurring_scheduled_body,
      NotificationCategoryDto.RecurringScheduled,
  )
  ```
- New i18n keys × 5 locales.

**TASK-3.2.C — Deep link handler**
- File: `core/notifications/NotificationDeepLink.kt`
- For `recurring.scheduled` event with `orderId` payload, route to
  Order Detail screen with a "Confirm this booking" CTA visible.
- Or alternatively, route to a dedicated `Routes.ConfirmRecurringOrder(orderId)`
  screen — cleaner UX but a new screen to build.
- **Recommend:** route to Order Detail; surface the Confirm CTA when
  `order.recurringTemplateId != null && order.paymentStatus == Pending`.

**TASK-3.2.D — Order Detail confirm CTA**
- File: `features/orders/OrderDetailScreen.kt`
- When the order matches the conditions above, show a primary "Confirm and
  pay" button that navigates to the existing booking confirm flow with the
  order's data pre-filled.
- Handled in Wave 3.3 (the actual confirm endpoint).

---

### Wave 3.3 — Customer confirm flow [SHIPPED]

**Goal:** customer can confirm + pay an existing Pending Order through the
existing Stripe PaymentSheet flow (mobile) — Card and Cash both supported.

**Shipped:** see [post-android-followups.md "Wave 3.3 (shipped)"](post-android-followups.md) for the line-by-line summary. Single endpoint serves both flows; the AutoCancelStaleRecurringOrders sweep runs hourly and reuses the existing OrderStatus.Cancelled + `order.cancelled` push.

#### Tasks

**TASK-3.3.A — `ConfirmRecurringOrder` command**
- File: `src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs` (new)
- Surface:
  ```csharp
  public record Command(string OrderId) : ICommand<Response>;
  public record Response(string? StripeSessionId, string? CheckoutUrl);
  ```
- Handler:
  - Loads the Order, validates ownership (UserId == JWT user) + state
    (Pending + linked to template).
  - Refreshes pricing if the discount snapshot is older than 24h (loyalty
    tier may have advanced; membership may have expired).
  - For Card payment: creates a Stripe Checkout Session, returns the URL.
  - For Cash payment: marks the order Confirmed immediately, returns
    `null` for both URL fields.

**TASK-3.3.B — Customer mobile confirm UX**
- New `ConfirmRecurringOrderApi` in `core/orders/`.
- Order Detail's Confirm CTA invokes the API; on Card response, opens
  Stripe PaymentSheet (same as today's booking flow); on Cash, navigates
  to a success screen.

**TASK-3.3.C — Cancel-before-confirm**
- Existing `CancelOrder` flow already handles Pending orders. Verify the
  cancel reason picker on Order Detail is shown for these.

**TASK-3.3.D — Auto-cancel on missed confirm**
- Background sweep: any Pending recurring Order older than `cleaningDate - 1h`
  with no payment confirmed gets auto-cancelled with reason "missed confirm window".
- Customer gets a push: "Your weekly cleaning was cancelled — confirm earlier
  next time."
- Frees the cleaner's slot on the Available tab.

**Manual steps:**
- ⚠️ **MANUAL_STEP**: Owner regenerates NSwag clients (new ConfirmRecurringOrder endpoint).

---

## Out of scope (future waves)

- **Auto-charge from saved card** (Option A in original Wave 3 decision).
  Defer until customer demand signal or Plus value-prop push needs it.
- **Cleaner-side recurring badge** (visual hint that an order came from a
  template, e.g. "weekly cleaning for Anna"). Nice-to-have, not blocking.
- **Customer recurring dashboard** showing upcoming materialized orders.
  Today's RecurringBookingsScreen shows templates only; could add a
  "next 4 occurrences" card. Defer.

---

## Risks + mitigations

| Risk | Mitigation |
|---|---|
| Booking flow regression from CreateOrder refactor | Run end-to-end booking smoke tests on customer mobile + web before merging Wave 3.1. Compare Stripe sessions before/after. |
| Materializer creates duplicate Orders if the template's `LastMaterializedFor` isn't updated correctly | Wrap materializer in a transaction; only mark `LastMaterializedFor` after `SaveChangesAsync` confirms the Order persisted. Idempotency key on the Order to be safe. |
| Cleaner takes a recurring Order, customer never confirms → cleaner shows up to nothing | Auto-cancel sweep (TASK-3.3.D) fires before the slot, plus the cleaner sees `paymentStatus = Pending` and can be trained to expect the customer's confirm step. |
| 24h push misses (FCM rate limit, app killed) | OrderEventBus push isn't critical here — customer can also see the upcoming order in the Orders tab. Add a "missed-confirm" auto-cancel as the safety net. |

---

## Success metrics (post-3.3 ship)

- ≥ 95% of materialized Pending Orders get confirmed before their cleaning date.
- ≥ 80% of materialized Orders get taken by a cleaner before the customer confirm.
- 0 regressions on standard (non-recurring) booking flow.

---

## TL;DR for execution

When you're ready to start Wave 3:
1. Read this spec.
2. Start with **Wave 3.1** in a dedicated branch — that PR alone is 3-4 days.
3. Test the booking flow exhaustively before merging 3.1.
4. Wave 3.2 + 3.3 follow as separate PRs, each ~1-2 days.

Total elapsed time across all 3 sub-PRs: 5-8 days of focused work. Don't try
to bundle all of it into a single push.
