---
id: T-0545
title: Write the promo global-redemption reconciliation script — campaigns may already be dead on DEV
status: retired
size: S
owner: pm
created: 2026-08-04
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0038]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
source: ADR-0038 §D6.4, carried in `8cdf548f` and `da88b695` as *"STILL OWED, owner-gated"*. It has been
  named in three commit messages and `INDEX.md` and has never been a ticket. Filed by the PM in the
  sprint-15 reconciliation.
---

## Context

Before `da88b695`, **every order placed with a promo code threw `23503` and no order was created** — a
raw self-committing `INSERT` against `FK_PromoCodeRedemptions_Orders_OrderId` for an `Orders` row the
UnitOfWork pipeline commits *after* the handler returns.

The global counter was incremented **before** that insert, and on the failure path the increment had
already auto-committed. So:

> **Every promo attempt since the bug shipped burnt a global slot with no row to show for it, so
> campaigns may be ALREADY DEAD in DEV.**

`PromoCodes.CurrentRedemptionsCount` is therefore **higher than the redemption ledger** by the number of
failed attempts. A campaign capped at N can refuse legitimate customers while `PromoCodeRedemptions`
holds far fewer than N rows.

The outage itself is fixed (`da88b695`), and the compensating decrement now fires on **any** reservation
non-success rather than only the `null` return, so no *new* slot is burned. `d78b816b` then fixed a
defect in that fix — an `await` inside `finally` meant a throwing compensation **replaced** the in-flight
exception, in exactly the transient-database case the compensation exists for.

**But nothing un-burns the slots already lost.** ADR-0038 §D6.4 specifies a `sql-scripts/` repair
reconciling the count to the ledger. **Verified at HEAD, 2026-08-04: it does not exist.** `sql-scripts/`
contains ten files and none mentions `CurrentRedemptionsCount`.

### Two constraints on this that are easy to get wrong

1. **It is a `sql-scripts/` script — NOT a migration and NOT a job.** ADR-0038 says so explicitly. A
   migration would run on every environment at deploy time; this is a one-off repair against a known
   corrupt state.
2. **It must run AFTER the fix is deployed, and during low traffic.** Run before the fix and it repairs a
   state the next booking re-breaks. The owner runs it (`CLAUDE.md` — agents do not execute against a
   database); this ticket **writes** it.

## Acceptance criteria

- [ ] **AC1 — the script exists at `sql-scripts/` and is idempotent.** Given it is run twice in a row,
      When the second run completes, Then nothing changes. A repair that double-corrects on a re-run is
      worse than no repair.
- [ ] **AC2 — it reconciles the count to the LEDGER, and the ledger's definition is stated in the file.**
      Given `PromoCodes.CurrentRedemptionsCount`, When the script runs, Then it is set from the actual
      `PromoCodeRedemptions` rows. ⚠️ **`AnonymizeCustomerData` nulls `PromoCodeId` while keeping
      `PromoDiscountAmount`** — ADR-0038 §D6.3's *"exact iff"* was already false because of this, and
      CH-7 re-keyed the reconciliation **onto the amount INSTEAD OF rather than in addition to** the id.
      Get this right or the script under-counts every anonymized customer's redemption and hands a
      campaign free capacity.
- [ ] **AC3 — it reports before it writes.** Given the script, When it runs, Then it first prints one row
      per affected promo code — id, code, stored count, ledger count, delta — so the owner can see the
      blast radius **before** anything is updated. A repair that silently changes money-adjacent state is
      not reviewable.
