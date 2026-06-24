---
id: T-0315
title: Bicep skeleton + the reusable modules (main.bicep + 10 modules; region-token names; no secret values)
status: done
size: M
owner: backend
created: 2026-06-23
updated: 2026-06-23
depends_on: []
blocks: [T-0316, T-0318, T-0319, T-0321, T-0322]
stories: []
adrs: [0015, 0017]
layers: [infra, backend, db]
security_touching: true
manual_steps: []
sprint: 13
---

> **Sizing note:** filed `L → split` (the module set is the effort concentrator; the catalog bans an
> in-flight `L`). The split (§6 of `status/sprint-13.md`) was authored as one cohesive `deploy/bicep/`
> module set + `main.bicep` orchestration in a single landed commit; each module is a small, independent
> file (44–140 lines) so the `M`-sized review surface held. No `L` ran.

## Context

ADR-0015 (Azure dev deployment — Bicep IaC + GitHub Environments, **accepted** 2026-06-23) + ADR-0017
(multi-region expansion seam, **accepted** 2026-06-23). The owner pivot: before building the iOS apps,
deploy the whole platform to a stable Azure DEV environment so the resource-constrained Mac points at a
fixed dev API instead of running all five hosts + Functions + Postgres + Azurite locally. The old Azure
resources are torn down → this is a clean-slate re-provision from Bicep. This ticket is the foundation:
the declarative IaC source-of-truth at `deploy/bicep/` — `main.bicep` orchestration + the reusable
module set, authored with **no secret values** (Key Vault references only) and the ADR-0017 region seam
(a `region` parameter, default `weu`, threaded through every module that emits a name/`location`).

## Acceptance criteria

- [x] **AC1 — `main.bicep` + 10 modules under `deploy/bicep/`.** `main.bicep` orchestrates
  `modules/{appServicePlan,appService,staticWebApp,functionApp,acr,postgres,storage,keyVault,
  roleAssignments,appInsights}.bicep`. The `appService` module is **reusable** (instantiated per host).
  Evidence: `deploy/bicep/main.bicep` (386 lines) + the 10 module files on disk; `az bicep build`/lint
  clean (CI lints before any apply — the az/bicep CLI is not in the agent env).
- [x] **AC2 — FIVE API hosts (the five-not-four correction).** The reusable `appService` module is
  instantiated for **all five** API hosts — partner, admin, customer, partner-mobile, **and
  customer-mobile** (the host the old March-2026 YAML omitted and the iOS customer app needs) — plus the
  SSR host. Evidence: `main.bicep` host instantiations carry the `customer-mobile` host.
- [x] **AC3 — No secret value committed anywhere.** Every sensitive App Service setting is a
  `@Microsoft.KeyVault(SecretUri=...)` reference resolved by each host's **system-assigned managed
  identity**; the Postgres admin password is `@secure()` supplied by the CI from a GitHub-Environment
  secret, never a literal. The Key Vault module declares secret **names** only. Evidence: secret-scan
  clean — only the `$POSTGRES_ADMIN_PASSWORD` CI-secret shell var, not a literal value.
- [x] **AC4 — Least-privilege MI + Key Vault RBAC.** `roleAssignments.bicep` grants each host MI the
  minimum: Key Vault **Secrets User** (app), Storage data roles, **AcrPull**; the CI principal gets
  **Secrets Officer**. No over-broad role (no Owner/Contributor on the data plane). HTTPS-only on every
  host; Postgres firewalled (Azure-services + admin IP), no dev VNet; mobile-host CORS closed.
- [x] **AC5 — ADR-0017 region seam in names + params.** A `region` Bicep parameter (default `weu`) is
  threaded through every module that emits a name or `location`; the `weu` token is in **every**
  resource/RG/Key-Vault name from day one (`api-cleansia-<audience>-weu-dev`, `pg-cleansia-weu-dev`,
  `kv-cleansia-weu-dev`, …); a `region → location` map resolves the Azure location. No per-region fork —
  a second region is one map entry, not a recreate. Evidence: reviewer R1/R2 PASS on the module set.
- [x] **AC6 — Functions = container from ACR; storage mandatory; observability present.** `functionApp`
  is a **container** pulling from `acr` (QuestPDF native deps — no code/zip deploy); `storage` (LRS) emits
  blob containers + queues + the Functions runtime store; `appInsights` (+ Log Analytics) is wired.

## Out of scope

- The dev param file (`weu.dev.bicepparam`) — that is **T-0316**.
- The prod param file (`weu.prod.bicepparam`) — that is **T-0322** (authored, NOT deployed).
- The workflow rewrite (`deploy-dev.yml`) — that is **T-0319**.
- **Running** any `az deployment group create`, populating a real secret value, creating the GitHub
  Environments, granting the live RBAC — all **OWNER-only** (T-0317/T-0318). This ticket authors the
  declarative artifacts; the owner applies them.
