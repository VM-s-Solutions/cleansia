# Role — PlatformOrderCancellation (ADR-0064 D2, accepted 2026-09-16) (CRC card)

> Introduced by **ADR-0064** (`docs/decisions/adr-0064.md`, **`accepted`** 2026-09-16), shipped in T-0762
> as the extraction of `AdminCancelOrder.Handler`'s body into a two-caller service — a money path, reviewed
> adversarially. The files: `Core.AppServices/Services/Interfaces/IPlatformOrderCancellation.cs`,
> `Core.AppServices/Services/PlatformOrderCancellation.cs`; the callers are
> `Features/Orders/AdminCancelOrder.cs` and `Services/CompanyWindDownService.cs`.

## Responsibility (one sentence)

Cancel one order **on the platform's behalf** — no fee, and whatever the customer paid goes back — and do
everything a platform cancellation owes in one call: the status transition, the Live Activity end, the
card refund under the caller's refund reason (or the credit returned when the card was never charged),
the express-waiver release, the cleaners told, the loyalty revoked, a guest told by e-mail with their old
links retired; and offer the **refund leg alone** so a refund Stripe refused can be driven again under
the same key.

## Collaborators

- **`CancelAsync(order, actorId, cancelledBy, reasonKey, refundReason, ct)`** →
  `PlatformOrderCancellationResult(RefundAmount, Refund)`. `order.Cancel(now, cancelledBy, feeRate: 0,
  refundAmount: TotalPrice, reason)` — which records a refund of **0** instead on an order that took no
  payment (`Order.TookNoPayment`: `Pending` or `Failed`) — and an `OrderStatusTrack` append;
  `ILiveActivityProducer` end-push unconditionally beside the append; then, for a card order that is
  `Paid` with a refundable charge surface, `RefundAsync` — otherwise, when not `Paid`,
  `ICreditAccountRepository.ReturnUnpaidOrderCreditAsync` (the credit taken at checkout is the only money
  the customer paid); `IExpressWaiverConsumer.ReleaseForOrderAsync` unconditionally;
  `OrderAssignmentCancellationNotifier` for every assigned cleaner;
  `ILoyaltyService.RevokeForCancelledOrderAsync`; last, `GuestCancellationEmail.EnqueueAsync` — on a
  guest order only (no `UserId`), `GuestOrderAccessTokenIssuer.RevokeAsync` retires every token the
  booking had, then `IPendingDispatch` stages `email:guest-order-cancelled:{orderId}` carrying the
  refund's `RefundedAmount` and nothing else, so the e-mail names money only when a refund went through.
  The result's `RefundAmount` is the row's `CancellationRefundAmount`. **No commit** — the caller owns
  it: the admin handler through the UnitOfWork pipeline, the sweep per order.
- **`RefundAsync(order, actorId, refundReason, ct)`** → `PlatformRefundOutcome` (`NotAttempted`, `Issued`,
  `Failed(message)`; `RefundedAmount` is set only on `Issued`, to what the refund actually returned):
  `IRefundService.IssueRefundAsync(new RefundRequest(orderId, TotalPrice, refundReason, actorId))` and, on
  success, the customer's `OrderRefunded` notification keyed on the **refund** id, not the order (three
  handlers raise that event; keying on the order minted duplicate outbox keys).
- **`RefundService.BuildRefundKey`** — the key is derived from the reason: `CustomerCancellation →
  refund:{id}:cancel`, anything else → `refund:{id}:admin`. That is why the `refundReason` parameter
  exists: **the admin cancel keeps `CustomerCancellation` and its byte-identical key; the wind-down passes
  `ServiceNotRendered` and writes `:admin`**, and the platform absorbs the Stripe fee (`RefundPolicy`).
- **`AdminCancelOrder.Handler`** — a status-gated caller: loads the order, checks
  `CancellationAssessor.BlockedReason`, then `CancelAsync(…, CancelledBy.Admin, command.Reason,
  RefundReason.CustomerCancellation)`. Holds no refund, credit, waiver, loyalty or notification
  collaborator of its own any more.
- **`CompanyWindDownService`** — `CancelAsync(order, WindDownRequestedBy, CancelledBy.System,
  OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered)` per open order, committed
  per order; and `RefundAsync(order, WindDownRequestedBy, ServiceNotRendered)` alone for every earlier
  run's cancelled, card-paid, still-`Paid` order — the key resolves to the `Pending` row, the live
  refundable ceiling re-clamps it, Stripe replays once.

## Does NOT know

- **Whether the order may be cancelled.** `CancellationAssessor.BlockedReason` (Cancelled, Completed,
  InProgress) is the caller's gate; the sweep re-reads `CurrentStatus` untracked right before calling.
- **The apology credit.** `CancelUnfilledOrders` grants the no-show credit and raises a different push; it
  is **not** a caller — a third arm to serve one caller is what CLAUDE.md §4 forbids.
- **The customer's own cancellation.** `CancelOrder` (fee tiers, the oops window) is its own path.
- **When to commit.** Never inside; the admin path commits once through the pipeline, the sweep once per
  order so a refund that succeeded is recorded before the next Stripe call.

## Invariants a reviewer checks

1. **The admin cancel's key is still `refund:{id}:cancel`** and the sweep's is `refund:{id}:admin` —
   asserted as literals (`AdminCancelOrderHandlerTests`, `PlatformOrderCancellationTests`,
   `CompanyWindDownSweepTests`).
2. **The extracted collaborators moved, not re-implemented** — a reviewer's diff of `b2deb803` shows the
   body lifted from `AdminCancelOrder.Handler`; the handler's old money assertions live on in the
   service's tests. The guest step's two, `GuestOrderAccessTokenIssuer` and `IPendingDispatch`, came
   later (2026-09-28) and have no counterpart in the old handler.
3. **The refund is attempted only on a card order that is `Paid` with a charge surface**; any order not
   `Paid` gets its credit back. An order that took no payment (`Pending` or `Failed` — a cash booking not
   yet collected, a card never charged) records a refund of **0** on the row, and the admin cancel's
   `RefundAmount` reads the row, so it reports 0 too. A `Paid` cash order — a confirmed recurring
   occurrence, nothing collected — gets neither refund nor credit and still records the full price
   → [Business rules — cancellation](/product/business-rules#cancellation).
4. **`RefundAsync` on a `Pending` row replays under the same key and never inserts a second row**
   (`The_Refund_Leg_Alone_Re_Drives_The_Same_Key_And_Notifies_On_Success`).

## Watch-list

- A third platform-initiated cancellation with a *different* set of side effects is a new caller only if
  it wants exactly this set; otherwise it is its own path, as `CancelUnfilledOrders` stayed.
