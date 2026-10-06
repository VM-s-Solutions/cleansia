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
  refundAmount, reason)`, where `refundAmount` is what this cancellation gives back
  (`RefundService.LeftToGiveBackAsync` on the key the refund reason derives, since 2026-10-06): the whole
  price on an order nothing came back from, the rest of the sale on one already partly refunded. When a
  refund row already exists on that key — a guest's own cancel claimed it and never committed — the
  figure is held to what that claim sends, its amount plus any credit leg already returned on the key,
  because `RefundAsync` replays it at its own amount (since 2026-10-06: 750 of a 1 000 booking at the
  25 % tier, not 1 000). `Cancel` records **0** instead on an order that took no payment
  (`Order.TookNoPayment`: `Pending` or `Failed`).
  Then an `OrderStatusTrack` append;
  `ILiveActivityProducer` end-push unconditionally beside the append; then, for a card order that is
  `Paid` or, since 2026-10-06, `PartiallyRefunded`, with a refundable charge surface, `RefundAsync`,
  which asks the seam for the price and lets it hold the refund to what the sale has left on each tender
  — otherwise, when not `Paid`, `ICreditAccountRepository.ReturnUnpaidOrderCreditAsync`, passed the order's
  card refunds from `IRefundRepository`, confirmed or pending (`RefundService.CardRefundedOrOwedAsync`,
  since 2026-10-06; confirmed only since 2026-10-05): an order that took no payment gets its credit back
  less any complaint settled in credit, the card term ignored, and one already refunded in full, or with
  no charge surface, the credit the sale still owes after its card refunds, credit returned and
  settlements
  → [Business rules — the credit return](/product/business-rules#when-the-cleaner-cancels-or-no-shows);
  `IExpressWaiverConsumer.ReleaseForOrderAsync` unconditionally;
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
  handlers raise that event; keying on the order minted duplicate outbox keys). A Stripe transport fault
  (`RefundService.IsStripeTransportFailure`) is caught and reported as `Failed(refund.failed)`, since the
  refund's claim already committed the cancellation. On a failure that leaves
  the row `Pending` (since 2026-10-06), `CreditUnwind.ReturnPendingRefundCreditLegAsync`
  returns the slice's credit share at once on the refund's key, as `CustomerOrderCancellation` and
  `CleanerNoShowCancellation` do, so the re-drive reads the slice back as card plus that leg, to the minor
  unit, instead of in proportion from the card amount.
- **`RefundService.BuildRefundKey`** — the key is derived from the reason: `CustomerCancellation →
  refund:{id}:cancel`, anything else → `refund:{id}:admin`. That is why the `refundReason` parameter
  exists: **the admin cancel keeps `CustomerCancellation` and its byte-identical key; the wind-down passes
  `ServiceNotRendered` and writes `:admin`**, and the platform absorbs the Stripe fee (`RefundPolicy`).
- **`AdminCancelOrder.Handler`** — a status-gated caller: loads the order, refuses `Cancelled`,
  `Completed` and `InProgress` itself — not through `CancellationAssessor.BlockedReason`, whose
  start-passed refusal (2026-09-28) binds the customer only — then `CancelAsync(…, CancelledBy.Admin,
  command.Reason, RefundReason.CustomerCancellation)`. Holds no refund, credit, waiver, loyalty or notification
  collaborator of its own any more.
- **`CompanyWindDownService`** — `CancelAsync(order, WindDownRequestedBy, CancelledBy.System,
  OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered)` per open order, committed
  per order; and `RefundAsync(order, WindDownRequestedBy, ServiceNotRendered)` alone for every earlier
  run's cancelled, card-paid order still `Paid` or, since 2026-10-06, `PartiallyRefunded`, skipping one
  whose own `refund:{id}:admin` row already `Succeeded` — the key resolves to the `Pending` row, whose
  retry first records the refund Stripe made on that key and sends on it only when Stripe has none
  (since 2026-10-06).

## Does NOT know

- **Whether the order may be cancelled.** The status gate is the caller's; the sweep re-reads
  `CurrentStatus` untracked right before calling.
- **The apology credit.** `CleanerNoShowCancellation` — the one body behind the unfilled sweep and an
  administrator's no-show confirmation (`AdminCancelOrderAsNoShow`, 2026-09-28) — grants the no-show
  credit and raises its own outcome push; it is **not** a caller — a third arm to serve one caller is what
  CLAUDE.md §4 forbids.
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
3. **The refund is attempted only on a card order that is `Paid` or `PartiallyRefunded` with a charge
   surface** (`PartiallyRefunded` since 2026-10-06); any other order not `Paid` gets its credit back. An order that took no payment (`Pending` or `Failed` — a cash booking not
   yet collected, a card never charged) records a refund of **0** on the row, and the admin cancel's
   `RefundAmount` reads the row, so it reports 0 too. Since 2026-09-28 a confirmed recurring cash
   occurrence stays `Pending` until the cash is recorded, so it records 0 like any cash booking; cash is
   recorded only while `InProgress`, which neither caller cancels
   → [Business rules — cancellation](/product/business-rules#cancellation).
4. **`RefundAsync` on a `Pending` row replays under the same key and never inserts a second row**
   (`The_Refund_Leg_Alone_Re_Drives_The_Same_Key_And_Notifies_On_Success`).

## Watch-list

- A third platform-initiated cancellation with a *different* set of side effects is a new caller only if
  it wants exactly this set; otherwise it is its own path, as `CancelUnfilledOrders` stayed.
