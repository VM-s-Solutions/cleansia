---
id: T-0529
title: Digest watermark can never advance under multi-tenancy (tenant-scoped read in a tenant-ignoring sweep)
status: draft
size: S
owner: pm
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0028]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0036-C-digest.md` CH-Q8.3.
  Owner-verified 2026-08-02. A defect that belongs to no ADR. **Latent today, fatal the day
  multi-tenancy is switched on.**
---

## Context

`NewJobsDigestService` reads its cleaners with `GetQueryableIgnoringTenant()`
(`src/Cleansia.Core.AppServices/Services/NewJobsDigestService.cs:63`) — deliberately, because the sweep is
platform-wide — and **never sets a tenant override** for the iteration. But the write-back does the
opposite:

```csharp
// NewJobsDigestService.cs:211-220
private async Task StampWatermarkAsync(string employeeId, DateTimeOffset stamp, CancellationToken ct)
{
    var employee = await employeeRepository.GetByIdAsync(employeeId, ct);
    if (employee is null) return;            // ← :217, silent
    employee.MarkNewJobsDigestSent(stamp);
    await unitOfWork.CommitAsync(ct);
}
```

`EmployeeRepository.GetByIdAsync` (`src/Cleansia.Infra.Database/Repositories/EmployeeRepository.cs:44-51`)
uses `GetDbSet()` — **tenant-scoped**, so the global query filter applies. For any cleaner whose
`TenantId` is **not null**, the lookup returns `null` and `:217` **returns silently — no log, no throw** —
*after* `notificationProducer.NotifyAsync` at `:168` has already enqueued the push.

**Result:** the watermark never moves for that cleaner, so `sinceUtc` at `:90` is frozen (worst case
`DateTimeOffset.MinValue` for a never-notified cleaner), and **the same cleaner is pushed about the same
jobs on every 30-minute sweep, forever.** 48 duplicate pushes a day, per tenanted cleaner, with an
ever-widening candidate set.

Harmless today — `TenantId` is null on every row in single-tenant mode (`CLAUDE.md`) — and **guaranteed
the moment a tenant is created.** The existing coverage cannot see it:
`ColdPathCurrentStatusQueryTests.cs:53` wires `new FixedTenantProvider(tenantId: null)`.

**The fix already exists in the same class**, `EmployeeRepository.cs:53-57`:

```csharp
public Task<Employee?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
{
    return GetQueryableIgnoringTenant()
        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
}
```

`GetQueryableIgnoringTenant()` is `GetDbSet().IgnoreQueryFilters()`
(`src/Cleansia.Infra.Database/BaseRepository.cs:153-156`) — **still change-tracked**, so
`MarkNewJobsDigestSent` + `CommitAsync` behave exactly as today and the watermark/outbox atomicity the
comment at `:179-181` depends on is preserved. It also drops the three `Include`s
(`User`, `Address`, `Address.Country`) that the current call fetches per cleaner per sweep to flip one
`DateTimeOffset?`.

**Cross-reference ADR-0028** (multi-tenancy activation) and
`agents/architecture/decisions/multi-tenancy-and-region.md`: this is the class of defect that makes
"turn multi-tenancy on" a bigger switch than it looks. It is filed as its own ticket precisely so the
activation work inherits a fixed sweep rather than discovering this in production.

## Acceptance criteria

- [ ] **AC1 — the watermark advances for a tenanted cleaner.** Given a cleaner with a **non-null**
      `TenantId` who qualifies for a digest, When a sweep runs, Then `LastNewJobsDigestAt` is advanced to
      the sweep-start instant; And When a second sweep runs with no new orders, Then **no** push is
      enqueued. **Evidence:** an automated test with a non-null tenant — the first test in this area that
      does not wire `tenantId: null`.
- [ ] **AC2 — single-tenant behaviour is unchanged.** Given a cleaner with `TenantId == null`, When a
      sweep runs, Then the behaviour is byte-for-byte what it is today.
- [ ] **AC3 — the atomicity guarantee survives.** Given the change, When the stamp commits, Then the feed
      + outbox rows and the watermark still commit **together**, per the comment at `:179-181`.
      **Evidence:** reviewer confirmation tied to the diff — the entity must still be tracked. **Do not**
      reach for `ExecuteUpdateAsync` here: it commits outside the change tracker and breaks this guarantee.
- [ ] **AC4 — a missing employee is no longer silent.** Given `StampWatermarkAsync` finds no employee for
      an id the sweep just selected, When it returns early, Then it logs a warning naming the employee id.
      That state is now genuinely anomalous (the sweep read the row seconds earlier); today it is the
      normal path for a tenanted cleaner and says nothing.
- [ ] **AC5 — no other tenant-scoped read hides in this sweep.** Given the whole of
      `NewJobsDigestService`, When it is walked, Then every repository call is either tenant-ignoring or
      documented as intentionally tenant-scoped. **Known second instance, in scope for the walk but NOT
      for the fix:** `orderRepository.HasOverlappingOrderAsync` (`OrderRepository.cs:281`) uses
      `GetDbSet()` and is called at `:137` — under a tenant it would silently return `false` for every
      cleaner and the digest would start pushing double-booked jobs. If AC5's walk confirms it, **record it
      in the status log and file it**; do not widen this ticket.
- [ ] **AC6 — the over-fetch is gone.** Given the new call, When the sweep runs, Then the stamp no longer
      loads `User`, `Address` and `Address.Country` per cleaner per sweep.

## Out of scope

- The **correctness** defect in the same method's caller — **T-0528** (skipped orders burned). Serialize:
  both edit `NewJobsDigestService.cs`, so **these two tickets must not run concurrently.**
- Hoisting the per-cleaner `CommitAsync` out of the loop / clearing the change tracker per iteration
  (an O(C²) `DetectChanges` cost the optimizer lane raised). Real, separate, cost-shaped.
- Fixing `HasOverlappingOrderAsync`'s tenancy (see AC5 — walk it, file it, do not fix it here).
- Turning multi-tenancy on. ADR-0028's lane.

## Implementation notes

Swap `employeeRepository.GetByIdAsync` → `employeeRepository.GetByIdIgnoringTenantAsync` at
`NewJobsDigestService.cs:216`, add the AC4 warning log. That is the whole code change.

**Why this is `security_touching`.** It changes tenancy scoping on a write path
(`agents/process/quality-gates.md` Gate 3 names tenancy scoping explicitly). The security reviewer's job
here is specific: confirm that reading an `Employee` across the tenant filter in this sweep **cannot leak a
tenanted row into a message addressed to another tenant** — note `:175` already passes `cleaner.TenantId`
(read from the same tenant-ignoring projection at `:72`) into the queue message, so the sweep is already
tenant-aware in its *output*; this change makes its *write* agree.

**Precedent for the alternative, if the reviewer prefers it:** `MaterializeRecurringBookings.cs:70-74`
sets a per-iteration tenant override instead of ignoring the filter. That is the other legitimate shape.
Either is acceptable; pick one, and say in the diff why.

**Archetype:** `agents/knowledge/consistency.md` — repository tenant-scoping rules (S8) + background-sweep
tenancy.

**No-decision note:** this is a mechanical fix — an existing method, already written for this purpose, is
substituted for one whose filter is wrong in this context. No new behaviour, no new pattern, **no panel**.
The only judgement call (ignore-filter vs per-iteration override) has two documented in-repo precedents and
is delegated to the reviewer.

## Status log
- 2026-08-02 — draft (created by pm from the challenger round). Passes DoR on merit; **held out of `ready`
  only by the shared-file lane with T-0528** — sequence T-0529 first (it is `S` and touches five lines),
  then T-0528.
- 2026-08-02 — **implemented (backend)**. Diff is confined to `StampWatermarkAsync` — the sweep body, the
  status-set constant and the `:49-53` comment were left untouched for T-0530/T-0528.
  - **Chose the ignore-filter shape, not the `SetTenantOverride` precedent** (the ticket delegates this).
    Three reasons: (1) the stamp creates no child rows, so the override's stated purpose in
    `IEmployeeRepository:22-28` ("so child rows inherit the right tenant") does not apply — the feed +
    outbox rows are created earlier by `NotificationProducer`, which stamps `tenantId` **explicitly**
    (`NotificationProducer.cs:34,58`), and `CleansiaDbContext.CommitAsync:89` only auto-stamps when
    `TenantId` is empty, so an override would change nothing it touches; (2) `SetTenantOverride` mutates
    scoped provider state inside a loop that iterates **many tenants** — `MaterializeRecurringBookings`
    can do it because it clears per iteration, and adding that lifecycle here is more moving parts than
    the defect warrants; (3) the read is by primary key on an id the sweep selected seconds earlier, so it
    cannot widen a result set. **AC3 holds:** `GetQueryableIgnoringTenant()` is
    `GetDbSet().IgnoreQueryFilters()` — change-**tracked**, no `AsNoTracking`, no `ExecuteUpdateAsync` —
    so the watermark still rides the same `CommitAsync` as the feed + outbox rows. **AC6 holds:** the new
    call drops the three `Include`s (`User`, `Address`, `Address.Country`) that `EmployeeRepository`'s
    `GetByIdAsync` override carries.
  - **AC4 — warning added.** After the fix a null here means the row vanished between the sweep's read and
    the stamp; it is logged with the employee id (an internal identifier, not PII — same shape as the
    existing per-cleaner warning at `:190`). Worth keeping precisely because the silent `return` is what
    made a permanent re-notification loop invisible.
  - **AC1/AC2 — new test `src/Cleansia.Tests/Services/NewJobsDigestTenantWatermarkTests.cs`**, a
    `[Theory]` over `tenantId: null` and `tenantId: "tenant-digest-1"`. Real `CleansiaDbContext` over
    SQLite, real `EmployeeRepository`, real unit of work (the context itself), seeded through a
    tenant-carrying context so `CommitAsync` stamps `TenantId` the way a migrated tenanted cleaner looks;
    the sweep then runs on a **null-tenant** context, which is what the timer
    (`SendNewJobsDigestTimerHandler`) actually gives it. Asserts the watermark **persisted**, then that a
    second sweep enqueues nothing. First test in this area that does not wire `tenantId: null`.
  - **Mutation-proved.** Reverting only the load to `GetByIdAsync` and re-running:
    `Failed: 1, Passed: 1` — the tenanted case fails on a **null** `LastNewJobsDigestAt` (watermark never
    advanced), the `null`-tenant case still passes. Restored → `Failed: 0, Passed: 2`. That asymmetry is
    the evidence for AC2 as well: the same test body is byte-for-byte unaffected in single-tenant mode.
  - **AC5 walk — every repo call in `NewJobsDigestService`:** `employeeRepository
    .GetQueryableIgnoringTenant():63` ✓ · `orderRepository.GetQueryableIgnoringTenant():98` ✓ ·
    **`orderRepository.HasOverlappingOrderAsync():137` ✗ tenant-SCOPED** (`OrderRepository.cs:281`
    `GetDbSet()`) — **confirmed, not fixed here** · `preferencesRepository
    .GetQueryableIgnoringTenant():156` ✓ · `employeeRepository.GetByIdIgnoringTenantAsync():216` ✓ (this
    fix). Transitively, `NotificationProducer` → `UserNotificationRepository
    .GetUnreadByUserAndEventAsync` is already tenant-ignoring **and documented as such**
    (`UserNotificationRepository.cs:12-15`), so the digest still collapses onto the cleaner's single
    unread feed row under a tenant.
  - **PM: `HasOverlappingOrderAsync` needs its own ticket — it does not have one.** T-0401 (the
    status-set fix on the same method) is `done` and never touched tenancy; the only other mentions are
    inside T-0528/T-0529 and `adr/challenges/0036-C-digest.md`. Under a tenant it returns `false` for
    every cleaner, so the digest would advertise double-booked jobs — and the same method is the booking
    write gate, so the blast radius is wider than the digest. Please file it against ADR-0028's lane.
  - **Verification:** `dotnet test src/Cleansia.Tests` → **2466 passed / 4 failed / 2470 total**. All 4
    failures are in `CancelOrderStandardTierFeeTests` + `CancellationAcceptanceSignalTests` (cancellation
    fee tiers), which belong to a **concurrent sibling lane** editing `BookingPolicy.cs` / `CancelOrder.cs`
    in the same working tree — untouched and **not reverted** per `shared-file-lanes.md`. Excluding those
    two classes: **2457 passed / 0 failed**.
  - **No MANUAL_STEPs**: no schema change (no ef-migration), no DTO/endpoint shape change (no
    nswag-regen), no new `BusinessErrorMessage` key.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

**Catalog harvest (backend, 2026-08-02).** Added the mirror of S8's existing "anonymous-write /
authenticated-read" trap to `agents/knowledge/security-rules.md` §S8: *a tenant-ignoring sweep whose
write-back is tenant-scoped*. It states the rule (both sides of the loop, not just the selection), why it
is invisible (the `if (x is null) return;` guard drops only the bookkeeping, never the effect), the
testing requirement (the fixture must seed a non-null `TenantId`), and the two in-repo references
(T-0529, T-0361) plus when to prefer `SetTenantOverride` instead (the mutation creates child rows). No
existing rule was redefined — this extends S8 with the opposite polarity, which the T-0361 precedent did
not cover and which is why this defect reached `master`.
