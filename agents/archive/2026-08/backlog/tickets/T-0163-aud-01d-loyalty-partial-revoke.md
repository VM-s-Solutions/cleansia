---
id: T-0163
title: "AUD-01d: ILoyaltyService.RevokeForPartialRefundAsync (proportional, per-refund-keyed) + LoyaltyEarnSource.OrderPartiallyRefunded + filtered-unique-index backstop"
status: done
size: M
owner: —
created: 2026-06-06
updated: 2026-06-15
depends_on: [T-0160]
blocks: []
stories: []
adrs: [0009]
layers: [backend, db]
security_touching: false
manual_steps: [ef-migration]
sprint: 2
source: ADR-0009 D6 follow-up AUD-01d (proportional loyalty clawback on partial refund)
---

## Context

Wave-2 BUILD: the proportional loyalty clawback ADR-0009 D6 designed. The existing
`RevokeForCancelledOrderAsync` (`LoyaltyService.cs:99-147`) is a **full mirror** keyed on
`(orderId, OrderCancelled)` that **no-ops on a second call** (`:126-131`) — so a second partial refund
would silently revoke nothing. ADR-0009 D6 requires a **new per-refund-keyed** method that revokes
`floor(refundNet/10)` per refund, cumulative-capped at the original earn, skipping anonymous/legacy
(`UserId == null`), mirroring the manual `GrantPointsManuallyAsync`/`RevokePointsManuallyAsync` shape
(`:181-291`: `requestId` fast-path read + the S7b `FlushCollapsingUniqueViolationAsync` unique-index flush).

## Acceptance criteria
- [ ] **AC1 — New keyed partial-revoke method.** Given ADR-0009 D6, When this lands, Then
  `ILoyaltyService.RevokeForPartialRefundAsync(string orderId, decimal refundNet, string refundKey, string
  actorId, ct)` exists; it revokes `floor(refundNet / 10)` points; it is **idempotent on `refundKey`** (a
  second call with the same key revokes once). Evidence: a test — same `refundKey` twice → revokes once;
  two **different** partial refunds → each revokes (NOT a no-op, proving it is not the old mirror).
- [ ] **AC2 — Cumulative cap at the original earn.** Given the original `OrderCompleted` earn
  (`LoyaltyService.cs:46`, `floor(TotalPrice/10)`), When multiple partial refunds revoke, Then
  Σ(revoked across this order's partials) never exceeds the original earn magnitude. Evidence: a near-full
  set of partials cannot over-revoke.
- [ ] **AC3 — Anonymous/legacy skipped.** Given `UserId == null` skips earn (`:41-44`) and full-revoke
  (`:112-115`), When the order has no `UserId`, Then the partial revoke is a no-op. Evidence: a test.
- [ ] **AC4 — `LoyaltyEarnSource.OrderPartiallyRefunded` added.** Given the per-key idempotency must not
  collide with the cancel mirror's `(orderId, OrderCancelled)`, When this lands, Then a new
  `LoyaltyEarnSource.OrderPartiallyRefunded` enum value exists and the new method keys on it +
  `refundKey`. Evidence: enum value + the filtered-unique-index backstop on the key.
- [ ] **AC5 — S7b unique-index backstop.** Given concurrent double-submit, When two partial revokes with
  the same `refundKey` race, Then they collapse on a filtered unique index via
  `FlushCollapsingUniqueViolationAsync` (mirroring `:303-318`), not a double-revoke. Evidence: a
  concurrency test.

## Out of scope
- Calling `RevokeForPartialRefundAsync` from the refund command — that wiring is AUD-01c (the command) /
  AUD-01e (cancel/dispute). This ticket delivers the method + the enum + the index.
- The refund amount math (the caller passes `refundNet`) — AUD-01c.

## Implementation notes
- **Governing ADR:** ADR-0009 D6 (the exact signature is frozen there). The existing cancel mirror is
  **NOT reusable** (the no-op-on-second-call hazard, ADR-0009 fact 7 / CH-4).
- **Serialization (TICKET-MAP `LoyaltyService.cs` cluster):** `T-0112 (W0) → T-0148 → T-0143 (W1)` then the
  Wave-2 editors. This edits `LoyaltyService.cs` — **serialize against T-0148 and T-0158** (the outbox
  Bucket-B `LoyaltyService.cs` edit) and any other live `LoyaltyService.cs` editor; do not run concurrently.
- **`security_touching: false`** (loyalty points, not money/PII auth) — reviewer in parallel; no Security gate.
- **Manual step:** `ef-migration` (owner) — the new `LoyaltyEarnSource` value persistence + the filtered
  unique index on the refund key. Folds with AUD-01a's migration if sequenced together.
- **TEST-FIRST:** TC-REFUND-LOYALTY (per-refund key, cumulative cap, anon-skip, second-partial-revokes,
  same-key-idempotent) red-first.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0009 follow-up AUD-01d; depends_on T-0160; Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
