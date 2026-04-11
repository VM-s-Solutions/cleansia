# Payment Flow Redesign — Deferred Capture Proposal

> Created: 2026-04-05 | Status: **PROPOSAL — needs product decision before implementation**

---

## Problem Statement

Current flow charges the customer's card **immediately at booking time** via a standard `PaymentIntent` (auto-capture). If no partner accepts the order, we have to issue a **refund** — slow, poor UX, and incurs Stripe fees on both sides of the transaction.

Additionally, for orders booked far in advance, we hold the customer's money for days/weeks before any service is rendered, which is suboptimal for the customer's cash flow and unusual for marketplace UX.

---

## Proposed Approach

Replace the immediate-charge model with a **deferred capture** model using one of two Stripe primitives, selected by order lead-time.

### Option A — Manual Capture (`capture_method: 'manual'`)

- Create `PaymentIntent` with `capture_method: 'manual'` at booking
- Stripe **authorizes and holds** funds (visible as pending on customer statement)
- Hold lasts **~7 days** (card network guarantee; some issuers release earlier — plan for **5 days safe window**)
- Partner accepts → `stripe.paymentIntents.capture(id)` → funds settle
- No partner within timeout → `stripe.paymentIntents.cancel(id)` → hold released, **no refund needed**

### Option B — SetupIntent (save card, charge later)

- Create `SetupIntent` at booking → verify + tokenize card, **zero authorization, nothing held**
- Store `payment_method` on file (attached to Stripe Customer)
- Charge via `PaymentIntent` with stored `payment_method` closer to the cleaning date (e.g., T-2 days)
- No 7-day constraint — works for any lead time
- Risk: card may expire/decline at charge time → requires "update card in 24h" recovery flow

### Hybrid Strategy (Recommended)

Neither option alone covers all booking scenarios. Route by lead time:

| Lead time to cleaning | Strategy | Reason |
|---|---|---|
| **≤ 5 days** | Manual capture (A) | Hold fits 7-day window; guaranteed funds; no re-charge risk |
| **> 5 days** | SetupIntent (B) | Hold would expire before service; save card, charge T-2 |
| **Partner accepts immediately** | Capture on acceptance | Lock in revenue ASAP (both paths) |

This mirrors how Uber, Airbnb, and most service marketplaces actually operate.

---

## Benefits

1. **Eliminate refund flow for unmatched orders** — the biggest immediate win. Cancel a hold vs. process a refund = zero fees, instant, invisible to customer.
2. **Better customer cash flow** — long-lead bookings don't tie up funds for weeks.
3. **Reduced chargeback exposure** — shorter time between charge and service delivery = less window for disputes.
4. **Easier cancellation policy** — canceling a hold is operationally trivial vs. tracking refund states.
5. **Stripe fees only on successful services** — no fees on auth+cancel cycles.

---

## Architectural Impact — What Breaks

### 1. Receipt generation timing

**Current:** [CreateOrder.cs](../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs) generates + emails the receipt PDF immediately after payment success.

**New model:** At booking time, payment has NOT been captured. There is no receipt yet — only a **booking confirmation**. Two distinct documents needed:

- **BookingConfirmationPdf** — sent at booking time. States "Your card has been authorized for X. You will be charged only when a cleaner is assigned."
- **ReceiptPdf** — sent only after successful capture.

This ties in with the **Phase 8 PDF rewrite** (native QuestPDF layouts, see `fancy-painting-cake.md` plan). Adding a second layout builder `IBookingConfirmationLayoutBuilder` fits the existing architecture cleanly.

### 2. Czech VAT / accounting

A receipt issued before funds are actually captured is **legally questionable** under Czech tax law. Receipts must reflect completed transactions. **Blocker — confirm with accountant before building.** Partner invoicing (pay-period driven) is unaffected.

### 3. Order state machine

New states / fields on `Order`:

```csharp
public enum PaymentState
{
    Authorized,        // hold placed, not yet captured
    SetupSaved,        // setup intent succeeded, no hold
    Captured,          // funds settled
    CaptureFailed,     // delayed capture declined — recovery flow active
    HoldExpired,       // released without capture
    Refunded           // post-service refund (unchanged from today)
}

public DateTimeOffset? PaymentHoldExpiresAt { get; private set; }
public DateTimeOffset? ScheduledCaptureAt { get; private set; }
public string? StripePaymentMethodId { get; private set; } // for SetupIntent path
```

### 4. Background jobs (fits into `Cleansia.Functions` plan)

