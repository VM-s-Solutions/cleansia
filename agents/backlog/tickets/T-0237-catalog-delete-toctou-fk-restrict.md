---
id: T-0237
title: Catalog delete TOCTOU — FK Restrict + 23505/restrict-violation → in_use mapping (replace check-then-act)
status: done
size: M
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: [T-0191]
blocks: []
stories: []
adrs: [0007]
layers: [backend, db]
security_touching: true
manual_steps: [ef-migration]
sprint: 6
source: T-0191a security re-gate note 1 (S7a TOCTOU residue) + the RecurringBookingTemplate JSON-id case (Wave-3 close)
---

## Context
T-0191a's in-use guard (merged `5d631f8c`) is **check-then-act**: `IsInUseAsync` → `Remove`. The
security re-gate passed it (the cart-orphan FAIL was genuinely closed) but filed note 1: a
cart/order line inserted **between the check and the commit** is still cascade-orphaned
(`OrderServices` FK is ON DELETE CASCADE). The durable S7a fix the gate recommended: change the
catalog-reference FKs to **Restrict** and map the resulting constraint violation to the existing
`service.in_use` / `package.in_use` error contract — making the database the arbiter, the same
pattern as the promo-code conditional UPDATE.

**Folded in (same gate, second note):** `RecurringBookingTemplate` stores service/package ids inside
a **JSON column** — invisible to both `IsInUseAsync` and any FK. A deleted catalog item leaves
dangling JSON references that materialize into broken recurring bookings. The fix must cover this
non-FK reference class too (include template JSON ids in the in-use check, since no FK can guard
them — this part stays check-then-act but the window is acceptable for templates; document why).

## Acceptance criteria
- [ ] **AC1** — Catalog-referencing FKs (OrderServices/OrderPackages/PackageServices/
  EmployeePayConfigs/Cart*Items → Service/Package) are `Restrict` (not Cascade) for the delete paths
  the admin guard protects; the EF model + a single owner **ef-migration** carry the change.
