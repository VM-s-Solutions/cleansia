---
id: T-0219
title: Anonymous catalog entities → platform config (Service/ServiceCategory/Package/Extra/ServiceCity)
status: done
size: M
owner: —
created: 2026-06-02
updated: 2026-06-15
depends_on: [T-0100, T-0113]
blocks: []
stories: []
adrs: [0001]
layers: [backend, db]
security_touching: true
manual_steps: [ef-migration]
sprint: 2
source: finding LG-SEC-05 sibling class; ADR-0001 Addendum A1 §D-A1.4; platform-expandability doctrine §7b/§8
pairs_with: T-0126
---

## Context

The **sibling class** of T-0113 (LG-SEC-05). Five public-facing catalog entities are `ITenantEntity`
yet served on `[AllowAnonymous]` customer/mobile routes, so under an anonymous request (no JWT → no
`tenant_id` claim) the EF global query filter collapses to the `TenantId == null` slice. They are
correct **today only** because single-tenant mode makes the null-slice equal the only tenant; in a real
multi-tenant deployment an anonymous visitor could only ever see the null-tenant catalog (wrong/empty),
and a `TenantId == null` "shared" row would leak to every tenant's anonymous page.

This ticket is created per the **architect panel + platform-expandability deliberation (2026-06-02)**,
which ruled these get the **same Option A treatment as `MembershipPlan` (T-0113)** but in their **own
batch** (NOT folded into T-0113 — scope discipline; avoids the double-fix collision). Governing docs:
- ADR-0001 **Addendum A1** (`adr/0001-authorization-model.md`) — D-A1.1 doctrine + D-A1.4 routes these here.
- `agents/knowledge/platform-expandability.md` §7b/§8 — the per-entity index reality + the
  forward-safe/reverse-constrained FK note (carry both into this ticket's implementation).

> **"BSP-9" naming collision (resolve before scheduling):** the label "BSP-9" is overloaded — in T-0123
> it means the `Order/LookupBatch` hardening; in Addendum A1 D-A1.4 / the doctrine it meant *this* catalog
> batch. **This ticket (T-0219) is the canonical home for the catalog fix.** Do not reuse "BSP-9" for it.

## Entities in scope (the batch)
`Service`, `ServiceCategory`, `Package`, `Extra`, `ServiceCity` → drop `ITenantEntity`, make platform
config (like `Currency`/`Language`/`Country`, which are already platform config). `ServiceCategory` is
reached transitively via `Service.GetOverview` (no own anonymous controller) — it MUST move with `Service`.

## Per-entity index reality (file-verified 2026-06-02 — the reversal is NOT uniform; do not inherit MembershipPlan's "cheap" story)
- **`ServiceCategory`** — `(TenantId, Slug)` UNIQUE (`ServiceCategoryEntityConfiguration.cs:19`). Forward: → `(Slug)` unique.
- **`Extra`** — `(TenantId, Slug)` UNIQUE (`ExtraEntityConfiguration.cs:22`). Forward: → `(Slug)` unique.
- **`Service`** — **NO unique index** (only the Category FK, `ServiceEntityConfiguration.cs`). Nothing to swap forward; decide whether a `(Code/Slug)` unique should exist at all.
- **`Package`** — **NO unique index** (`PackageEntityConfiguration.cs`). Same as Service.
- **`ServiceCity`** — only non-unique `(CountryId, Name)` + `ZipPrefix` index (`ServiceCityEntityConfiguration.cs:34,36`), no tenant index. Decide whether `(CountryId, Name)` should become unique.

## Forward-safe / reverse-constrained (must be stated + tested)
The **forward** flip is safe for **prices** (no `CurrencyId`; converted by `Currency.ExchangeRate`) and
**relationships** (platform-config parent / tenant-scoped `OrderService`/`OrderPackage` child = the same
shape as Currency-on-Order). The **reverse** (re-tenant) is **constrained**, NOT a clean index swap: it
requires a `TenantId` backfill that preserves Order↔Service/Package tenant agreement, and `OnDelete.Restrict`
+ the `PackageService` M2M block a naive re-slice. Record this in the migration notes so a future
multi-tenant move is not assumed cheap.

## Acceptance criteria
- [ ] **AC1** — `Service`, `ServiceCategory`, `Package`, `Extra`, `ServiceCity` no longer implement
  `ITenantEntity`; their EF entity configs drop the `TenantId`-bearing indexes and (where the panel says
  so) replace with the platform-wide equivalent (`(Slug)` unique for ServiceCategory/Extra; an explicit
  decision recorded for Service/Package/ServiceCity uniqueness).
- [ ] **AC2** — The `[AllowAnonymous]` overview reads (`ServiceController.GetOverview`,
  `PackageController.GetOverview`, `ExtraController.GetOverview`, `ServiceCityController.GetServiceCities`
  on both customer hosts) return the full active catalog identically to the authenticated path — no
  null-tenant collapse, no dependence on a JWT.
- [ ] **AC3** — No `TenantId == null` footgun: the dimension is gone, so no shared row can leak (verify
  per entity; structural test asserts none of the five is `ITenantEntity`).
- [ ] **AC4** — The anonymous **`Order/Quote` pricing read** (`OrderPricingCalculator`) is covered: after
  the flip, anonymous quote pricing resolves the same catalog rows as authenticated (no half-fix where
  listing is correct but pricing is null-sliced). Test the anonymous-Quote path.
- [ ] **AC5** — `Referral/Validate` is **explicitly out of scope** (accepted-as-is per the panel: fails
  shut, rate-limited, no zero-row page) — do NOT re-tenant or touch it here.
- [ ] **AC6** — `ef-migration` flagged (drops `TenantId` columns + reindexes the five). Folds into the
  owner's regenerated Initial (or an incremental if the owner restores the chain). Claude does NOT run `dotnet ef`.
- [ ] **AC7** — Tests written test-first (red→green): the host-boot integration test (T-0100/T-AUTHZ-0
  harness) asserts each anonymous catalog read returns the full catalog with two tenants' data seeded
  (no null-slice), plus the structural anti-regression test (none of the five is `ITenantEntity`).

## Out of scope
- `MembershipPlan` (that is **T-0113** — same doctrine, separate ticket; this batch may merge after it).
- `Referral/Validate` (accepted-as-is, see AC5).
- Building inbound host/subdomain tenant-resolution middleware (only needed if/when the product goes
  per-tenant catalogs — a future feature with its own ADR per the doctrine).
- Admin catalog CRUD surfaces (separate Wave-2 tickets).

## Implementation notes
- **Governing:** ADR-0001 Addendum A1 (D-A1.1/D-A1.4) + `agents/knowledge/platform-expandability.md`.
- **Serialization:** check at spawn time that no other ticket is concurrently editing these five entity
  configs or `CleansiaDbContext` global-filter registration; serialize if so. Coordinate with T-0123
  (which uses the "BSP-9" label for a different fix) so the two don't collide.
- **Pattern (per entity):** mirror the T-0113 contract — drop `: ITenantEntity`, fix the entity config
  index, fix any XML doc that says "unique per tenant". No `CleansiaDbContext` change (the filter loop
  auto-excludes once not `ITenantEntity`); no handler/repo change (they already query without tenant logic).
- **TEST-FIRST**, security gate mandatory (`security_touching: true`, S3/S8). Pairs with T-0126 (host harness).

## Status log
- 2026-06-02 — draft (created by orchestrator per owner approval, carrying the architect-panel verified
  index reality + forward-safe/reverse-constrained note from the platform-expandability deliberation).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
