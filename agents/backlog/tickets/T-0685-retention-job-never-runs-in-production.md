---
id: T-0685
title: The GDPR retention sweep has never run in production — an absent feature flag reads as disabled
status: in_progress
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

- [x] **AC1** — Given a production-shaped database with no FeatureFlags rows, When the retention job
      runs, Then it executes rather than skipping.
- [x] **AC2** — Given the job is meant to be switchable, Then whatever replaces today's silent
      default makes "off" a deliberate act rather than the consequence of an empty table.
- [x] **AC3** — Given a test, Then it fails if the job would no-op against an empty flag table. The
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

## Decisions taken

1. **The gate stays, but leaves the database.** Owner ruling 2026-09-07. The switch is now
   `DataRetention:Enabled`, bound from configuration by `DataRetentionConfig` with a default of
   **true** — the same shape `OutboxRetentionConfig` gives the sibling `PruneOutbox` job in the same
   folder, and the shape ADR-0002 §A6.1 already prescribes. Absence means ON; "off" is something
   somebody typed.
2. **Production does not need to get a row**, because there is no row. This decision dissolved with
   decision 1 — which was its strongest argument.
3. **`IsFeatureEnabledAsync` keeps returning false for an unknown flag.** Deliberately not changed.
   The same method answers `GET /api/FeatureFlag/check`, which is `[AllowAnonymous]` on the customer
   host and validates only that the name is non-empty; inverting the default would make every
   invented flag name answer "on" to any anonymous caller. Fail-closed is right for the other six
   flags. The retention job needed fail-open for *itself*, which is a property of the job, not of the
   flag mechanism.

## Status log

- 2026-09-07 — found during a pre-release feature-flag review. Verified independently before filing.
- 2026-09-07 — ground-truthed: all four claims still hold, each independently re-verified and
  adversarially challenged. Three corrections to the ticket: the job is **weekly** (Sunday 03:00 UTC,
  `DataRetentionTimerFunction.cs:10`), not nightly; the deployed **DEV** database is in the same state
  as production, because `main.bicep:520` sets no `ASPNETCORE_ENVIRONMENT` so every deployed host runs
  as Production; and the "disabled by feature flag" line may never have been visible, since
  `host.json:12` floors Functions logging at `Warning` and that line was `Information`.
- 2026-09-07 — implemented. AC3 proven rather than asserted: with the old gate restored the new test
  fails exactly as production does (the 100-day-old row survives). 4377 unit + 223 integration tests
  green. **Not yet shipped** — gated on the production count below.
- 2026-09-07 — adversarial review of the change caught two real defects in it, both fixed:
  1. **The first attempt shipped a `DataRetention` block in `Cleansia.Functions/appsettings.json`**, to
     make the value visible. That would have removed the kill switch. The Functions worker composes
     configuration in the OPPOSITE order to the five API hosts — `ConfigureFunctionsWorkerDefaults`
     registers the environment-variable providers first and `Program.cs` adds `appsettings.json` last,
     and last provider wins — so a committed JSON value BEATS the `DataRetention__Enabled` app setting
     an operator would set in Azure. Reproduced empirically against a real host build. The entry was
     removed; the code default already yields `true` and the env override now works. A warning against
     re-adding it is on `DataRetentionConfig`. `OutboxRetentionConfig` ships no entry there either.
  2. **The production check script only counted `SavedAddresses`**, and would have reported a false
     zero. `Addresses` has three dependants; the likeliest sharing case is two ORDERS at one flat, one
     past the window and one recent. Section 3 now covers Orders, SavedAddresses and Employees.
## Gate before this ships

Turning the sweep on activates `CleanOrderCustomerPii`, which calls `Anonymize()` on the order's
`Address` row. Address rows are **deduplicated across users** on (Street, City, ZipCode, CountryId)
(`AddressRepository.GetAddressAsync`), so one row is shared by everyone in a building, and
`Anonymize()` overwrites Street/City/ZipCode/State in place with `[DELETED]`. A two-year-old order can
therefore blank the saved address of a different, currently-active customer.

`sql-scripts/check-orders-past-retention-window.sql` counts the exposure. Run it against PRO via the
Execute SQL Script workflow. **If section 3 returns non-zero, do not enable the sweep** until the
shared-address defect is fixed — it becomes a blocker on this ticket rather than a separate one.
