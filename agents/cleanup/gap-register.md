# Gap register — P1

**CL-013.** Every gap the end-to-end walks surfaced, triaged into **fix / accept / not-a-risk**.

The triage test, from the owner's brief: *does this actually happen, what is the blast radius, and
what is the cheapest correct fix?* A gap that fails the first question is recorded and closed, not
ticketed. Perfecting is not the goal.

## Coverage — what has actually been walked

| Flow | Ticket | State |
|---|---|---|
| Auth & identity | CL-003 | **walked** |
| Cross-cutting — tenancy, silent-catch, async, authz posture, rate limiting, outbox, idempotency | CL-012 | **walked** |
| Booking & pricing (incl. recurring materialization) | CL-004 | **walked** |
| Payment & fiscal | CL-005 | **walked** |
| Offerability, hold, take | CL-006 | **walked** |
| Execution & completion | CL-007 | **walked** |
| Cancellation, refund, dispute | CL-008 | **walked** |
| Pay, periods, invoices, payouts | CL-009 | **walked** |
| Loyalty, memberships, metered benefits, referrals | CL-010 | **walked** |
| GDPR, retention, audit, admin override | CL-011 | **walked** |

---

## FIX

### G-01 — Three cross-tenant sweeps write rows without stamping the tenant

**Where:** `Features/Bookings/AutoCancelStaleRecurringOrders.cs:63-102` ·
`Features/Bookings/SendRecurringOrderReminders.cs:63-94` ·
`Features/Memberships/SendMembershipLifecycleNotifications.cs:76-113`

All three read across tenants with `GetQueryableIgnoringTenant()`, then **write** — `AddOrderStatus`
(a new `OrderStatusTrack` row), notification rows, reminder stamps — and commit inside the loop.
None calls `SetTenantOverride`.

`CleansiaDbContext.cs:89-91` stamps a new row's `TenantId` from the **ambient** tenant at
`SaveChanges` time:

```csharp
if (entity.Entity is ITenantEntity tenantEntity && string.IsNullOrEmpty(tenantEntity.TenantId))
{
    tenantEntity.TenantId = currentTenantId;
}
```

A system job carries no JWT, so `currentTenantId` is null and every inserted row lands with
`TenantId = null` regardless of which tenant the parent order or membership belongs to.

**The correct shape already exists in the tree.** `CleanupStalePendingOrders.cs:77-86` groups by
`TenantId`, clears the override between groups (so a non-empty override cannot leak into a
single-tenant group), sets it per group, and commits inside. That file's own comment names
`AutoCancelStaleRecurringOrders` as *"the exact complement of that sweep"* — the two split the
Pending-order population between them. One handles tenancy correctly; its named complement does not.

**Does it happen today?** No. Production is single-tenant, every `TenantId` is null, so a null stamp
is the correct answer by accident.

**Blast radius when it does:** ADR-0017 and ADR-0028 exist to activate multi-tenancy. On the day that
flips, these three jobs silently write mis-tenanted rows — cancellation history attached to the wrong
tenant, reminders that the owning tenant's queries cannot see. Silent wrong data, discovered late.

**Cheapest correct fix:** copy the reference shape — `GroupBy(o => o.TenantId ?? string.Empty)`,
`ClearTenantOverride()`, `SetTenantOverride`. No new type, no abstraction, ~8 lines per file. The
pattern is already written and already tested.

**Verdict: fix.** → CL-015.

