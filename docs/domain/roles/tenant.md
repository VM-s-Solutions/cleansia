# Role — `Tenant` (registry row) (CRC card)

> Introduced by **ADR-0061** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13, owner ruling
> *"We'll make a holding company and more companies under it for each region. So I think that assigning
> it from the beginning is the easiest approach."*; **amended 2026-09-15** on the owner's rulings
> Q-TENANCY-01..05). `Cleansia.Core.Domain.Tenancy.Tenant : BaseEntity`, three columns. **Seed-only
> writer**: no DTO, no endpoint, no admin surface — a screen with one row in it and no caller that needs
> to add a second is the shape CLAUDE.md §4 forbids. One row today: `('cleansia-cz', 'Cleansia CZ
> s.r.o.')`, the first insert in `sql-scripts/insert_seed_data.sql`.

## Responsibility (one sentence)
Name an operating company under the holding — the legal entity that contracts the customer, employs the
cleaner, issues the receipt and pays the payout — so that every stamped row and the country map can
point at it, **and be the thing they are constrained to point at**.

## Collaborators
- **Every stamped table** — `FK_<T>_Tenants_TenantId` on all 48 of them (`Restrict`, no navigation;
  ADR-0061 D1 as amended 2026-09-15): the 46 `TenantAuditable` types through the shared
  `TenantAuditableEntityConfiguration`, the two `BaseEntity + ITenantEntity` audits by hand. A row
  stamped with an id this registry does not hold fails `23503` at commit
  (`TenantForeignKeyEnforcedTests`). Two of the 48 — `OutboxMessages`, `DeadLetters` — keep a nullable
  column, and a `NULL` passes the FK.
- `CountryConfiguration.OperatorTenantId` — the map (`FK_CountryConfigurations_Tenants_OperatorTenantId`,
  Restrict, indexed). A country is served by at most one tenant; a tenant serves one or more countries.
- The seed — the only writer. A second operating company is a second seed row plus an
  `OperatorTenantId` on its country's configuration (ADR-0061 D12).
- **`ITenantRepository.GetAllIdsAsync`** — the one production reader, and the one member the
  repository has. The retention job loops it (every company in the registry, **deactivated ones
  included**) and runs its nine sweeps once per company under that company's override, so each reads
  its own `TenantConfiguration` windows. It was cut on 2026-09-13 because nothing read the registry;
  the first reader is what brought it back.
- `SeededDatabaseHasNoOrphanTenantRowsTests` and the HostTests harness — read `ctx.Set<Tenant>()` to
  prove closure: every distinct `TenantId` on every stamped table exists here. Redundant with the FK
  on purpose — it is the seed's contract.

## Does NOT know
- **Which countries it serves.** The configuration row knows; the tenant carries no list.
- **Which users, orders or money belong to it.** The query filter knows. The 48 foreign keys point
  *at* this row; nothing on it points back — there is no collection navigation, and no reader walks
  from a company to its rows.
- **Whether it is active in any operational sense.** `IsActive` is `BaseEntity`'s and **no reader
  consults it** — `GetAllIdsAsync` deliberately returns a deactivated company too, so a wound-down
  company keeps its retention windows. What deactivation *means* is ruled (Q-TENANCY-03, 2026-09-15:
  refuse its logins → delist its markets → the rest, in that order) and **not built**: it is Batch 2's
  lifecycle ADR, and until that lands the default of "nothing" still applies. **Retires when:** a
  reader of `Tenant.IsActive` exists under `src/Cleansia.Core.AppServices/` (the lifecycle ADR's
  deactivation arm).
- **Its region.** ADR-0017's `HomeRegion` seam sits on `CountryConfiguration` beside the operator column;
  "every country of one operator shares a region" is the check for the day that column lands.
- **How to spell itself.** `MaxLength(26)` is the whole rule — every `TenantId` column is `varchar(26)`
  and the first stamped insert enforces it. There is no id grammar and no grammar test (ADR-0061 CH-7).
- **Its Stripe account.** The holding runs one Stripe account and settles card revenue intercompany
  (owner note 2026-09-15, with Q-TENANCY-01/05); a company that must take its own card revenue is the
  day this changes, and it is not this row's concern until then.

## Invariants a reviewer checks
- **The seed inserts this row first.** Everything that names `cleansia-cz` — the CZE configuration,
  `CompanyInfo`, the pay defaults, the promo codes, the dev admin — comes after it; with the FK in
  place, an insert in the wrong order is a `23503`, not a silent orphan.
- **The default market's `OperatorTenantId` is non-null**, or every registration that names no market
  fails `tenant.not_found`. The closure scan asserts it (D11 d) and `SetDefaultMarket` refuses to flag a
  market nobody operates.
- **A test that stamps rows names a registered company.** The fixtures' `TestTenants` /
  `HostTestTenants` constants, or an id the test inserts into `Tenants` in its own arrange — an ad-hoc
  string fails the FK, which is what ended the eleven tests that used to prove tenancy against ids the
  seed forbids.
- **No DTO carries a tenant id** (S4). The wire names a market (`countryId`); the server maps it here.

## Watch-list
The row is inert by design; every consequence of its existence lives in the map, the filter and the
foreign keys. The day a second company exists, the ticket that onboards it decides whether this needs a
writer — not before. The day a company is wound down, Batch 2's lifecycle ADR decides what `IsActive`
gates — and `GetAllIdsAsync`'s "deactivated included" is the first sentence it re-reads.
