# Order concurrency — what keeps one order and its customer's credit consistent when writers overlap

> **Status of this page: THE CURRENT SHAPE — binding.** [ADR-0073](/decisions/adr-0073) is `accepted`
> (2026-10-12) and shipped with this page. The ADR is the immutable record with the
> `## Challenge` / `## Defense` / `## Verdict` trail; this page is the evolving companion.
>
> Companion pages: [`outbox.md`](./outbox.md) (what a commit carries out of the process),
> [`order-availability.md`](./order-availability.md) (which orders a cleaner may take).
> Published view: `docs/flows/cancellation-refund-dispute.md`, `docs/product/business-rules.md`.

---

## The order row: one concurrency token today

`Orders.CurrentStatus` is the only concurrency token on an order
(`src/Cleansia.Infra.Database/EntityConfigurations/OrderEntityConfiguration.cs:252-254`). Every tracked
UPDATE of an order compares it, so a writer that decided on a fulfilment status it read earlier is refused
when another writer moved that status in between. `PaymentStatus` is indexed, not compared: a writer that
decided on a payment status read earlier still overwrites one committed since. That second axis is the
subject of ADR-0071, which adds `PaymentStatus` as the second token; this section is rewritten when it ships.

Nothing writes `Orders` outside the change tracker: no `ExecuteUpdate`/`ExecuteDelete` targets it, and the
only raw SQL naming the table is a `SELECT … FOR UPDATE`.

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
| `AutoCancelStaleRecurringOrders` | one per occurrence, inside the loop |
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
`CleanupStalePendingOrders` has no catch at its commit and says why.

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

- **`CompleteOrder` and a company's group can deadlock** when a referrer and the customer they referred both
  have abandoned checkouts with credit in the same group, the referrer sorts first, and the referred
  customer's first job completes in that second. Postgres aborts one side (40P01) and both roll back whole;
  the sweep retries next tick and the cleaner retries the completion.
- **A future caller that ends an order and gives the credit back another way loses the atomicity.** The guard
  is `CreditUnwind`'s rule that every order end returns credit through it.
- **`AutoCancelStaleRecurringOrders` still catches around each occurrence's commit in one shared scope**, the
  shape the join makes lossy. ADR-0072 gives each occurrence its own scope.

## Tests that pin it

- `src/Cleansia.Tests/Features/Credit/ReturnUnpaidOrderCreditTests.cs` — the lock comes before the reads and
  the return; none is taken without credit applied or without a customer account.
- `src/Cleansia.IntegrationTests/Features/Tenancy/CleanupStalePendingOrdersTwoOperatorsTests.cs` — a refused
  company commit rolls its credit back and the rerun returns it once; the locks are taken in ordinal order.
- `src/Cleansia.IntegrationTests/Features/Orders/PlatformCancellationCreditReturnAtomicityTests.cs` — a
  refused administrator cancel returns nothing, and its retry returns the credit once.
