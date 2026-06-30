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
manual_steps: [db-migration]  # architect ruling 2026-06-30: pre-prod Initial-regen adds OrderStatusTrack.Sequence
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

## Architect ruling (2026-06-30)

**VERDICT ON THE SCHEMA QUESTION: a schema change IS needed — add a strictly-increasing `Sequence` column
to `OrderStatusTrack`. The "ULID-Id tiebreaker, no migration" option is REJECTED as unsound.** This is a
pre-prod **Initial-regen** (no production data exists; the single `20260623112626_Initial` migration is
regenerated, not a new incremental migration), which is the owner-authorized class I can run — **flagged
below as the manual_step.**

### Why the no-migration ULID-Id tiebreaker does NOT work (the load-bearing finding)

`OrderStatusTrack : Auditable : BaseEntity`, and `BaseEntity.Id = Ulid.NewUlid().ToString()` — a ULID, so
on the surface a `ThenByDescending(s => s.Id)` looks free. It is not sound here:

- The package is **`Ulid` 1.4.1 (Cysharp/Ulid)** (`Directory.Packages.props:37`), and the only factory used
  anywhere is the bare **`Ulid.NewUlid()`** (`BaseEntity.cs:5`; the two other call sites are unrelated). That
  factory is **NOT monotonic within a millisecond** — the 48-bit timestamp is millisecond-resolution and the
  80-bit tail is **fresh-random per call**, not an incremented value. Two ULIDs minted in the same ms have a
  **random** lexical order relative to each other.
- A `CreatedOn` tie (the bug) is `DateTimeOffset.UtcNow` colliding — i.e. the two tracks were appended within
  the **same millisecond**, which is exactly the window where the ULID tail is random vs. creation order.
- So `ThenByDescending(Id)` would make the result **deterministic but not necessarily correct**: for a given
  persisted Id set the answer is stable, but ~50% of same-ms pairs sort the *earlier-created* track last. For
  the flaky test that is arguably *worse* than a flake — it would convert an intermittent failure into a
  **consistently-wrong** one for whichever Ids that run happened to mint. It also leaves the production
  latent-correctness hazard (wrong "current status" on a genuine same-ms transition) **unfixed**.
- "Make timestamps strictly monotonic at append time" (option 2 in the ticket) is also rejected: it pushes a
  clock-ordering invariant into every append path, is fragile across instances/retries, and a `DateTimeOffset`
  is still not a guaranteed-unique key. The correct primitive for "append order" is an **append counter**, not
  a timestamp.

The conclusion that "an existing monotonic field already encodes append order" is **false for this codebase's
ULID flavor**. A real strictly-increasing field is required.

### The canonical derivation (single source of truth — route ALL call sites through it)

Add a per-order, strictly-increasing **`Sequence`** to `OrderStatusTrack`, assigned at append time, and a
single domain helper on `Order`:

```
// Order.cs — the ONE way to read current status. Tiebreaks on the monotonic append sequence.
public OrderStatus? CurrentStatus =>
    _orderStatusHistory
        .OrderByDescending(s => s.CreatedOn)
        .ThenByDescending(s => s.Sequence)
        .FirstOrDefault()?.Status;
```

- `Sequence` is an `int`, assigned in `Order.AddOrderStatus` as `(_orderStatusHistory.Count == 0 ? 0 :
  _orderStatusHistory.Max(s => s.Sequence) + 1)` — derived from the aggregate's own history (the aggregate is
  the consistency boundary; no DB round-trip, no global counter, works in the in-memory unit test exactly as
  in Postgres). `OrderStatusTrack.Create(status, order)` takes the next sequence from the order. Keep
  `CreatedOn` as the **primary** sort (human-meaningful, and correct across the normal ms-apart case);
  `Sequence` is the deterministic tiebreaker that also happens to be globally correct on same-ms ties.
- **`Order.CurrentStatus`** is the canonical helper. Route the in-memory call sites through it:
  `AdminOverrideOrderStatus.cs:78-79`, `AdminCancelOrder.cs:72`, `CancelOrder.cs:78`, `NotifyOnTheWay.cs:66`,
  `StartOrder.cs:82`, `CompleteOrder.cs:99`, `OrderMappers.cs:13` (`GetCurrentStatus`), and the test
  assertions in `AdminOverrideOrderStatusHandlerTests` / siblings.
- **The two IQueryable (translated-to-SQL) call sites** cannot call the C# property — keep them as an
  EF-translatable expression but add the same tiebreaker so they agree with the helper:
  `OrderSpecification.cs:111` (`GetPagedOrders` `OrderStatuses` filter) and
  `OrderRepository.cs:189-191` — both become `.OrderByDescending(s => s.CreatedOn).ThenByDescending(s =>
  s.Sequence)`. (Postgres orders a tie on `Sequence` deterministically; this is the SQL mirror of the helper.)
  `StartOrder.cs:112-113` (the `AnyAsync(... OrderByDescending(CreatedOn) ...)` latest-status probe) gets the
  same `ThenByDescending(Sequence)`. The `.Any(h => h.Status == X)` existence checks
  (`ReferralService`, `DataRetentionBackgroundService`, `StaleOrderCleanupService`, `CancelOrder` accepted-check)
  do not depend on ordering and are **out of scope** — do not touch them.

### Schema change: explicit verdict

- **A new column IS added:** `OrderStatusTrack.Sequence` (`int`, `NOT NULL`). This is a schema change.
- **Class:** **pre-prod Initial-regen, owner-authorized.** There is no production data (single
  `20260623112626_Initial`), so this folds into the **regeneration of the Initial migration** rather than a
  new incremental migration — the regen-class the architect/owner workflow permits me to run. It is **not** a
  data-migration of live rows.
- **manual_step (flag):** `db-migration` — regenerate `Initial` so `OrderStatusHistory` gains the `Sequence`
  column. Update the front-matter `manual_steps: [db-migration]` and the `Done when` note (the ticket
  currently says "(owner manual_step)" for this branch — confirmed, it applies). No NSwag/codegen impact
  (`Sequence` is internal ordering state, not surfaced on any DTO).

### Why a schema change earns its place (the seam argument)

"Current status" is read in **9+ places** across handlers, a mapper, a spec, and the repository — it is a core
read of the Order aggregate. Encoding append order in a real monotonic field (vs. leaning on a
timestamp-or-random-ULID heuristic) makes that read **correct by construction** everywhere and gives us one
canonical `Order.CurrentStatus` to converge on. The migration is one-time and free pre-prod; the alternative
(a heuristic that is "usually right") is the kind of latent hazard that surfaces as a production incident on a
fast box exactly as it surfaced as a CI flake here. Decisive: **migrate.**

### De-flake gate

Once `Order.CurrentStatus` + the SQL tiebreaker land, `AdminOverrideOrderStatusHandlerTests` is deterministic
because `Sequence` reflects the `ArrangeOrder(...)` insertion order regardless of equal `CreatedOn`. Gate:
the class passes **20×/20×** locally; update the test assertions to read `order.CurrentStatus` (not the raw
`OrderByDescending(CreatedOn).First()`), so the test encodes the canonical derivation rather than re-deriving it.
