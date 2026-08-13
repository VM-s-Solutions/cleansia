---
id: T-0164
title: "AUD-01e: Migrate CancelOrder + ResolveDispute onto the IRefundService seam (remove inline un-keyed Stripe refund; honour the window-exempt chargeback row)"
status: done
size: M
owner: —
created: 2026-06-06
updated: 2026-06-15
depends_on: [T-0160, T-0161]
blocks: []
stories: []
adrs: [0006, 0009]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 2
source: ADR-0006 D1.1 follow-up AUD-01e (migrate the two existing money paths onto the one seam)
---

## Context

Wave-2 BUILD: close the two legacy money paths by routing them through the one `IRefundService` seam
(AUD-01b). Today:
- `ResolveDispute.Handler` (`ResolveDispute.cs:43-60`) records `RefundAmount` + `Status = Resolved` but
  **never calls Stripe** — the money is refunded on paper only (ADR-0006 fact 1).
- `CancelOrder.Handler` (`CancelOrder.cs:129-177`) issues an inline **un-keyed** Stripe refund (`:142`),
  flips `PaymentStatus.Refunded` (`:143`) — the double-refund hazard (ADR-0006 fact 2). Its ADR-0002
  notification enqueue (`:158-176`) is already correct and **stays**.

ADR-0006 D1.1 decides: `ResolveDispute` **records intent and calls the seam** (Reason=DisputeResolution,
DisputeId set); `CancelOrder` **migrates onto the seam** (Reason=CustomerCancellation) — only the money call
moves; the notification enqueue stays in `CancelOrder`; confirm-then-record preserves the `:143` ordering.

## Acceptance criteria
- [ ] **AC1 — `ResolveDispute` issues a real refund via the seam.** Given it sends nothing today, When a
  dispute resolves with a refund amount, Then `ResolveDispute.Handler` calls
  `IRefundService.IssueRefundAsync` (Reason=DisputeResolution, DisputeId set) — it does **not** gain a raw
  `IStripeClientFactory`; the dispute is `Resolved` with `RefundAmount` **and** the refund is actually sent.
  Evidence: a handler test — resolving with a refund issues exactly one Stripe refund + one `Refund` row.
- [ ] **AC2 — `CancelOrder` uses the seam, not the inline call.** Given the inline `:142` call, When a
  cancel-with-refund runs, Then it calls `IRefundService.IssueRefundAsync` (Reason=CustomerCancellation) with
  the `refundAmount` `order.Cancel(...)` computes; the inline `RefundCheckoutSessionAsync` at
  `CancelOrder.cs:142` is **removed**; the ADR-0002 `OrderRefunded` enqueue (`:158-176`) **remains** in
  `CancelOrder`. Evidence: a grep that `CancelOrder` no longer calls Stripe refund directly; the
  notification test still passes.
- [ ] **AC3 — Confirm-then-record (no phantom Refunded).** Given today `:143` flips `PaymentStatus.Refunded`
  inside the handler, When a Stripe failure occurs, Then the payment-status transition happens **inside the
  seam after Stripe confirms** (ADR-0006 D7); on failure the response reports `RefundInitiated = false` and
  the requested amount, never a phantom "Refunded". Evidence: a test — a simulated Stripe failure leaves
  `PaymentStatus` un-flipped.
- [ ] **AC4 — Retried cancel/dispute cannot double-refund.** Given the deterministic `RefundKey`
  (cancel→`refund:{OrderId}:cancel`, dispute→`refund:{OrderId}:dispute:{DisputeId}`), When the same
  cancel/resolve is retried, Then exactly one Stripe refund + one `Refund` row result. Evidence:
  TC-IDEMP-REFUND on both paths.
- [ ] **AC5 — Window-exempt chargeback row honoured.** Given ADR-0009 D1 + ADR-0006 D5, When a
  `Source=Chargeback` reconciliation row exists, Then it is **not** subject to the refund window and is
  counted in `refundable(order)` so an admin cannot double-refund a charged-back order. Evidence: a test
  (the chargeback row reduces the refundable ceiling; the window check is skipped for it).

## Out of scope
- The `charge.dispute.*` webhook case + `LinkStripeDispute` wiring — D-06 (T-0174), separate ticket.
- The admin partial-refund command / allocator — AUD-01c (T-0162). The loyalty clawback method — AUD-01d
  (T-0163) (this ticket may **call** it for the cancel/dispute paths per ADR-0009 D6 if AUD-01d is `done`).
- The fiscal corrective registration — T-0220/T-0221 go-live cluster.

## Implementation notes
- **Governing ADRs:** ADR-0006 D1.1/D2/D7 (which path records vs issues; confirm-then-record), ADR-0009 D1
  (chargeback window-exempt). Depends on T-0160 (`Refund` entity) + T-0161 (the seam) being `done`.
- **Serialization (TICKET-MAP):** `CancelOrder.cs` is in the money-path/CreateOrder-evolution surface;
  serialize the AUD-01 children that edit `CancelOrder.cs` against each other and against the
  `D-01/DA-1/SEC-DSP-06/07` (T-0173) dispute bundle if it reaches the same files. The `AddDisputeMessage`/
  dispute backend cluster (T-0102 → T-0118 W0; T-0172 → T-0173 W2) is adjacent — sequence accordingly.
- **`security_touching: true`** — money-out paths; Security verifies no inline Stripe refund survives and
  the no-double-refund property holds on both paths.
- **No manual step** (no schema/DTO change here; the `Refund` migration is T-0160, the key param is T-0161).
- **TEST-FIRST:** TC-IDEMP-REFUND on cancel + dispute, the confirm-then-record failure test, red-first.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0006 follow-up AUD-01e; depends_on T-0160, T-0161; Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
