---
id: T-0322
title: Author prod Bicep — weu.prod.bicepparam — NOT DEPLOYED
status: done
size: M
owner: backend
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0315, T-0316]
blocks: []
stories: []
adrs: [0015, 0017]
layers: [infra, db]
security_touching: false
manual_steps: []
sprint: 13
---

## Context

ADR-0015 D-"Bicep for BOTH dev and prod, parameterized; DEPLOY DEV ONLY; author prod Bicep, do not
deploy it." With the module set (T-0315) + dev param (T-0316) done, this authors the **prod** parameter
file so prod is one `az deployment group create` away when the owner is ready — but **it is NOT
deployed** this wave. The prod hardening seam (VNet/private-endpoint + Postgres-MI) is left **togglable**
per Q-INFRA-03 (a module flag, default off for dev posture, flippable before prod go-live).

## Acceptance criteria

- [x] **AC1 — `weu.prod.bicepparam` with prod values.** `region='weu'`, `env='prod'`; the prod host
  names (`-weu-prod`); prod SKUs — S1 plan, GP `D2s_v3` Postgres, Standard SWAs, ZRS storage. Evidence:
  `deploy/bicep/weu.prod.bicepparam` on disk (83 lines).
- [x] **AC2 — Prod-only hardening seam left togglable.** The VNet/private-endpoint + Postgres-MI seam is
  a togglable module flag (Q-INFRA-03 — default off for the dev posture; flippable before prod go-live),
  not hard-wired on or off.
- [x] **AC3 — Authored, reviewed, NOT deployed.** The prod param is reviewed but **not applied**; the
  rewritten `deploy-dev.yml` does **not** reference it (ADR-0015 #1 — prod param not referenced by the
  dev deploy). No prod deployment runs this wave.

## Out of scope

- **Deploying** prod — explicitly NOT this wave (authored only).
- A prod workflow rewrite — not this wave (the dev workflow is T-0319; prod deploy is a future owner
  decision once Q-INFRA-01/03 are answered).
- Populating prod secret values / the prod Environment — owner, future (T-0317 creates the `prod-weu`
  Environment scaffolding but it stays protected + unpopulated until prod is on the table).
- Answering Q-INFRA-01 (custom domain) / Q-INFRA-03 (VNet/MI hardening) — owner, pre-prod.

## Implementation notes

Mirror T-0316's structure against the same T-0315 module contract, with prod SKUs and the `-weu-prod`
names. Leave the prod-hardening seam as a flag, not a decision — Q-INFRA-03 is the owner's pre-prod call.
Reviewer ADR-0015 #1 (prod param not referenced by deploy-dev) is the key check.

**Routing:** `infra`/`db` authored the prod param against T-0315/T-0316 → `reviewer`.

## Status log

- 2026-06-23 — draft → ready (created by pm). DoR met: AC observable; sized `M`; `depends_on: [T-0315,
  T-0316]`; `layers: [infra, db]`; `security_touching: false` (no secret value — names + SKUs only);
  `manual_steps: []`. ADR-0015/0017.
- 2026-06-23 — ready → in_progress → in_review → **done** (authored + reviewed, **NOT deployed**; commit
  `38a10375`, pushed). **All three AC satisfied.** `weu.prod.bicepparam` (83 lines): `region='weu'`/
  `env='prod'`; S1 / GP `D2s_v3` Postgres / Standard SWAs / ZRS storage; the `-weu-prod` names; the
  prod-hardening (VNet/private-endpoint + Postgres-MI) seam left as a togglable flag per Q-INFRA-03.
  **PASS.** Reviewer confirmed ADR-0015 #1 — the prod param is **not** referenced by `deploy-dev.yml`;
  the prod env is authored, not deployed. **Recorded: AUTHORED-NOT-DEPLOYED** — prod is one
  owner-approved `az deployment group create` away once Q-INFRA-01/03 are answered, but no prod
  deployment ran this wave.

## Review

## Review — reviewer (2026-06-23)

- ADR-0015 #1 (Bicep in deploy/bicep, one reusable appService module, **prod param NOT referenced by
  deploy-dev**): PASS — `weu.prod.bicepparam` exists and is not referenced by the dev workflow.
- Gate 2 AC: PASS — prod SKUs (S1 / D2s_v3 / Standard / ZRS) + `-weu-prod` names present; the
  VNet/private-endpoint + Postgres-MI hardening is a togglable flag (Q-INFRA-03), not hard-wired.
- Deploy state: **authored, NOT deployed** — confirmed no prod `az deployment` runs in this wave.

Verdict: APPROVED — authored, not deployed (as scoped).
