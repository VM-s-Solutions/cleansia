# Role — `Tenant` (registry row) (CRC card)

> Introduced by **ADR-0061** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13, owner ruling
> *"We'll make a holding company and more companies under it for each region. So I think that assigning
> it from the beginning is the easiest approach."*). `Cleansia.Core.Domain.Tenancy.Tenant : BaseEntity`,
> three columns, 27 lines. **Seed-only**: no repository, no DTO, no endpoint, no admin surface — a screen
> with one row in it and no caller that needs to add a second is the shape CLAUDE.md §4 forbids. One row
> today: `('cleansia-cz', 'Cleansia CZ s.r.o.')`, the first insert in `sql-scripts/insert_seed_data.sql`.

## Responsibility (one sentence)
Name an operating company under the holding — the legal entity that contracts the customer, employs the
cleaner, issues the receipt and pays the payout — so that stamped rows and the country map can point at
it.

## Collaborators
- `CountryConfiguration.OperatorTenantId` — the **only** foreign key to it (`FK_CountryConfigurations_Tenants_OperatorTenantId`,
  Restrict, indexed). A country is served by at most one tenant; a tenant serves one or more countries.
- The seed — the only writer. A second operating company is a second seed row plus an
  `OperatorTenantId` on its country's configuration (ADR-0061 D12).
- `SeededDatabaseHasNoOrphanTenantRowsTests` and the HostTests harness — the only readers of
  `ctx.Set<Tenant>()`, and they read it to prove closure: every distinct `TenantId` on every stamped table
  exists here.

## Does NOT know
- **Which countries it serves.** The configuration row knows; the tenant carries no list.
- **Which users, orders or money belong to it.** The query filter knows; there is no FK from the 44
  stamped tables to this one — a tenant id enters the system at two points only (the operator map,
  which is FK'd, and the claim, minted from a row stamped through the map), and forty-odd constraints
  would guard a value nothing types.
- **Whether it is active in any operational sense.** `IsActive` is `BaseEntity`'s and no reader consults
  it (Q-TENANCY-03, default: nothing until a reader exists).
- **Its region.** ADR-0017's `HomeRegion` seam sits on `CountryConfiguration` beside the operator column;
  "every country of one operator shares a region" is the check for the day that column lands.
- **How to spell itself.** `MaxLength(26)` is the whole rule — every `TenantId` column is `varchar(26)`
  and the first stamped insert enforces it. There is no id grammar and no grammar test (ADR-0061 CH-7).

## Invariants a reviewer checks
- **The seed inserts this row first.** Everything that names `cleansia-cz` — the CZE configuration,
  `CompanyInfo`, the pay defaults, the promo codes, the dev admin — comes after it.
- **The default market's `OperatorTenantId` is non-null**, or every registration that names no market
  fails `tenant.not_found`. The closure scan asserts it (D11 d) and `SetDefaultMarket` refuses to flag a
  market nobody operates.
- **No DTO carries a tenant id** (S4). The wire names a market (`countryId`); the server maps it here.

## Watch-list
The row is inert by design; every consequence of its existence lives in the map and the filter. The
day a second company exists, the ticket that onboards it decides whether this needs a writer — not
before.
