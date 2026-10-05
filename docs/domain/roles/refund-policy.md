# Role — `RefundPolicy` (CRC card)

> Introduced by **ADR-0009** (`docs/decisions/adr-0009.md`). Sibling to `BookingPolicy`.
> A pure policy class in `Cleansia.Core.AppServices.Features.Orders`. Caller-side — the admin refund
> command / `ResolveDispute` consults it; it is NOT inside `IRefundService` (ADR-0006 scopes the seam to
> the refundable ceiling + idempotency only).

## Responsibility (one sentence)
Decide, once for the platform, the refund **window** (14 calendar days, soft, anchored to
`Order.CompletedAt` UTC; null anchor → closed; chargeback `Source=Chargeback` exempt; admin-overridable
with a mandatory recorded reason) and the Stripe-fee **bearer** rule (platform absorbs the processing fee
on service-fault refunds — `RefundReason.ServiceNotRendered` / `DisputeResolution`; deducts the
non-refunded fee only on pure goodwill — `RefundReason.AdminDiscretion`).

## Collaborators
- `Order.CompletedAt` — the window anchor (`Order.cs:79`).
- `RefundReason` (enum, ADR-0009 D4) — drives the fee-bearer switch.
- The caller (admin refund command / `ResolveDispute`) — reads the window verdict and the fee rule, then
  computes `RefundRequest.Amount` and calls `IRefundService` (ADR-0006).

## Does NOT know
- **How the Stripe refund is sent** — that is `IRefundService` (ADR-0006 D1).
- **The refundable ceiling** — the seam clamps `Amount` to `amountCharged − Σ(succeeded refunds)`
  (ADR-0006 D2) — since 2026-10-06 `− Σ(pending refunds on any other key)` too, because a pending row may
  be one Stripe already paid (`RefundService.CardRefundedOrOwedAsync`); only the row being retried is left
  out, and it keeps its amount — and since 2026-10-05 first holds the requested slice of the sale to what
  the order has left once a complaint settled in credit is counted: the card leg is `min(card ceiling, card
  share of min(requested, TotalPrice − card refunded or pending − credit returned − settled in credit))`,
  where *credit returned* leaves out a credit leg already returned on the refund's own key, which counts as
  part of its slice instead, so a retry sends Stripe the same amount → [Refund](/flows/cancellation-refund-dispute#refund).
  `RefundPolicy` is policy, not the money primitive.
- **How a line's amount is allocated** — the share-of-`TotalPrice` allocation (ADR-0009 D2) is the caller's
  computation, not the policy's; the policy gates *whether and on what fee terms*, not *how much per line*.
- **Discount / express-surcharge math** — computed once, at booking, from `ResolveLoy003Discount` to
  `ApplyExpressSurcharge` (`OrderFactory.cs:121-137`), and embedded in `Order.TotalPrice`. Each discount is stored in its own
  column, and the surcharge is stored beside them as `Order.ExpressSurchargeAmount` by
  `order.SetExpressSurcharge` (`:183`); no refund actor re-applies or re-derives them.
- **The cancel penalty** — `BookingPolicy`'s cancel-fee tiers (`BookingPolicy.cs:98-127`,
  `CustomerOrderCancellation.cs:32`) are a different, distinct fee; `RefundPolicy` never touches the cancel penalty.