> **Correction, 2026-08-13 (from working the fix).** This entry claimed three files. Only
> **`AutoCancelStaleRecurringOrders`** is affected. The other two write nothing that gets stamped from
> the ambient tenant: `SendRecurringOrderReminders` and `SendMembershipLifecycleNotifications` insert
> only notification rows, and `NotificationProducer` passes the tenant down explicitly —
> `UserNotification.Create(userId, eventKey, argsJson, tenantId)` — so `CleansiaDbContext` never
> reaches its ambient fallback (it stamps only when `TenantId` is empty). Their other write,
> `Mark…ReminderSent`, is an UPDATE to an existing row and does not re-stamp.
>
> What makes the one remaining case real is `OrderStatusTrack`: it is `ITenantEntity` and
> `OrderStatusTrack.Create` never sets `TenantId`. `CleanupStalePendingOrders` inserts the *same* row
> type and *does* group by tenant — so the fix makes two sweeps that write the same entity treat it the
> same way, which is the whole argument.
>
> I over-claimed the blast radius in PR #192 by reading the pattern (cross-tenant read + write + commit)
> instead of checking what each write actually inserts.

### G-15 — Two cleaners can take the same seat concurrently; nothing at the database rejects the second

**Where:** `Features/Orders/TakeOrder.cs:242-265` · `Order.cs:137, 666` ·
`Migrations/CleansiaDbContextModelSnapshot.cs:3337-3360`

The take is gated by **three unlocked reads and no write-side constraint**:

1. `TakeOrder.Validator` checks `HasAvailableSpots`.
2. The handler re-loads the order and re-checks `HasAvailableSpots` (`:258`).
3. `Order.AddAssignedEmployee` checks it again in memory (`Order.cs:666`).

Every one of them reads its own tracked copy of the aggregate. Verified against the model:

- **`OrderEmployees` has no unique index.** The snapshot declares exactly two indexes, both
  non-unique: `EmployeeId` and `OrderId`. There is no `OrderEmployeeEntityConfiguration.cs` at all —
  the entity is mapped by convention.
- **`Order` carries no concurrency token.** The only `IsConcurrencyToken()` in the entire model is a
  shadow `xmin` on `RefreshToken` (`CleansiaDbContext.cs:150-153`).
- **No isolation-level override.** Default Read Committed.

**Failure scenario.** Two cleaners tap the same one-seat job within the same transaction window. Both
load `AssignedEmployees = []`. Both pass all three checks. Both `INSERT` into `OrderEmployees`. Both
commit. The order now carries two assigned cleaners against `MaxEmployees = 1`.

**What the existing test does and does not cover.** `TakeOrderSeatRaceTests` is a *mocked unit test*
covering the **sequential** case — the second cleaner loads an order that already has the first
assigned — and it correctly asserts a clean `NoAvailableSpots` refusal instead of a 500. That case is
closed. The **concurrent** case, where both load before either commits, has no test and no constraint.

**Likelihood — not theoretical.** New jobs are broadcast to many cleaners at once, and
`NotifyLapsedPreferredOffers` is a *designed* synchronized event: when an ADR-0036 preferred hold
lapses, the seat opens to everyone simultaneously. That is precisely the arrival distribution that
produces two loads inside one window.

**Blast radius — money.** Pay is one row per assigned employee, and `OrderEmployeePay` is unique on
`(OrderId, EmployeeId)`, so two *different* cleaners each get a valid pay row. That is a second full
wage against an unchanged customer price — the exact outcome `BookingPolicy.SpareSeatsPerOrder = 0`
was set to prevent, arrived at by a different route. Two cleaners also turn up at the customer's home.

**Cheapest correct fix — two options, both needing your call:**

- **Seat ordinal (recommended).** Give `OrderEmployee` a `SeatOrdinal` with a unique
  `(OrderId, SeatOrdinal)` index and derive the ordinal in SQL. **This pattern is already shipped
  here** — `MembershipBenefitUsage` reserves a quota slot with one atomic
  `INSERT … SELECT … ON CONFLICT DO NOTHING RETURNING` that picks the smallest free ordinal. The
  insert either wins a seat or returns nothing, and "nothing" becomes the `NoAvailableSpots` refusal
  that already exists. Nothing new is invented. **Needs an EF migration → `MANUAL_STEP`, owner-only.**
- **Row lock, no schema change.** Take the order row `FOR UPDATE` at the start of the handler's
  transaction. Closes the window with no migration, at the cost of serialising takes on one order.

