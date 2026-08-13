---
id: T-0319
title: Rewrite deploy-dev.yml — Bicep-gated, OIDC + EF-bundle preserved, parallelized, five hosts, dev-weu
status: done
size: M
owner: backend
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0315, T-0316]
blocks: [T-0320]
stories: []
adrs: [0015, 0017]
layers: [infra, backend]
security_touching: false
manual_steps: []
sprint: 13
---

## Context

ADR-0015/0017. With the Bicep modules (T-0315) + dev param (T-0316) authored, the dev deploy workflow is
rewired to be **Bicep-gated** while preserving OIDC + the EF-migration bundle, parallelizing the deploys
on B2, scoping to the `dev-weu` GitHub Environment, adding the one-element `matrix.region:[weu]`, and
adding the **customer-mobile** host the old March-2026 YAML omitted. The workflow is **authored now** but
**runs after the owner's first provision** (T-0318) — it is a declarative artifact, not a run.

## Acceptance criteria

- [x] **AC1 — One-element `strategy.matrix.region: [weu]`.** Every region-scoped job carries the
  one-element matrix (ADR-0017 — no-op today, additive tomorrow). Evidence:
  `.github/workflows/deploy-dev.yml` provision/migrate/deploy jobs all carry `matrix.region:[weu]`.
- [x] **AC2 — Per-leg Bicep provision gate.** A `provision` job runs `az deployment group what-if` on
  **PR** (a reviewable, non-mutating preview) and `az deployment group create` on **push to master**,
  scoped to the `dev-weu` GitHub Environment. Evidence: the `provision` job (what-if/create split).
- [x] **AC3 — EF-migration bundle preserved, before deploys.** The `migrate-database` job (EF bundle)
  runs after `provision` and **before** any deploy, applying **already-committed** migrations only
  (never `migrations add`). Evidence: `migrate-database` `needs: [build-dotnet, provision]`; deploys
  `needs: [..., migrate-database]`.
- [x] **AC4 — Parallelized deploys with the migrate edge intact.** Each API/SSR/Functions deploy
  `needs: [build-dotnet, provision, migrate-database]` (not each other) — they fan out in parallel on
  B2, but the migrate-before-deploy ordering is held by the `needs` edge, not a serial chain.
- [x] **AC5 — OIDC preserved + all five hosts incl. customer-mobile.** OIDC auth to Azure is kept; the
  deploy fans out to all five API hosts incl. a new `deploy-customer-mobile-api` job + the SSR +
  Functions (container from ACR) + 2 SWAs. Evidence: the `deploy-customer-mobile-api` job +
  `customer-mobile-api` publish/artifact steps.

## Out of scope

- The Bicep modules/params — **T-0315/T-0316**.
- **Running** the workflow / the first provision — **OWNER** (T-0318). This authors the YAML; the owner
  triggers the first run.
- The prod workflow — out of this wave (prod Bicep is authored not deployed at T-0322; no prod workflow
  rewrite this pass).
- The smoke definition — **T-0320**.

## Implementation notes

Read ADR-0015 D5 (the B2 parallelize-with-migrate-edge model) + the old `deploy-dev.yml`/`deploy-pro.yml`
as the topology reference. Keep the OIDC login + the EF-bundle job verbatim in shape; the new pieces are
the `provision` gate, the `matrix.region`, the `dev-weu` Environment scoping, and the customer-mobile
host. Reviewer ADR-0015 #8 (OIDC + migrate-before-deploy, CI only *applies* a committed migration), #10
(what-if on PR), #11 (parallelized deploys with the migrate edge) + ADR-0017 R4 apply.

**Routing:** `infra`/`backend` authored the workflow against T-0315/T-0316 → `reviewer`.

> **Reporting-vs-work note (this ticket):** the in-workflow dev agent's final **StructuredOutput**
> report call hit the retry cap and errored — a **reporting** failure, not a work failure. The rewritten
> `deploy-dev.yml` had already landed on disk. Per `quality-gates.md` §"A final-report (StructuredOutput)
> failure ≠ a work failure", the orchestrator **gated the on-disk result by hand**: read the workflow,
> confirmed all five hosts + OIDC + the EF-migration bundle + the provision (what-if/create) gate +
> `matrix.region:[weu]` + the `dev-weu` Environment scoping, secret-scanned it clean. **Verified-done by
> the hand gate even though the in-workflow reviewer instance didn't run its final report.**

## Status log

- 2026-06-23 — draft → ready (created by pm). DoR met: AC observable; sized `M`; `depends_on: [T-0315,
  T-0316]`; `layers: [infra, backend]`; `security_touching: false` (no secret value — OIDC + GH-secret
  references only); `manual_steps: []` (authoring; the run is the owner's at T-0318). ADR-0015/0017.
- 2026-06-23 — ready → in_progress → in_review → **done** (authored + **hand-gated by the orchestrator**;
  commit `38a10375`, pushed). **All five AC satisfied.** `deploy-dev.yml` rewritten: `matrix.region:[weu]`
  on every region-scoped job; a `provision` job (what-if on PR / create on push) scoped to `dev-weu`;
  the `migrate-database` EF-bundle `needs:[build-dotnet, provision]` and runs before deploys; the five
  API deploys + SSR + Functions(ACR) + 2 SWAs each `needs:[build-dotnet, provision, migrate-database]`
  (parallel, migrate edge intact); OIDC preserved; the `deploy-customer-mobile-api` host added.
  **VERIFICATION NOTE:** the dev agent's final StructuredOutput report call **failed (retry cap)** — a
  reporting failure, NOT a work failure (the YAML landed on disk). The orchestrator **gated it BY HAND**:
  confirmed all five hosts + OIDC + the EF-migration bundle + the provision (what-if/create) gate +
  `matrix.region:[weu]` + `dev-weu` scoping; secret-scan clean. **Verified-done** despite the in-workflow
  reviewer not emitting its final report. Process lesson reinforced in `quality-gates.md`.

## Review

## Review — orchestrator hand-gate (2026-06-23, in lieu of the in-workflow reviewer whose StructuredOutput failed)

- ADR-0015 #8 (OIDC + migrate-before-deploy; CI only applies a committed migration): PASS — OIDC login
  preserved; `migrate-database` runs after `provision`, before deploys, EF-**bundle** apply only.
- ADR-0015 #10 (what-if on PR): PASS — `provision` does `what-if` on PR, `create` on push.
- ADR-0015 #11 (parallelized deploys with the migrate edge): PASS — each deploy `needs:[provision,
  migrate-database]`, not each other.
- ADR-0017 R4 (one-element `matrix.region:[weu]`): PASS — on every region-scoped job.
- Five hosts incl. customer-mobile: PASS — `deploy-customer-mobile-api` job + artifact present.
- Secret scan: PASS — OIDC + GH-Environment secret references only; no literal.

Verdict: APPROVED (hand-gate). The in-workflow reviewer's final StructuredOutput call failed (retry cap)
— a reporting failure; the work was on disk and is gated here by hand.
