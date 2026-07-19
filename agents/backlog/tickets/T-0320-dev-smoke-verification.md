---
id: T-0320
title: Dev smoke + verification (5 APIs + SSR + 2 SPAs + Functions reachable; queue→Functions pipeline live)
status: in_progress
size: M
owner: pm
created: 2026-06-23
updated: 2026-07-19
depends_on: [T-0318, T-0319]
blocks: []
stories: []
adrs: [0015, 0017]
layers: [infra, backend, qa]
security_touching: false
manual_steps: []
sprint: 13
---

## Context

ADR-0015 §"Phase 2 SMOKE." Once the owner's dev provision (T-0318) brings the env up and the rewritten
`deploy-dev.yml` (T-0319) deploys all the hosts, this ticket verifies the dev environment is healthy
end-to-end — the gate that lets the iOS apps point at the five stable dev hosts (ADR-0015 D6, the Wave-10
enabler). The smoke **definition** is agent-authorable; running it requires the live env, so the ticket
is **blocked** until T-0318 (env up) + T-0319 (workflow) are satisfied.

## Acceptance criteria

- [ ] **AC1 — SMOKE-API.** All five `api-cleansia-*-weu-dev` hosts are healthy; a partner-mobile **and** a
  customer-mobile login issues a token (the two mobile hosts the iOS apps depend on actually work).
- [ ] **AC2 — SMOKE-STORAGE.** A photo upload lands in blob; a payment → `generate-receipt` →
  Functions → a PDF in `generated-receipts` (the queue→Functions pipeline is live end-to-end).
- [ ] **AC3 — SMOKE-WEB.** The SSR renders; the 2 SPAs load and reach their APIs; CORS is correct (the
  SPA origins succeed, the mobile hosts stay closed).
- [ ] **AC4 — SMOKE-MIGRATE.** The committed EF migrations are applied pre-deploy (the `migrate-database`
  job ran before the deploys; the schema is current).

## Out of scope

- Provisioning the env / running the apply — **T-0318** (owner).
- The workflow itself — **T-0319** (agent, done).
- Prod smoke — prod is authored-not-deployed (T-0322); no prod smoke this wave.

## Implementation notes

The smoke set can be authored now (the definition is declarative — health endpoints, a login token
assertion per mobile host, the blob/queue/Functions round-trip, the CORS check, the migrate-before-deploy
assertion). It **runs** only against the live dev env, so it is held until T-0318 + T-0319 land. When the
owner confirms the env is up, dispatch the smoke (`qa` + `backend`/`infra`) and record the results as AC
evidence. Green smoke = the dev env is the stable API the Mac/iOS point at (the wave's outcome).

**Routing:** `infra`/`backend`/`qa` author the smoke definition (can start on approval); the **run** is
held until the owner confirms T-0318. `reviewer` on the smoke definition.

## Status log

- 2026-06-23 — draft → **blocked** (created by pm). DoR met for the definition; the **run** is gated on
  the live env. `depends_on: [T-0318, T-0319]` — **T-0319 ✓ done** (the workflow), **T-0318 = owner
  (blocked)** so this stays blocked; `layers: [infra, backend, qa]`; `security_touching: false`;
  `manual_steps: []` (the smoke itself runs no owner-only step — it verifies the owner's provision).
  **Runs once the dev env is live** (after the owner completes T-0317 → T-0318). Surfaced on the OWNER
  PROVISIONING CHECKLIST as the verification step that closes the wave.
- 2026-07-19 — blocked → **in_progress** (backend: T-0318 reconciled to done — the dev env has been
  live for weeks; the externally-reachable smoke was EXECUTED. Not flipped to done because one host
  failed and two surfaces are unverifiable from outside). Results (public probes, 2026-07-19):
  - **AC1 (SMOKE-API) — health PASS, token-issuance indirect.** `GET /health` → **200 "Healthy"** on
    all five hosts: `api-cleansia-{partner,admin,customer,partner-mobile,customer-mobile}-weu-dev
    .azurewebsites.net` (partner + admin needed a cold-start retry — B2, no Always On — then answered
    in <0.5s). Bad-credential `POST /api/Auth/Login` on both mobile hosts → **400** business error =
    full MediatR pipeline + live Postgres round-trip. Real token issuance not run (no test credentials
    in the agent's hands); owner-attested daily by the iPhone apps running against DEV.
  - **AC2 (SMOKE-STORAGE) — NOT RUN.** The blob-upload → payment → `generate-receipt` → PDF round-trip
    needs authenticated app flows + storage inspection (az CLI unavailable locally). Outstanding.
  - **AC3 (SMOKE-WEB) — SSR PASS, SPAs unverifiable, CORS negative-check PASS.** SSR root
    (`web-cleansia-customer-weu-dev`) → **200**. The two SWA default hostnames are Azure-generated and
    recorded nowhere in the repo/docs — unverifiable without `az staticwebapp show`; note them in the
    runbook once read. Mobile-host CORS confirmed closed: a cross-origin probe with a foreign `Origin`
    returns no `Access-Control-Allow-Origin` header.
  - **AC4 (SMOKE-MIGRATE) — indirect PASS.** The DB-backed login rejection proves the schema serves the
    live pipeline; `deploy-azure.yml` structurally guarantees migrate-before-deploy (every deploy job
    `needs: migrate-database`).
  - **FAIL — Functions host:** `https://func-cleansia-weu-dev.azurewebsites.net/` → **503
    "Application Error"** consistently (3 attempts over ~40s; a healthy Functions v4 container serves a
    200 splash page at `/`, and the host is `alwaysOn: true` so this is not cold start). The container
    appears not to be responding — needs owner/az investigation (`…scm.azurewebsites.net/detectors`,
    container logs). Queue processing may or may not be affected; nothing else is publicly probeable.
  Remaining to close: the Functions 503, AC2's authenticated round-trip, and the SWA URL check.

## Review
<!-- reviewer / qa write verdicts here once the smoke runs against the live dev env -->
- 2026-07-19 backend: reachable smoke executed and recorded above — 5/5 API health green, SSR green,
  DB round-trip green, mobile CORS closed; **Functions root 503 is the open failure**, AC2 + the SWA
  checks are outstanding. Held at `in_progress` rather than done.
