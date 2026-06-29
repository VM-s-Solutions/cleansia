---
id: T-0348
title: "Backend: add a PaymentIntent refund path for mobile-paid (PaymentSheet) card orders"
status: proposed
size: M
owner: backend
created: 2026-06-28
updated: 2026-06-28
depends_on: [T-0347]
blocks: []
stories: []
adrs: [0008]
layers: [backend]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: T-0347 Gate-SEC (sprint-12 §7.16) — residual LATENT finding (refund coverage gap)
---

> **Refund-COVERAGE gap (the opposite of the double-CAPTURE T-0347 closed) — LATENT, admin-only, not a live breach.** After T-0347, a mobile (PaymentSheet) card order has `StripeSessionId == null` (its single charge surface is the Stripe **PaymentIntent**). But the only refund implementation keys off the **Checkout Session**, so a mobile-paid card order cannot be refunded by any code path. **Required before mobile card goes LIVE with real refunds** (alongside the Stripe key + T-0347); does NOT gate the T-0313 build or the T-0347 fix.

## The gap
- The only refund path is `RefundService.RefundCheckoutSessionAsync(order.StripeSessionId, …)` (`RefundService.cs:113`), which resolves session→PaymentIntent in `StripeClient.cs:75-101`.
- Both `RefundService.cs:44-48` and `AdminRefundOrder.cs:79-86` short-circuit to `RefundOrderNotRefundable` when `StripeSessionId` is empty — now always true for a mobile card order.
- There is **no** `RefundPaymentIntent` path. (Pre-T-0347 it was also effectively broken for mobile: the old double-surface set a `StripeSessionId` whose Session was never charged, so the refund would have thrown `"…has no PaymentIntent — likely unpaid"` — T-0347 changes the failure mode from throw→guard-reject, not the outcome.)
- Refund endpoints are **Admin-host-only** (`Web.Admin` AdminOrderController / AdminRefundController) — not customer-exposed; hence latent, not a live customer breach.

## Fix
- Add a `RefundPaymentIntentAsync(paymentIntentId, …)` path in `StripeClient`/`RefundService` (Stripe `Refund.Create` against the PaymentIntent).
- Persist the `PaymentIntentId` on the order when the mobile PaymentIntent is created (`CreatePaymentIntent.cs` already has `StripePaymentIntentId` — confirm it's stored on the Order) so refunds can resolve it.
- Route the refund by surface: if `StripeSessionId` present → the existing Checkout-Session refund (web); else if a `PaymentIntentId` present → the new PaymentIntent refund (mobile). Remove the empty-`StripeSessionId`→NotRefundable short-circuit for the PaymentIntent case.
- Tests: a mobile-paid (PaymentIntent, no session) card order refunds via the PaymentIntent path; the web (session) refund is non-regressing; partial + full refund parity.

## Done when
- [ ] A mobile-paid card order (StripeSessionId null, PaymentIntentId set) is refundable (full + partial).
- [ ] The web Checkout-Session refund path is non-regressing.
- [ ] Reviewer APPROVE + (money path) security PASS.

## Status log
- 2026-06-28 — filed from the T-0347 Gate-SEC residual finding. Latent (admin-only refund surface); gates LIVE mobile-card refunds, not the T-0347 double-capture fix or the T-0313 build.
