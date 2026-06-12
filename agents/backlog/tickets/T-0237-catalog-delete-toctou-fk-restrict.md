---
id: T-0237
title: Catalog delete TOCTOU — FK Restrict + 23505/restrict-violation → in_use mapping (replace check-then-act)
status: draft
size: M
owner: —
created: 2026-06-12
updated: 2026-06-12
depends_on: [T-0191]
blocks: []
stories: []
adrs: [0007]
layers: [backend, db]
security_touching: true
manual_steps: [ef-migration]
sprint: 4
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

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
