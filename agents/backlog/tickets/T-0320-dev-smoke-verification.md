---
id: T-0320
title: Dev smoke + verification (5 APIs + SSR + 2 SPAs + Functions reachable; queue→Functions pipeline live)
status: blocked
size: M
owner: pm
created: 2026-06-23
updated: 2026-06-23
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

## Review
<!-- reviewer / qa write verdicts here once the smoke runs against the live dev env -->
