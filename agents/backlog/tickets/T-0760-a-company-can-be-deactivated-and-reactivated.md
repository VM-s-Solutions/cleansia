---
id: T-0760
title: A company can be deactivated and reactivated — the Tenant row carries its lifecycle, "serviced" requires an active operator everywhere, its cleaners are refused on the partner audiences
status: done
size: M
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: []
blocks: [T-0762, T-0763, T-0764]
stories: []
adrs: [ADR-0064, ADR-0061, ADR-0058]
layers: [backend, db, android-locales, ios-locales, frontend-locales]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0064 D1 and D4. Owner ruling Q-TENANCY-03 (2026-09-15): *"I'd build up to (c)"* — this is (a).
`Tenant.IsActive` had zero readers; "is this country open for business?" was answered by
`CountryRepository.GetServicedAsync` / `IsServicedAsync` (`IsServiced && IsActive`) behind fifteen files
and `OperatorTenantResolver` read the same two flags off the entity; six anonymous identity requests are
refused `tenant.not_found` by `OperatorTenantScopeBehavior` when no default market is listed — which is
why deactivating the company that holds the default market is refused (ADR-0064 C1).

## Doing (shipped `0601b0f2`; review `92c5d674`; cross-ticket review `3f2e8018`)

- **Schema.** `Tenant : Auditable` (was `BaseEntity`); `Auditable.Reactivated()`; the nine lifecycle
  columns (`WindDownFrom`, `WindDownRequestedOn/By`, `WindDownRunStartedOn`, `WindDownLastRunOn`,
  `ArchiveRequestedOn/By`, `ArchivedOn`, `ArchiveManifestSha256` char 64); the predicates
  (`IsDeactivated`, `IsWindDownRequested`, `IsWindDownRunning(now)` with the one-hour staleness,
  `IsFrozen`, `IsArchived`, `State`); the transition methods with their refusals; `Tenant.Create` stamps
  `CreatedBy = "seed"`; `TenantEntityConfiguration : AuditableEntityConfiguration`; the seed row and both
  Postgres fixtures' raw `INSERT INTO "Tenants"` carry `CreatedBy`/`CreatedOn`. **`Initial` regenerated
  → `20260915232921`** (remove + add, `Cleansia.Web.Partner` startup; 87 tables; `InitialMigrationTenantDdlTests`
  still pins 48). The DEV drop is owed at the deploy (MS-2).
- **The serviced predicate (C8).** `GetServicedAsync` / `IsServicedAsync` gained
  `&& CountryConfigurations.Any(cc => cc.CountryId == c.Id && cc.OperatorTenant != null && cc.OperatorTenant.IsActive)`;
  `OperatorTenantResolver.ResolveAsync` asks `IsServicedAsync`; `SetCountryServiced.MarketIsReadyAsync`
  requires a non-deactivated operator (`OperatorIsActiveAsync`, `country.market_not_ready`);
  `SetDefaultMarket` refuses a deactivated operator's market through its existing `IsServicedAsync` rule
  — **`country.not_serviced`**, not the `market_not_ready` the ADR drafted; `MaterializeRecurringBookings`
  reads the `Tenant` row once per group and skips a deactivated company. Diff-empty: `GetMarkets`,
  `CountryConfigurationRepository`, `CreateOrder`, the two membership writers,
  `SetRecurringBookingActive`. **Review (`92c5d674`):** `CreateRecurringBooking` and `AddSavedAddress`
  read no serviced predicate at all — the ADR's inheritance table counted them as readers — and each
  gained its own `IsServicedAsync` rule refusing `country.not_serviced` (a recurring booking from a saved
  address in a closed market; a new saved address in one), with the lifecycle route tests proving the
  open half.
- **Sign-in gate.** `ICompanySignInGate.RefusalForAsync(user, audience)` — scoped, memoised per request;
  `RefusedProfiles = { Employee }`, gated audiences Partner and Mobile; one
  `ITenantRepository.GetByIdAsync` only for a cleaner on a partner audience; `auth.company_deactivated`
  on the e-mail field. Called in `PartnerLogin`, `MobilePartnerLogin`, `GoogleAuth`, `AppleAuth`
  (existing-staff branch), `RefreshToken.Handler` (partner audiences); `TokenService.GenerateTokenAsync`
  throws the invariant beside `Admits` through the same instance. Administrators not refused (O-1).
