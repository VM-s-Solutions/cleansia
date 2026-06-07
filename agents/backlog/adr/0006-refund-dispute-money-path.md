# ADR-0006 — Refund / dispute money path: one refund seam, deterministic idempotency, chargeback linkage, and the fiscal-corrective-document boundary

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-06
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting (financial/fiscal)
- **Extends:** ADR-0001 (who may refund), ADR-0002 (refund notifications ride `IPendingDispatch`), ADR-0004 (fiscal corrective-document boundary), ADR-0005 (the Stripe refund is a classified, idempotency-keyed integration call)
- **Ticket:** T-0140 (ADR-REFUND) · **Consumers (Wave-2):** AUD-01 (admin issue-refund), the D-01/DA-1/SEC-DSP-06/07 dispute bundle, D-06 (chargeback webhook)

> This ADR is **ADR-REFUND**. It freezes the refund/dispute money-path **contract** — who issues a
> refund, where `RefundAmount` is consumed, the deterministic refund idempotency key, how a chargeback
> links to our `Dispute`, the fiscal-corrective-document boundary, and the Order/Dispute state effects.
> It ships **no code**: the admin refund command, the chargeback webhook case, and the `IStripeClient`
> idempotency-key change are the Wave-2 consumers. Once `accepted` it is immutable — supersede, never
> edit.

---

## Context

Refunds and disputes are written in two unrelated places with two different, partly-broken behaviors,
and chargeback linkage is dead code. All facts verified against the real code (2026-06-06).

**1 — Dispute refunds are recorded but never issued.** `ResolveDispute.Handler`
(`src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:43-60`) takes a `decimal?
RefundAmount`, validates it `>= 0` (`:26-29`), and calls `dispute.Resolve(...)` (`:53-57`), which only
sets `RefundAmount` + `Status = Resolved` on the entity (`Dispute.cs:82-90`). **No Stripe call is made**
— the handler has no `IStripeClientFactory` dependency. The money is refunded on paper only.

**2 — Order-cancel refunds are issued inline, un-keyed.** `CancelOrder.Handler`
(`src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:129-177`) computes `refundAmount`, calls
`stripe.RefundCheckoutSessionAsync(order.StripeSessionId, refundAmount, ct)` (`:142`) inside a narrow
`StripeException` try/catch, flips `PaymentStatus.Refunded` (`:143`), and reports `RefundInitiated`
(`:144,186`). `IStripeClient.RefundCheckoutSessionAsync`
(`src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs:13`) takes **no idempotency key**, so a
retried cancel can **double-refund**. The refund notification *is* already on the ADR-0002 seam
(`CancelOrder.cs:158-176` — `pending.Enqueue` for `OrderRefunded`), so that half is correct; the **money
call** is the broken half. Two money paths exist, one silently no-ops (dispute), one is un-keyed (cancel).

**3 — Chargeback linkage is dead.** `Dispute.LinkStripeDispute(...)` and `Dispute.StripeDisputeId`
(`Dispute.cs:38,104-108`) have **no producer**: `Constants.StripeEventType`
(`src/Cleansia.Core.AppServices/Common/Constants.cs:21-54`) defines only `checkout.session.*`,
`payment_intent.*`, and `customer.subscription.*`/`invoice.*` — there is **no `charge.dispute.*`** case,
and `HandlePaymentNotification`'s event switch (`HandlePaymentNotification.cs:201-213`) never reaches
disputes. A real Stripe-side chargeback is never reflected against our `Dispute`.

**Governing context.** This decision sits on top of ADR-0001 (who may refund), ADR-0002 (a refund's
notification side effect), ADR-0004 (fiscal: a refund of a paid order is a *correction* of a registered
sale), and ADR-0005 (the Stripe refund is now an idempotency-aware, classified integration call). It
does **not** re-decide any of them — it cites them.

This is **one decision** — "the refund/dispute money path" — because the parts are inseparable: naming
the refund seam is meaningless without pinning where `RefundAmount` is consumed; the amount is unsafe
without a deterministic idempotency key; the key, the state transitions, and the fiscal correction all
key off the **same** refund event; and the chargeback path is the inbound mirror of the same money seam.

---

## Decision

