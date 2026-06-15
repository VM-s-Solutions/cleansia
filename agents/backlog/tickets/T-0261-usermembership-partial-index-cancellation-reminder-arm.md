---
id: T-0261
title: "LG-PERF-06: UserMembership partial index doesn't cover the cancellation-reminder sweep arm"
status: done
size: S
owner: pm
created: 2026-06-14
updated: 2026-06-15
depends_on: [T-0204]
blocks: []
stories: []
adrs: []
layers: [db, backend]
security_touching: false
manual_steps: [ef-migration]
sprint: 6
source: T-0204 (PERF cluster) finding — partial-index predicate covers only the renewal-reminder arm
---

## Context
Surfaced (not fixed) by **T-0204** (the Wave-5 PERF cluster, which shipped 4 indexes incl. the UserMembership
one). The index T-0204 added is a **partial index** on `UserMembership (Status, CurrentPeriodEnd)` with the
predicate `WHERE RenewalReminderSentAt IS NULL` — tuned for the **renewal-reminder** sweep.

But the membership sweep has **a second arm**: the **cancellation-reminder** sweep, which filters on a
different "reminder-not-yet-sent" column (the cancellation-reminder timestamp, not `RenewalReminderSentAt`).
That arm's query predicate is **not covered** by the partial index's `WHERE RenewalReminderSentAt IS NULL`
clause, so the cancellation-reminder sweep falls back to a less-selective scan on a populated table.

This is a pure **DB optimization follow-up** — no behavior change, just closing the index coverage gap so both
sweep arms are index-supported. It carries `ef-migration` (the index is owner-applied, CONCURRENTLY on a
populated table, like the T-0204 batch).

## Acceptance criteria
- [ ] **AC1 (cover the second arm)** — Given the cancellation-reminder sweep query, When the fix lands,
  Then there is a partial index whose predicate matches the cancellation-reminder sweep's filter (the
  cancellation-reminder-not-sent column IS NULL, plus the `Status`/`CurrentPeriodEnd` key columns it filters
  on), so the sweep uses an index instead of a fuller scan. Verify with an `EXPLAIN`/query-plan check that
  the cancellation-reminder sweep now hits the new index.
- [ ] **AC2 (no behavior change)** — Given the new index, When both membership reminder sweeps run, Then the
  rows selected and the side effects are **identical** to today — this is index-only; no query result,
  reminder logic, or DTO change.
- [ ] **AC3 (migration flagged, owner-applied)** — Given the index addition, When the schema change is
  prepared, Then it is a single additive migration the **owner** builds and applies `CONCURRENTLY` on the
  populated `UserMembership` table; the ticket is held at the migration boundary (PM never runs it).
- [ ] **AC4 (no regression on the renewal arm)** — Given the new index sits alongside the T-0204 renewal
  partial index, When the renewal-reminder sweep runs, Then it still uses the T-0204 index — the two partial
  indexes coexist without conflict and the renewal arm's plan is unchanged.

## Out of scope
- Re-touching the 4 indexes T-0204 already shipped (incl. the renewal-reminder partial index).
- Any change to the membership reminder sweep logic, cadence, or messages.
- Consolidating the two arms into one query (a behavior change — out of scope).

## Implementation notes
- Confirm the exact cancellation-reminder column name and the sweep's filter predicate against current
  source at dispatch (the cancellation-reminder sweep arm in the membership Functions/sweep handler).
- Mirror the partial-index pattern T-0204 used for the renewal arm (composite `(Status, CurrentPeriodEnd)`
  key + `WHERE <reminder-col> IS NULL` predicate) so the two are symmetric.
- Single additive migration; `CONCURRENTLY`; owner-applied. Optimizer review on the query-plan evidence.

## Status log
- 2026-06-14 — draft (created by pm; Wave-5 close-out follow-up from the T-0204 finding — the UserMembership
  `(Status, CurrentPeriodEnd)` partial index `WHERE RenewalReminderSentAt IS NULL` covers only the
  renewal-reminder arm and not the cancellation-reminder sweep arm). DB optimization follow-up;
  `manual_step: ef-migration` (owner, CONCURRENTLY). Wave-6 candidate.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6B**). Dep T-0204✓. Pure DB index follow-up, no
  behavior change → no panel. **Optimizer gate** on the `EXPLAIN`/query-plan evidence. **ef-migration
  (owner, CONCURRENTLY)** on the populated `UserMembership` table — held at the migration boundary; coexists
  with the T-0204 renewal partial index without conflict. Plan: `status/sprint-8.md` §3 Batch 6B.
