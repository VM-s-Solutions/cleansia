---
id: T-0317
title: OWNER — GitHub Environments (dev-weu / prod-weu) + flat-secret migration into per-env scopes
status: blocked
size: S
owner: pm
created: 2026-06-23
updated: 2026-06-23
depends_on: []
blocks: [T-0318]
stories: []
adrs: [0015, 0017]
layers: [infra, docs]
security_touching: true
manual_steps: [gh-environments, secret-migration]
sprint: 13
---

> **OWNER-only ticket (PM never runs these).** Per ADR-0015 §"Agent-authorable vs OWNER-only" and the
> CLAUDE.md hard line: an agent **never** creates the GitHub Environments and **never** migrates secret
> values. This ticket is the owner's provisioning prerequisite — it does not run until the owner runs it.
> No agent work product lands here; it is `blocked` (on the owner) until the owner confirms it done.

## Context

ADR-0015 D-"GitHub Environments: `dev` (auto on merge to master) + `prod` (protected — required
reviewers + manual approval); migrate the flat `*_DEV`/`*_PRO` secrets into the per-environment scopes."
+ ADR-0017's `<stage>-<region>` Environment naming (`dev-weu`/`prod-weu`, so a second region is
`dev-eus`/`prod-eus` additively). This is the **first** owner provisioning step — the rewritten
`deploy-dev.yml` (T-0319) scopes its `provision` job to the `dev-weu` Environment, so the Environment +
its per-env secrets must exist before the workflow can run.

## Acceptance criteria

- [ ] **AC1 — `dev-weu` Environment (auto on merge).** A GitHub Environment named **`dev-weu`** that
  auto-deploys on merge to master (no required reviewer — dev is fast).
- [ ] **AC2 — `prod-weu` Environment (protected).** A GitHub Environment named **`prod-weu`** with
  **required reviewers + manual approval** (the protected prod gate; ADR-0015 #9). It stays empty/unused
  this wave (prod is authored-not-deployed) but exists as the scaffolding.
- [ ] **AC3 — Flat secrets migrated into the per-env scopes.** The flat `*_DEV`/`*_PRO` secrets — OIDC
  ids (`AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`), `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER`
  / `_ADMIN`, `ACR_NAME`, and the bootstrap `DB_CONNECTION_STRING` — are moved into the **`dev-weu`** (dev
  values) and **`prod-weu`** (prod values) Environment scopes, and the flat repo-level copies removed.

## Out of scope

- Populating the **Key Vault** secret values (DB/JWT/Stripe/SendGrid/Sentry/Storage/Mapbox) + the RBAC
  grants + running the dev provision — that is **T-0318** (the next owner step).
- Authoring the workflow that consumes these Environments — that is **T-0319** (agent, done).

## Implementation notes (for the OWNER)

The `provision` job in `deploy-dev.yml` (T-0319) references `environment: dev-weu` and reads the OIDC ids
+ `ACR_NAME` + the SWA tokens from that Environment's secrets. Create `dev-weu` first (the dev workflow
needs it); create `prod-weu` with the reviewer gate so it is ready when prod is on the table. After
migrating the secrets, the flat repo-level `*_DEV`/`*_PRO` copies should be deleted so there is one
source of truth per environment.

**Routing:** OWNER-only. No agent authors this. `docs` may record the Environment/secret map in the
living doc once the owner confirms the names, but the creation/migration is the owner's.

## Status log

- 2026-06-23 — draft → **blocked** (created by pm — **OWNER provisioning prerequisite**). Not an agent
  ticket: `owner: pm` is the tracking owner, but the **work is the owner's** (creating GitHub Environments
  + migrating secret values is OWNER-only per ADR-0015 + CLAUDE.md). `depends_on: []` (the owner can do
  this any time); `blocks: [T-0318]` (KV values + the dev apply need the Environments + per-env secrets
  first); `security_touching: true` (secret handling); `manual_steps: [gh-environments,
  secret-migration]`. **Held until the owner confirms it done.** Surfaced on the OWNER PROVISIONING
  CHECKLIST relayed to the owner.

## Review
<!-- no agent work product — this is an owner provisioning step; verified by the owner confirming it done -->
