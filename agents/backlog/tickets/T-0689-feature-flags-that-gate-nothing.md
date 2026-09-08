---
id: T-0689
title: The feature-flag mechanism gated nothing and has been removed (no admin UI ever existed)
status: done
size: S
owner: —
created: 2026-09-07
updated: 2026-09-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** the admin UI has switches for six features. None of them do anything. Turning
"StripePayments" off would show the switch flip and payments would carry on working. That is worse
than not having the switch, because someone will eventually believe it.

Seeded in `sql-scripts/insert_seed_data.sql`, with **zero call sites** anywhere in the codebase:

| Flag | Seeded | Reads |
|---|---|---|
| `StripePayments` | true | none |
| `EcoFriendlyBadge` | true | none |
| `EmployeeSelfService` | true | none |
| `AutoInvoiceGeneration` | true | none |
| `DisputeSystem` | true | none |
| `PushNotifications` | false | none |

The seventh, `DataRetentionJobEnabled`, is the only one anything reads — and it has its own defect,
[T-0685](T-0685-retention-job-never-runs-in-production.md).

Separately, in the Angular apps: **`betaGateEnabled` and `betaGateUrl` have six declarations across
partner and admin environments and zero readers.** Dead config.

Full admin CRUD exists for the flag table — controller, list, create, edit — so the UI presents all
six as working controls.

## Acceptance criteria

- [x] **AC1** — Given the admin feature-flag screen, Then every flag it shows gates something, or is
      not shown.
- [x] **AC2** — Given `betaGateEnabled`/`betaGateUrl`, Then they are removed from all six
      environment files, or wired to something.

## Open decisions

1. **Delete the six rows, or wire them up?** Deleting is honest and small. Wiring six features to
   flags is real work and adds six ways to break production. Recommendation: delete, and keep the
   mechanism for flags that earn one.
2. **Keep the admin CRUD screen?** With one real flag left it may not deserve a screen. If it stays,
   it should not be able to create a flag that gates nothing without saying so.
3. Note the flags live in a **dev-only seed**, so "deleting" them means deleting seed rows plus
   deciding what a production table should contain — which is the same question T-0685 asks. Do
   these two together.

## Premise correction — read this before quoting the ticket above

**The headline was wrong.** "An admin can switch StripePayments off and payments keep working" cannot
happen, because there is no feature-flag screen anywhere in the admin app — no page, no route, no menu
entry, no component. Across all three Angular apps and every shared library, exactly ten files mentioned a
feature flag under any spelling, and every one was a translation file, a test, a generated API client, or a
list of permission names. The ticket inferred a front end from a back end.

Two smaller errors in the same paragraph: there was no **edit** operation (the five were Check, Create,
Delete, GetAll, Toggle), and line 36 — "`DataRetentionJobEnabled` is the only one anything reads" — went
stale when [T-0685](T-0685-retention-job-never-runs-in-production.md) moved that switch to configuration.

**What was actually true, and worse:** after T-0685 the table gated *nothing at all*. A closed loop — an
endpoint whose only purpose was to report on a table nothing consumed. That is what was removed.

## What the removal covered

Five controllers across all five API hosts, five CQRS operations plus their DTO, the entity, repository,
EF configuration and `DbSet`, five permission policies and their map rows, two error constants and their
five locale entries, `IsFeatureEnabledAsync` (and with it the now-unused `ITenantProvider` dependency on
`AppConfigurationProvider`), the six seed rows, and the twelve `betaGate` lines across six environment
files plus the orphaned `BETA_TOKEN` storage key. 54 files, -1831/+666.

The `Initial` migration was regenerated (77 -> 76 tables, 259 -> 257 indexes — exactly the one table and
its two indexes) and the DEV database dropped and recreated. All three NSwag clients were regenerated and
both mobile OpenAPI specs re-dumped.

## Decisions taken

1. **Delete the mechanism rather than keep it or trim the rows.** Owner ruling 2026-09-08. The
   intermediate option — deleting only the six seed rows — was rejected as strictly worse than either end
   state: it keeps every maintenance surface and gains only the appearance of tidiness. The case for
   keeping was built and lost on the evidence: the per-tenant scoping is unreachable (single-tenant,
   `TenantId` null everywhere), the per-country scoping duplicates `CountryConfiguration`, and the two
   switches the product genuinely needs — `Fiscal:CzechEet2:Enabled` and `APNS:Enabled` — both chose
   configuration over this table.
2. **If a future feature needs a switch that flips without a process restart, or one scoped per tenant,
   the whole mechanism is in git history.** That is the only capability configuration does not have
   (`AutoBindConfig` binds once, as a singleton, at startup). Nothing in the tree needed it.

## Status log

- 2026-09-07 — found during a pre-release feature-flag review. The review's headline was that
  nothing is being held back by a flag that could ship in V1; this is the other half of it.

- 2026-09-08 — ground-truthed: headline REFUTED (no admin UI), the six dead flags CONFIRMED, betaGate
  CONFIRMED at twelve lines across six files with zero readers. Implemented and green: 4376 unit + 223
  integration tests, admin Jest suite, eight repo checkers, typecheck across all three Angular apps.
  Migration regenerated and DEV dropped/recreated; clients and mobile specs regenerated.
