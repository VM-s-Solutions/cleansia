# Order concurrency — what keeps one order and its customer's credit consistent when writers overlap

> **Status of this page: THE CURRENT SHAPE — binding.** [ADR-0071](/decisions/adr-0071) and
> [ADR-0073](/decisions/adr-0073) are `accepted` (2026-10-12) and shipped with this page. The ADRs are the
> immutable records with the `## Challenge` / `## Defense` / `## Verdict` trail; this page is the evolving
> companion.
>
> Companion pages: [`outbox.md`](./outbox.md) (what a commit carries out of the process),
> [`order-availability.md`](./order-availability.md) (which orders a cleaner may take).
> Published view: `docs/flows/cancellation-refund-dispute.md`, `docs/product/business-rules.md`.

---

## The order row: one concurrency token per lifecycle axis (ADR-0071 D1)

An order moves on two axes, fulfilment (`CurrentStatus`) and payment (`PaymentStatus`), and both are its
concurrency tokens (`src/Cleansia.Infra.Database/EntityConfigurations/OrderEntityConfiguration.cs:252-260`).
Every tracked UPDATE of an order compares both, so a writer that decided on the order as it read it is refused
when another writer moved either axis in between. Before the payment axis was a token, a cancel decided on an
unpaid checkout overwrote a payment committed after its read: the sweep's commit wrote `Failed`, a customer's
cancel left `Cancelled` and `Paid`, and either way the money was kept with nothing refunded or escalated.

| A payment lands while… | What happens now |
|---|---|
| the stale-checkout sweep cancels the company's checkouts | the company's commit is refused and rolls back whole, credit return included; the order stays paid; the next run does not select it |
| the recurring auto-cancel cancels the occurrence | the occurrence's unit is refused and counted as failed; it stays paid and is not selected again |
| a customer or an administrator cancels | the cancel gets one error and its credit return rolls back; a retry reads the paid order |
| a webhook commits after a cancel | the webhook is refused, Stripe retries, and the retry escalates the paid-after-cancel order (unchanged) |