**Verdict: fix — seat ordinal. Owner decision, 2026-08-12.** → CL-015.

Walking the rest of the system settled the argument: **every other contested resource in this codebase
is already guarded this way** — `MembershipBenefitUsage` on
`(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)`, `PromoCodeRedemption` on
`(TenantId, PromoCodeId, UserId, SlotOrdinal)`, both `AreNullsDistinct(false)`, plus `ON CONFLICT`
counters for fiscal and payout numbering. The order seat is the only contested resource without one, so
this is an inconsistency with an established in-house pattern rather than a new mechanism.

Implementation notes for CL-015:
- `OrderEmployee` gains `SeatOrdinal`; unique `(OrderId, SeatOrdinal)` with `AreNullsDistinct(false)`.
- The insert derives the smallest free ordinal in SQL and returns nothing when the order is full;
  "nothing" maps onto the `NoAvailableSpots` refusal that already exists, so the error contract does
  not change and `TakeOrderSeatRaceTests`' one-error requirement still holds.
- The existing three in-memory checks stay as the cheap fast path — they are correct, just not
  sufficient alone.
- **`MANUAL_STEP`: EF migration, owner-only.** Pre-prod, so it folds into a regenerated `Initial`.
- Add the integration test the current suite lacks: two genuinely concurrent takes against real
  Postgres, asserting exactly one assignment survives.

---

## ACCEPT — real, recorded, not worth code today

### G-02 — ADR-0030 records two gates as OPEN that the tree has closed

ADR-0030 (*Web.Admin access-token TTL is 15 minutes*) states as a live consequence that
`environment.prod.ts` points admin auth at `api.cleansia.cz` — the **partner** API host — making the
15-minute flip *"inert for the deployed admin app"* until T-0400 corrects it.

The tree disagrees: `apps/cleansia-admin.app/src/environments/environment.prod.ts` reads
`apiBaseUrl: 'https://api-admin.cleansia.cz'`, and there is no `authApiBaseUrl` key at all. The
pairing is correct and the control is effective.

**Verdict: accept, and correct the ADR text during the P6 migration** — not a code change. Recorded
here because an ADR that overstates an open risk trains readers to discount ADRs.

### G-03 — `rememberMe` is re-derived from lifetime arithmetic on every refresh

`RefreshTokenService.cs:91-92` infers whether a token was issued as long-lived by measuring
`ExpiresAt - CreatedOn` against `RefreshTokenShortExpDays + 0.5`. It is correct for every shipped
config (30 vs 1, and 90 vs 1 on Mobile.Customer) but couples a security property to the *gap between*
two independently-tunable numbers. Set them within half a day of each other and every session
silently becomes short-lived.

**Verdict: accept.** No shipped config is near the boundary, the failure is fail-safe (shorter
sessions, not longer), and storing the flag explicitly costs a column and a migration. Document the
constraint alongside the config in P7 instead.

### G-11 — Entry instructions reach every admin unconditionally, with no reveal and no audit

`Order.AccessInstructions` is free text of the form *"key under the mat"*, *"gate code 4455"*. It is
correctly withheld from a **browsing** cleaner (see G-13), and an **assigned** cleaner needs it. The
residue is the admin surface: it renders unconditionally, with no reveal step and therefore no audit
record of who read it.

`CreateOrder.cs:351-358` says so itself — *"it carries no extra access control today despite being the
more sensitive of the two note fields."* **T-0483** exists for it, `status: draft`, created 2026-08-02,
never started.

The comparable control is already shipped and proven in this codebase: revealing a cleaner's payout
identifiers is a **command**, not a query, precisely so the audit engine records it and the entity can
stamp `LastRevealedAt`/`RevealCount` (ADR-0034). The shape to copy exists.

