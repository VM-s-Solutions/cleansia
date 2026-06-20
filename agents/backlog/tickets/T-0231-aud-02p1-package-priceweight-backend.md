---
id: T-0231
title: "AUD-02p1 (split of T-0165): PackageService.PriceWeight column + even-weight backfill migration + bundled-gross derivation (db+backend)"
status: done
size: M
owner: —
created: 2026-06-07
updated: 2026-06-15
depends_on: []
blocks: [T-0167, T-0232]
stories: []
adrs: [0009]
layers: [db, backend]
security_touching: false
manual_steps: [ef-migration, nswag-regen]
sprint: 2
source: split of T-0165 (AUD-02p) — schema + derivation half; ADR-0009 D5/D5.1
open_questions: [Q-REFUND-03]
---

## Context

Schema + derivation half of the L-split of **T-0165 (AUD-02p)**. Lands the per-included-service package
pricing the partial-refund allocator needs: the `PackageService.PriceWeight` column, the even-weight
backfill migration, and the bundled-gross derivation that feeds the D2 allocator (T-0167) unchanged.
`Package.Price` stays the single source of truth (the weight is dimensionless — ADR-0009 D5 chose a relative
weight over an absolute per-included price column, CH-6/CH-7). **Blocks T-0167** (a bundled service has no
gross until this exists, ADR-0009 fact 8) and **T-0232** (the admin weight UX). No admin UX here.

## Acceptance criteria
- [ ] **AC1 — `PackageService.PriceWeight` column** (ADR-0009 D5). `PackageService` gains a non-null
  `decimal PriceWeight`. Evidence: entity field + EF config.
- [ ] **AC2 — Even-weight backfill of legacy rows.** The migration backfills every existing included service
  with a default weight producing an even split (e.g. `1` each). Evidence: the data-migration step; a test
  that a 3-service legacy package splits `Package.Price` into thirds (penny-perfect, last absorbs residual).
  **Even-split-vs-business-weight for specific live bundles is Q-REFUND-03 (non-blocking; owner sets weights
  via T-0232 post-migration); this migration ships even weights only.**
- [ ] **AC3 — Bundled-gross derivation** (ADR-0009 D5). `includedServiceGross = round(ps.PriceWeight /
  Σ(PriceWeight over the package's included services) × packageLineGross, 2)`, last included service absorbs
  the residual; feeds the D2 allocator unchanged. Evidence: TC-PKG-WEIGHT.
- [ ] **AC4 — `Package.Price` stays single source of truth.** Changing weights redistributes shares but
  Σ(included grosses) == packageLineGross; no second source of truth, no rounding drift. Evidence: a test.
- [ ] **AC5 — Manual step flagged.** `manual_step: ef-migration` (owner — `PriceWeight` column + even-weight
  backfill) flagged; T-0167's bundled-line path held until the migration is confirmed applied.

## Out of scope
- The admin package-form weight UX (T-0232). The partial-refund command/allocator (T-0167). Inventing
  per-bundle business weighting in the migration — Q-REFUND-03 leaves that to the owner's admin-UI edits.
- Any change to standalone `Service.BasePrice`/`PerRoomPrice` or `OrderPricingCalculator` — D5 uses existing
  catalog grosses only as ratio weights (D5.1), not a new pricing path.

## Implementation notes
- **Governing ADR:** ADR-0009 D5 + D5.1. The weight splits `Package.Price`, never `Service.BasePrice`
  (fact 8 / CH-7).
- **Routing:** architect-confirmed contract (column + derivation surface) → db (column + backfill migration)
  → backend (gross derivation), reviewer per developer. Contract-lock before T-0167's bundled-line path.
- **Manual step:** `ef-migration` (owner-only). Folds with the T-0160 refund migration if sequenced together.
- **TEST-FIRST:** TC-PKG-WEIGHT (weight-share gross, even-weight backfill, residual absorption, re-normalise
  to `Package.Price`) red-first.

## Status log
- 2026-06-07 — draft (created by pm as the db+backend half of the T-0165 L-split; blocks T-0167, T-0232;
  Q-REFUND-03 recorded as the non-blocking open question on legacy weighting; Wave-2 build).
- 2026-06-07 — db+backend done (test-first). `PackageService.PriceWeight` (non-null decimal, default `1`)
  added with `PackageServiceEntityConfiguration` (precision `(18,2)`, `HasDefaultValue(1)`); the pure
  weight-split derivation `PackagePricing.DeriveIncludedServiceGrosses` (last service absorbs the
  sub-cent residual, Σ == package line gross); TC-PKG-WEIGHT landed red→green (12 cases). Full solution
  builds; Cleansia.Tests 669 passed/0 failed. NOT committed. Even weights only (Q-REFUND-03 deferred to
  the owner via T-0232). **MANUAL_STEP — ef-migration (owner, see `## Migration` below).**

## Migration (MANUAL_STEP: ef-migration — owner runs, NOT the agent)

Owner generates and applies the migration after this change is merged:

```
dotnet ef migrations add AddPackageServicePriceWeight \
  --project src/Cleansia.Infra.Database --startup-project src/Cleansia.Web.Admin
```

Expected schema delta (single, additive, safe):
- **ADD COLUMN** `PackageServices."PriceWeight" numeric(18,2) NOT NULL DEFAULT 1`.
- The `NOT NULL DEFAULT 1` **is** the even-weight backfill: every existing included-service row gets
  weight `1`, so a legacy N-service bundle splits `Package.Price` into N equal weight-shares (a 3-service
  package → thirds, penny-perfect, last service absorbs the residual). No separate data-migration step or
  hand-written SQL is required — the column default backfills in the same `ADD COLUMN`.

Safety notes:
- Non-nullable add **with a constant default** → safe (Postgres backfills in place; no table rewrite of
  application data semantics, no NULL window). No rename, no drop, no column still referenced by code is
  removed.
- Per-bundle business weighting of specific live bundles is the owner's later call (Q-REFUND-03) via the
  T-0232 admin weight UX; this migration ships even weights only.
- Small table — no `CREATE INDEX CONCURRENTLY` needed (no new index in this delta).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