> **Contract principle.** There is **one seam** through which money leaves via Stripe — a refund
> application service, `IRefundService.IssueRefundAsync(RefundRequest)` — and every refund (admin-issued,
> dispute resolution, order cancellation) flows through it. The seam is **idempotent on a deterministic
> key derived from the refunded charge**, records the refund as a first-class `Refund` projection of the
> originating `Order`/`Receipt`/`Dispute`, writes the Order/Dispute state transition **only after** Stripe
> confirms, routes the success notification through ADR-0002 `IPendingDispatch`, and triggers the
> fiscal-correction obligation per ADR-0004. A refund is **never** recorded-but-not-sent or
> sent-but-not-recorded.

### D1 — One refund seam: `IRefundService` (AC2)

A new application service **`IRefundService`** (in `Cleansia.Core.AppServices`) owns "issue a refund":

```csharp
public interface IRefundService
{
    // Idempotent on RefundKey (D3). Calls Stripe (ADR-0005 classified, keyed),
    // records the Refund projection, writes the payment-status transition, and
    // returns the classified outcome. Does NOT enqueue notifications (the caller does, D6).
    Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken ct);
}

public sealed record RefundRequest(
    string OrderId,           // the charge's owning order — source of StripeSessionId/PaymentIntentId
    decimal Amount,           // the amount actually sent to Stripe (D2 reconciliation)
    RefundReason Reason,      // CustomerCancellation | DisputeResolution | AdminDiscretion
    string? DisputeId,        // set when the refund resolves a dispute (linkage, D4)
    string ActorId);          // the admin/system actor (audit + authz already checked by caller, D6)
```

**D1.1 — Who issues, who records intent.**
- **`ResolveDispute` records intent; it does NOT issue inline.** Resolving a dispute with a refund amount
  is a *decision*; the *money movement* is the refund seam. `ResolveDispute.Handler` calls
  `IRefundService.IssueRefundAsync(...)` (Reason = `DisputeResolution`, `DisputeId` set) — it does **not**
  gain a raw `IStripeClientFactory`. The dispute is marked `Resolved` with its `RefundAmount` **and** the
  refund is actually sent through the one seam. (Today it sends nothing — fact 1; this closes it.)
- **`CancelOrder` is migrated onto the same seam.** Its inline `stripe.RefundCheckoutSessionAsync` call
  (`CancelOrder.cs:142`) is replaced by `IRefundService.IssueRefundAsync(...)` (Reason =
  `CustomerCancellation`). **Reason for migrating rather than carving it out:** the cancel path is the
  *only* path with a working money call, but it is un-keyed (fact 2) and duplicates logic the dispute
  path needs anyway; one seam means the idempotency key, the `Refund` record, the state transition, and
  the fiscal-correction trigger are written **once**, not twice. The existing ADR-0002 notification
  enqueue (`CancelOrder.cs:158-176`) stays in `CancelOrder` (D6).
- **Admin issue-refund (AUD-01, Wave-2)** is a thin command over the same seam.

**D1.2 — The seam is the only place an `IStripeClient` refund call appears.** Outside `IRefundService`'s
implementation, no handler may call `RefundCheckoutSessionAsync` / a refund API. Verification #1.

### D2 — `RefundAmount` consumption, pinned end-to-end (AC3)

**Source of truth for the amount sent to Stripe = `RefundRequest.Amount`, computed by the caller and
clamped to the refundable ceiling inside `IRefundService`.**

- **Cancel path:** `RefundRequest.Amount` = the `refundAmount` `order.Cancel(...)` already computes from
  `Order.TotalPrice` minus the cancel fee (`CancelOrder.cs:121-126`). `IRefundService` re-asserts
  `0 < Amount <= refundable(order)` where `refundable(order) = amountCharged - alreadyRefunded` (read
  from the `Refund` projection, D5) — so a partial-then-full sequence can never exceed the charge.
- **Dispute path:** `RefundRequest.Amount` = `Dispute.RefundAmount` (`Dispute.cs:32`), clamped the same
  way. `Dispute.RefundAmount` remains the *recorded decision*; the seam consumes it as the amount to send.
