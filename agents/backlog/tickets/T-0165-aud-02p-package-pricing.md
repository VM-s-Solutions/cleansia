---
id: T-0165
title: "AUD-02p: Per-included-service package pricing — PackageService.PriceWeight + even-weight backfill + bundled-gross derivation + admin weight UX"
status: draft
size: L
owner: —
created: 2026-06-06
updated: 2026-06-06
depends_on: []
blocks: [T-0162]
stories: []
adrs: [0009]
layers: [backend, db, frontend]
security_touching: false
manual_steps: [ef-migration, nswag-regen]
sprint: 2
source: ADR-0009 D5 — NEW per-included-service package-pricing epic (owner override of the panel's whole-package-only v1)
open_questions: [Q-REFUND-03]
---

## Context

Wave-2 BUILD: the per-included-service package-pricing model ADR-0009 D5 designed — the owner's deliberate
long-term schema change so a **single service bundled inside a package is independently refundable**. Today
this is impossible: `Package.Price` is a single bundled decimal (`Package.cs:18`) and the included-services
join `PackageService` is a bare `BaseEntity` with only `PackageId` + `ServiceId` (`PackageService.cs:6-21`,
verified 2026-06-06) — **a bundled service has no gross** for the share-of-`TotalPrice` allocator (ADR-0009
fact 8).

ADR-0009 D5 chose a **relative weight** on the join (not an absolute per-included price column): a service's
gross within a package line is `round(ps.PriceWeight / Σ(PriceWeight) × packageLineGross, 2)`, the last
included service absorbs the residual. The weight keeps `Package.Price` the single source of truth (an
absolute column could disagree with the bundle price — D5 Alternatives B/C, CH-6/CH-7).

**This epic BLOCKS AUD-01c (T-0162)** — the partial-refund command cannot refund a bundled service until the
gross basis exists.

> **SIZE = L — must be SPLIT before it goes `ready`** (PM constraint). Captured as one L to track the
> `blocks: T-0162` edge + the two manual steps; the PM splits it (db: column + even-weight backfill;
> backend: bundled-gross derivation feeding the D2 allocator; admin frontend: package-form weight UX),
> contract-locked first, before promoting any child to `ready`.

## Acceptance criteria
- [ ] **AC1 — `PackageService.PriceWeight` column.** Given the bare join, When this lands, Then
  `PackageService` gains a non-null `decimal PriceWeight` (ADR-0009 D5). Evidence: entity field + EF config.
- [ ] **AC2 — Even-weight backfill of legacy rows.** Given existing `PackageService` rows have no weight,
  When the migration runs, Then every existing included service gets a default weight that produces an
  **even split** (e.g. `1` each). Evidence: the data-migration step; a test that a 3-service legacy package
  splits its `Package.Price` into thirds (penny-perfect, last absorbs residual). **Whether even-split is
  right for any specific live bundle is Q-REFUND-03 (non-blocking — owner sets per-bundle weights in the
  admin UI post-migration); the migration ships even weights only.**
- [ ] **AC3 — Bundled-gross derivation.** Given ADR-0009 D5, When a bundled service's gross is needed, Then
  `includedServiceGross = round(ps.PriceWeight / Σ(PriceWeight over the package's included services) ×
  packageLineGross, 2)` with the last included service absorbing the residual, and this feeds the D2
  allocator (AUD-01c) **unchanged** — only the gross derivation is new, never the allocation formula.
  Evidence: TC-PKG-WEIGHT — a bundled service's gross = weight-share × `Package.Price`, penny-perfect.
- [ ] **AC4 — `Package.Price` stays the single source of truth.** Given the weight is dimensionless, When
  weights change, Then the bundle price is unchanged and the derived grosses always re-normalise to the
  price actually charged (no second source of truth, no rounding drift). Evidence: a test — changing a
  weight redistributes shares but Σ(included grosses) == packageLineGross.
- [ ] **AC5 — Admin package-form weight UX.** Given the admin package form, When an admin edits a package,
  Then they can set each included service's relative weight via `<cleansia-*>`/PrimeNG controls (no raw
  form controls), logic in a facade, strings via `TranslatePipe` in all 5 locales. Evidence: facade +
  component + i18n in all 5 files.
- [ ] **AC6 — Manual steps flagged.** Given the column + backfill and the package DTO change, When this
  closes, Then `manual_step: ef-migration` (owner — `PriceWeight` column + even-weight backfill) and
  `manual_step: nswag-regen` (admin package DTO gains `PriceWeight`) are flagged, and the admin-UX child is
  held until the owner confirms the regen.

## Out of scope
- The partial-refund command / allocator / `RefundPolicy` — AUD-01c (T-0162, the consumer this blocks).
- Inventing per-bundle business weighting in the migration — Q-REFUND-03 leaves that to the owner's admin-UI
  edits; the migration is even-weight only.
- Any change to standalone `Service.BasePrice`/`PerRoomPrice` or the `OrderPricingCalculator` quote path —
  D5 uses the existing catalog grosses only as ratio weights (D5.1), not a new pricing path.

## Implementation notes
- **Governing ADR:** ADR-0009 D5 + D5.1 (the weight model, the bundled-gross + the package-line gross basis,
  the even-weight backfill). The weight splits `Package.Price`, never the standalone `Service.BasePrice`
  (fact 8 / CH-7).
- **Open question gating legacy weighting:** **Q-REFUND-03** (non-blocking). The even-split default ships;
  the owner sets per-bundle weights in the admin UI after this lands. Do **not** hold this ticket on
  Q-REFUND-03 — it gates only the *business* correctness of specific legacy bundles, which is reversible.
- **Routing:** architect-confirmed contract (the column + the derivation surface) → db (column + backfill
  migration) → backend (gross derivation) → frontend (admin UX), reviewer per developer. Contract locks
  before the AUD-01c bundled-line path can go green.
- **Manual steps:** `ef-migration` + `nswag-regen` (both owner-only). Hold the admin-UX child until regen.
- **TEST-FIRST:** TC-PKG-WEIGHT (weight-share gross, even-weight backfill, residual absorption, re-normalise
  to `Package.Price`) red-first.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0009 D5; NEW package-pricing epic; blocks T-0162;
  L — must be split before ready; Q-REFUND-03 recorded as the non-blocking open question on legacy weighting;
  Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
