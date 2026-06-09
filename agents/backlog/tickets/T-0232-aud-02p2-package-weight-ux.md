---
id: T-0232
title: "AUD-02p2 (split of T-0165): Admin package-form per-included-service weight UX (facade, i18n ×5)"
status: draft
size: S
owner: —
created: 2026-06-07
updated: 2026-06-07
depends_on: [T-0231]
blocks: []
stories: []
adrs: [0009]
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 2
source: split of T-0165 (AUD-02p) — admin weight UX half; ADR-0009 D5
open_questions: [Q-REFUND-03]
---

## Context

Frontend half of the L-split of **T-0165 (AUD-02p)**. The admin package form gains a per-included-service
relative-weight editor so the owner can correct any legacy bundle whose included services are NOT of equal
value (resolving Q-REFUND-03 per-bundle, post-migration). **Held until the owner regenerates the admin NSwag
client** (the package DTO gains `PriceWeight` in T-0231).

## Acceptance criteria
- [ ] **AC1 — Admin package-form weight UX** (ADR-0009 D5). On editing a package, the admin sets each
  included service's relative weight via `<cleansia-*>`/PrimeNG controls (no raw form controls), logic in a
  facade, strings via `TranslatePipe` in all 5 locales (en/cs/sk/uk/ru). Evidence: facade + component +
  i18n in all 5 files.
- [ ] **AC2 — Weights re-normalise visibly.** The form shows the derived per-service gross (weight-share ×
  `Package.Price`) so the admin sees the effect before saving; Σ(shown grosses) == the package price.
  Evidence: a facade/component test.
- [ ] **AC3 — Three explicit data states + OnPush.** Evidence: component spec.

## Out of scope
- The `PriceWeight` schema/derivation (T-0231). The partial-refund command/UX (T-0167/T-0168). Inventing
  business weights in code — the owner sets them here per Q-REFUND-03.

## Implementation notes
- **Hard dependency on T-0231** (`done`) + the owner `nswag-regen` (admin package DTO gains `PriceWeight`).
  Do not start the component until the regen is confirmed.
- **Routing:** frontend (admin-app) + reviewer in parallel. `security_touching: false`.
- **TEST-FIRST:** the facade spec first, then the component.

## Status log
- 2026-06-07 — draft (created by pm as the frontend half of the T-0165 L-split; depends_on T-0231 +
  owner nswag-regen; resolves Q-REFUND-03 per-bundle post-migration; Wave-2 build).
- 2026-06-09 — backend prerequisite **T-0231b** landed (the original T-0231 exposed neither `PriceWeight` on
  the package DTO nor a write path): `PackageServiceDto.PriceWeight` + `UpdatePackageCommand.serviceWeights`
  map (default 1m even-split, validated > 0), owner regenerated the admin client.
- 2026-06-09 — frontend (ACCEPTED): per-included-service weight editor on the package form
  (`<cleansia-text-input dataType=number>`, no raw controls), logic in `package-form.facade` (`weightRows`,
  `derivedGrosses` computed, `buildServiceWeights`), pre-filled from `priceWeight`. AC2 — live derived gross
  per service = round(weight/Σweights × Package.Price, 2), last row absorbs the residual so Σ == price;
  identical algorithm to the backend `PackagePricing.DeriveIncludedServiceGrosses` (frontend/backend parity
  confirmed by the reviewer). Save sends `serviceWeights`. i18n ×5 incl. `errors.package.invalid_weight`. 3
  states + OnPush, 15 specs. Reviewer **APPROVE** (re-derived 3/1→75/25, 1/1/1→33.33/33.33/33.34). Verified
  independently: `nx test package-management` 15/15 green. The dev also added the lib's missing Jest/eslint
  config (it had none). Resolves Q-REFUND-03 (owner now sets per-bundle weights via this UI).

## Review
- 2026-06-09 reviewer: **APPROVE** — AC1–AC3 met, derived-gross math re-derived and matches backend parity,
  15/15 tests, zero new lint errors.