- **`CancelOrder.Response.RefundAmount`** (`CancelOrder.cs:29,184`) reports the **requested** refund;
  `RefundResult` carries the **confirmed** amount Stripe accepted. They agree on success; on a Stripe
  failure the response reports `RefundInitiated = false` and the requested amount, never a phantom
  "Refunded" status (fact 2's `:143` flip moves *inside* the seam, after Stripe confirms — D7).

**No amount is recorded-but-not-sent** (the dispute hole) **or sent-but-not-recorded** (the un-keyed
cancel hole): the seam writes the `Refund` row and the payment-status transition in the **same commit
that follows the confirmed Stripe call**, and a redelivery short-circuits on the key (D3).

### D3 — Deterministic refund idempotency key (AC4 — S7a/S7b)

`IStripeClient`'s refund method gains an **idempotency key** parameter (the Wave-2 consumer adds it; this
ADR freezes the **formula**):

```
RefundKey = refund:{OrderId}:{purpose}            // purpose ∈ { cancel, dispute:{DisputeId}, admin:{RefundRequestId} }
```

- **Stripe-side:** the key is passed as Stripe's `IdempotencyKey` on the refund request, so a Polly
  retry (ADR-0005 D1.2) or a handler retry sends the **same** key → Stripe returns the **same** refund,
  never a second one.
- **Our side (S7a):** before the Stripe call, the seam asserts no committed `Refund` row already exists
  for `RefundKey`; a **unique index on `Refund.RefundKey`** is the DB backstop — a concurrent double-issue
  has one winner; the loser catches PG **23505** and resolves to the existing refund (ack, not crash),
  exactly the S7a/S7b shape ADR-0004 D-F4.1(b) uses for receipts. The key is **deterministic, never
  `Guid.NewGuid()`** (S7 "client-stable key").
- **Partial refunds:** a deliberate *second* partial refund of the same order is a **different purpose/
  intent** and therefore a different key (`admin:{RefundRequestId}` is per-request). The cancel and the
  single dispute resolution are one-per-order/one-per-dispute and collapse on the key — a retried
  `CancelOrder`/`ResolveDispute` cannot double-refund.

This is named as the **TC-7 (refund money-math) / TC-IDEMP** obligation the consumer ticket lands with
(D8, AC7).

### D4 — Chargeback linkage (AC5 — handed to D-06, Wave-2, not implemented here)

An inbound Stripe **chargeback** is the customer disputing the charge at their bank — distinct from our
in-app `Dispute`, but it must link to it.

- **Events:** add `charge.dispute.created`, `charge.dispute.updated`, `charge.dispute.closed` to
  `Constants.StripeEventType` and a `charge.dispute.*` arm to `HandlePaymentNotification`'s switch
  (`HandlePaymentNotification.cs:201-213`).
- **Resolution to our `Dispute`/`Order`:** the Stripe event's `charge`/`payment_intent` resolves to our
  `Order` via the **same lookup** webhooks already use (`Order.StripeSessionId` / `StripePaymentIntentId`
  — the `HandlePaymentNotification` order-resolution path at `:188-196`). From the `Order`, find or
  **create** the linked `Dispute`: a chargeback may arrive with **no pre-existing in-app dispute**, so the
  handler creates a `Dispute` (Reason = `Chargeback`, Status = `Escalated`) when none exists, then calls
  `dispute.LinkStripeDispute(stripeDisputeId, "stripe-webhook")` (`Dispute.cs:104-108`) — this is the
  long-dead method's first producer.
- **Status transitions:** `charge.dispute.created` → `Dispute.Status = Escalated` (+ link);
  `charge.dispute.closed` with `status = won/lost` → record the outcome (a *lost* chargeback is an
  involuntary refund — see D5 reconciliation; it does **not** re-call `IRefundService` because the bank
  already pulled the funds). The exact won/lost → Order/payment-status mapping is **D-06's** detail; this
  ADR fixes the linkage seam and the create-if-absent rule.
- **Idempotency:** the chargeback consumer dedups on the existing `ProcessedStripeEvent` (per webhook
  event id) — unchanged from the current webhook idempotency design.

**The webhook case is implemented by D-06, not here** (out of scope), but the linkage contract above is
frozen so D-06 lands without a second design pass.

### D5 — The `Refund` projection: linking refund ↔ Order/Receipt/Dispute (AC3)

A new lightweight entity **`Refund`** (child of `Order`, `ITenantEntity`) records each money-out event:

```
Refund { Id, OrderId, ReceiptId?, DisputeId?, Amount, Currency, RefundKey (unique),
         Reason, StripeRefundId?, Source (AppRefund | Chargeback), Status (Pending|Succeeded|Failed),
         CreatedOn, ConfirmedOn? }   // ITenantEntity; FK to Order; nullable FK to Receipt/Dispute
```

