---
id: T-0539
title: MaterializeRecurringBookings has no per-template isolation — and the naive fix ships a phantom order
status: done
size: M
owner: architect
created: 2026-08-04
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 15
source: flagged at `8e7aae16` (poison-pill risk), attempted and **deliberately not shipped** at
  `077b7e8a` §2b with the trap recorded. Filed by the PM in the sprint-15 reconciliation so the trap is
  not rediscovered by whoever tries next.
---

## Context

`MaterializeRecurringBookings` loops active templates (`:70`) and calls `unitOfWork.CommitAsync` inside
the loop (`:166`, moved there by `077b7e8a` §2a). It has **no per-template `try`/`catch`**, so one
template that throws aborts the sweep for **every other customer**.

`8e7aae16` raised it when it capped order duration: an over-cap template would now be a poison pill.
Pre-existing in shape — a missing currency does the same today — and not reachable without a deliberate
all-packages selection, but the shape is wrong regardless.

### The trap, which is the reason this ticket exists

`077b7e8a` §2b **did not ship the catch**, and said why:

> `CleansiaDbContext.Rollback()` sets every tracked entry to `Unchanged`, and **Added → Unchanged is NOT
> Detached** — the half-built order stops being an insert and **stays in the tracker as a phantom
> existing row** for every later iteration.

Verified at HEAD: `CleansiaDbContext.cs:107-113` is exactly

```csharp
public void Rollback()
{
    foreach (var entry in ChangeTracker.Entries())
    {
        entry.State = EntityState.Unchanged;
    }
}
```

So **anyone implementing catch-and-continue via `Rollback()` would ship the half-built-order bug
believing they had detached it.** That is the single most valuable sentence on this ticket. It is
recorded here rather than left in a commit message because a commit message is not where the next
implementer looks.

**The durable answer is one DI scope per template, not a catch** — a fresh scope gives a fresh
`DbContext` and therefore a fresh change tracker, so a failed template cannot leave residue in the next
one's tracker at all. That is a design choice with consequences (scoped-service lifetimes, the tenant
provider, the notification producer), which is why this ticket opens with an architect, not a backend
instance.

`077b7e8a` §2a also **shrank the blast radius** in passing: because the commit now happens inside the
loop, a throw loses only the failing template onward rather than the whole sweep. The problem is smaller
than it was; it is not gone.

### The tenancy defect that already got fixed here, and why it belongs in the record

The same commit fixed the per-template tenant stamping: the override was **decorative** because the
commit was deferred to the pipeline and ran **once**, with the last template's tenant. The production
defect, verbatim from its mutation proof: *tenant A's customer got an order only tenant B can see, and
when the last template was a legacy null-tenant one, tenant A's order was stamped null.* Any change to
this loop's transaction boundary must keep that fixed — which is another reason the answer is a scope
per template rather than a catch around a shared context.

## Acceptance criteria

- [ ] **AC1 — an architect ruling on the isolation mechanism, written down before any code.** Given the
      three candidates (a DI scope per template · a catch plus a genuinely detaching reset · moving the
      span rule onto the recurring-template validator so the poison pill cannot exist), When the ruling
      lands, Then it names the choice, what it costs, and what it does about the tenant override. **A
      scope per template changes what `SetTenantOverride` is scoped to — that must be answered, not
      discovered.**
- [ ] **AC2 — one failing template does not stop the sweep.** Given N active templates where template *k*
      throws, When the sweep runs, Then the other N−1 materialize. **Evidence:** a test that seeds a
      genuinely failing template (not a mocked throw) and asserts on the **child rows** of the others.
- [ ] **AC3 — no phantom order survives the failure.** Given template *k* fails **after** its `Order` was
      added to the tracker but **before** commit, When the sweep continues, Then no row for it is written
      by any later iteration's commit. **Evidence: mutation proof — implement the naive `Rollback()`
      variant, show it writes the phantom row, then show the chosen mechanism does not.** This AC is the
      whole ticket; do not accept "no test failed".
- [ ] **AC4 — the tenant stamping stays correct.** Given two templates under **two different non-null
      tenants**, When the sweep runs, Then each occurrence and all its child rows carry their own
      template's tenant. **Evidence:** the pinning test must seed two **different non-null** tenants and
      assert on the **CHILD** rows — `security-rules.md` §S8 was corrected in `077b7e8a` to say exactly
      this, because a single-tenant fixture cannot see the bug.
- [ ] **AC5 — the recurring-template validator is assessed against the span cap.** Given
      `BookingPolicy.MaxBookableOrderSpanHours`, When a recurring template would produce an over-cap
      order, Then either the template is refused at creation (preferred — it moves the failure to
      somewhere a human can react) or the ruling records why not. `8e7aae16` named this as the
      alternative to a catch and it was never decided.
- [ ] **AC6 — the three test suites run green.**

## Out of scope

- `CleansiaDbContext.Rollback()`'s own semantics. **Do not "fix" it here.** It has other callers; whether
  `Unchanged` is the right reset for them is a separate question with a separate blast radius. This
  ticket must not rely on it.
- The optimizer's O(C²) `DetectChanges` concern about per-iteration commits. Real, separate, cost-shaped.
- The span cap itself — shipped in `8e7aae16`.

## Implementation notes

**Files:** `Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs` (the loop at
`:70`, the commit at `:166`), `Cleansia.Infra.Database/CleansiaDbContext.cs:107-113` (read only — see Out
of scope).

**Precedent to read first:** `20021098` fixed the identical decorative-override defect in
`CleanupStalePendingOrders` by moving the commit inside the group loop, and its mutation proof prints
`Expected "tenant-a", Actual null`. That is the shape of evidence AC4 wants.

**Archetype:** `agents/knowledge/security-rules.md` §S8 (background-sweep tenancy, as amended by
`077b7e8a`) + `patterns-backend.md` (UnitOfWork — handlers never commit; a sweep is the documented
exception and must say so).

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Passes DoR: AC observable with
  a named mutation proof, `M`, no dependencies, no owner-only steps, archetype named, and **step 1 is an
  architect ruling** because the durable fix changes a DI lifetime rather than adding a `catch`.

- 2026-08-05 — **`ready` → `done` (PM reconciliation pass 4).** **Verified at HEAD.** The durable answer
  shipped, not the naive one: `Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs:87`
  opens `using var scope = serviceScopeFactory.CreateScope()` **inside** the per-template loop and resolves a
  fresh `IMediator` from it, so a failed template's tracked entries die with its scope instead of being
  `Rollback()`-ed to `Unchanged` — the `Added → Unchanged is NOT Detached` phantom-row trap the ticket
  named is avoided by construction rather than by cleanup. The loop's failure branch logs and continues,
  leaving the template's previous marker for the next tick. Pinned by
  `Cleansia.Tests/Features/Bookings/RecurringSweepPerTemplateIsolationTests.cs` and
  `MaterializeRecurringBookingsTenantStampingTests.cs`, both of which build a real provider and rely on
  `CreateScope()` being what produces isolation. Shipped in `0c76f94a`.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
