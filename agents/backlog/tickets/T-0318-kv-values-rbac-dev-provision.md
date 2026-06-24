---
id: T-0318
title: OWNER — Key Vault values + RBAC grants + run/approve the first dev az deployment
status: blocked
size: M
owner: pm
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0315, T-0316, T-0317]
blocks: [T-0320]
stories: []
adrs: [0015, 0017]
layers: [infra]
security_touching: true
manual_steps: [kv-secret-values, rbac-grants, az-deployment]
sprint: 13
---

> **OWNER-only ticket (PM never runs these).** Per ADR-0015 + the CLAUDE.md hard line: an agent **never**
> populates a real secret value, **never** grants the live RBAC, and **never** runs `az deployment group
> create`. This is the owner's provision step. The declarative inputs are all authored + done (T-0315 the
> modules, T-0316 the dev param, T-0317 the Environments/secrets); this ticket is the owner applying them.

## Context

ADR-0015 D4 (config/secrets as Key Vault references resolved by each host's MI) + the §"OWNER-only" split.
With the Bicep modules (T-0315), the `weu.dev.bicepparam` (T-0316), and the GitHub Environments + migrated
secrets (T-0317) in place, this is the **provision**: the owner populates the Key Vault secret *values*
(the Bicep references them by **name** only — no value is committed), grants CI = Secrets Officer + the MI
role assignments the Bicep declares, and runs/approves the first `az deployment group create` for dev.
This brings the five `api-cleansia-*-weu-dev.azurewebsites.net` hosts into existence — the iOS-pivot
enabler.

## Acceptance criteria

- [ ] **AC1 — Key Vault secret values populated.** The owner populates the dev Key Vault secrets the
  Bicep references by name: the **DB connection string**, **`Jwt--Key`**, **Stripe TEST keys** (never
  live), **SendGrid** key, **Sentry** DSN, **Storage** connection string, **Mapbox** token. No value is
  committed to the repo (the Bicep carries names only).
- [ ] **AC2 — RBAC grants applied.** The CI principal is granted **Key Vault Secrets Officer**; the MI
  role assignments the Bicep declares (each host MI = Secrets User; Storage data roles; AcrPull) are in
  place.
- [ ] **AC3 — First dev provision run/approved.** The owner runs (or approves the workflow's `provision`
  job which runs) `az deployment group create --resource-group rg-cleansia-weu-dev --parameters
  weu.dev.bicepparam` and it completes — the five API hosts + SSR + 2 SWAs + Functions(ACR) + Postgres +
  Storage + Key Vault + App Insights exist in `rg-cleansia-weu-dev`.

## Out of scope

- Authoring the Bicep / param / workflow — done (T-0315/T-0316/T-0319).
- Creating the GitHub Environments + migrating the OIDC/SWA/ACR/bootstrap secrets — **T-0317** (the prior
  owner step).
- The smoke verification that the env is up end-to-end — **T-0320** (runs once this provision completes).
- Stripe **live** keys / a custom domain / prod hardening — prod concerns (Q-INFRA-01/03), not dev.

## Implementation notes (for the OWNER)

The Bicep references each secret as `@Microsoft.KeyVault(SecretUri=...)` — the secret **name** is in the
Bicep/param; the **value** is the owner's to populate in the Key Vault (`kv-cleansia-weu-dev`). The
Postgres admin password is `@secure()` and is supplied to the deployment from the `dev-weu` Environment
secret (T-0317), not the Key Vault. Run order: ensure the RG `rg-cleansia-weu-dev` exists (or let the
deployment create it at the target scope), grant the CI principal Secrets Officer so the MI grants in the
Bicep can be applied, populate the KV values, then run/approve the deployment. After it completes,
T-0320's smoke confirms the env is healthy.

**Routing:** OWNER-only. No agent authors or runs this.

## Status log

- 2026-06-23 — draft → **blocked** (created by pm — **OWNER provisioning prerequisite**). Not an agent
  ticket: the **work is the owner's** (KV secret values, RBAC grants, running the apply are OWNER-only per
  ADR-0015 + CLAUDE.md). `depends_on: [T-0315, T-0316, T-0317]` (the modules ✓, the dev param ✓, the
  Environments/secrets — owner T-0317); `blocks: [T-0320]` (the smoke needs the env up);
  `security_touching: true` (secret values + RBAC); `manual_steps: [kv-secret-values, rbac-grants,
  az-deployment]`. **Held until the owner confirms the dev provision is complete.** Surfaced on the OWNER
  PROVISIONING CHECKLIST relayed to the owner.

## Review
<!-- no agent work product — this is an owner provisioning step; verified by the owner confirming the dev env is provisioned -->
