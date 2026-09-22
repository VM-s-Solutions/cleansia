# Order Tracking

`/track-order` is the customer web app's guest surface: the page a booking's e-mail link lands on. It
is implemented by `TrackOrderComponent` + `TrackOrderFacade` in `@cleansia-customer/orders`, and it is
public — no auth guard.

```
/track-order                   Public — no auth guard
/orders/lookup                 → redirect /track-order (pathMatch full)
/orders/lookup/:orderId        → redirect /track-order (pathMatch full)
```

`/orders/lookup` used to be a second form and a second result screen doing the same job. Both
literals now redirect here.

## One page, three states

The page has no toggle and no form. What it shows is decided by what the visitor arrived with:

| State | When | What is on screen |
|---|---|---|
| **The booking** | the token opened one | the two axes (order progress and payment), what was booked, the facts, the total, and **Cancel booking** when the status allows it |
| **Not found** | a token that opens nothing | an amber panel — nothing failed; a link stops working 30 days after the clean, and every link the guest already held stops at once when the booking is cancelled — plus **Try again** |
| **No link** | the visitor arrived with no token at all | a panel saying where the link is (the confirmation e-mail), the remembered bookings this browser still holds tokens for, a way to write in, and a link to sign in |

Amber, not red, on the middle state: a link that has expired is the system working as designed, and a
red error panel would say otherwise.

## The credential

**One factor: the per-order access token.** The page reads it off `?token=` on load and looks the
booking up with it.

```
/track-order?orderNumber=CLN-2026-001&email=jane@example.com&token=p8Jw2hQx…
```

The page reads **only `token`**. `orderNumber` and `email` are on the URL because `EmailService`
builds one link shape for every order e-mail; nothing on this page reads either of them, and neither
opens anything. The lookup is `POST /api/Order/Lookup` with `{ accessToken }`; a failure leaves the
token in a signal so **Try again** can retry without going back to the mailbox.

