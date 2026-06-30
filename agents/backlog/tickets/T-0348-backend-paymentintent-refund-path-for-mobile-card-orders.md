---
id: T-0348
title: "Backend: add a PaymentIntent refund path for mobile-paid (PaymentSheet) card orders"
status: done
size: M
owner: backend
created: 2026-06-28
updated: 2026-06-30
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
- [x] A mobile-paid card order (StripeSessionId null, PaymentIntentId set) is refundable (full + partial).
- [x] The web Checkout-Session refund path is non-regressing.
- [x] Reviewer APPROVE + (money path) security PASS.

## Status log
- 2026-06-28 — filed from the T-0347 Gate-SEC residual finding. Latent (admin-only refund surface); gates LIVE mobile-card refunds, not the T-0347 double-capture fix or the T-0313 build.
- 2026-06-30 — **proposed → done** (HARDENING-1, `64f6525` on `phase/hardening-1`, off master `3e7ce52`;
  bundled in the backend trio with T-0346 + T-0350). Added `RefundPaymentIntentAsync` on
  `IStripeClient`/`StripeClient` (Stripe `Refund.Create` against the PaymentIntent) and routed `RefundService`
  by charge surface: `StripeSessionId` present → the existing Checkout-Session refund (web, non-regressing);
  else a stored `StripePaymentIntentId` present → the new PaymentIntent refund (mobile). **NO schema change** —
  `Order.StripePaymentIntentId` already existed (set when the mobile PaymentIntent is created), so there was
  nothing to persist/migrate. **Money-correctness fix folded in:** extended the refundable-surface gate to the
  **two CANCEL paths** (`CancelOrder` + `AdminCancelOrder`) via `Order.HasRefundableChargeSurface`, so a
  cancelled paid mobile/recurring card order now actually refunds (previously short-circuited to
  `RefundOrderNotRefundable`). **Test/correctness findings folded:** added cancel-refund-wiring coverage
  (`CancelOrderRefundWiringTests`/`AdminCancelOrderHandlerTests`), the PaymentIntent refund path
  (`RefundServiceTests`/`AdminRefundOrderHandlerTests`), and retargeted the stale web-only refund tests; the
  architect's "both-surfaces" finding was **refuted and dropped** — a card order has exactly **one** charge
  surface (web Session XOR mobile PaymentIntent, by T-0347), so the surface router is an either/or, not a
  both. **Security review CLEAN** (money path: surface-correct, no double-refund, admin-only). Build 0 errors;
  `Cleansia.Tests` 1685. Reviewer APPROVE. NOT committed by the PM — the owner commits the backlog edits with
  the phase PR.
