# Role — `Tenant` (registry row + the company's lifecycle) (CRC card)

> Introduced by **ADR-0061** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13, owner ruling
> *"We'll make a holding company and more companies under it for each region. So I think that assigning
> it from the beginning is the easiest approach."*; **amended 2026-09-15** on the owner's rulings
> Q-TENANCY-01..05). **Given its lifecycle by ADR-0064** (`docs/decisions/adr-0064.md`, **`accepted`**
> 2026-09-16, owner ruling Q-TENANCY-03 *"I'd build up to (c). Archive is also a good functionality to
> introduce in the beginning"*). `Cleansia.Core.Domain.Tenancy.Tenant : Auditable` — the registry pair
> (`Id`, `Name`), the `Auditable` stamps, and nine lifecycle columns. **The registry is seed-only** (which
> companies exist — no DTO, no endpoint creates, renames or deletes a row); **the lifecycle is
> admin-written**, by the company's own administrators, through `DeactivateCompany`, `ReactivateCompany`,
> `WindDownCompany` and `ArchiveCompany` → [Company lifecycle](./company-lifecycle). One row today:
> `('cleansia-cz', 'Cleansia CZ s.r.o.')`, the first insert in `sql-scripts/insert_seed_data.sql`, stamped
> `CreatedBy = 'seed'`.

## Responsibility (one sentence)
Name an operating company under the holding — the legal entity that contracts the customer, employs the
cleaner, issues the receipt and pays the payout — so that every stamped row and the country map can point
at it, **be the thing they are constrained to point at**, and **hold where that company stands**:
operating, winding down from a date, deactivated, frozen for archive, archived.

## Collaborators
- **Every stamped table** — `FK_<T>_Tenants_TenantId` on all 48 of them (`Restrict`, no navigation;
  ADR-0061 D1 as amended 2026-09-15): the 46 `TenantAuditable` types through the shared
  `TenantAuditableEntityConfiguration`, the two `BaseEntity + ITenantEntity` audits by hand. A row stamped
  with an id this registry does not hold fails `23503` at commit (`TenantForeignKeyEnforcedTests`). Two
  of the 48 — `OutboxMessages`, `DeadLetters` — keep a nullable column, and a `NULL` passes the FK.
- `CountryConfiguration.OperatorTenantId` — the map (`FK_CountryConfigurations_Tenants_OperatorTenantId`,
  Restrict, indexed). A country is served by at most one tenant; a tenant serves one or more countries.
  **`CountryRepository.GetServicedAsync` / `IsServicedAsync` join through it** to `OperatorTenant.IsActive`
  — the one place the column behind `IsDeactivated` is read directly (a computed predicate does not
  translate to SQL), so every reader of "serviced" inherits the company's deactivation (ADR-0064 D1).
- The seed — the only writer of the registry pair. A second operating company is a second seed row plus
  an `OperatorTenantId` on its country's configuration (ADR-0061 D12).
- **The five lifecycle predicates and the transition methods** — `IsDeactivated` (`!IsActive`),
  `IsWindDownRequested`, `IsWindDownRunning(now)` (a run stamp younger than one hour),
  `IsFrozen` (`ArchiveRequestedOn`), `IsArchived`; `State` is the highest that applies
  (`CompanyLifecycleState`: Operating, WindingDown, Deactivated, Frozen, Archived). `Deactivate(by, now)` /
  `Reactivate()` (clears the wind-down stamps), `RequestWindDown(from, by, now)` (once),
  `StartWindDownRun(now)` / `RecordWindDownRun(now)`, `RequestArchive(by, now)` (deactivated and wound
  down only), `MarkArchived(sha256, now)` (frozen only). Every transition refuses when frozen; a caller
  that skips the validator meets an `InvalidOperationException`, never a silent write.
- **Readers of the predicates** (the whole list — a reviewer greps for it):
  `CountryRepository` (the two serviced reads, by column), `CompanySignInGate` (a cleaner of a deactivated
  company is refused `auth.company_deactivated` on the partner audiences), `MaterializeRecurringBookings`
  (skips a deactivated company's templates), `PayPeriodBackgroundService` (opens no successor period for
  a deactivated company), `SetCountryServiced` (a country whose operator is deactivated cannot be switched
  on), `CompanyWindDownService` and `CompanyArchiveService` (their no-op checks), the four lifecycle
  validators, `GetCompanyLifecycle`, and `CleansiaDbContext.CommitAsync` (reads `ArchiveRequestedOn` by
  column — the archived-company write guard).
- **`ITenantRepository.GetAllIdsAsync`** — the registry's job reader. The retention job loops it (every
  company in the registry, **deactivated and archived ones included**) and runs its nine sweeps once per
  company under that company's override, because a company's GDPR obligations do not end with its
  trading (ADR-0064 D3, the legal-obligation gate).