**Verdict: accept — but carry it out of the archive.** The fix is a genuine four-platform feature
(admin web, partner web, Android, iOS), not a cleanup edit, so it does not belong in this track. It is
recorded here because **T-0483 is a draft ticket inside the backlog P9 is about to archive**, and it is
the only privacy item in there. It must land on your desk as a decision, not disappear into
`agents/archive/`.

### G-18 — Recurring materialization is protected by the Functions timer lease, not by a constraint

`MaterializeRecurringBookingTemplate.cs:135-141` decides "did we already spawn this occurrence" with an
**unlocked read**, and `Order.RecurringTemplateId` carries a **non-unique** index. Two overlapping
invocations would both see "not materialized" and both create the occurrence — a duplicate order, and
for a card template a duplicate charge.

What actually prevents it is outside the code: Azure Functions timer triggers hold a singleton lease,
so `MaterializeRecurringBookingsFunction`'s `[TimerTrigger]` cannot run twice concurrently. There is no
`IIdempotencyGuard` on this sweep and no unique index behind it.

**Verdict: accept, and document the dependency.** The behaviour is correct today. The risk is that the
guarantee lives in the hosting model rather than the schema, so moving this sweep to any other
scheduler — or fanning it out — silently reintroduces duplicate billing. That belongs in the P7
operations page next to the cron config, not in a code change now.

---

## NOT A RISK — checked, closed, recorded so it is not re-checked

