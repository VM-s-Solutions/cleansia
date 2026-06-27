---
id: T-0341
title: "Backend: deterministic order status-history 'current status' ordering (same-tick CreatedOn tie) + de-flake AdminOverrideOrderStatus tests"
status: proposed
size: M
owner: backend
created: 2026-06-27
updated: 2026-06-27
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: discovered while running the backend suite locally during the T-0339 verification (pre-existing flaky test on master)
---

> Pre-existing flaky test on `master`, unrelated to T-0339 (verified: the T-0339 branch never touched
> `AdminOverrideOrderStatus`). Surfaced when the full backend suite was run locally (real Postgres) during the
> T-0339 fix.

## The root cause

The "current status" of an order is derived in several places as:

```csharp
order.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).First().Status
```

(e.g. `AdminOverrideOrderStatus.cs:78-79`, the `GetPagedOrders` `OrderStatuses` filter clause, and the test
assertions). `OrderStatusTrack.Create` stamps `CreatedOn = UtcNow`. When two tracks are appended in the **same
tick** (e.g. a test's `ArrangeOrder(Confirmed, OnTheWay)` adds both in a tight loop, or any rapid back-to-back
transition), the two rows **tie on `CreatedOn`**, so `OrderByDescending(CreatedOn).First()` is
**nondeterministic** — it may return the earlier status. The "current status" then reads wrong.

**Symptom:** `Cleansia.Tests.Features.Orders.AdminOverrideOrderStatusHandlerTests` flakes (1–2 of 7 fail,
varying) — e.g. `Admin_Override_Backwards_Returns_InvalidTransition`: `Expected: OnTheWay, Actual: Confirmed`.
Environment-sensitive (trips reliably on a fast Apple-Silicon machine; may pass on slower CI runners). Because
`backend-ci` runs `Cleansia.Tests` first (fail-fast), a CI hit here reds the run before the integration tests.

Not a production data-corruption bug in practice (real lifecycle transitions occur ms apart, not same-tick), but
it is a latent correctness hazard in the derivation pattern and a real CI-flake source.

## Fix (decide the approach — likely an architect call on the canonical "current status" derivation)

Make the status-history ordering **deterministic on ties**. Options:
- Add a monotonic tiebreaker to the ordering everywhere it's used: `OrderByDescending(s => s.CreatedOn)
  .ThenByDescending(s => s.<seq>)` — needs a strictly-increasing per-order sequence/index on `OrderStatusTrack`
  (a new column → **EF migration**, owner manual_step), or
- ensure `OrderStatusTrack` timestamps are strictly monotonic per order at append time, or
- introduce a single canonical `Order.CurrentStatus` derivation (one place, deterministic) and route all call
  sites (AdminOverrideOrderStatus, GetPagedOrders, etc.) through it.
Recommend the architect rule the canonical shape (it's the "one way to derive current status").

Then **de-flake the tests**: `AdminOverrideOrderStatusHandlerTests` (and any sibling using `ArrangeOrder` with
multiple tracks) should be deterministic once the production ordering is — verify by running the class repeatedly.

## Done when
- [ ] The status-history "current status" derivation is deterministic regardless of equal `CreatedOn` (no tie).
- [ ] `AdminOverrideOrderStatusHandlerTests` passes 20×/20× locally (no flake).
- [ ] All call sites of the `OrderByDescending(CreatedOn).First()` status pattern use the canonical derivation.

## Notes
- Reproduced locally: `dotnet test src/Cleansia.Tests --filter AdminOverrideOrderStatusHandlerTests` fails
  1–2/7 across repeated runs on an Apple-Silicon Mac.
- If a sequence column is added to `OrderStatusTrack`, that's an owner **EF migration** manual_step.

## Status log
- 2026-06-27 — filed. Pre-existing flaky test found during the local backend-suite run for the T-0339
  verification (the integration-test fix is separate, already landed `fbe21e8`). Unrelated to T-0339.
