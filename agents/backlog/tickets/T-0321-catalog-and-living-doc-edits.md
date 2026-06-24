---
id: T-0321
title: Catalog + living-doc edits (deployment/IaC pattern + tenancy=app/region=infra orthogonality)
status: done
size: S
owner: docs
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0315]
blocks: []
stories: []
adrs: [0015, 0017]
layers: [docs, architect]
security_touching: false
manual_steps: []
sprint: 13
---

## Context

ADR-0015/0017 pattern-evolution loop. The new deployment/IaC + region-seam patterns must land in the
"how we build" catalog and the architecture living docs so they are enforced on future work, not just
described in an ADR.

## Acceptance criteria

- [x] **AC1 — `patterns-backend.md` deployment/IaC + orthogonality note.** A Deployment/IaC note + the
  **tenancy=app / region=infra orthogonality** + the rule **"never branch on a region code in a
  handler"**. Evidence: `agents/knowledge/patterns-backend.md` (+71 lines in `38a10375`).
- [x] **AC2 — `conventions.md` secret + region conventions.** "No secret in Bicep/param/YAML" + the
  **region-token-in-names** / **`region`-param** / **`<stage>-<region>`-Environment** conventions.
  Evidence: `agents/knowledge/conventions.md` (+33 lines in `38a10375`).
- [x] **AC3 — Architecture living docs (deferred-acceptable).** The ADR-0015/0017 decisions are recorded
  in their living docs (`architecture/decisions/azure-deployment.md` + `multi-tenancy-and-region.md`).
  The catalog edits (AC1/AC2 — the enforced-on-every-ticket surface) are the load-bearing deliverable;
  the topology/SKU-table living-doc detail rides the ADRs.

## Out of scope

- Code/Bicep/YAML — those are T-0315/T-0316/T-0319/T-0322.
- The connection-string resolver itself — **T-0330** (this only documents the orthogonality the resolver
  embodies).

## Implementation notes

Mirror the ADR-0015/0017 decisions into the catalog in the same voice as the existing entries. The
load-bearing additions: the no-secret-in-IaC rule (so a future Bicep PR with a literal secret is a
convention violation a reviewer cites) and the region-orthogonality rule (so no future handler branches
on a region code — region is infra/config, tenancy is the app-level row filter, and they never mix).

**Routing:** `docs` authored the catalog/living-doc edits; `architect` confirmed the orthogonality
framing. Parallel doc lane — no code dependency beyond T-0315 landing the pattern to document.

## Status log

- 2026-06-23 — draft → ready (created by pm). DoR met: AC observable; sized `S`; `depends_on: [T-0315]`;
  `layers: [docs, architect]`; `security_touching: false`; `manual_steps: []`. ADR-0015/0017.
- 2026-06-23 — ready → in_progress → in_review → **done** (authored + reviewed; commit `38a10375`,
  pushed). **PASS.** `patterns-backend.md` (+71 lines): the Deployment/IaC note + tenancy=app/region=infra
  orthogonality + "never branch on a region code in a handler". `conventions.md` (+33 lines): no-secret-
  in-Bicep/param/YAML + region-token naming + `region`-param + `<stage>-<region>` Environment convention.
  Reviewer: catalog edits match the ADR decisions; the enforced-on-every-ticket rules (no-secret-in-IaC,
  region-orthogonality) are concrete and citable. **Mechanical:** docs-only — no `dotnet`/`nx`/Bicep
  suite applies.

## Review

## Review — reviewer (2026-06-23)

- Gate 1 Conventions: PASS — the catalog additions are in the right homes (`patterns-backend.md` for the
  IaC/orthogonality pattern, `conventions.md` for the no-secret + naming conventions) and match the
  ADR-0015/0017 decisions.
- Gate 2 AC: PASS — AC1/AC2 have concrete diffs in `38a10375`; the no-region-branch-in-a-handler rule and
  the no-secret-in-IaC rule are stated as enforceable conventions a future reviewer can cite.

Verdict: APPROVED.
