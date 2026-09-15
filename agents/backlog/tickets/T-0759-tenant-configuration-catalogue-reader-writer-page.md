---
id: T-0759
title: TenantConfiguration has a key catalogue, a reader that works under jobs, an admin writer and a Company settings page
status: done
size: M
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0758]
blocks: []
stories: []
adrs: [ADR-0061]
layers: [backend, db, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-TENANCY-04** (ADR-0061 O-4): *"There is a need to introduce reader and
writer for it."* `TenantConfiguration` was a per-company key/value table with **zero rows, zero
writers** and one reader — `IAppConfigurationProvider.GetTenantSettingAsync` under the retention job,
which ran with no claim and no override, so every read returned null and every sweep ran on
`RetentionDefaults.*`. The `CanCreate/Update/DeleteTenantConfiguration` policies existed with nothing
behind them.

## Doing (backend — `e7f2c6b9`, review `24df12ee`)

- **`TenantSettingCatalog`**: nine typed definitions (`IntTenantSetting` with `min`/`max`,
  `BoolTenantSetting`) for the `retention.*` keys — floor 1, ceiling 100 years / 36 500 days, defaults
  from `RetentionDefaults`. A key outside it is refused `tenant_setting.unknown_key`, a value its
  definition rejects `tenant_setting.invalid_value`; a second category joins by adding entries.
- **Reader**: `TenantSettingReader.GetAsync(provider, definition)` — the ambient company's row through
  the filter, resolved by the definition, the default when no row or an unacceptable one. The retention
  job now loops **`ITenantRepository.GetAllIdsAsync`** (the registry gains its one member and its
  first production reader — deactivated companies included, `IsActive` still read by nothing), sets
  the override per company, runs the nine sweeps under it, commits inside each, clears. Pinned on
  Postgres: a `retention.customer_audit.years = 1` row for company B deletes B's two-year-old rows
  and keeps A's (`CustomerActionAuditRetentionTests`). `GdprDeletionService` reads the dispute-text
  window the same way.
- **Writer**: `SetTenantSetting.Command(Key, Value)` (upsert for the admin's own company, canonical
  form stored), `ResetTenantSetting.Command(Key)` (hard delete → default, idempotent),
  `GetTenantSettings.Query` (every catalogue key: type, range, default, effective value, override
  flag — the page is the catalogue, not the table) on `api/AdminTenantSettings/{get-all,set,reset/{key}}`
  under the existing `CanView/Update/DeleteTenantConfiguration` policies (`AssertComplete` green),
  audited `tenant_setting.set` / `.reset` with a `TenantSettingSnapshot(Key, Value)` before/after; the
  two writes under the `auth` rate window. Five admin locales for the two keys; `TenantSettingsRouteTests`.
- Review: the handlers trust the pipeline's validator (`Find(key)!`); every comment the per-company
  sweep made false rewritten.

## Doing (admin web — `a84d205c`, review `9f9bedb3`; clients `1f3c70b8`)

- `libs/cleansia-admin-features/company-settings`: one row per catalogue key (key, description from
  the locale, category, range, default, value in force, override/default), inline edit with the typed
  input the value type calls for (number field / checkbox), Reset behind a confirmation on an
  overridden row, re-read after either write; facade in signals with a `switchMap` load and an
  in-flight guard; specs. Sidebar entry `/company-settings` gated by `CanViewTenantConfigurations`,
  Edit/Reset by `CanUpdate` / `CanDeleteTenantConfiguration`. `adminTenantSettingsClient` on the
  hand-written aggregator; five locales; `tenant-setting-catalogue.spec.ts` reads the backend catalogue
  file so a key without its copy fails. Review: each window's description states its real mechanism.

## Phase-A/B review findings (`c8bd9e13`, `c4fe56b9`)

The pay-calc consumer runs under the envelope's tenant; every envelope consumer is guarded for the
tenant override; the pay-calc consumer dead-letters a body it cannot run; an administrator's partner
session refreshes; an empty refresh profile pin fails closed.

## NOT

No settings beyond the catalogue. No per-country settings — that is `CountryConfiguration`. No
holding-level write.

## Status log

- 2026-09-15 — shipped across the commits above. Recorded in ADR-0061 O-4 as ruled, D1/D7/D10 as
  amended and §Rulings; the new `/domain/roles/tenant-configuration` card; `/domain/model`;
  `/product/features` (Company settings); `/product/business-rules#customer-record` (the nine windows,
  per company, with ranges); `/flows/cross-cutting` (the registry-driven job shape);
  `/admin-app/overview`; CLAUDE.md landmine 2; `patterns-backend.md`; the audit-log and tenancy living
  notes; CHANGELOG (Added, Changed).
