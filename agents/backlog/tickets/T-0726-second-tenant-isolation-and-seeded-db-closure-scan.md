---
id: T-0726
title: Second-tenant isolation host test and the seeded-DB closure scan (ADR-0061 D11)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0722, T-0723, T-0724, T-0725]
blocks: [T-0729]
stories: []
adrs: [ADR-0061]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

The proof that T-0722 did what it says: a second operating company seeded whole, a CZ admin who
lists none of it and 404s on every row by id while listing its own company's config rows, and the
seed applied to the migration-built schema leaving no orphan.

## Doing

- `SecondTenantIsolationHostTests`: `cleansia-sk` with an SVK configuration, customer, cleaner,
  order + receipt, pay default, promo code, company record, active membership; the CZ admin's lists
  (company, pay configs, promo codes, orders, employees, users) contain its own rows and none of SK's;
  by-id reads of every SK row are refused; the SK order is reachable anonymously only by its secret;
  a registration naming SVK lands in `cleansia-sk` and cannot be repeated in CZ; login resolves the
  account by email and every minted JWT carries `tenant_id`.
- `SeededDatabaseHasNoOrphanTenantRowsTests`: (a) the seed applied, (b) zero `TenantId IS NULL` on
  every stamped table, (c) every distinct tenant is in `Tenants`, (d) the default market is operated
  by `cleansia-cz`, plus the re-homed config rows read back under that company.
- `CleanupStalePendingOrdersTwoOperatorsTests`: the sweep smoke over two companies — every status
  track, feed row and outbox row carries its own order's company.
- `Ac9…Ac14`, `CrossTenantOrderReadTests`, `AuditLogByIdViewPolicyTests` constants →
  `HostTestTenants`.
- The filter-clause mutation check (`e.TenantId == current` replaced by `true`) was run locally: the
  two isolation reads went red; restored byte-exact.

## Acceptance criteria (met)

All of the above, all three suites green (4887 / 351 / 183).
