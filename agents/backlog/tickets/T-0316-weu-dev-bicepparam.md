---
id: T-0316
title: weu.dev.bicepparam + the region/env-param wiring (the five host names, dev SKUs, CORS, firewall)
status: done
size: M
owner: backend
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0315]
blocks: [T-0318, T-0319]
stories: []
adrs: [0015, 0017]
layers: [infra]
security_touching: false
manual_steps: []
sprint: 13
---

## Context

ADR-0015/0017. With the reusable Bicep module set authored (T-0315), this ticket supplies the **dev**
parameter file that binds the modules to concrete dev values: `region='weu'`, `env='dev'`, the five API
host names, the SSR + 2 SWA names, the dev SKUs, West Europe, the dev CORS origins per host, HTTPS-only,
and the Postgres firewall. Param-file naming follows ADR-0017's `<region>.<stage>.bicepparam` convention.

## Acceptance criteria

- [x] **AC1 — `weu.dev.bicepparam` binds region/env + the five host names.** `region='weu'`,
  `env='dev'`; the five API host names are `api-cleansia-{partner,admin,customer,partner-mobile,
  customer-mobile}-weu-dev` + `web-cleansia-customer-weu-dev` (SSR) + the 2 SWAs. Evidence:
  `deploy/bicep/weu.dev.bicepparam` on disk (83 lines).
- [x] **AC2 — Dev SKUs.** B2 (plan) / B1ms (Postgres) / Free (SWA) / LRS (storage), West Europe.
- [x] **AC3 — CORS = dev SPA/SSR origins per host; mobile hosts closed.** Each API host's CORS is the
  dev SPA/SSR origins; the two mobile API hosts have CORS closed (they are consumed by native apps, not
  a browser).
- [x] **AC4 — HTTPS-only + Postgres firewall.** HTTPS-only on every host; the Postgres firewall = Azure
  services + admin IP (no public-internet-wide rule).
- [x] **AC5 — Naming convention.** The file is named `weu.dev.bicepparam` (the ADR-0017
  `<region>.<stage>.bicepparam` form) so a second region is `eus.dev.bicepparam` additively.

## Out of scope

- The module definitions — **T-0315**.
- The prod param file — **T-0322**.
- **Populating real secret values** (the KV secret *values* the param references by name) — **OWNER**
  (T-0318). The param file carries no secret value.
- Running the apply with this param — **OWNER** (T-0318).

## Implementation notes

Mirror the module parameter contract T-0315 defines. The host names are immutable in Azure → the `weu`
token is baked in from day one (ADR-0017). The CORS origin list is the dev SPA/SSR origins; the two
mobile hosts stay CORS-closed. Reviewer ADR-0015 #2 (five hosts named correctly incl. customer-mobile),
#6 (HTTPS-only + firewalled Postgres), #7 (CORS per host, mobile closed) and ADR-0017 R1/R3 apply.

**Routing:** `infra` authored the param file against T-0315's module contract → `reviewer`. Not
security-gated (no secret value in a param file — names only; the values are the owner's KV population).

## Status log

- 2026-06-23 — draft → ready (created by pm). DoR met: AC observable; sized `M`; `depends_on:
  [T-0315]` (the module contract); `layers: [infra]`; `security_touching: false` (no secret value —
  names only); `manual_steps: []`. ADR-0015/0017 finalized.
- 2026-06-23 — ready → in_progress → in_review → **done** (authored + reviewed; commit `38a10375`,
  pushed). **All five AC satisfied.** `weu.dev.bicepparam` (83 lines): `region='weu'`/`env='dev'`; the
  five API host names incl. `api-cleansia-customer-mobile-weu-dev` + the SSR + 2 SWAs; B2/B1ms/Free/LRS
  in West Europe; dev CORS origins per host with the mobile hosts closed; HTTPS-only; Postgres firewall
  = Azure-services + admin IP; `<region>.<stage>.bicepparam` naming. Reviewer ADR-0015 #2/#6/#7 +
  ADR-0017 R1/R3 PASS. **PASS-WITH-NOTES:** the notes are owner-provisioning reminders (the referenced
  KV secret values are populated by the owner at T-0318; the admin-IP firewall entry is an owner value),
  not param defects — both are tracked on T-0318, not blockers for this authoring ticket.

## Review

## Review — reviewer (2026-06-23)

- Gate 1 Conventions: PASS — `<region>.<stage>.bicepparam` naming (ADR-0017).
- ADR-0015 #2 (five hosts named correctly incl. customer-mobile) / #6 (HTTPS-only + firewalled Postgres)
  / #7 (CORS per host, mobile closed): PASS.
- ADR-0017 R1 (weu token in names) / R3 (the `<stage>-<region>` Environment naming is honored by the
  param's host names): PASS.
- Gate 2 AC: PASS — all five AC have file evidence in `weu.dev.bicepparam`.

Verdict: APPROVED — **PASS-WITH-NOTES.** Notes (owner-provisioning, not defects): the KV secret values
this param references by name are populated by the owner (T-0318); the Postgres admin-IP firewall entry
is an owner value. Neither blocks this authoring ticket.
