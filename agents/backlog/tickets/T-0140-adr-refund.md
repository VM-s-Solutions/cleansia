---
id: T-0140
title: "ADR-REFUND: refund/dispute money path (who issues Stripe refund, where RefundAmount is consumed, chargeback linkage)"
status: draft
size: M
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: []
blocks: []
stories: []
adrs: [0001, 0002]
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 1
source: defense-panel ADR; theme 3
---

## Context

Defense-panel theme 3 ("the money path"): refunds and disputes are written in two unrelated places
with two different, partly-broken behaviors, and chargeback linkage is dead. This ticket produces a
**Wave-1 foundational ADR** (`ADR-REFUND`, the next free number in `agents/backlog/adr/`) that freezes
the refund/dispute money-path *contract* — it does not build the admin refund UI or the chargeback
handler. Those are the Wave-2 consumers (`AUD-01`, the `D-01/DA-1/SEC-DSP-06/07` bundle, `D-06`) which
the TICKET-MAP already lists as `depends_on: ADR-REFUND`. The ADR must be `accepted` before any of
them can go `ready`.

The three verified holes in the real code this ADR must adjudicate:

1. **Dispute refunds are recorded but never issued.** `ResolveDispute.Handler`
   (`Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:43-60`) takes a
   `decimal? RefundAmount`, validates it `>= 0` (`:26-29`), and passes it to
   `dispute.Resolve(...)` (`:53-57`) which only sets `RefundAmount` / `Status = Resolved` on the
   entity (`Cleansia.Core.Domain/Disputes/Dispute.cs:82-90`). **No Stripe call is ever made** — there
   is no `IStripeClientFactory` dependency in the handler. The money is "refunded" on paper only.

2. **Order-cancel refunds are issued inline, with a different, un-keyed flow.** `CancelOrder.Handler`
   (`Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:129-180`) computes `refundAmount`,
   calls `stripe.RefundCheckoutSessionAsync(order.StripeSessionId, refundAmount, ct)` (`:142`) inside
   a narrow `StripeException` try/catch, flips `PaymentStatus.Refunded` (`:143`), and reports
   `RefundInitiated` (`:144`). There is **no idempotency key** on the refund call
   (`IStripeClient.RefundCheckoutSessionAsync`, `Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs:13`),
   so a retried cancel can double-refund, and the dispute path that *should* refund does not use this
   flow at all. Two money paths, one of them silently no-ops.

3. **Chargeback linkage is dead code.** `Dispute.LinkStripeDispute(...)` and the `StripeDisputeId`
   column (`Dispute.cs:38,104-108`) have **no producer**: the Stripe event catalog
   (`Cleansia.Core.AppServices/Common/Constants.cs:21-46`, `StripeEventType`) handles only
   `checkout.session.*`, `payment_intent.*`, and `customer.subscription.*` — there is **no
   `charge.dispute.*` case** in `HandlePaymentNotification` (`Features/Payments/HandlePaymentNotification.cs`).
   A real Stripe-side chargeback is never reflected against our `Dispute`.

The ADR also governs (and must stay consistent with) the post-commit dispatch contract in ADR-0002
(any notification side effect of a refund) and the authorization model in ADR-0001 (who may issue a
refund).

## Acceptance criteria

- [ ] **AC1 — ADR file exists and is `accepted`.** Given the `agents/templates/adr.md` template,
  When the architect writes `agents/backlog/adr/NNNN-refund-dispute-money-path.md` (next free number),
  Then it has frontmatter `Status: accepted`, `Date: 2026-06-01`, `Applies to: backend`, and a
  Challenge/Defense/Verdict trail per `agents/process/deliberation.md` with **zero blocking**
  challenges, mirroring the structure of ADR-0002.

- [ ] **AC2 — Single owner of "issue a refund" is named.** Given the two live refund call shapes
  (dispute = record-only at `ResolveDispute.cs:53`, cancel = inline Stripe at `CancelOrder.cs:142`),
  When the ADR's Decision is read, Then it names **one** seam through which a Stripe refund is issued
  (e.g. a refund application-service / domain method), states whether `ResolveDispute` issues the
  refund itself or records intent for that seam, and states whether `CancelOrder` is migrated onto the
  same seam or keeps its inline call under a documented carve-out — with the reason cited.

- [ ] **AC3 — `RefundAmount` consumption is pinned end-to-end.** Given `Dispute.RefundAmount`
  (`Dispute.cs:32`) and the `CancelOrder.Response.RefundAmount` (`CancelOrder.cs:29`), When the ADR
  is read, Then it specifies exactly **where each `RefundAmount` is consumed** (which field is the
  source of truth for the amount actually sent to Stripe, how it is reconciled with `Order.TotalPrice`
  and any cancel fee, and what `PaymentStatus`/dispute-`Status` transitions are written) so no amount
  is recorded-but-not-sent or sent-but-not-recorded.

