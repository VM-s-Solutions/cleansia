---
id: T-0700
title: A currency cannot be switched on, and a new one is on by default with no prices
status: todo
size: M
owner: —
created: 2026-09-10
updated: 2026-09-10
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

**The market switch has no writer, and the gate that guards promotion is vacuous for every currency an admin can create.**

Two halves of the same gap.

**Nothing flips an existing row.** There is no `ActivateCurrency` twin to `DeactivateActivateService`
/ `DeactivateActivatePackage`, `UpdateCurrency.Command` carries no `IsActive`, and the admin table now
renders it as a read-only column. The seeded EUR can only be activated by SQL — which is forbidden on
production.

**And a new one is on already.** `Currency.Create` inherits `BaseEntity`'s `IsActive = true` and
`CreateCurrency` never clears it, so a currency created from the admin screen is immediately active
with zero price rows — and `SetDefaultCurrency`'s only gate is `IsActive`. An admin can therefore
create a currency and star it, at which point all three customer catalogue overviews withhold every
entry (fail-closed) and the platform has no bookable catalogue. Recoverable by promoting the old
currency back, which is why this is a major and not a blocker.

## Acceptance criteria

1. An existing currency's `IsActive` can be changed from the admin screen.
2. `SetDefaultCurrency` refuses a currency the catalogue has no price rows in, with a translated
   business error rather than a silently empty catalogue.
3. A newly created currency is NOT operable until someone says so — either created inactive, or
   blocked from promotion by (2).
4. Tests cover: promote-with-no-prices refused; activate then promote accepted.

## Notes

The plan of record (T-0688 §3 B7) already anticipated replacing the IsActive gate with "has price rows in this currency".

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