| | Checked | Why it is fine |
|---|---|---|
| **G-04** | ~100 controllers carry no `[Authorize]` | `ServiceExtensions.cs:42-47` sets a default policy of `RequireAuthenticatedUser`. Secure by default; `[AllowAnonymous]` is the explicit opt-out. |
| **G-05** | Anonymous guest order lookup (`GET /Order/Lookup`) | Gated on `(DisplayOrderNumber, CustomerEmail)`. The number is `ORD-` + 8 random hex from a GUID (`Order.cs:33`) — 32 bits, not sequential — plus an email match and the `interactive` rate-limit partition. Not enumerable. |
| **G-06** | Anonymous batch lookup (`POST /Order/LookupBatch`) | Hard-capped at 10 items, keyed on the internal GUID `Order.Id` rather than the human-typed number, and each item still requires its own email match. Strictly narrower than the single lookup it builds on. |
| **G-07** | Silent exception swallowing | Zero empty `catch` blocks across all production projects. |
| **G-08** | `async void` / sync-over-async | None, except two `GetAwaiter().GetResult()` calls in `AuthorizationCompletenessStartupFilter` — a synchronous startup-filter context where that is the correct call. |
| **G-09** | Backend error keys vs frontend translations | All three apps' `error-contract-parity.spec.ts` pass; the full Jest run across the three apps is green. |
| **G-10** | Refresh-token theft handling | Rotation-reuse detection revokes the whole chain and **self-commits** so the revoke survives the failure path; retry-on-`xmin`-collision with a fail-closed set-based bulk revoke once the budget is spent. The kill switch cannot be outraced into a 500. |
| **G-12** | Client-supplied `CreateOrder.TotalPrice` | Server-authoritative. The validator recomputes via `OrderPricingCalculator` and compares; the client value is a confirm-the-quote check, never an input to the charge. The one input that can legitimately change between quote and submit — an express waiver whose monthly quota was exhausted in between — is classified into its own error rather than a generic mismatch (`CreateOrder.cs:267-303`). |
| **G-13** | What a browsing (unassigned) cleaner can read | `OrderPiiRedaction` strips name, email, phone, address, coordinates, confirmation code, **all free text including `AccessInstructions`**, notes, issues, review, and crew phone numbers — list and detail in one file so the two cannot drift, with `OrderRedactionSurfaceTests` failing the build until a newly-added field is classified. Among the strongest controls in the codebase. |
| **G-16** | Stripe webhook: signature, idempotency, replay, double-settlement | `EventUtility.ConstructEvent` verifies the signature; a `ProcessedStripeEvent` UNIQUE index makes replay a no-op; effects are enqueued **only after** the stamp + state commit, so a Stripe retry cannot emit a second receipt or push; and `SettledInCash` is checked **before** the terminal-state short-circuit so a cash-then-card double payment escalates instead of being waved through as a benign duplicate. |
| **G-17** | Webhook does not reconcile the amount received against the order total | Not needed. The charge amount is `ToMinorUnits(order.TotalPrice)` from the persisted, server-computed value (`StripeClient.cs:46`); the client never supplies it, so there is nothing to reconcile against. |
| **G-19** | Refund cannot exceed what was paid | `RefundService` computes `refundable = order.TotalPrice - consumed`, takes `Math.Min(request.Amount, refundable)`, refuses at `<= 0`, and clamps a re-driven existing row. The Stripe call happens **before** the status flip, so a failed call never leaves a phantom `Refunded`. |
| **G-20** | Ownership on order mutations | `StartOrder`, `CompleteOrder`, `NotifyOnTheWay` each gate on `EmployeeIsAssignedToOrderAsync`; `CancelOrder` is `CustomerOnly` and checks `order.UserId != userId` in the handler. No mutation is reachable by a non-participant. |
| **G-21** | Outbox drain under scale-out | Claim is a single `UPDATE … RETURNING` with a claim token and lease cutoff, `IgnoreQueryFilters()` because the drainer is a system process. Atomic; two drainers cannot claim the same row. Dead-letter writes own their commit. |
| **G-22** | Pay-period double-close / double-pay | Every transition gates on the current status — `ClosePayPeriod` requires `Open`, `MarkPayPeriodPaid` requires `Closed`, `ReopenPayPeriod` refuses `Paid`. `EmployeeInvoice.InvoiceNumber` and `VariableSymbol` are both unique; fiscal and payout numbering use atomic `ON CONFLICT` counters. |
| **G-23** | Loyalty double-grant and self-referral | `LoyaltyTransaction` is unique on `(TenantId, IdempotencyKey)`. `ReferralService.ValidateAsync` rejects a code whose owner is the accepting user (`:112`) and rejects an already-referred user (`:117`). Referral codes are randomly generated, not derived from names. |
| **G-24** | GDPR erasure leaves loyalty, referral, promo and benefit rows | Correct as designed. Erasure is **anonymise-in-place**: the `User` row survives with its id and its PII fields scrubbed, so rows that carry only a foreign key plus non-PII scalars need no walk. Deleting them would corrupt the loyalty ledger and the promo/benefit one-shot guards for no privacy gain. |
| **G-25** | Admin action audit | `AuditEntryFactory` writes an append-only `AdminActionAudit` for **both** success and failure, carrying the actor session — so a refused privileged action is recorded too, not just a successful one. |
| **G-14** | Package + service double-count in span/pricing | Owner ruling, 2026-08-04: selecting a package *and* a service inside it buys that service twice, so it is priced twice and takes twice as long. Documented at `CreateOrder.cs:236-250` with an explicit *"must not be fixed with a Distinct"*. Correct as written. |

---

## Carried in from the 2026-08-12 repository analysis

Already ticketed as **CL-014**, listed here so the register is the single view:

1. `check-consistency.mjs` fails with 84 violations and runs in no CI workflow.
2. `agents/backlog/INDEX.md` cites `catalog-claims.yml` as a live `T1-CI` gate; it was deleted.
3. `README.md` documents `Add-Migration` against two paths that do not exist.
4. Six root docs are a frozen second "how we build" corpus (Jan–Jun 2026).
5. The graphify graph is 70 commits stale and `CLAUDE.md` points at a `wiki/` that was never built.
6. One live EF 10 deprecation plus ~30 xUnit analyser warnings.
7. `agents/tools/` mixes 6 live checkers with ~25 spent one-shot wave scripts.
8. `DbConstraintViolation` now has five call sites — arguably retiring `CLAUDE.md` §4's criticism of
   it rather than confirming it. Owner's call, not mine.
