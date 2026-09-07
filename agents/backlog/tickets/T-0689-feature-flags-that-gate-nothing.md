---
id: T-0689
title: Six feature flags and a beta gate that control nothing — an admin can switch Stripe "off" and take payments
status: todo
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

- [ ] **AC1** — Given the admin feature-flag screen, Then every flag it shows gates something, or is
      not shown.
- [ ] **AC2** — Given `betaGateEnabled`/`betaGateUrl`, Then they are removed from all six
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

## Status log

- 2026-09-07 — found during a pre-release feature-flag review. The review's headline was that
  nothing is being held back by a flag that could ship in V1; this is the other half of it.