- **Why a projection and not just `Order.PaymentStatus = Refunded`:** the boolean status cannot represent
  *partial* refunds, *multiple* refunds, or a chargeback that bypassed our seam. The `Refund` rows are
  the audit-trail and the `refundable(order)` source (D2). `Order.PaymentStatus` becomes a *derived
  summary* (`Refunded` when sum(succeeded refunds) >= amountCharged; a new `PartiallyRefunded` when
  `0 < sum < amountCharged`).
- **Receipt link:** `ReceiptId` ties the refund to the fiscal document it corrects (D6 — fiscal).
- **Chargeback link:** a `charge.dispute.lost` reconciliation writes a `Refund { Source = Chargeback }`
  so `refundable(order)` accounts for funds the bank already pulled (prevents an admin double-refunding a
  charged-back order).

### D6 — Authorization, dispatch, and fiscal boundaries (AC6 — cite, don't redo)

- **Authz (ADR-0001):** issuing a refund is **admin-only**. The admin issue-refund command (AUD-01) is
  gated by an `AdminOnly` permission; `ResolveDispute` is already `CanResolveDispute = AdminOnly`
  (ADR-0001 D2). A non-admin can never reach `IRefundService`. No new permission is *decided* here beyond
  "the admin refund command is `AdminOnly`" — the exact `Policy.*` constant is added by AUD-01 under
  ADR-0001's map-completeness rule (additive row, same-PR snapshot update).