- [ ] **AC2** — Given a reference inserted after `IsInUseAsync` passed but before commit, When the
  delete commits, Then the DB rejects it and the handler maps the violation to the existing
  `service.in_use`/`package.in_use` BusinessResult — no orphan, no 500. Proven by a real-database
  test (Testcontainers/SQLite per the repo's existing pattern; Postgres for the violation mapping).
- [ ] **AC3** — `IsInUseAsync` (kept as the fast pre-check for honest UX) additionally checks
  `RecurringBookingTemplate` JSON references for both Service and Package ids; a template-referenced
  item reports in-use. Test included.
- [ ] **AC4** — No behavior change for activate/deactivate (T-0191b) or the admin UX beyond the
  unchanged error keys; existing `CatalogInUseCartGuardTests` stay green.

## Out of scope
- Soft-delete policy changes (ADR-0007 stands). Cleaning up already-dangling template references
  (separate data-fix if found in prod).

## Implementation notes
DB layer first (FK behavior is the contract), then the violation-mapping in the delete handlers
(Npgsql FK-restrict violation = SQLSTATE 23503; the gate note's "23505" shorthand refers to the
S7a constraint-mapping pattern — map the actual restrict code). Mind the tenant filter on
RecurringBookingTemplate reads. ef-migration is owner-run — flag and HOLD dependent verification.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; from the T-0191a security notes, both folded)
- 2026-06-14 — **backend+db — implemented (status → review).** Test-first, honest red→green captured below.
  **AC1 (FK Cascade → Restrict, EF model):** catalog-referencing FKs flipped from the convention/explicit
  Cascade to `Restrict` — `OrderService→Service`, `OrderPackage→Package`, `PackageService→Service`
  (new `OrderServiceEntityConfiguration`/`OrderPackageEntityConfiguration`; extended
  `PackageServiceEntityConfiguration`), `CartServiceItem→Service`, `CartPackageItem→Package`
  (existing cart configs). `EmployeePayConfig→Service/Package` was ALREADY Restrict (unchanged).
  Aggregate-owning sides stay Cascade (`Order→OrderService/OrderPackage`, `Package→PackageService`,
  `Cart→Cart*Item`) so deleting an order/package/cart still removes its own child rows. **No snapshot/
  migration file hand-edited** — see MANUAL_STEP below.
  **AC2 (violation mapping):** both delete handlers now FLUSH the `Remove` in-handler
  (`serviceRepository.CommitAsync` in a `try/catch`) per S7b — the pipeline's `CommitAsync` runs AFTER the
  handler returns, so a tracked-delete violation would otherwise surface at the pipeline as a 500. New
  `DbConstraintViolation.IsForeignKeyViolation` (reflective `SqlState`, no Npgsql ref in AppServices, same
  shape as `CreateMembershipSubscription.IsUniqueViolation`) maps the violation to `service.in_use`/
  `package.in_use`. **REAL-DB finding:** an explicit `ON DELETE RESTRICT` raises SQLSTATE **`23001`
  (restrict_violation)**, NOT `23503` — the Postgres integration test caught this (the gate note's "23505"
  was shorthand). The classifier maps BOTH `23001` and `23503`. The pipeline's later commit is then a safe
  no-op (row already Deleted/detached).
  **AC3 (RecurringBookingTemplate JSON ids):** `ServiceRepository`/`PackageRepository.IsInUseAsync` now also
  check the templates' JSON `SelectedServiceIds`/`SelectedPackageIds` via `CatalogReferenceJson`. No FK can
  guard JSON refs, so this stays a check-then-act PRE-check (window documented + acceptable — templates are
  a small, no-UI-yet table). The JSON columns carry a value converter, so the id collections are
  materialized and matched in memory (provider-agnostic; a SQL `LIKE`/`EF.Property<string>` approach threw
  `InvalidCastException` because the CLR type is `List<string>`). Read is `IgnoreQueryFilters()` because the
  catalog row is tenantless platform config — a reference held by ANY tenant's template counts (cross-tenant
  test included).
  **AC4:** `CatalogInUseCartGuardTests` (4) + `CatalogActiveVisibilityTests` + `CatalogLifecycleEndpointPermissionTests`
  stay green; no activate/deactivate or error-key change.
  **TDD red→green evidence:**
  • AC3 — `CatalogInUseTemplateGuardTests` (5, SQLite real DbContext) RED 3/5 (JSON refs + cross-tenant
    reported NOT in-use) → GREEN 5/5 after the IsInUseAsync template check.
  • AC2 — `CatalogDeleteFkRestrictPostgresTests` (3, Testcontainers Postgres, schema via EnsureCreated from
    the live model so it reflects the new FK config ahead of the owner migration). The race is made
    deterministic with a `RaceBlind*Repository` (IsInUseAsync → false, modelling the post-check window) so
    only the FK — not the handler pre-check — can reject. RED twice: (1) classifier only `23503` → the raw
    `23001 DbUpdateException` escaped as a 500 on the service case; (2) `CartPackageItem→Package` still
    Cascade → the package delete SUCCEEDED + orphaned (`Assert.False(IsSuccess)` got True). GREEN 3/3 after
    adding `23001` to the classifier and flipping the cart FK to Restrict.
  • AC2 handler unit — `DeleteServiceHandlerTests`/`DeletePackageHandlerTests` gained a `[Theory]` over
    `23001`/`23503` asserting the mapping (fake `SqlState` inner exception); existing 4 stay green.
  **Verification achieved here:** Cleansia.Tests Catalog+delete-handler subset 53/53 GREEN
  (`--no-build` against project-scoped builds, env-trap workaround); `CatalogDeleteFkRestrictPostgresTests`
  3/3 GREEN on real Postgres. Full-suite note: 8 failures in `Features/Orders` cancellation-fee tests are a
  DIFFERENT in-flight lane (T-0242, `BookingPolicy.cs` modified there) — disjoint from this ticket.
  **MANUAL_STEP: ef-migration (owner).** The FK `onDelete` change is schema. Entity-config is written; the
  owner runs `dotnet ef migrations add CatalogDeleteFkRestrict` (the diff will DROP+ADD the 5 affected FKs
  with `onDelete: Restrict`). Dependent DEPLOYED-schema verification HELD until the migration is applied;
  the model-level behavior is already proven by the EnsureCreated Postgres test. **No nswag-regen** —
  Command/Response shapes and error keys (`service.in_use`/`package.in_use`, already in all 5 locales) are
  unchanged.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6D**). Dep T-0191✓. Durable S7a fix recommended by
  the security re-gate (DB-as-arbiter, same shape as the promo-code conditional UPDATE) → no panel.
  **DB layer FIRST** (FKs `Cascade → Restrict` is the contract) then SQLSTATE-23503 violation-mapping in the
  catalog-delete handlers, plus the `RecurringBookingTemplate` JSON-id in-use check (no FK guards JSON refs).
  **Security gate** (TOCTOU). **ef-migration (owner)** for the FK-behavior change — held at the migration
  boundary; dependent violation-mapping verification HELD until confirmed. Real-database test (Postgres for
  the violation mapping). Disjoint from all other batches → parallel with 6A/6B/6C.
  Plan: `status/sprint-8.md` §3 Batch 6D.