- 2026-06-14 — **review** (db). Implemented the cancellation-arm partial index in
  `UserMembershipEntityConfiguration.cs` and confirmed both arms are index-served.

  **Confirmed sweep predicate** (`SendMembershipLifecycleNotifications.cs` cancellation arm):
  `CancelledAt != null AND CancellationReminderSentAt == null AND Status == Active AND
  CurrentPeriodEnd >= now AND CurrentPeriodEnd <= now+CancellationLeadDaysHigh`.

  **New index** (mirrors the T-0204 renewal shape, named overload so two indexes share the
  `(Status, CurrentPeriodEnd)` tuple — EF treats same-tuple `HasIndex` calls as ONE unless named):
  ```csharp
  builder.HasIndex(m => new { m.Status, m.CurrentPeriodEnd },
          "IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation")
      .HasFilter("\"CancelledAt\" IS NOT NULL AND \"CancellationReminderSentAt\" IS NULL");
  ```
  The `CancelledAt IS NOT NULL` clause (beyond just `CancellationReminderSentAt IS NULL`) keeps the
  partial index to the few rows with a live pending cancellation. The renewal index was given its
  existing default name explicitly via the named overload so the migration delta is ADD-only (renewal
  index unchanged).

  **TDD red→green:** added `UserMembership_HasCancellationReminderPartialIndex_ForCancellationArm` to
  `PerfIndexModelMetadataTests` (+ a `HasPartialIndexOn` helper asserting the EF `GetFilter()`), and a
  characterization test pinning the renewal arm's filter. Captured RED (cancellation test FAIL:
  `Expected: True Actual: False`; renewal characterization PASS). Implemented the index → GREEN. A
  transient intermediate where the renewal characterization went RED surfaced the EF same-tuple-merge
  gotcha; switching both to the named `HasIndex` overload made all green.

  **Optimizer-gate EXPLAIN evidence** (real Postgres, Testcontainers — new
  `UserMembershipCancellationSweepIndexPlanTests` in `Cleansia.IntegrationTests`, schema from the
  CURRENT EF model via `EnsureCreated` since the owner migration is not yet applied; 5000 noise rows +
  skewed matches + `ANALYZE`):
  - `CancellationSweepPredicate_UsesCancellationPartialIndex_NotSeqScan` — plan contains
    `IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation`, asserts NO seq scan. **PASS** (AC1).
  - `RenewalSweepPredicate_StillUsesRenewalPartialIndex` — plan still uses
    `IX_UserMemberships_Status_CurrentPeriodEnd`, does NOT use the cancellation index. **PASS** (AC4).
  Both green: `Passed! Failed: 0, Passed: 2` (14 s). Unit/metadata: `Passed! Failed: 0, Passed: 10`.

  **AC2 (no behavior change):** index-only; no query, handler, DTO, or reminder-logic edit — the sweep
  source is untouched. **AC3 / MANUAL_STEP ef-migration (owner, CONCURRENTLY):** see below; I did NOT
  run `dotnet ef`. The model snapshot (`CleansiaDbContextModelSnapshot.cs`) is owner-regenerated by the
  migration.

  **MANUAL_STEP: ef-migration** — owner generates one additive migration. Expected delta (single
  `CreateIndex`, no drops, no renames):
  ```csharp
  migrationBuilder.CreateIndex(
      name: "IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation",
      table: "UserMemberships",
      columns: new[] { "Status", "CurrentPeriodEnd" },
      filter: "\"CancelledAt\" IS NOT NULL AND \"CancellationReminderSentAt\" IS NULL");
  ```
  On the populated table apply `CONCURRENTLY` (edit the scaffolded migration to
  `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ...")` + `MigrationBuilder` with
  `suppressTransaction` per the T-0204 batch convention), since `CREATE INDEX CONCURRENTLY` cannot run
  inside a transaction.

  **Build/verification achieved:** `Cleansia.Infra.Database`, `Cleansia.Tests`, and
  `Cleansia.IntegrationTests` each built green in isolation; targeted unit + integration tests green.
  NOTE: the shared worktree had several concurrent lanes mid-flight (transient `ToJsonArrayToken`
  helper not yet defined in a Catalog lane; `RecordFailedCurrentPasswordAttemptAsync` /
  `User.Merge` / `DayOfWeek` ambiguity in an Auth lane) that intermittently broke solution-wide
  compilation; I polled until each settled before the authoritative runs. The orchestrator's clean run
  is authoritative.

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