- **Dispatch (ADR-0002):** a refund-success notification goes through `IPendingDispatch` (the
  `OrderRefunded` push the cancel path already enqueues at `CancelOrder.cs:158-176`), **not** a direct
  `IQueueClient.SendAsync` from the refund handler/seam. `IRefundService` itself does **not** enqueue —
  the **calling handler** records the notification intent post-success (so the dispatch gates on the
  caller's commit, per ADR-0002 D1). Verification #4.
- **Fiscal (ADR-0004):** a refund of a **fiscally-registered** sale is a **correction** of a registered
  document. For `None`/`AsyncBackground` regimes (CZ/SK/PL today) the corrective obligation is recorded
  but not blocking. For **BlockingOnline** regimes (DE/AT/ES) a refund/cancellation of a registered
  receipt legally requires a **corrective fiscal document** (a cancellation/credit registration with the
  tax authority). **Whether, and in what form, each BlockingOnline country requires a corrective fiscal
  registration on refund is an owner/legal decision** — raised as **Q-REFUND-01** (below), **gated to the
  same go-live gate ADR-0004 already binds DE/AT/ES to**. This ADR fixes the **seam** (the `Refund` row
  carries `ReceiptId`; a `RefundRegistered` fiscal effect rides the existing fiscal
  retry/reconciliation machinery when the answer lands) and does **not** invent the per-country corrective
  rule. **Customer-facing completion/cancellation is never blocked by the corrective registration** —
  same invariant as ADR-0004 (`docs/architecture/fiscal-compliance.md`).

### D7 — State-machine effects (Order / Dispute)

- **Order:** the `PaymentStatus.Refunded` flip moves **inside** `IRefundService`, written **only after**
  Stripe confirms the refund (today `CancelOrder.cs:143` flips it before the commit but after the inline
  call — the seam preserves "confirm-then-record"). `PartiallyRefunded` is added for partial refunds
  (D5). The Order *lifecycle* status (New→…→Cancelled/Completed) is **unchanged** — a refund is a payment
  fact, not an order-lifecycle transition; a completed order that gets a goodwill refund stays
  `Completed`.
- **Dispute:** `ResolveDispute` writes `Status = Resolved` with `RefundAmount` (unchanged shape,
  `Dispute.cs:82-90`) — now backed by a real refund. A **chargeback** sets `Status = Escalated` and links
  `StripeDisputeId` (D4). `RefundReason.DisputeResolution` ties the `Refund` row to the `Dispute`.

### D8 — Test contract handed to the consumers (AC7)

The consumer tickets write these **red first** (test-first per `agents/knowledge/testing.md`; refund math
is on the strict red-green list):
- **TC-7 (refund money-math):** `refundable(order) = amountCharged - sum(succeeded refunds)`; a partial
  then a top-up never exceed the charge; cancel-fee reconciliation matches `order.Cancel`'s computation.
- **TC-IDEMP-REFUND:** a retried `ResolveDispute`/`CancelOrder`/admin-refund with the same `RefundKey`
  issues **exactly one** Stripe refund and writes **one** `Refund` row; the loser of a concurrent
  double-issue catches 23505 and resolves to the existing refund (ack, no double-send).
- **TC-KEY-REFUND:** two invocations with the same domain inputs emit the same `RefundKey` (no
  `Guid.NewGuid()`/timestamp).
- **TC-CHARGEBACK-LINK (D-06):** a `charge.dispute.created` resolves to the Order, links/creates the
  `Dispute`, sets `Escalated`; a redelivery (same Stripe event id) is a no-op.
- **TC-FISCAL-CORRECTIVE (gated on Q-REFUND-01):** a refund of a registered receipt records the
  corrective obligation without blocking the refund.

---

## Alternatives considered

- **Keep two paths; just add a key to the cancel path and a Stripe call to the dispute path.** Rejected:
  it leaves two implementations of the same money movement (the duplication that produced fact 1's
  no-op). One seam is the only way the key, the `Refund` record, the fiscal trigger, and the state
  transition are written once.
- **`CancelOrder` keeps its inline refund under a carve-out (don't migrate).** Considered (the AC2
  carve-out option). Rejected: the inline call is the un-keyed one (the double-refund hazard); leaving it
  out means duplicating the idempotency + `Refund`-record logic, and the cancel path is no more
  "load-bearing for the commit" than the dispute path (the order commits regardless of refund outcome —
  `CancelOrder.cs:136-138` already treats refund failure as non-blocking). Migrating is the smaller total
  surface.
- **Store refunds only as `Order.PaymentStatus = Refunded` (no `Refund` projection).** Rejected: a
  boolean cannot represent partial/multiple/chargeback refunds, cannot compute `refundable`, and gives no
  audit trail or `ReceiptId` link for the fiscal correction. The projection is the financial source of
  truth.
- **Random per-call Stripe idempotency key.** Rejected (same reasoning as ADR-0002): a fresh GUID makes a
  *redelivery* dedupable but a *retried cancel* mints a new key and double-refunds. Deterministic on the
  charge identity collapses both.
- **Create the corrective fiscal document automatically for all regimes now.** Rejected / escalated: the
  per-country corrective-document rule is a legal decision (Q-REFUND-01) bound to the DE/AT/ES go-live
  gate ADR-0004 already owns. Inventing it here would bake an unverified compliance assumption into the
  seam.
- **Handle chargebacks by treating `charge.dispute.created` as an automatic refund through `IRefundService`.**
  Rejected: a chargeback is the **bank** pulling funds, not us issuing a refund — re-calling Stripe refund
  would either error or double-move money. D5 records it as a `Refund { Source = Chargeback }`
  reconciliation row, not a seam call.

---

## Consequences

**Cheaper / safer:**
- **One** place issues a refund; the dispute no-op (fact 1) and the un-keyed double-refund (fact 2) are
  both closed at the source.
- A retried/concurrent refund cannot double-issue (deterministic key + unique-index backstop).
- Partial/multiple/chargeback refunds are representable and reconciled (`Refund` projection); admins
  cannot double-refund a charged-back order.
- Chargebacks are finally reflected against our `Dispute` (the dead `LinkStripeDispute` gets a producer).
- The fiscal-correction obligation has a seam (the `Refund.ReceiptId` link) ready for the per-country
  rule, riding ADR-0004's existing retry/reconciliation rather than a new mechanism.

**More expensive (new obligations):**
- A new `Refund` entity + unique index on `RefundKey` → **`manual_step: ef-migration` (owner-only)**,
  flagged in the consumer tickets.
- `IStripeClient.RefundCheckoutSessionAsync` gains an idempotency-key parameter → a small interface
  change; if any refund-related response DTO surface changes, the consumer ticket flags
  `manual_step: nswag-regen` (this ADR ships none).
- `ResolveDispute.Handler` gains `IRefundService` (replacing the no-op); `CancelOrder.Handler` swaps its
  inline Stripe call for the seam (a small, contained edit in the existing CreateOrder/CancelOrder
  evolution cluster the consumers already own).
- `HandlePaymentNotification` gains a `charge.dispute.*` arm (D-06); `Constants.StripeEventType` gains the
  three events.
- `Order.PaymentStatus` gains `PartiallyRefunded` (enum addition — additive, no breaking read).

**Rollout (Wave-2 consumers, each test-first):**
- **AUD-01:** admin issue-refund command over `IRefundService` (`AdminOnly`) + the `Refund` entity +
  migration + the `IStripeClient` key param. Lands TC-7 + TC-IDEMP-REFUND + TC-KEY-REFUND.
- **D-01 / DA-1 / SEC-DSP-06 / SEC-DSP-07 bundle:** `ResolveDispute` onto the seam; dispute backend
  hardening in its existing serialization cluster.
- **D-06:** the `charge.dispute.*` webhook case + `LinkStripeDispute` wiring + create-if-absent. Lands
  TC-CHARGEBACK-LINK.
- **CancelOrder migration:** folds into whichever consumer ships `IRefundService` first (AUD-01), so the
  inline call is replaced once.

---

## How a reviewer verifies compliance

**Mechanical (the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **One refund seam.** Grep for `RefundCheckoutSessionAsync` / any Stripe refund API call in
   `Features/**` → must appear **only** inside `IRefundService`'s implementation. A call in
   `CancelOrder`/`ResolveDispute`/any handler is a blocking finding (today `CancelOrder.cs:142` is the
   one to remove).
2. **Refund call carries a deterministic key.** The `IStripeClient` refund call passes a `RefundKey`
   matching the D3 formula; a `Guid.NewGuid()`/timestamp in the key fails TC-KEY-REFUND.
3. **`Refund.RefundKey` is unique** (EF config) and the seam catches 23505 → resolve-to-existing (not a
   throw/500).
4. **Refund notification rides `IPendingDispatch`.** No direct `IQueueClient.SendAsync` from the refund
   seam/handler; the `OrderRefunded` enqueue stays in the calling handler (ADR-0002).
5. **Chargeback arm present (after D-06).** `Constants.StripeEventType` has `charge.dispute.*` and
   `HandlePaymentNotification`'s switch handles it; `LinkStripeDispute` has exactly one producer.

**Test contract:** TC-7, TC-IDEMP-REFUND, TC-KEY-REFUND, TC-CHARGEBACK-LINK, TC-FISCAL-CORRECTIVE
(D8) — the consumer tickets land them with the code, red first.

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`refund-service.md`** (new) — `IRefundService`: *responsibility:* issue exactly one Stripe refund per
  deterministic `RefundKey`, record the `Refund` projection + payment-status transition after Stripe
  confirms, and trigger the fiscal-correction obligation. *Collaborators:* `IStripeClient` (ADR-0005
  classified/keyed), the `Refund` repo, `Order`, the fiscal-correction trigger (ADR-0004 machinery).
  *Does NOT know:* who is allowed to refund (the caller/authz owns that), how notifications reach the user
  (the caller enqueues via `IPendingDispatch`), or refund-policy windows (the caller computes `Amount`).
- **`refund.md`** (new, entity CRC) — `Refund`: *responsibility:* be the durable, per-key record of one
  money-out event linking `Order`/`Receipt?`/`Dispute?`. *Collaborators:* `Order` (parent),
  `OrderReceipt`, `Dispute`. *Does NOT know:* Stripe API shapes, fiscal regime, or how the amount was
  decided.
- **`Dispute` (existing)** — updated: `Resolve` now denotes a *backed* refund decision (via the seam);
  `LinkStripeDispute` gains its first producer (D-06 chargeback path); a chargeback may *create* a
  `Dispute(Reason = Chargeback)`.
- **`queue-consumer.md` (existing, ADR-0002)** — the chargeback webhook is an idempotent consumer
  (dedup on `ProcessedStripeEvent`); unchanged shape.

Catalog edit (same change): `agents/knowledge/patterns-backend.md §B8` / `security-rules.md §S7`
cross-reference ADR-0006 — a refund issued outside `IRefundService`, or without the deterministic
`RefundKey`, is a B8/S7/ADR-0006 violation.

---

## Open questions raised (owner / legal)

Filed in `agents/backlog/questions/open.md`:
- **Q-REFUND-01 (blocking for DE/AT/ES go-live, NOT for CZ/SK/PL launch)** — per-country corrective
  fiscal-document requirement on refund/cancellation of a registered sale. Bound to ADR-0004's existing
  BlockingOnline go-live gate.
- **Q-REFUND-02 (non-blocking)** — refund-policy windows (time limit / partial-refund rules / who bears
  the Stripe fee on refund). This ADR makes the *amount* a caller input (`RefundRequest.Amount`) and does
  not hard-code a policy; the default is the cancel path's existing fee computation. The product policy is
  an owner decision; the seam supports any answer.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (distributed-systems, fiscal-compliance, pragmatic) attacked; the Lead
re-verified every citation against the real code and adjudicated. **Verdict: all challenges RESOLVED;
zero blocking (two owner questions escalated, neither blocks CZ/SK/PL launch); consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 | A boolean `PaymentStatus.Refunded` cannot represent partial/multiple/chargeback refunds → `refundable` is uncomputable, double-refund still possible (MAJOR) | CONCEDE + REVISE | D5 `Refund` projection; `PartiallyRefunded`; `refundable(order)` |
| CH-2 (fiscal) | A refund of a registered BlockingOnline sale legally needs a corrective fiscal doc; silently skipping it is a compliance incident (CRITICAL for DE/AT/ES) | CONCEDE + ESCALATE | D6 fiscal boundary; Q-REFUND-01 bound to ADR-0004 go-live gate; seam ready, rule deferred to owner |
| CH-3 | Treating `charge.dispute.created` as an `IRefundService` call would double-move money (the bank already pulled funds) (MAJOR) | CONCEDE + REVISE | D4/D5 chargeback is a `Refund { Source = Chargeback }` reconciliation row, never a seam call |
| CH-4 | A chargeback can arrive with no in-app `Dispute` → `LinkStripeDispute` has nothing to link (MAJOR) | CONCEDE + REVISE | D4 create-if-absent `Dispute(Reason = Chargeback, Escalated)` |
| CH-5 | Migrating `CancelOrder` risks regressing its working (if un-keyed) refund + its already-correct ADR-0002 notification (MODERATE) | DEFEND | D1.1/D6 keep the notification enqueue in `CancelOrder`; only the money call moves; confirm-then-record (D7) preserves `:143` ordering |
| CH-6 | Partial refunds collide on a per-order key → a legitimate second partial would be deduped away (MAJOR) | CONCEDE + REVISE | D3 per-purpose key (`admin:{RefundRequestId}`); cancel/dispute are one-per-subject and collapse correctly |
| CH-7 | A Polly retry of the un-keyed Stripe refund (ADR-0005 D1.2) would double-refund unless the key lands first (CRITICAL — cross-ADR) | DEFEND + ALIGN | D3 makes the refund key load-bearing; ADR-0005 D1.2 only auto-retries keyed writes — the two ADRs are mutually consistent |
| CH-8 | "Confirm-then-record": if Stripe succeeds but our commit fails, a redelivery must not double-refund (MAJOR) | DEFEND | D3 same-key Stripe idempotency returns the same refund on retry; unique-index 23505 backstop resolves the row |

**Affirmed unchallenged:** one refund seam over two paths; deterministic key over random GUID; refund is
a payment fact not an order-lifecycle transition; admin-only authz; refund notification on the ADR-0002
seam; the chargeback create-if-absent linkage.

**Lead re-verification (against current code):** `ResolveDispute.cs:43-60` record-only (no
`IStripeClientFactory`); `Dispute.cs:32,82-90,104-108` `RefundAmount`/`Resolve`/`LinkStripeDispute`
(dead); `CancelOrder.cs:129-177` inline keyed-less Stripe refund (`:142`), `PaymentStatus.Refunded` flip
(`:143`), `RefundInitiated` (`:144,186`), ADR-0002 notification enqueue already present (`:158-176`);
`IStripeClient.cs:13` `RefundCheckoutSessionAsync` no idempotency key; `Constants.cs:21-54` no
`charge.dispute.*`; `HandlePaymentNotification.cs:188-213` order-resolution + event switch with no
dispute arm; `BusinessErrorMessage` `dispute.invalid_refund_amount` present.

**Escalations to the owner:** Q-REFUND-01 (per-country corrective fiscal document — legal, gated to
DE/AT/ES go-live), Q-REFUND-02 (refund-policy windows — product). Neither blocks the Wave-2 *seam*
implementation for CZ/SK/PL.