- 2026-06-14 — **review-fix (backend) — reviewer blocker 1 cleared.** Deleted the stray untracked scratch
  file `src/Cleansia.Tests/Features/Catalog/_ZzModelFkProbe.cs` (an FK-dump diagnostic, not assertion-bearing
  — it referenced `ITenantProvider` without `using Cleansia.Core.Domain.Repositories`, CS0246 broke the whole
  `Cleansia.Tests` build). Nothing in it was worth folding into a real test: it only `WriteLine`d delete
  behaviors, and the assertion-bearing FK coverage already lives in `CatalogDeleteFkRestrictPostgresTests`
  (Postgres) + `CatalogInUseTemplateGuardTests`. **Verification:** `dotnet build Cleansia.Tests.csproj` now
  0 errors / 0 warnings; the Catalog + delete-handler subset is **53/53 GREEN directly from the tree** (no
  probe-removal workaround needed) — `--no-build -p:BuildProjectReferences=false`, env-trap workaround. The
  full `Cleansia.Tests` project compiles; `Features/Orders` 182/182 green here too (the earlier T-0242
  failures resolved in that lane — disjoint from this ticket). No source/behavior change; FK configs,
  handlers, error keys untouched.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-14 — CHANGES REQUESTED (one blocker; design is sound)
Verify-not-trust performed: built `Cleansia.Infra.Database` (refs AppServices) clean, 0 warnings;
ran the Catalog + delete-handler unit subset = **53/53 PASS** (matches dev). i18n `service.in_use`/
`package.in_use` confirmed present in all 5 admin locales (no key change). `check-consistency.mjs`
on the changed backend dirs surfaced only pre-existing violations — none in this ticket's files.

**The design is correct and the tests are substantive.** AC1 FK-Restrict configs are right (catalog
side Restrict, owning aggregate side Cascade, navigations match the entities); AC2 flush-in-handler +
`DbConstraintViolation` mirrors the established `IsUniqueViolation` duck-typing pattern, the pipeline
double-commit is a proven no-op on success / skipped on failure; AC3 JSON template check reads the
correct `_selectedServiceIds`/`_selectedPackageIds` backing fields with `IgnoreQueryFilters`; AC4
holds. The 23001-vs-23503 deviation is honest and justified. The integration test's `RaceBlind` seam
genuinely isolates the FK backstop (would go red pre-fix).

**Blocker (must fix before approve):**
1. **Stray build-breaking scratch file `src/Cleansia.Tests/Features/Catalog/_ZzModelFkProbe.cs`**
   (untracked, NOT in the dev's file list, plainly this ticket's FK-probe). It references
   `ITenantProvider` without its using directive → **CS0246 fails the entire `Cleansia.Tests` build**.
   The claimed "53/53 PASS" is only reproducible after removing it. **Delete the probe file** (or, if
   any of it is worth keeping, fold it into a real test with the correct using). The 53/53 above was
   obtained by me with the probe temporarily moved aside, then restored — the tree still contains it.

**Notes for the PM (non-blocking):**
- **Catalog edit needs Architect ratification:** the change adds a NEW lettered rule **B10a** to
  `consistency.md` ("the one way" to do catalog-delete-restrict). It accurately documents this ticket
  and matches B10's style, but per reviewer charter a new canonical rule is an Architect call — please
  have the Architect bless B10a (and decide whether it warrants a mechanical check). Do not treat the
  inline rule add as self-approving.
- **MANUAL_STEP ef-migration** correctly recorded and NOT run; deployed-schema behavior is held until
  the owner migration. Model-level FK behavior is proven now via EnsureCreated-from-model on Postgres.
- 8 unrelated `Features/Orders` failures are a different lane (T-0242) — disjoint, confirmed not this
  ticket's.
