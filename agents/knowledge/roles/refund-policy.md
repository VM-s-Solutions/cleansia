# Role — `RefundPolicy` (CRC card)

> Introduced by **ADR-0009** (`agents/backlog/adr/0009-refund-policy.md`). Sibling to `BookingPolicy`.
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
  (ADR-0006 D2). `RefundPolicy` is policy, not the money primitive.
- **How a line's amount is allocated** — the share-of-`TotalPrice` allocation (ADR-0009 D2) is the caller's
  computation, not the policy's; the policy gates *whether and on what fee terms*, not *how much per line*.
- **Discount / express-surcharge math** — those are already embedded in `Order.TotalPrice`
  (`OrderFactory.cs:91-95`); no refund actor re-applies them.
- **The cancel penalty** — `BookingPolicy`'s cancel-fee tiers (`BookingPolicy.cs:98-127`,
  `CancelOrder.cs:119`) are a different, distinct fee; `RefundPolicy` never touches the cancel penalty.