::: warning The three-field lookup form is gone
The page used to ask for an order number, an e-mail and a confirmation code. That triple was not a
secret: the display number is sequential, the e-mail is not private, and the confirmation code was
served on the order detail to every cleaner assigned to the job — so a cleaner could open, and cancel,
their own customer's booking. There is nothing to type on this page any more.
→ [Guest order lookup](/flows/booking-and-pricing#guest-order-lookup)
:::

## Remembered bookings

`GuestOrderService` keeps, in `localStorage` under `cleansia_guest_orders`, the bookings **this
browser can still prove** — at most five, newest first:

```typescript
interface GuestOrder {
  orderId: string;
  accessToken: string;
  createdAt: string;
}
```

The token is what makes an entry worth keeping, so an entry without one — a bundle written by an
earlier release — is dropped on read. Entries are written from two places: the wizard, out of
`CreateOrder`'s `guestAccessToken`, and the track page itself when a link opens a booking.

```typescript
guestOrderService.save(orderId, accessToken);
guestOrderService.getAll();   // GuestOrder[]
guestOrderService.clear();    // e.g. after sign-in
```

On load the page sends every remembered token to `POST /api/Order/LookupBatch` (cap: 10) and lists
what comes back. A row opens that booking **in place** — the batch already returned the whole order,
so there is no second screen, no cache to keep in step and no extra round trip. A failed batch is
silent by design: the remembered list is a convenience and the rest of the page still reads.

::: warning Device-specific
`localStorage` is per browser. Clearing site data, or switching device, loses the list — the e-mail
link is the durable route back, and every status e-mail carries a fresh one.
:::

## Cancelling from the page

`TrackOrderFacade` holds the selected booking's token and the cancellation state in signals. **Cancel
booking** appears only while the status is `New`, `Confirmed` or `OnTheWay` and a token is held. It
opens a dialog that first fetches `POST /api/Order/GuestCancellationPreview` with the same token and
shows the tier, the fee (amount and rate) and the refund estimate before anything is submitted;
confirming sends `POST /api/Order/CancelGuest`.

Both calls go through `errorToastSuppressingHttpClient()` — they answer **inline**, so they opt out of
the shared error snackbar. A red *"An error occurred"* toast over an amber panel that already explains
the outcome contradicts it. Four backend keys are surfaced verbatim (`order.not_found`,
`order.already_cancelled`, `order.in_progress_cannot_cancel`, `order.already_completed`); anything else
falls back to the page's own message. Backend keys resolve under `api.*`.

After a successful cancellation the page flips the booking to `Cancelled` locally, states the refund
amount when one was actually issued, and hides the cancel action. Server-side the booking's
outstanding tokens are revoked at that point — including the one this page is holding — so the link
in hand opens nothing on a later visit. The cancellation e-mail carries a fresh one.
→ [Guest cancellation](/flows/booking-and-pricing#guest-cancellation)

## What the page draws

**Two axes, not one list.** An order's progress and its money move independently — a cash job reaches
`Completed` while still unpaid, and a card job is paid before anyone is assigned — so merging them
would interleave two sequences with no order between them.
→ [Order lifecycle](/domain/order-lifecycle)

The **order axis** is drawn as the whole journey, not only the stops already made, with each step's
timestamp read off the order's own `statusHistory` rather than inferred (an order can skip a state —
a cash job confirmed and started in one motion — and a timeline that infers timestamps invents them).
`Pending` is deliberately absent: it has no production writer.

The **payment axis** carries no timestamps. The guest lookup returns `statusHistory` for the order
only; the payment has no history of its own on the response, and borrowing the order's clock would
date a different event. Its rows say what state the money is in and what happens next.

| Also on the page | |
|---|---|
| Facts | cleaning date and time, estimated duration, payment type |
| What was booked | every selected service and package, as chips |
| Total | formatted with the **order's** currency code and the UI language's locale, with a note that says *paid* or *settled with the cleaner* — saying the wrong one is the difference between having your wallet ready and not |
| Actions | *Need help* → `/disputes`, *Look up another* (clears the selection), and for a visitor with no session a link to register |

**No address and no crew.** The signed-in order board draws both; the guest lookup returns neither, on
purpose.

## Status pipes

Status values are mapped by the shared `OrderStatusIconPipe` / `OrderStatusLabelPipe` /
`OrderStatusSeverityPipe` (`libs/shared/pipes/src/lib/order-status/`) — one source of truth across
customer, partner and admin.

| `OrderStatus` | Value | Icon | Severity |
|---|---|---|---|
| `New` | `0` | `pi pi-inbox` | `info` |
| `Pending` | `1` | `pi pi-clock` | `warn` |
| `Confirmed` | `2` | `pi pi-check` | `info` |
| `OnTheWay` | `3` | `pi pi-send` | `info` |
| `InProgress` | `4` | `pi pi-spin pi-spinner` | `info` |
| `Completed` | `5` | `pi pi-check-circle` | `success` |
| `Cancelled` | `6` | `pi pi-times-circle` | `danger` |

`New` has an explicit arm in both pipes, not the default one: it is the resting state of every booking
nobody has taken yet — **including a card order the customer has already paid for** — and falling
through to the generic dot rendered it as an absence. It is `info`, not `warn`: nothing is wrong with
a booking that is waiting for a cleaner. The `warn` row belongs to `Pending`, which has no production
writer. → [Order lifecycle](/domain/order-lifecycle)

`PaymentStatus` is the axis that carries *"card payment initiated, waiting for the webhook"* — a card
order sits at `OrderStatus.New` with `PaymentStatus.Pending` until Stripe confirms.

| `PaymentStatus` | Value |
|---|---|
| `Pending` | `1` |
| `Paid` | `2` |
| `Failed` | `3` |
| `Refunded` | `4` |
| `Disputed` | `5` |
| `PartiallyRefunded` | `6` |