- [ ] **AC4 — it refuses to run against a schema that still has the bug.** Given the script, When it
      starts, Then it asserts the fix is deployed (e.g. the interim's shape is present) or aborts with a
      message saying so. This is the *"run it AFTER deploy or the next booking re-breaks what it
      repaired"* constraint, mechanized rather than left in a comment — the exact class of mitigation that
      this sprint found living only in comments three separate times.
- [ ] **AC5 — it is tenancy-correct.** Given `TenantId` is nullable and Postgres treats NULLs as DISTINCT
      (**T-0531**), When the script groups, Then it uses `IS NOT DISTINCT FROM` semantics rather than
      `=`, so single-tenant rows are not silently skipped.
- [ ] **AC6 — the header comment tells the operator exactly what to do.** Given the file, When the owner
      opens it, Then it states: what broke, why the counts drifted, that it must run **after** the fix is
      deployed, that it should run during low traffic, and that it is safe to re-run.
- [ ] **AC7 — a test or a dry-run transcript proves it on a seeded corrupt state.** Given a database
      seeded with a promo whose stored count exceeds its ledger, When the script runs, Then the count
      equals the ledger and a second run is a no-op. **Evidence:** the transcript, in the status log.

## Out of scope

- **Running it.** Owner-only. This ticket delivers a reviewed script and flags the run.
- Moving the reservation strictly post-commit — **T-0532**, which retires the interim.
- `.AreNullsDistinct(false)` on the promo per-user index — **already folded into the regenerated
  `Initial`** at `7e1cf7f5` and it lands with the database drop. It is no longer a separate owner item.
- Any change to `PromoCodeService` or `OrderPromoApplier`. If the script reveals one is needed, file it.

## Implementation notes

**Read first:** ADR-0038 §D6.3 and §D6.4, plus CH-7's ruling in `f7828fb8` (the amount-instead-of-id
re-key — the challenger's *"in addition"* would have re-blinded it).

**Archetype:** `sql-scripts/README.md` and the two shipped backfills
(`backfill-employee-work-country.sql`, `backfill-order-completed-at.sql`) — same header style, same
idempotency expectation.

⚠️ **The DEV database is being dropped** (see the owner list in `status/sprint-15.md § ADDENDUM C`). If
the drop happens first, the corrupt DEV state disappears with it and the **run** becomes unnecessary —
but the script is still worth having, because the same drift is reproducible on any environment that ran
the broken code. **Ask the owner which comes first before scheduling the run; do not assume.**

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Named as owed in `8cdf548f`,
  `da88b695` and `INDEX.md` and never ticketed. Passes DoR: AC observable, `S`, no dependencies, and the
  owner-only step (**running** it) is declared.

- 2026-08-05 — **`ready` → `retired` (PM reconciliation pass 4). Retired as OBSOLETE, not as done, and not
  as a failure.** The ticket's own body already carried the condition that decides it: *"⚠️ The DEV database
  is being dropped… If the drop happens first, the corrupt DEV state disappears with it and the **run**
  becomes unnecessary — but the script is still worth having."* **The owner has settled the ordering: the
  drop comes first.** The drift this script repairs exists in exactly one place — the DEV
  `PromoCodes.CurrentRedemptionsCount` column — and a drop-and-reseed removes the rows the drift lives in.
  There is no other environment: PROD does not exist, and both test fixtures build fresh schemas.
  **What is retired is the WORK, and the reason is on the record**, per `ticket-lifecycle.md`'s definition
  of `retired` ("the WORK is no longer wanted… the ticket records why"). Retiring it also removes the last
  *run*-shaped item from the owner's list.
  **Two things deliberately NOT retired with it, because they are not this ticket:**
  1. **The cause is already fixed and stays fixed** — `da88b695` closed the outage and `d78b816b` fixed the
     defect in that fix (an `await` inside `finally` let a throwing compensation **replace** the in-flight
     failure). No *new* slot is burned, so no successor script is owed.
  2. **ADR-0038 §D6.4 still names a counter repair as owed.** With this ticket retired, that reference has
     no ticket behind it. That is a documentation fact for the ADR's own lane to settle — **the PM does not
     edit `backlog/adr/`** — and it is listed for the architect in `status/sprint-15.md § ADDENDUM D`.
     If a second environment ever runs the broken code, this ticket is the specification to re-file from.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