New timer/queue functions required (align with existing Azure Functions migration plan):

| Function | Trigger | Purpose |
|---|---|---|
| `ExpireUnmatchedHoldsFunction` | Timer, every 15 min | Cancel holds for orders with no partner + past `PaymentHoldExpiresAt - 24h` buffer |
| `CaptureScheduledOrdersFunction` | Timer, hourly | For SetupIntent orders: attempt capture at T-2 days |
| `RetryFailedCaptureFunction` | Queue trigger | Retry capture after customer updates card |
| `NoMatchReminderFunction` | Timer, daily | Email customer if order still unmatched at T-24h before hold expires |

### 5. Stripe webhooks — new event handlers

Current: `HandlePaymentNotification` handles `payment_intent.succeeded`.

New handlers needed in [HandlePaymentNotification.cs](../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs):

- `payment_intent.amount_capturable_updated` — hold successfully placed → transition order to `Authorized`
- `payment_intent.canceled` — hold released → transition to `HoldExpired`, free partner queue
- `payment_intent.payment_failed` (on delayed capture) — transition to `CaptureFailed`, trigger card-update recovery flow
- `setup_intent.succeeded` — transition to `SetupSaved`, schedule `ScheduledCaptureAt`
- `setup_intent.setup_failed` — notify customer, don't create order

### 6. Card-update recovery flow (non-trivial)

When delayed capture fails (card expired, insufficient funds):

1. Generate signed token (JWT, 24h expiry) tied to order ID
2. Email customer with link: `https://cleansia.cz/payment-update/{token}`
3. Build new Angular page: hosted card update form using Stripe Elements (`cleansia-customer-features/payment-recovery/`)
4. On successful card update → retry capture immediately
5. If no update within 24h → auto-cancel order, notify partner (if already assigned — this is the tricky case), apologize to customer
6. Email templates in 5 languages (en, cs, sk, uk, ru)

**SMS notification (from original proposal):** Not currently in stack. Would require Twilio/Vonage integration. **Recommend skipping for v1** — marginal reach improvement over email, adds vendor dependency. Revisit if data shows email-only recovery has poor conversion.

### 7. Partner acceptance → immediate capture race

Currently, partner acceptance is just a DB update. New model requires a domain event → Stripe API call:

```
OrderAcceptedByPartner event → CaptureOrderPaymentHandler
  → stripe.paymentIntents.capture(order.StripePaymentIntentId)
  → on success: Order.MarkAsCaptured() + generate receipt + email
  → on failure: rollback partner assignment, notify both parties
```

The failure branch is **critical** — if capture fails after a partner accepts, we have a committed partner on an unpaid order. Current code has no concept of "unaccepting" an order.

---

## Phased Implementation Plan

### Phase 1 — Manual Capture for Short-Lead Orders (Low Risk)

**Scope:** Orders with cleaning date ≤ 5 days out only. Long-lead orders keep current immediate-charge flow temporarily.

- Add `PaymentState` enum + `PaymentHoldExpiresAt` field to `Order`
- Modify `CreateOrder` to use `capture_method: 'manual'` when `CleaningDateTime <= now + 5 days`
- New `ExpireUnmatchedHoldsFunction` timer function
- Capture on `OrderAcceptedByPartner` event
- New `BookingConfirmationPdf` layout (delays service-receipt until capture)
- Webhook handlers for `amount_capturable_updated` + `canceled`
- **Covers probably 60-70% of booking volume** based on typical marketplace patterns
- **Eliminates most refunds** from unmatched orders
- **Minimum surface area** — defer complexity of Phase 2/3

**Effort:** Medium. Depends on `Cleansia.Functions` project being ready (timer triggers). Aligns with Phase 8 PDF rewrite for the booking confirmation doc.

### Phase 2 — SetupIntent for Long-Lead Orders

**Scope:** Orders with cleaning date > 5 days out.

- SetupIntent creation at booking time
- Store `StripePaymentMethodId` + `StripeCustomerId` on `Order` (or a new `CustomerPaymentMethod` entity for reuse)
- `CaptureScheduledOrdersFunction` runs hourly, captures orders where `CleaningDateTime - now <= 2 days`
- Handle `payment_failed` on delayed capture → flag order for recovery (Phase 3)
- Temporary: if Phase 3 not ready, failed delayed captures trigger manual ops ticket + customer email

**Effort:** Medium. Reuses Phase 1 infrastructure.

### Phase 3 — Card Update Recovery Flow

