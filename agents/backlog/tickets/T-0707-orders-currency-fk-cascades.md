---
id: T-0707
title: FK_Orders_Currencies_CurrencyId is Cascade — deleting a currency would delete its orders
status: todo
size: S
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [db]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**Every sibling money table is Restrict. Orders is not.**

`FK_Orders_Currencies_CurrencyId` is `ReferentialAction.Cascade` in the migration, while
`ServicePrices`, `PackagePrices`, `ExtraPrices`, `CreditAccounts` and the payroll tables are all
`Restrict`.

Unreachable through the app today: `DeleteCurrency` calls `IsInUseAsync`, which checks Orders first
and refuses. But the guard is application-level and the cascade is not — one bypass, one background
job, one hand-run statement, and the orders are gone rather than the delete being refused.

Pre-production, so the fix is a one-line EF configuration change plus a migration regeneration and a
DEV drop. It costs nothing now and cannot be done cheaply later.

## Acceptance criteria

1. The FK is `Restrict`, like every other table that references Currencies.
2. `Initial` is regenerated and DEV dropped.
3. The integration suite proves the schema and the model agree.

## Notes

Batch with any other schema change so there is one regeneration, not two.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
