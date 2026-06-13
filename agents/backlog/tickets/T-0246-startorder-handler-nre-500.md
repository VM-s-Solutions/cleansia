---
id: T-0246
title: "BUG: StartOrder handler NullReferenceException → 500 on validator/handler load divergence"
status: ready
size: S
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0215]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0215 (TC-9) AC14 — confirmed live on the Mobile partner host with tenant-consistent seed data
---

## Context
**Confirmed production defect**, reproduced live by the Wave-4 Batch-4C cross-tenant/cross-user
write-path integration suite (T-0215, `Ac14CrossTenantAndUserOrderWriteMobileTests` on the Mobile
partner host) with **fully tenant-consistent seed data** — not a multi-tenant edge.

`src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs:137` dereferences the loaded order with
the **null-forgiving operator** — `order!.StartOrder()` — on an **unguarded** load:

```
orderRepository.GetQueryable()
    .Include(o => o.OrderStatusHistory)
    .Include(o => o.Currency)
    .Include(o => o.CustomerAddress)
    .Include(o => o.User)
    .FirstOrDefaultAsync(...)
```

The handler's load uses this **Include-shaped query path**, while the **validator**
(`StartOrder.cs:45`) gates existence through a **different** query path — `_orderRepository.ExistsAsync`.
When the two **disagree** — the handler's Include-shaped `FirstOrDefaultAsync` returns **null** where
`ExistsAsync` reported the row exists (filter/join/Include divergence between the two query shapes) —
the `order!` dereference throws a **`NullReferenceException`**, which surfaces to the caller as a
**500 "Server error"** instead of a clean business **not-found**. The validator's existence guarantee
is silently invalidated by the handler reloading the order through a non-equivalent query.

This is the same class of **handler/validator load divergence** the project has hit before (the
existence check and the actual load must agree, or the null-forgiving operator turns a missing/hidden
row into a 500). T-0215's `Ac14` happy-path leg hit it on legitimate, in-tenant, fully consistent
seed data; the suite worked around it by proving the Mobile success leg via `TakeOrder` (reachable
end-to-end) instead, while still covering the cross-user `StartOrder` **rejection** (reached before
line 137). This ticket is the fix.

## Acceptance criteria
- [ ] **AC1** — `StartOrder.cs:137` no longer dereferences an unguarded null. The handler **guards the
  loaded order**: when the Include-shaped load returns null, it returns the clean business
  `BusinessErrorMessage.OrderNotFound` (a `BusinessResult` not-found), **never** an NRE/500.
- [ ] **AC2** — The handler load and the validator existence check are **reconciled** so they cannot
  disagree on the happy path: the handler's Include-shaped query resolves the **same** order the
  validator's `ExistsAsync` (`StartOrder.cs:45`) asserted exists (align the predicate/filter/tenant
  scope so an existing, validator-approved order is never returned null by the handler load).
- [ ] **AC3** — Given a legitimate, in-tenant, approved cleaner assigned to a `Confirmed` order, When
  `StartOrder` is invoked, Then the order advances to `InProgress` (the happy path the Ac14 test could
  not exercise because of the NRE now succeeds end-to-end) — a regression test proves it.
- [ ] **AC4** — A regression test reproduces the divergence (the handler load returning null where the
  validator passed) and asserts a clean `OrderNotFound` business result with **no 500/NRE**. Written
  test-first: RED against current `order!` code, GREEN after the guard.

## Out of scope
- The other Order lifecycle handlers (`TakeOrder`, `CompleteOrder`) unless they carry the **same**
  unguarded `order!`-after-load pattern — a quick same-file scan; fix any found or note them, but the
  primary target is `StartOrder`.
- Cross-tenant/cross-user authorization behavior (that boundary is correct and locked by T-0215;
  this is purely the null-load guard + query reconciliation).
- Any DTO/contract change — this is an internal handler robustness fix; no nswag-regen, no migration.

## Implementation notes
- **Exact symbols:** `src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs:137`
  (`order!.StartOrder()` on the unguarded Include-shaped `FirstOrDefaultAsync` load) and
  `StartOrder.cs:45` (the validator's `_orderRepository.ExistsAsync` existence rule that the handler
  load must agree with).
- Prefer reconciling the handler query with the validator's existence predicate so the divergence
  cannot recur, **and** keep a defensive null-guard returning `OrderNotFound` (belt-and-suspenders —
  per the CQRS rule, handlers carry happy-path logic, so the guard returns a business result, it does
  not re-validate).
- **Routing** (`routing.md`): backend authors the fix + regression test; spawn a reviewer in parallel.
  `security_touching: false` (no auth/ownership surface change — the rejection path is unchanged; this
  fixes a 500-vs-clean-not-found robustness gap).

## Status log
- 2026-06-13 — draft (created by pm; confirmed production finding from T-0215 (TC-9) Ac14, reproduced
  live on the Mobile partner host with fully tenant-consistent seed data. Filed S; ranked below the
  multi-tenant go-live blocker T-0245.)
- 2026-06-13 — **ready** (PM, Wave-5 intake / Batch **5A**). Dep T-0215✓ is `done` (Wave 4 merged,
  PR #77 `ee95a57f`). Owner folded this to the FRONT of Wave 5 as a priority bug. DoR met: AC1–AC4
  observable (null load → clean `OrderNotFound`, never NRE/500; happy path advances to `InProgress`;
  regression test red-first on `order!`), sized S, no migration/regen, `security_touching: false`
  (rejection path unchanged; this is a 500-vs-clean-not-found robustness fix). Archetype:
  handler/validator load reconciliation + defensive null-guard returning a business result. Runs in
  **Batch 5A ∥ T-0245** (disjoint files — `StartOrder.cs` vs webhook validator/repo). Stale-text note
  for the implementing agent recorded in `status/sprint-7.md` §3.)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