- `SeededDatabaseHasNoOrphanTenantRowsTests` and the HostTests harness — read `ctx.Set<Tenant>()` to
  prove closure: every distinct `TenantId` on every stamped table exists here. Redundant with the FK on
  purpose — it is the seed's contract.

## Does NOT know
- **Which countries it serves.** The configuration row knows; the tenant carries no list.
- **Which users, orders or money belong to it.** The query filter knows. The 48 foreign keys point *at*
  this row; nothing on it points back — there is no collection navigation, and no reader walks from a
  company to its rows. **Which of its rows are books and which are the person's** is the write guard's
  sort (`ArchivedCompanyWriteGuard.AccountSurface`), not this row's.
- **Whether its books are settled.** The archive validator asks `ICompanySettlementReader`, a set of
  filtered counts → [Company settlement reader](./company-settlement-reader); the row holds stamps, never
  counts.
- **Its region.** ADR-0017's `HomeRegion` seam sits on `CountryConfiguration` beside the operator column;
  "every country of one operator shares a region" is the check for the day that column lands.
- **How to spell itself.** `MaxLength(26)` is the whole rule — every `TenantId` column is `varchar(26)`
  and the first stamped insert enforces it. There is no id grammar and no grammar test (ADR-0061 CH-7).
- **Its Stripe account.** The holding runs one Stripe account and settles card revenue intercompany
  (owner note 2026-09-15, with Q-TENANCY-01/05); every wind-down refund and Plus cancel goes through it.
- **Where its bundle is.** `<tenantId>/<ArchiveRequestedOn:yyyyMMddTHHmmssZ>/` in `company-archives` is a
  function of two things the row already holds; there is no `ArchiveBlobPath` and no DTO carries one.

## Invariants a reviewer checks
- **The seed inserts this row first**, with `CreatedBy`/`CreatedOn` (the two NOT NULL `Auditable`
  columns — an insert without them is `23502`). Everything that names `cleansia-cz` comes after it.
- **The default market's operator is not deactivated**, or every registration and sign-in that names no
  market fails `tenant.not_found`. `DeactivateCompany` refuses `company.operates_default_market` while the
  company holds the flag; `SetDefaultMarket` refuses a deactivated operator's market
  (`country.not_serviced`). With one company in the registry, that company cannot be deactivated at all.
- **The wind-down date is set once.** `RequestWindDown` throws on a second date; only `Reactivate()`
  clears it, and a later wind-down carries a new `WindDownRequestedOn`, so the notice's idempotency key
  is new and the people are told again.
- **`IsFrozen` is the guard's predicate, not `IsArchived`.** The freeze is the request; the bundle comes
  after. A frozen company whose build failed is asked to build again, never unfrozen from the product.
- **A test that stamps rows names a registered company.** The fixtures' `TestTenants` /
  `HostTestTenants` constants, or an id the test inserts into `Tenants` in its own arrange — an ad-hoc
  string fails the FK.
- **No DTO carries a tenant id** (S4). The wire names a market (`countryId`); the server maps it here.
  The lifecycle DTO names actors by e-mail and carries the manifest hash, never a blob path.

## Watch-list
- **`CompanySignInGate.RefusedProfiles` is the one constant** that decides whether a deactivated
  company's own administrators are refused too (ADR-0064 O-1, filed as Q-LC-01). T-0748 (ADR-0066 D7)
  kept it at `{ Employee }` — an administrator of any role is still admitted — and narrowed the act
  instead: since then only an **Administrator-role** administrator can reactivate the company
  (`CanReactivateCompany` → `AdministratorOnly`). A holding role is still ADR-0061 D14's open item.
- **The day a second company exists**, the ticket that onboards it decides whether the registry needs a
  writer — not before. The lifecycle needs none: it already has four.
- **A tenth tenantless child of a books row** fails `ArchivedCompanyWriteGuardRosterTests` until it is
  named with the reason its write is bounded; a new stamped table fails the same test until it is sorted
  as books or as the account surface.