**Scope:** Full automated recovery for failed delayed captures.

- Token generation + signed URL service
- New Angular page `payment-recovery` with Stripe Elements card update form
- `RetryFailedCaptureFunction` queue trigger
- 5-language email templates for "payment failed, update your card"
- 24h expiry → auto-cancel + partner notification
- Partner compensation policy (if partner already assigned to a now-canceled order) — **product decision needed**

**Effort:** High. Should only be built once Phase 2 data shows the failure rate justifies it.

### Skip for now

- **SMS notifications** — email is sufficient for v1
- **Customer "saved cards" wallet UI** — SetupIntent stores the PM but we don't expose it as a managed list to the customer yet

---

## Open Questions / Product Decisions Needed

1. **Accounting/legal:** Is a pre-capture "booking confirmation" document acceptable under Czech tax law in place of a receipt? **Must confirm with accountant before building.**
2. **Quantify current pain:** What % of orders currently end in refunds due to no partner acceptance? If <1%, the ROI on this whole initiative is questionable. **Run a query on historical data first.**
3. **Partner compensation on capture failure:** If a partner accepts an order and the delayed capture fails, who eats the cost? Partner's travel? Cleansia fronts it? Customer charged a cancellation fee on card update?
4. **Cancellation policy change:** Currently customer can cancel before X hours before cleaning with full refund. New model: cancellation before capture = zero-cost; cancellation after capture = refund policy applies. Needs customer-facing T&C update.
5. **Lead-time threshold:** Is 5 days the right cutoff? Data-driven — look at current booking lead-time distribution. If most bookings are <5 days, we can defer Phase 2 entirely and ship Phase 1 as a complete solution.
6. **Partner auto-assignment:** If we build auto-assignment logic alongside this, the "unmatched order" scenario becomes rarer and the whole initiative's value shifts toward the "customer cash flow" benefit rather than "eliminate refunds."

---

## Decision Points Before Implementation

This proposal should NOT proceed to implementation until:

- [ ] Czech VAT/accounting review confirms legality of pre-capture booking confirmation
- [ ] Historical data query: % of orders currently ending in refund due to no partner match
- [ ] Product decision: partner compensation policy on capture failure
- [ ] Product decision: lead-time cutoff (5 days vs. data-driven)
- [ ] Dependency: `Cleansia.Functions` project exists (from `fancy-painting-cake.md` plan) — timer triggers required
- [ ] Dependency: Phase 8 PDF rewrite complete — booking confirmation layout needs the new QuestPDF layout builder pattern

---

## Files That Would Be Affected (Preview — Phase 1 only)

| File | Action |
|---|---|
| `src/Cleansia.Core.Domain/Orders/Order.cs` | MODIFY — add `PaymentState`, `PaymentHoldExpiresAt` |
| `src/Cleansia.Core.Domain/Orders/PaymentState.cs` | CREATE |
| `src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs` | MODIFY — conditional manual capture |
| `src/Cleansia.Core.AppServices/Features/Orders/AcceptOrder.cs` (or equivalent) | MODIFY — trigger capture on acceptance |
| `src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs` | MODIFY — new webhook events |
| `src/Cleansia.Core.AppServices/Features/Payments/CaptureOrderPayment.cs` | CREATE |
| `src/Cleansia.Functions/Functions/ExpireUnmatchedHoldsFunction.cs` | CREATE |
| `src/Cleansia.Infra.Services/Pdf/Layouts/DefaultBookingConfirmationLayoutBuilder.cs` | CREATE |
| `src/Cleansia.Infra.Services/Pdf/Models/BookingConfirmationPdfData.cs` | CREATE |
| EF migration | CREATE — `Order.PaymentState`, `Order.PaymentHoldExpiresAt` columns |
| Email templates (5 languages) | CREATE — booking confirmation, hold expiring soon, hold expired |

---

## Summary

This is a **valuable architectural improvement** but it is **not a bug fix** — it's a significant product/payment redesign with legal, operational, and engineering implications. Recommendation:

1. **Answer the open questions first** (especially legal + current refund volume) before scoping engineering work
2. **Do NOT bundle this with the current bug-fix sprint** — it deserves its own initiative with product + finance stakeholders
3. **Phase 1 alone** (manual capture for short-lead orders) is the MVP and should be treated as the entire initiative for v1; Phases 2 and 3 are follow-ons contingent on Phase 1 data
4. **Hard-depends on** the `Cleansia.Functions` background-work migration being complete — do that first
