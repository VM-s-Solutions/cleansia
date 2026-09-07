---
id: T-0685
title: The GDPR retention sweep has never run in production — an absent feature flag reads as disabled
status: todo
size: S
owner: —
created: 2026-09-07
updated: 2026-09-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** there is a nightly job whose whole purpose is to delete or anonymise personal
data the platform is no longer allowed to keep. In production it starts, decides it is switched off,
writes "disabled by feature flag" to the log, and reports success. It has never deleted anything.

Four facts, each verified:

1. `AppConfigurationProvider.IsFeatureEnabledAsync` ends `return globalFlag?.IsEnabled ?? false`.
   **A flag that does not exist is treated as switched off.**
2. `DataRetentionBackgroundService` asks that question about `DataRetentionJobEnabled` and returns
   early when the answer is no. It is the ONLY real consumer of the feature-flag table.
3. **No migration inserts any FeatureFlags row** — zero `InsertData`/`HasData` across all migrations.
   A production database has an empty table.
4. The rows only exist in `sql-scripts/insert_seed_data.sql`, which runs solely under
   `if (environment.IsDevelopment())`, and `.github/workflows/execute-sql.yml` refuses to run it
   against production: *"insert_seed_data.sql is a DEVELOPMENT FIXTURE, not an operational script."*

So the flag is `true` on every developer machine and absent — therefore false — in production.

### Why nothing caught it

`DataRetentionFeatureFlagSeedTests` asserts on the text of the **dev seed file**. It passes forever
while production does nothing. That is the same class of mistake the test was written to prevent,
one layer up: it pins the fixture rather than the behaviour.

### What is actually not happening

The sweep owns seven tasks, including **anonymising customer PII on old orders** and **purging
withdrawn consents**. Both are retention obligations, not housekeeping.

## Acceptance criteria

- [ ] **AC1** — Given a production-shaped database with no FeatureFlags rows, When the retention job
      runs, Then it executes rather than skipping.
- [ ] **AC2** — Given the job is meant to be switchable, Then whatever replaces today's silent
      default makes "off" a deliberate act rather than the consequence of an empty table.
- [ ] **AC3** — Given a test, Then it fails if the job would no-op against an empty flag table. The
      current seed-text test does not, and should be replaced rather than added to.

## Open decisions

1. **Keep the flag at all, or delete it?** It is the only flag in the table anything reads. If the
   job should always run, deleting the gate removes a whole class of this bug. If it must stay
   switchable, the default has to invert.
2. **If the flag stays: how does production get the row?** A migration `InsertData`, or a startup
   ensure-exists, or an explicit admin action as part of go-live. Note the seed file cannot be the
   answer — CI blocks it from production by design.
3. **`IsFeatureEnabledAsync` returning false for an unknown flag is a platform-wide default.**
   Changing it fixes this case and affects any future flag. Decide it once, here.

## Status log

- 2026-09-07 — found during a pre-release feature-flag review. Verified independently before filing.
