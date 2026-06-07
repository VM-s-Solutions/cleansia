---
id: T-0161
title: "AUD-01b: IRefundService implementation (one seam, ceiling clamp, deterministic RefundKey) + IStripeClient idempotency-key param"
status: draft
size: M
owner: —
created: 2026-06-06
updated: 2026-06-06
depends_on: [T-0160]
blocks: [T-0162]
stories: []
adrs: [0005, 0006, 0009]
layers: [backend, clients]
security_touching: true
manual_steps: [nswag-regen]
sprint: 2
source: ADR-0006 D1/D2/D3 follow-up AUD-01b
---

## Context

Wave-2 BUILD: the single refund **seam** ADR-0006 D1 froze. Today there are two broken money paths —
`ResolveDispute` records a refund but never calls Stripe (`ResolveDispute.cs:43-60`), and `CancelOrder`
issues an inline **un-keyed** Stripe refund (`CancelOrder.cs:142`,
`IStripeClient.RefundCheckoutSessionAsync` has no idempotency key, `IStripeClient.cs:13`). This child adds
`IRefundService.IssueRefundAsync` as the one place money leaves via Stripe, and adds the idempotency-key
parameter to `IStripeClient`'s refund call so a Polly retry (ADR-0005 D1.2) or a handler retry cannot
double-refund.

## Acceptance criteria
- [ ] **AC1 — `IRefundService.IssueRefundAsync` exists (the one seam).** Given ADR-0006 D1, When this
  lands, Then `IRefundService.IssueRefundAsync(RefundRequest, ct)` returns `BusinessResult<RefundResult>`,
  calls Stripe (ADR-0005 classified + keyed), records the `Refund` projection (T-0160) + the payment-status
  transition **after** Stripe confirms (confirm-then-record, ADR-0006 D7), and does **not** enqueue
  notifications (the caller does). Evidence: handler test — a successful refund writes exactly one `Refund`
  row (Status=Succeeded) + `PaymentStatus` transition.
- [ ] **AC2 — Deterministic `RefundKey`.** Given ADR-0006 D3, When the seam issues a refund, Then the
  Stripe call carries `RefundKey = refund:{OrderId}:{purpose}` (purpose ∈ {cancel, dispute:{DisputeId},
  admin:{RefundRequestId}}) as Stripe's `IdempotencyKey` — never `Guid.NewGuid()`/timestamp. Evidence:
  TC-KEY-REFUND — two invocations with the same domain inputs emit the same key.
- [ ] **AC3 — Refundable ceiling clamp + 23505 resolve-to-existing.** Given ADR-0006 D2, When the seam
  runs, Then it clamps `Amount` to `refundable(order) = amountCharged − Σ(succeeded refunds)` (read from the
  `Refund` projection), and a concurrent double-issue has one winner — the loser catches PG 23505 on the
  unique `RefundKey` index (T-0160 AC4) and resolves to the existing refund (ack, not a 500). Evidence:
  TC-IDEMP-REFUND — a retried same-key issue produces exactly one Stripe refund + one `Refund` row.
- [ ] **AC4 — `IStripeClient` refund gains the idempotency-key param.** Given `IStripeClient.cs:13` has no
  key today, When this lands, Then `RefundCheckoutSessionAsync` takes an idempotency key and passes it to
  Stripe; the ADR-0005 retry policy auto-retries this write **only because** it is now keyed. Evidence:
  TC-RETRY-IDEMP — a transient failure retries with the same key, no second refund.
- [ ] **AC5 — The seam is the ONLY Stripe refund call site.** Given ADR-0006 verification #1, When the
  codebase is grepped, Then `RefundCheckoutSessionAsync` / any Stripe refund API appears **only** inside
  `IRefundService`'s implementation (the `CancelOrder.cs:142` call is removed by AUD-01e). Evidence: a grep
  check / reviewer confirmation.

## Out of scope
- The admin partial-refund command, the share-of-`TotalPrice` allocator, `RefundPolicy` window/fee — AUD-01c.
- Loyalty clawback — AUD-01d. Migrating `CancelOrder`/`ResolveDispute` callers onto the seam — AUD-01e.
- The fiscal corrective-document registration (DE/AT/ES) — the T-0220/T-0221 go-live cluster (ADR-0009 D7).

## Implementation notes
- **Governing ADRs:** ADR-0006 D1/D2/D3/D7 (seam, amount, key, confirm-then-record), ADR-0005 D1.2
  (idempotency-aware retry — only keyed writes auto-retry), ADR-0009 (the policy the caller applies, not
  the seam). **The window is NOT enforced in the seam** (ADR-0009 D1 / verification #1) — the seam enforces
  only the ceiling + idempotency.
- **Serialization (TICKET-MAP `EmailService.cs`/`StripeClient.cs` cluster + the refund money-path):** the
  `IStripeClient` refund-method change touches `StripeClient.cs`; serialize against T-0144 (BLIND-5 pooled
  client) if both are in flight (T-0144 first per its cluster). AUD-01b depends on T-0160 (the `Refund`
  entity) being `done` + the owner migration applied.
- **`security_touching: true`** — a money-out path; Security reviews the no-double-refund property and that
  no non-seam refund call survives.
- **Manual step:** `nswag-regen` only if a refund response DTO surface changes; flag at implementation time.
- **TEST-FIRST:** TC-7 (refund money-math), TC-IDEMP-REFUND, TC-KEY-REFUND, TC-RETRY-IDEMP red-first.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0006 follow-up AUD-01b; depends_on T-0160; Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