- [ ] **AC4 — Refund idempotency is frozen.** Given `IStripeClient.RefundCheckoutSessionAsync` has no
  idempotency key today (`IStripeClient.cs:13`) and ADR-0002's deterministic-key principle, When the
  ADR is read, Then it freezes a **deterministic refund idempotency key formula** (keyed on the
  order/dispute identity) and states the assertion that prevents a retried `ResolveDispute`/`CancelOrder`
  from double-refunding — and names this as a TC-7 (refund money-math) / TC-IDEMP obligation.

- [ ] **AC5 — Chargeback linkage path is defined.** Given `LinkStripeDispute`/`StripeDisputeId` are
  unreferenced and `charge.dispute.*` is absent from `StripeEventType` (`Constants.cs:21-46`), When
  the ADR is read, Then it specifies how an inbound Stripe chargeback maps to our `Dispute`
  (which `charge.dispute.*` event(s), how the Stripe event resolves to a `Dispute`/`Order`, when
  `LinkStripeDispute` is called and by what status transition) and explicitly hands the implementation
  to the Wave-2 `D-06` ticket — without implementing the webhook case here.

- [ ] **AC6 — Authorization + dispatch boundaries cited.** Given ADR-0001 (authz) and ADR-0002
  (post-commit dispatch), When the ADR is read, Then it states which `Policy.*` gate may issue a refund
  (admin-only) and routes any refund-success notification through `IPendingDispatch` per ADR-0002 D1
  (no direct `IQueueClient.SendAsync` from the refund handler), rather than re-deciding either contract.

- [ ] **AC7 — Consumers and tests enumerated.** Given the TICKET-MAP Wave-2 rows that depend on this
  ADR, When the ADR's "Consequences / rollout" is read, Then it lists the dependent tickets
  (`AUD-01`, `D-01/DA-1/SEC-DSP-06/07`, `D-06`) and the test obligations (TC-7 refund money-math,
  refund-idempotency) that must land **with** the implementation, so the implementer has the test
  contract before writing code (test-first per `agents/knowledge/testing.md`).

## Out of scope

- Implementing the admin "issue refund" command/endpoint or UI (that is `AUD-01` and the
  `D-01/DA-1/SEC-DSP-06/07` bundle, Wave 2).
- Implementing the `charge.dispute.*` webhook case / `LinkStripeDispute` wiring (that is `D-06`, Wave 2).
- Migrating `CancelOrder`'s inline refund call (only *decided* here; executed by the consumer ticket).
- Adding the refund idempotency key to `IStripeClient.RefundCheckoutSessionAsync` (decided here,
  implemented by the consumer ticket — note it will be a `nswag-regen` flag for those tickets if the
  DTO/response surface changes; this ADR ticket itself ships no code and no manual step).
- Redefining the authorization model (ADR-0001) or the dispatch contract (ADR-0002) — cite, don't redo.

## Implementation notes

- **Deliverable is the ADR document only** — no production code, no migration, no NSwag change in this
  ticket. `manual_steps: []` is correct. Layers `architect` (authors the ADR) then `backend` (sanity-
  reviews that the frozen contract is implementable against the real handlers) — the backend layer here
  is a review pass, not a code change.
- **Build the ADR TEST-FIRST in spirit** per `agents/knowledge/testing.md` "TDD — write the test first":
  refund math is on the strict red-green list, so the ADR must hand the implementer an executable test
  contract (TC-7 refund money-math + refund-idempotency cases per AC4/AC7) that predates any consumer
  code. The ADR specifies the cases; the consumer tickets write the failing tests first.
- **Governing ADRs:** ADR-0001 (authorization — who may refund, AC6) and ADR-0002 (post-commit
  dispatch — refund notifications go through `IPendingDispatch`, AC6). This ADR must not contradict or
  re-decide either.
- **Serialization cluster:** This ticket writes a **new** ADR file only and edits no shared source, so
  it is **not** in any TICKET-MAP shared-file serialization cluster. However, the dispute-backend
  cluster `AddDisputeMessage.Handler` + dispute controllers (SEC-DSP-01 → DA-2 → D-01 bundle) and the
  `CreateOrder.cs`/`CancelOrder.cs` evolution are the **downstream** surfaces the consumers of this ADR
  will touch — the ADR must be written so those consumers can land in their existing cluster order
  without a second contract rewrite.
- **Key file:line anchors for the architect (all verified 2026-06-01):**
  - `Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:43-60` — record-only refund.
  - `Cleansia.Core.Domain/Disputes/Dispute.cs:32,82-90,104-108` — `RefundAmount`, `Resolve`, `LinkStripeDispute`.
  - `Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:129-180` — inline Stripe refund, no key.
  - `Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs:13` — `RefundCheckoutSessionAsync` (no idempotency key).
  - `Cleansia.Core.AppServices/Common/Constants.cs:21-46` — `StripeEventType` (no `charge.dispute.*`).
  - `Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs:201` — the event `switch` chargebacks must join.
  - `Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs:199` — `dispute.invalid_refund_amount`.

## Status log
- 2026-06-01 — draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
