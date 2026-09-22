---
id: T-0722
title: Tenancy is active from day one — tenant registry, operator map, NOT NULL TenantId, index terms, seed re-homing, fixtures (one regen)
status: done
size: L
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0721]
blocks: [T-0723, T-0724, T-0725, T-0726, T-0727, T-0728, T-0729]
stories: []
adrs: [ADR-0061, ADR-0050, ADR-0058]
layers: [db, backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-13: *"We'll make a holding company and more companies under it for each region.
So I think that assigning it from the beginning is the easiest approach."* ADR-0061 (proposed, the
challenger round applied) makes a tenant an operating company under the holding, maps each market to
the company that serves it, and stamps every business row with that company from its first write.
Nothing had ever written a tenant: no registry, no resolver, every anonymous writer landed `NULL`,
five seeded config tables carried `NULL` on purpose, and no claim could ever be minted.

## Doing

- `Tenant` (`Core.Domain/Tenancy`), `Tenants` table, `DbSet<Tenant>`; no repository, no id grammar
  (ADR CH-7). Seeded first: `('cleansia-cz', 'Cleansia CZ s.r.o.')`.
- `CountryConfiguration.OperatorTenantId` (FK to `Tenants`, non-unique index) + `AssignOperator`;
  the seed maps CZE to `cleansia-cz`, every other configured country to nobody.
- `TenantId` NOT NULL varchar(26) on every `ITenantEntity` table except `OutboxMessages` and
  `DeadLetters` (`AuditableEntityConfiguration.TenantIdNullableTypes`); `AdminActionAudit` in its
  own EC; `OrderStatusTrack` moved onto the shared Auditable mapping — its convention-only mapping
  had left the column `text NULL`, which the roster-free `TenantIdRequiredModelTests` caught.
- `LoyaltyTierConfig` drops `ITenantEntity` (the brand's programme); index `(Tier)`.
- `Users (Email)` globally unique (ADR-0061 D5.1); `OrderReceipts (TenantId, ReceiptNumber)`
  NULLS NOT DISTINCT; `EmployeePayConfigs` gains the leading `TenantId` term.
- Seed: `Tenants` first, CZE operator, `CompanyInfo` / pay defaults / promo codes / dev admin stamped
  `cleansia-cz`, `LoyaltyTierConfigs` insert loses the column, every "single-tenant default" comment
  rewritten.
- `Initial` regenerated once (`20260913115255`). **The DEV drop is owed once, at deploy time.**
- Fixtures: `TestTenants` (TestUtilities), `HostTestTenants`; the harness resets re-insert the two
  registry rows; `BaseIntegrationTest` presets the scoped tenant and stamps arranged rows;
  `AuthzHostTestBase.SeedAsync` commits under the default; `TestJwtFactory` defaults the claim;
  `DomainSeed.EnsureReferenceDataAsync` makes CZ a market with an operator.
- **T-0723, T-0724 and T-0725 landed in the same commit** — with NOT NULL on `Users`, `Carts` and
  `RefreshTokens`, every anonymous registration and every login fails 23502 until the scope behaviour
  and the token-mint override exist, so the group could not be green without them. Their own rows
  carry the detail.

## NOT

No admin surface for `Tenants` or the operator map; no DTO carries a tenant (S4); the dead
`Auditable.TenantId` on tenantless tables stays; the filter, `TenantProvider` and the claim mint are
diff-empty; no docs page (T-0729); no locale (T-0727); no client sends `countryId` (T-0728); NSwag and
the mobile spec are the orchestrator's; the DEV drop is not run on the branch.

## Acceptance criteria (all met — see the report in the commit message)

- The regenerated `Initial` carries `CREATE TABLE "Tenants"` with `Id varchar(26)`,
  `CountryConfigurations.OperatorTenantId` with its FK, `IX_Users_Email` unique with no tenant term,
  `IX_OrderReceipts_TenantId_ReceiptNumber` and the five-column `EmployeePayConfigs` index both with
  `Npgsql:NullsDistinct=false`, and `IX_LoyaltyTierConfigs_Tier`.
- `TenantId … nullable: true` appears only on `OutboxMessages`, `DeadLetters` and the tenantless
  `Auditable` tables (`TenantIdRequiredModelTests` is the model-side twin).
- A stamped entity committed under a null provider raises 23502 (`TenantIdNotNullEnforcedTests`).
- Two `User` rows with one email in two companies raise 23505 on `IX_Users_Email`
  (`UsersEmailGloballyUniqueTests`).
- `NullsNotDistinctIndexModelTests`' roster is contract §1.5's list, `User` absent, `UserMemberships`
  still the negative control.
- The seed applied to the migration-built DB leaves no stamped row without a tenant, every tenant in
  `Tenants`, and CZE operated by `cleansia-cz` (`SeededDatabaseHasNoOrphanTenantRowsTests`).
- Cleansia.Tests 4887, IntegrationTests 351, HostTests 183 — all green; no test deleted to get there
  (one test rewritten out: `A_Legacy_Null_Tenant_Template_Following_A_Tenanted_One_Stays_Null`
  described a row that can no longer exist; its two-tenant sibling already covers the shape).

## Absorbed on the way (inside the ticket's files)

- `ResendConfirmationEmail` read the user through the filter on an anonymous request; under NOT NULL
  that is "user not found" for every resend. Now tenant-ignoring like its siblings (D5.1).
- `CreditAccountRepository.TryReturnAsync` flushed a new `CreditAccount` with a bare
  `SaveChangesAsync`, bypassing the commit-time stamp — a 23502 in production the day the column is
  NOT NULL. It commits through `CommitAsync`.
- `OrderAddressResolver` accepted any `SavedAddressId` from a guest (the panel's finding). A guest has
  no saved addresses; naming one is naming somebody else's, and it is refused `general.not_found` on
  both the address read and the country read.