- **Commands and read.** `DeactivateCompany.Command()` — `tenant.not_found` (first, on every lifecycle
  act: `3f2e8018`), `company.archived`, `company.already_deactivated`, `company.operates_default_market`
  off `GetDefaultMarketAsync().OperatorTenantId`; re-enqueues the wind-down when a date is set (with
  T-0762). `ReactivateCompany.Command()` — `tenant.not_found`, `company.archived`,
  `company.not_deactivated`; clears the wind-down stamps. Both return `Response(State)` (the page
  re-reads). `GetCompanyLifecycle.Query` → `CompanyLifecycleDto` (name, state, `OperatesDefaultMarket`,
  every stamp with the actor's e-mail, the manifest hash, the fourteen settlement facts and
  `ChargebackHorizonEndsOn`) over `ICompanySettlementReader.ReadAsync` (`Core.Domain.Tenancy`;
  `Infra.Database.Repositories.CompanySettlementReader`, every count filtered; `WindDownCutoff` shared
  with the sweep). `TenantSettingCatalog.ChargebackHorizonDays` (`lifecycle.chargeback_horizon_days`,
  category `lifecycle`, 180, 0–730). `AdminCompanyLifecycleController` `api/AdminCompanyLifecycle/{get,deactivate,reactivate}`;
  `Policy.CanViewCompanyLifecycle`, `CanDeactivateCompany`, `CanReactivateCompany`, `CanWindDownCompany`,
  `CanArchiveCompany` (`AdminOnly`, `FrozenPermissionMapTests`); the writes under the `auth` window;
  `[AuditAction("company.deactivate" / "company.reactivate", ResourceType = "Tenant")]` with
  `CompanyLifecycleSnapshot(State, WindDownFrom)` before/after through `IAuditContext.RecordChange`;
  `CompanyLifecycleAuditLabelTests`.
- **NSwag regen** of the admin client (`87ff17e0`, after c). **Locales:** `company.already_deactivated`,
  `company.not_deactivated`, `company.archived`, `company.operates_default_market` in the five admin
  locales; `auth.company_deactivated` in the five locales of partner web, partner Android and partner
  iOS; the parity specs updated.

## NOT

No wind-down, archive, guard, e-mail or queue (T-0762/c). No holding role. No change to
`Country.IsServiced`, `SetCountryServiced.Handler`, the filter, `TenantProvider`, `SetClaims`,
`ITenantRepository`'s members, `GetMarkets.ResolveMarketAsync`, `CreateOrder`, the two membership writers,
`SetRecurringBookingActive`. No `company.deactivated` key for template resume (C8). No bulk revocation of
refresh tokens. No admin page (T-0764). No platform shutdown.

## Acceptance criteria

- [x] AC1 (TC-LC-DEACT-1) — a deactivated company's market is absent from `Market/GetOverview` on the four
      hosts that expose it and from `Country/GetServiced`; present while operating.
- [x] AC2 (TC-LC-DEACT-2) — an anonymous `Register`, `QuoteOrder` or guest `CreateOrder` naming it is 400
      `country.not_serviced` on `CountryId`; no row written.
- [x] AC3 (TC-LC-DEACT-3) — a signed-in customer's `QuoteOrder`, `CreateOrder`, `CreateRecurringBooking`,
      `AddSavedAddress` and `CreateMembershipSubscription` in the closed market are refused
      `country.not_serviced` (the two review rules); `GetServiceOverview` returns nothing.
- [x] AC4 (TC-LC-DEACT-4) — the company's cleaner is refused `auth.company_deactivated` on partner web,
      partner mobile, Google on a partner host and on refresh, with no `RefreshToken` row; signs in,
      exports and erases on the customer host; its administrator and customer sign in; the other
      company's users sign in everywhere.
- [x] AC5 (TC-LC-DEACT-5) — reactivation inverts AC1–AC4; `DeactivatedOn/By` null, `IsActive` true, the
      `WindDown*` stamps null.
- [x] AC6 (TC-LC-DEACT-6) — deactivating the default-market holder is `company.operates_default_market`;
      admitted after `SetDefaultMarket` moves the flag; `SetDefaultMarket` naming a deactivated operator's
      market is refused (`country.not_serviced` — see the Doing note); `SetCountryServiced(true)` on one is
      `country.market_not_ready`.
- [x] AC7 — `DeactivatedOn/By`, `IsActive = false`, one `company.deactivate` audit row with the snapshot
      before/after; a second call `company.already_deactivated`; both commands `company.archived` on a
      frozen company.
- [x] AC8 — the materialiser creates no order for a deactivated company's templates.
- [x] AC9 — `GET api/AdminCompanyLifecycle/get` names the company, state `Operating`, every seeded
      settlement fact, the horizon = latest card-paid `CleaningDateTime` + 180 days (or the override);
      no tenant id, no blob path.
- [x] AC10 — the five policies rostered `AdminOnly`; the two labels frozen; the snapshot passes the PII
      guard; `TenantIdRequiredModelTests` green; the catalogue holds `lifecycle.chargeback_horizon_days`
      (180, 0, 730) under category `lifecycle`.
- [x] AC11 — every new key in all five locales of every app that can reach it; the admin and partner
      parity specs green; the customer apps' specs unchanged.

## Status log

- 2026-09-16 — shipped `0601b0f2`; review `92c5d674` (the two paths that read nothing); cross-ticket
  review `3f2e8018` (`tenant.not_found` before the frozen check). Admin client regen `87ff17e0`. The
  regen ran (`20260915232921`); the DEV drop is owed at the deploy. Recorded in ADR-0064 D1 and §*What
  shipped*; `/domain/roles/tenant` rewritten, `/domain/roles/company-lifecycle`,
  `/domain/roles/company-settlement-reader`; `/product/business-rules#company-lifecycle`; S8/S10;
  CLAUDE.md landmine 1; MS-2.