**What the tokens cost.** A writer whose read-to-commit window contains a write on either axis now fails instead
of overwriting: a request path returns one unmapped 500; `TakeOrder` answers `order.no_available_spots` when a take
races a refund's settle; `MarkCashCollected`'s repair commit after Stripe has settled can return one 500; and
`SendRecurringOrderReminders`, which commits per order inside a catch in one shared scope, fails every later
reminder of a run during which a payment lands (the first item of ADR-0072's canonicalization ticket, T-0841).

**Not `xmin`, and no lock.** A row version would also refuse writes to every unrelated column of the busiest row; a
per-sweep `FOR UPDATE` would cover only the sweeps that adopt it and lock `Orders` before the customer's `Users` row.

**`Orders` is written only through the change tracker**, which is what makes the tokens hold: no
`ExecuteUpdate`/`ExecuteDelete` targets it, and the only raw SQL naming the table is a `SELECT … FOR UPDATE`
(`EmployeeActionAuditRepository`). `OrderConcurrencyTokenTests` fails the build on a new one; the rule and the
guard's scope are in `agents/knowledge/patterns-backend.md` §"Order writes compare both lifecycle axes".

## A refund's settle reads again what it derives (ADR-0071 D2)

`RefundService.SettleAsync` saves the order's payment status after Stripe has paid, computed from what the
order has given back: card refunds confirmed, credit returned, complaints settled in credit. That status is
derived, not decided, so a save refused only because another refund moved it is read and saved again, in at
most three attempts. Failing there would leave a refund Stripe has paid pending, and a cancellation without the
notices and e-mail it sends after its refund. The credit leg is re-run with it and moves once on its key. The
settle reloads the order and tries again only when all three hold:

- the refused save names this order alone;
- nothing but `PaymentStatus`, `UpdatedOn` and `UpdatedBy` is staged on it. A reload discards what is staged,
  such as a cancel replayed on a claim still pending on its key;
- the order is still on the fulfilment status it was read with. A guest's cancel issues its refund before it
  stages the cancel, so reloading past a cleaner's start committed meanwhile would let that cancel be compared
  against the start instead of refused by it.

Anything else is rethrown as before. Without the retry, the payment-axis token would refuse the later of two
settles after Stripe had paid it; without the token, each saved a status counted without the other's refund.

Each pass sums the confirmed card refunds **without this refund's own key** and adds the refund once. Two settles
of one key share one row: an administrator's partial or full refund, a dispute's resolution or a guest's own
cancellation, sent twice at once; or the company wind-down's re-drive of a wound-down order's refund beside the hourly re-drive, or beside
another delivery of its own message. The one that commits second would
otherwise find that row already succeeded and count it twice: 600 back on a paid 1 000 ended `Refunded`, and a
later cancellation then refunded none of the rest. The retry path's ceiling and closure reads still count it
twice (T-0851).

## The credit an order gives back when it ends (ADR-0073 D1)

`CreditUnwind.ReturnUnpaidOrderCreditAsync` returns, under `order-ended-unpaid:{orderId}`, the credit an
order spent when it ends without a card refund of its own. It takes the customer's owner lock first
(`ICreditAccountRepository.LockForUserAsync`), which opens the context's lock transaction when none is open;
`TryReturnAsync` joins it, and `CommitAsync` commits it after the save or rolls it back whole if the save
fails. **So the return commits with the commit that ends the order, or not at all.**

Seven callers, none with code of its own for this:

| Caller | Commit that ends the order |
|---|---|
| `CleanupStalePendingOrders` | one per company's group, inside the loop |
| `AutoCancelRecurringOccurrence` (one per occurrence, sent by `AutoCancelStaleRecurringOrders`) | the pipeline's, inside the occurrence's own scope (ADR-0072 D2) |
| `CustomerOrderCancellation` (through `CancelOrder`) | the pipeline's |
| `PlatformOrderCancellation` (an administrator's cancel, the company wind-down) | the pipeline's, or the wind-down's per order |
| `CleanerNoShowCancellation` (the unfilled sweep, an administrator's no-show) | the sweep's per order, or the pipeline's |
| `HandlePaymentNotification`, expired session | the pipeline's |
| `AdminCancelOrderAsLockout` | the pipeline's (cash-only branch; credit is card-only, so nothing moves today) |

**Still self-committing, on purpose:** `CreateOrder`'s compensation after a failed Stripe dispatch
(`ReturnCreditAsync`, which must survive a request that returns a failure), and the refund's credit legs
(`ReturnCreditShareAsync`, `ReturnPendingRefundCreditLegAsync`).

**What the join costs.** The customer's lock is held from the return until the ending commit: milliseconds on
a request, seconds for a company's group of up to 500 orders. A checkout of the same customer that spends
credit, the loyalty lock in `CompleteOrder`, the `EnsureForUserAsync` callers and the erasure wait that long.

**What it forbids.** A loop that catches a refused commit and continues in the same scope now loses credit:
the failed order's staged changes ride the next commit, without the return the refused commit rolled back.
`CleanupStalePendingOrders` has no catch at its commit and says why, and the recurring auto-cancel gives each
occurrence a scope of its own ([`system-jobs.md`](./system-jobs.md), ADR-0072).

## Several customers' locks in one unit (ADR-0073 D2)

A unit that holds several customers' credit locks until one commit takes them in ordinal `UserId` order.

| Unit | Customers locked before one commit | Order |
|---|---|---|
| A company's group in `CleanupStalePendingOrders` | every customer with credit on an order of the group | ordinal `UserId`, then order `Id` |
| `ReferralService` grant | referrer and referred | ordinal `UserId` |
| `ReverseReferral` | referrer and referred | ordinal `UserId` |
| `ExpireStaleCredit` | one per commit | — |
| `CompleteOrder` | the completing customer (loyalty grant), then the referrer (referral award) | **not ordinal — accepted residual** |

## Residuals

- **`CustomerConfirmedAt` is not a token.** A confirm cannot meet the auto-cancel at the shipped settings: a
  confirm is refused within two hours of the slot and the auto-cancel acts only within the last hour. It reopens
  only if the grace is set to two hours or more (the validator allows up to 24).
- **Every other column of an order stays last-writer-wins**: notes, reminder stamps, lapse stamps.
- **The source guard misses** a bulk call on a query held in a variable from an earlier statement, and SQL assembled
  at runtime; the S8 grep in `docs/architecture/security-rules.md` still finds them.

- **`CompleteOrder` and a company's group can deadlock** when a referrer and the customer they referred both
  have abandoned checkouts with credit in the same group, the referrer sorts first, and the referred
  customer's first job completes in that second. Postgres aborts one side (40P01) and both roll back whole;
  the sweep retries next tick and the cleaner retries the completion.
- **A future caller that ends an order and gives the credit back another way loses the atomicity.** The guard
  is `CreditUnwind`'s rule that every order end returns credit through it.
- **A settle refused on the fulfilment axis, or three times, still fails after Stripe has paid**, as before:
  an administrator's refund whose order a cleaner took or started, or someone cancelled, during the Stripe
  call. The refund stays pending for the hourly job, which re-drives a cancelled order's own refund and raises
  any other to the company's administrators.

## Tests that pin it

- `src/Cleansia.Tests/Features/Credit/ReturnUnpaidOrderCreditTests.cs` — the lock comes before the reads and
  the return; none is taken without credit applied or without a customer account.
- `src/Cleansia.IntegrationTests/Features/Tenancy/CleanupStalePendingOrdersTwoOperatorsTests.cs` — a refused
  company commit rolls its credit back and the rerun returns it once; the locks are taken in ordinal order.
- `src/Cleansia.IntegrationTests/Features/Orders/PlatformCancellationCreditReturnAtomicityTests.cs` — a
  refused administrator cancel returns nothing, and its retry returns the credit once.
- `src/Cleansia.IntegrationTests/Features/Bookings/RecurringAutoCancelCreditAtomicityTests.cs` — a refused
  auto-cancel of an occurrence returns no credit, and the next run cancels it and returns the credit once.
- `src/Cleansia.IntegrationTests/Features/Refunds/ConcurrentRefundSettleTests.cs` — two refunds settling at once
  are both recorded and the order ends Refunded; the same refund settled twice at once is counted once and the order
  stays partly refunded; a cancellation whose refund settles beside another still notifies its crew, e-mails the
  guest and releases the waiver; a settle carrying a cancel staged on the order is refused whole.
- `src/Cleansia.Tests/Infrastructure/OrderConcurrencyTokenTests.cs` — the order's tokens are exactly the two axes;
  a cancel decided on an unpaid order whose payment landed meanwhile is refused (SQLite, two contexts); the source
  guard that keeps `Orders` written only through the change tracker.
- On Postgres, a payment landing mid-cancel is kept and nothing is cancelled or credited:
  `CleanupStalePendingOrdersTwoOperatorsTests.A_Payment_Landing_While_Its_Company_Is_Swept_Is_Kept_And_Nothing_Is_Cancelled`,
  `RecurringAutoCancelCreditAtomicityTests.A_Payment_Landing_While_The_Occurrence_Is_Cancelled_Is_Kept_And_Nothing_Is_Cancelled`,
  `src/Cleansia.IntegrationTests/Features/Orders/CustomerCancelRacingPaymentTests.cs`.
- `src/Cleansia.IntegrationTests/Features/Orders/GuestOrderCancellationTests.cs`
  (`Cleaner_start_during_refund_or_before_final_commit_cannot_be_overwritten`) — a guest's settle refused by a
  cleaner's start stays refused, and the start stands.