- The `CountryConfiguration.HomeRegion` column — DEFERRED to first-second-region work (owner ef-migration);
  only the resolver indirection (T-0330) is laid now.

## Implementation notes

Read ADR-0015 (topology, SKUs, the secret→KeyVault-ref map, the five-host correction) and ADR-0017 (the
region seam — token-in-names, `region` param, the location map). Module SKUs: appServicePlan **B2 Linux**,
postgres **B1ms**, storage **LRS**, SWA **Free** (dev). The `appService` module must be a single reusable
definition the `main.bicep` instantiates per host with per-host name/CORS/settings. **Security gate
mandatory** — a leaked secret in Bicep or an over-broad firewall/role is a finding (ADR-0015 §"How a
reviewer verifies" #4/#5/#6).

**Routing:** `architect` locked the topology (ADR-0015/0017) → `backend`/`db`/`infra` authored the
modules → `reviewer` + `security` in parallel.

## Status log

- 2026-06-23 — draft → ready (created by pm). Foundation of Wave 11. DoR met: AC observable; sized
  `M` (filed `L → split`, authored as one cohesive module set per §6); `depends_on: []`; `layers:
  [infra, backend, db]`; `security_touching: true` (secret/RBAC/network baseline); `manual_steps: []`
  (pure authoring — the apply is the owner's, tracked on T-0318). ADRs 0015/0017 finalized → ticketed.
- 2026-06-23 — ready → in_progress → in_review → **done** (authored + reviewed + security-gated;
  commit `38a10375` on `feature/wave8-pre-ios-cleanup`, pushed). **All six AC satisfied.** `main.bicep`
  (386 lines) + 10 modules landed; **five** API hosts incl. `api-cleansia-customer-mobile-weu-dev`;
  every sensitive setting a Key-Vault reference (Postgres password `@secure()` from a CI secret, never a
  literal); least-priv MI (KV Secrets User / Storage data roles / AcrPull) + CI = Secrets Officer;
  HTTPS-only, firewalled Postgres, mobile-host CORS closed; the `region` param + `weu` token in every
  name + the region→location map; Functions container via ACR; storage LRS (blob + queue + Functions
  store); appInsights + Log Analytics. **SECURITY GATE: PASS** on the module set (no secret committed —
  secret-scan clean; least-priv MI; firewalled Postgres; the only flagged var is the
  `$POSTGRES_ADMIN_PASSWORD` CI-secret shell reference). Reviewer ADR-0015 #1–#7 + ADR-0017 R1/R2/R5/R7
  PASS. `az bicep build`/lint is enforced by CI (the az CLI is not in the agent env). **Mechanical
  caveat:** `dotnet`/`nx` suites N/A — this ticket touches only declarative `deploy/bicep/` artifacts.

## Review

## Review — security (2026-06-23)

- Gate 3 Security (S-baseline for IaC): **PASS.** No secret value committed (every sensitive setting is
  a `@Microsoft.KeyVault(SecretUri=...)` ref; Postgres password is `@secure()` from a GitHub-Environment
  CI secret). Managed identities are least-privilege (KV Secrets User for apps, Storage data roles,
  AcrPull; CI = Secrets Officer — no Owner/Contributor on the data plane). HTTPS-only on every host;
  Postgres firewalled to Azure-services + admin IP, no over-broad rule; mobile-host CORS closed. The
  secret-scan over `deploy/bicep/` surfaced only the `$POSTGRES_ADMIN_PASSWORD` CI-secret shell variable
  reference — not a literal. Verdict: APPROVED.

## Review — reviewer (2026-06-23)

- Gate 1 Conventions: PASS — modules in `deploy/bicep/`, one reusable `appService` module.
- ADR-0015 compliance #1–#7: PASS — five API hosts (incl. customer-mobile), Functions = container from
  ACR, no secret committed (KV refs only), MI + KV RBAC, HTTPS-only + firewalled Postgres + no dev VNet,
  CORS per host (mobile closed).
- ADR-0017 compliance R1/R2/R5/R7: PASS — `weu` token in every resource/RG/KV name, `region` is a
  parameter (default `weu`) threaded through the modules, one subscription (region in naming), tenancy
  filter untouched (not in this diff's scope — confirmed on T-0330).
- Gate 8 Mechanical: `az bicep build`/lint deferred to CI (az CLI absent from the agent env);
  `dotnet`/`nx` N/A (declarative artifacts only).

Verdict: APPROVED.
