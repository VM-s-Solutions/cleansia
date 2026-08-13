# Sprint 13 — Wave 11: AZURE DEV (re)DEPLOYMENT — Bicep IaC + GitHub Environments (the iOS-pivot enabler)

**Status:** AGENT AUTHORING DONE — OWNER PROVISIONING PENDING (Bicep + region seam + rewired CI shipped
in commit `38a10375` on `feature/wave8-pre-ios-cleanup`; the dev env is not yet provisioned — that is
the owner's T-0317 → T-0318 → T-0320 path)
**Created:** 2026-06-23
**Updated:** 2026-06-23

> ## 🟦 WAVE-11 AUTHORING LANDED (2026-06-23, commit `38a10375`) — 6 done, 3 owner-blocked
>
> **The agent-authorable half is DONE, reviewed/verified, committed + pushed.** What shipped: `main.bicep`
> + 10 modules (FIVE API hosts incl. customer-mobile; no secret committed — KV refs + a `@secure()`
> Postgres password from a CI secret; least-priv MI; HTTPS-only + firewalled Postgres + mobile CORS
> closed; the ADR-0017 region seam — `region` param + `weu` token in every name + a region→location map);
> `weu.dev.bicepparam` + `weu.prod.bicepparam` (prod **authored, NOT deployed**); a rewritten
> `deploy-dev.yml` (Bicep provision gate, OIDC + EF-bundle preserved, parallelized, `matrix.region:[weu]`,
> `dev-weu` Environment, all five hosts); the T-0330 region connection-string resolver (one resolution
> point, behavior-preserving, **tenancy filter untouched**, no schema change); the catalog/living-doc edits.
>
> **Ticket states:** **DONE (6):** T-0315, T-0316, T-0319, T-0321, T-0322, T-0330. **BLOCKED on OWNER
> (3):** T-0317 (GH Environments + secret migration), T-0318 (KV values + RBAC + run the dev apply), T-0320
> (dev smoke — needs the env up; depends on T-0318 + T-0319).
>
> **Security gate PASSED on the module set (T-0315).** **Reporting-vs-work caveat:** the in-workflow
> StructuredOutput report tool failed (retry cap) on the **T-0319** and **T-0330** dev agents — a
> REPORTING failure, not a work failure (the work landed on disk). The orchestrator **gated those two BY
> HAND** (read the resolver + CI; built `Cleansia.Config` 0 errors; secret-scanned; confirmed tenancy
> untouched + 5 hosts + OIDC/migration/provision gate) → both **verified-done** even though their
> in-workflow reviewer didn't run. **3rd occurrence of the StructuredOutput failure across 2 waves**
> (T-0290 FE earlier) → reinforced as a standing rule in `quality-gates.md` + a mitigation (keep
> `buildEvidence`/`verifyEvidence` SHORT — the oversized log strings are the likely trigger).
>
> **OWNER PROVISIONING CHECKLIST (the exact ordered steps to get dev live) — §7 below.** Prod is
> authored-not-deployed (T-0322). Q-INFRA-01/02/03 + Q-REGION-01/02/03 are all non-blocking for the dev
> provision (tracked with defaults in `questions/open.md`).
**Source:** **ADR-0015** (`adr/0015-azure-dev-deployment-bicep-and-github-environments.md`, **accepted**
2026-06-23) **+ ADR-0017** (`adr/0017-multi-region-expansion-seam-and-its-composition-with-app-level-tenancy.md`,
**accepted** 2026-06-23 — the **region seam** folded into this wave's naming/param/Environment/matrix; **N-region
provisioning is OUT of scope**). Companion living docs `architecture/decisions/azure-deployment.md` +
`architecture/decisions/multi-tenancy-and-region.md`. Evidence base: the AppHost (`src/Cleansia.AppHost/Program.cs`
— five hosts + Functions), the five `Cleansia.Web.*.csproj`, the March-2026 `deploy-dev.yml`/`deploy-pro.yml`
(topology reference), the blob/queue infra projects, the verified tenancy code (`CleansiaDbContext.ApplyTenantQueryFilters`,
`TenantProvider`, `ITenantEntity`, `CountryConfiguration`), and the canonical `docs/architecture/infrastructure.md`.

> **ADR-0017 region-seam amendment to ADR-0015 (folded in here — naming/param only, NOT a supersede):** build
> single-region **West Europe** only, but lay the seam so a second region is a param value, not a rewrite:
> **(1)** a **region token (`weu`) in EVERY resource/RG/Key-Vault name from day one** (Azure names are immutable
> — free at clean-slate, a recreate later) → `api-cleansia-<audience>-weu-dev`, `rg-cleansia-weu-dev`,
> `pg-cleansia-weu-dev`, `kv-cleansia-weu-dev`, …; **(2)** a **`region` Bicep parameter** (default `weu`) threaded
> through the modules; **(3)** GitHub Environments named **`dev-weu` / `prod-weu`** (not bare `dev`/`prod`);
> **(4)** a one-element **`strategy.matrix.region: [weu]`** in the workflows (no-op today, additive tomorrow);
> **(5)** a **connection-string resolver indirection** (one place the DB connection string is chosen, today
> returning the single shared DB — the seam that makes per-region DBs later a resolver change, not an app
> rewrite, T-0330). **The `CountryConfiguration.HomeRegion` COLUMN is DEFERRED** to first-second-region work (a
> schema change → owner ef-migration) — only the resolver indirection is laid now, keeping this wave
> **migration-free**. **Tenancy is UNCHANGED** (app-level row-scoped `TenantId` filter); region is INFRA/config
> and orthogonal. Param-file naming becomes `<region>.<stage>.bicepparam` (e.g. `weu.dev.bicepparam`).

> **Why this wave, and why FIRST (the owner pivot):** before building the iOS apps (Wave 10, sprint-12), deploy
> the whole platform to a **stable Azure DEV environment** so the resource-constrained Mac points at a fixed dev
> API instead of running all five hosts + Functions + Postgres + Azurite locally. The Azure resources are
> **torn down** → this is a **clean-slate re-provision from Bicep**. This wave **unblocks** the iOS port's
> practical workflow (D6: the five stable `api-cleansia-*-dev.azurewebsites.net` hosts the iOS apps point at).

**Goal:** author a **parameterized Bicep IaC** source-of-truth (`deploy/bicep/`) for **both** environments,
**provision DEV only**, migrate the flat `*_DEV`/`*_PRO` GitHub secrets into **per-environment scopes**
(`dev` auto / `prod` protected), rewire the deploy workflows to be **Bicep-gated** while **preserving OIDC + the
EF-migration bundle**, and **author (not deploy) the prod Bicep**. **Ticket ids T-0315…T-0322** (next free after
the iOS tickets T-0296…T-0314; the Apple-compliance tickets T-0315… in sprint-12 are renumbered to avoid the
clash — see note below).

> **Ticket-numbering note:** the iOS Apple-compliance tickets (ADR-0016) and this Azure wave both follow the iOS
> port (T-0314). To keep numbers globally unique, **this Azure wave takes T-0315…T-0322** and the **ADR-0016
> Apple-compliance tickets take T-0323…T-0329** (appended to sprint-12). The PM confirms the split at dispatch.

---

## 1. Owner decisions this wave builds to (ADR-0015)

- **B2 Linux App Service Plan** for dev (the B1→B2 bump; the serialized deploy relaxes to **parallel** on B2 —
  ADR-0015 D5; migrate-before-deploy ordering preserved by the `needs` edge, not the serial chain).
- **Bicep for BOTH dev and prod, parameterized** (one module set + an `env` param + per-env `.bicepparam`).
  **DEPLOY DEV ONLY; author prod Bicep, do not deploy it.**
- **GitHub Environments:** `dev` (auto-deploy on merge to master) + `prod` (**protected** — required reviewers +
  manual approval). Migrate the flat `*_DEV`/`*_PRO` secrets into the per-environment scopes.
- **Re-provision strategy (architect's call, D1):** clean-slate from Bicep; **one subscription, two RGs**
  (`rg-cleansia-dev` / `rg-cleansia-prod`); West Europe; the reusable `appService` module instantiated for the
  **FIVE** API hosts (the five-not-four correction — the **customer-mobile** host the old YAML omitted) + the
  SSR.
- **Config/secrets (D4):** App Service settings as **Key Vault references**, resolved by each host's
  **system-assigned managed identity** (Key Vault Secrets User). **No real secret committed anywhere.** CI/
  provisioning creds (OIDC, SWA tokens, ACR name, bootstrap DB connection string) = per-Environment GitHub
  secrets.
- **Functions stays a CONTAINER via ACR** (D2 — QuestPDF native deps; mandatory). **Storage Account is
  mandatory** (blob + queue + Functions runtime). **Application Insights + Log Analytics added** (D2/D3).

---

## 2. Phase structure (the sequence)

```
P0  BICEP MODULES authored + reviewed (what-if-clean on a dry run) ── runnable on approval, no Azure access
   main.bicep + modules/{appServicePlan,appService,staticWebApp,functionApp,acr,postgres,storage,
   keyVault,appInsights,roleAssignments}.bicep + dev.bicepparam
        │
P1  GH ENVIRONMENTS + SECRET MIGRATION + KEY VAULT VALUES ── OWNER (portal/secret-value steps)
   create dev (auto) + prod (protected, reviewers); migrate *_DEV/*_PRO → per-env scopes; populate KV secrets
        │
P2  DEV PROVISION + WORKFLOW REWRITE + SMOKE ── OWNER runs/approves the apply; agent authors the YAML
   az deployment group create (dev) ; rewritten deploy-dev.yml (Bicep-gated, OIDC+EF-bundle kept, parallelized)
   ; SMOKE: all 5 APIs + SSR + 2 SPAs + Functions reachable + the queue→Functions pipeline live
        │
P3  PROD BICEP authored (prod.bicepparam + prod-only module flags) ── NOT DEPLOYED
```

**Agent-authorable vs OWNER-only (the clean split):**
- **Agent authors on approval (no Azure access needed):** the Bicep modules + `main.bicep` + `dev.bicepparam`
  (T-0315/T-0316), the rewritten `deploy-dev.yml` (T-0319), the smoke definition (T-0320), the prod Bicep
  (T-0322), the catalog + living-doc edits (T-0321).
- **OWNER-only (subscription/portal/secret-value/apply):** creating the GitHub Environments + reviewers,
  migrating the secrets into the per-env scopes, populating the Key Vault secret values (DB conn, JWT key,
  Stripe **test** keys, SendGrid, Sentry, Storage conn, Mapbox), granting the CI principal Key Vault Secrets
  Officer + the MI role assignments the Bicep declares, and **running/approving the first `az deployment group
  create` for dev** (T-0317/T-0318).

---

## 3. Wave-11 ticket table

| ID | Title | Size | Status | Layers | depends_on | manual_step | Phase |
|----|-------|------|--------|--------|-----------|-------------|-------|
| **T-0315** | **Bicep skeleton + the reusable modules** — `deploy/bicep/main.bicep` + `modules/{appServicePlan(B2 Linux),appService(reusable),staticWebApp,functionApp(container/ACR),acr,postgres(B1ms),storage(LRS: blob containers + queues + Functions store),keyVault(RBAC),appInsights(+Log Analytics),roleAssignments}.bicep`. **No secret values** — Key Vault secret *names* only. **ADR-0017 seam: a `region` parameter (default `weu`) threaded through every module that emits a name/`location`; resources/RG/Key-Vault names carry the `weu` token from day one** (`api-cleansia-<audience>-weu-dev`, `rg-cleansia-weu-dev`, `pg-cleansia-weu-dev`, …). | M (filed L→split) | **done ✅** `38a10375` (security **PASS**) | infra, backend, db | — (ADR-0015/0017) | — (authoring) | **0 FIRST** |
| **T-0316** | **`weu.dev.bicepparam` + the region/env-param wiring** — region='weu', env='dev', the five API host names (`api-cleansia-{partner,admin,customer,partner-mobile,customer-mobile}-weu-dev`) + `web-cleansia-customer-weu-dev` SSR + 2 SWAs, B2/B1ms/Free/LRS SKUs, West Europe, CORS=dev SPA/SSR origins per host, HTTPS-only, Postgres firewall (Azure services + admin IP). **Param-file naming = `<region>.<stage>.bicepparam`** (ADR-0017). | M | **done ✅** `38a10375` (PASS-WITH-NOTES) | infra | T-0315✓ | — | 0 |
| **T-0317** | **OWNER: GitHub Environments + secret migration** — create **`dev-weu`** (auto on merge) + **`prod-weu`** (**protected**: required reviewers + manual approval) — the **`<stage>-<region>`** naming (ADR-0017) so a second region is `dev-eus`/`prod-eus` additively; migrate flat `*_DEV`/`*_PRO` (OIDC ids, `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER/_ADMIN`, `ACR_NAME`, bootstrap `DB_CONNECTION_STRING`) into the per-env scopes. | S | **blocked (OWNER)** | infra, docs | — | **owner: GH Environments + secret migration** | 1 |
| **T-0318** | **OWNER: Key Vault values + RBAC + first dev provision** — populate KV secrets (DB conn, `Jwt--Key`, Stripe **test** keys, SendGrid, Sentry, Storage conn, Mapbox); grant CI = Secrets Officer + the MI role assignments the Bicep declares; **run/approve `az deployment group create --resource-group rg-cleansia-weu-dev --parameters weu.dev.bicepparam`**. | M | **blocked (OWNER)** | infra | T-0315✓, T-0316✓, T-0317 | **owner: subscription/RBAC + KV secret values + run the apply** | 2 (the provision) |
| **T-0319** | **Rewrite `deploy-dev.yml`** — a one-element **`strategy.matrix.region: [weu]`** (ADR-0017, no-op today/additive tomorrow) → per-leg Bicep `provision` job (`az deployment group create` on push; `what-if` on PR) → **`migrate-database`** EF-bundle (kept, before deploys) → **parallelized** API/SSR deploys (`needs:[provision,migrate-database]`, not each other) → Functions container (ACR) → 2 SWAs. **OIDC kept**, GH-Environment **`dev-weu`** scoped, the missing **customer-mobile** host added. | M | **done ✅** `38a10375` (**hand-gated** — SO report failed) | infra, backend | T-0315✓, T-0316✓ | — (authoring; runs after T-0318) | 2 |
| **T-0320** | **Dev smoke + verification** — SMOKE-API (all 5 `api-cleansia-*-weu-dev` healthy + a partner-mobile + customer-mobile login issues a token), SMOKE-STORAGE (photo→blob; payment→`generate-receipt`→Functions→`generated-receipts` PDF), SMOKE-WEB (SSR renders, 2 SPAs load + reach APIs, CORS correct), SMOKE-MIGRATE (committed migrations applied pre-deploy). | M | **blocked** (on T-0318 owner; T-0319✓) | infra, backend, qa | T-0318, T-0319✓ | — | 2 |
| **T-0321** | **Catalog + living-doc edits (ADR-0015/0017 pattern-evolution loop)** — `patterns-backend.md` Deployment/IaC note **+ the tenancy=app / region=infra orthogonality + "never branch on a region code in a handler"**; `conventions.md` "no secret in Bicep/param/YAML" **+ the region-token-in-names / `region`-param / `<stage>-<region>`-Environment convention**; create `architecture/decisions/azure-deployment.md` (topology + region parameterization + SKU table + resource→secret map) **+ `multi-tenancy-and-region.md`** (the composition note). | S | **done ✅** `38a10375` | docs, architect | T-0315✓ | — | 2 (parallel doc) |
| **T-0322** | **Author prod Bicep — NOT DEPLOYED** — `weu.prod.bicepparam` (region='weu', env='prod', S1 plan, GP D2s_v3 Postgres, Standard SWAs, the `-weu-prod` names) + any prod-only module flags (the VNet/private-endpoint + Postgres-MI seam left togglable per Q-INFRA-03). Reviewed; **not applied**. | M | **done ✅** `38a10375` (authored, **NOT deployed**) | infra, db | T-0315✓, T-0316✓ | — (authoring only; NOT deployed) | **3 (authored, not deployed)** |
| **T-0330** | **Connection-string resolver indirection (ADR-0017 region seam — the data-layer seam)** — introduce **one place** the DB connection string is resolved (a `region → connection-string` resolver), today returning the **single shared** West-Europe DB. Makes per-region DBs later a **resolver change, not an app rewrite**. **No schema change** (the `CountryConfiguration.HomeRegion` column is DEFERRED to first-second-region work). Reviewer check: the connection string is chosen in exactly one place; no handler/repo hard-codes a region or reaches a second connection string; the **tenancy filter is untouched**. | S | **done ✅** `38a10375` (**hand-gated** — SO report failed; tenancy untouched) | backend | — | — (no ef-migration; code seam only) | 0 (parallel; no Azure) |

**Sizing note:** **T-0315** is `L → split` (the module set is the effort concentrator) — split by module cluster
before dispatch (see §6). No `L` in-flight (the catalog bans it). **T-0330** is a small backend code seam
(ADR-0017) runnable on approval with no Azure access and no migration.

---

## 4. The clean split — what the OWNER does vs what an agent authors

| OWNER-only (subscription/portal/secret/apply) | Agent-authorable (on approval) |
|---|---|
| Create the two **GitHub Environments** + the `prod` required-reviewer gate (T-0317) | The Bicep modules + `main.bicep` + `dev.bicepparam` (T-0315/T-0316) |
| **Migrate the flat secrets** into the per-env scopes (T-0317) | The rewritten `deploy-dev.yml` (Bicep-gated, OIDC+EF-bundle kept, parallelized) (T-0319) |
| **Populate the Key Vault secret values** (DB/JWT/Stripe-test/SendGrid/Sentry/Storage/Mapbox) (T-0318) | The smoke definition (T-0320) |
| Grant **CI = Secrets Officer** + the MI role assignments (T-0318) | The catalog + living-doc edits (T-0321) |
| **Run/approve the first `az deployment group create` for dev** (T-0318) | The **prod** Bicep — authored, **not** deployed (T-0322) |
| Subscription access + the `rg-cleansia-dev`/`rg-cleansia-prod` RGs (T-0318) | |

**Hard line:** an agent **never** runs `az deployment group create`, **never** populates a real secret value,
and **never** creates the GitHub Environments — those are the owner's. An agent authors the declarative
artifacts; the owner applies them.

---

## 5. Dependency-ordered batch plan

```
P0 (runnable on approval — pure authoring, no Azure)
  T-0315 (Bicep modules, L→split) ── FIRST ──► T-0316 (dev.bicepparam)
        ├─► T-0319 (rewrite deploy-dev.yml) [authored now, runs after the provision]
        ├─► T-0321 (catalog + living doc) [parallel]
        └─► T-0322 (prod Bicep — authored, NOT deployed) [parallel]
                                                                    │
  ── OWNER: T-0317 (GH Environments + secret migration) ───────────┤
  ── OWNER: T-0318 (KV values + RBAC + RUN the dev apply) ─────────┤
                                                                    ▼
P2  T-0320 (dev SMOKE) ◀── needs T-0318 (env up) + T-0319 (workflow)
```

**Dispatch order:**
1. **On approval:** T-0315 first (split) → T-0316; fan out T-0319 (author), T-0321 (docs), T-0322 (prod Bicep).
2. **OWNER:** T-0317 (Environments + secrets) → T-0318 (KV values + RBAC + run the dev apply).
3. **T-0320 smoke** confirms the dev env is up end-to-end → **the iOS apps can now point at the five stable dev
   hosts (ADR-0015 D6) — Wave 10's practical enabler is satisfied.**

---

## 6. Indicative split for T-0315 (PM finalizes before dispatch)

- **T-0315a — compute:** `appServicePlan` (B2 Linux) + the reusable `appService` module + `staticWebApp`.
- **T-0315b — data + storage:** `postgres` (B1ms + firewall) + `storage` (LRS: blob containers + queues +
  Functions store).
- **T-0315c — Functions + ACR:** `functionApp` (container) + `acr`.
- **T-0315d — security + observability:** `keyVault` (RBAC) + `roleAssignments` (MI→KV/Storage/ACR) +
  `appInsights` (+ Log Analytics) + `main.bicep` orchestration wiring it all.

---

## 7. OWNER PROVISIONING CHECKLIST (NOT the agents) — the exact ordered steps to get dev live

> All agent-authorable artifacts are **done + on disk** (`38a10375`). The dev environment is NOT yet
> provisioned — the steps below are the **owner's** (the agent never creates an Environment, never
> populates a secret value, never runs `az deployment`). Run them in order. After step 3 the dev env is
> live and the iOS apps can point at the five `api-cleansia-*-weu-dev.azurewebsites.net` hosts.

**STEP 0 — Subscription + RGs (prereq).** Ensure subscription access and the resource group
`rg-cleansia-weu-dev` (the dev deployment is RG-scoped; create it or let the deployment create it at the
target scope). `rg-cleansia-weu-prod` can wait — prod is authored-not-deployed.

**STEP 1 — GitHub Environments + secret migration (ticket T-0317).**
1. Create Environment **`dev-weu`** — auto-deploy on merge to master, **no** required reviewer.
2. Create Environment **`prod-weu`** — **protected**: required reviewers + manual approval (it stays
   empty/unused this wave, but the protected gate must exist).
3. Migrate the flat secrets into the per-env scopes (and delete the flat repo-level copies):
   - OIDC: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
   - SWA deploy tokens: `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER`, `…_ADMIN`
   - `ACR_NAME`
   - bootstrap `DB_CONNECTION_STRING` (the migrate-bundle's connection)
   - the Postgres admin password the Bicep takes as `@secure()` (e.g. `POSTGRES_ADMIN_PASSWORD`)
   → put the **dev** values in `dev-weu`, the **prod** values in `prod-weu`.

**STEP 2 — Key Vault secret VALUES + RBAC grants (ticket T-0318).** In the dev Key Vault
`kv-cleansia-weu-dev`, populate the secret **values** the Bicep references by **name** (no value is in
the repo). The set:
- `DB connection string` (the app's Postgres connection)
- `Jwt--Key` (the JWT signing key)
- Stripe **TEST** keys (publishable + secret + webhook signing) — **never live keys for dev**
- SendGrid API key
- Sentry DSN
- Storage account connection string
- Mapbox token

Then grant RBAC:
- **CI principal = Key Vault Secrets Officer** (so the deployment can apply the MI grants).
- The **MI role assignments the Bicep declares**: each host's system-assigned MI = Key Vault **Secrets
  User**; the Storage **data roles**; **AcrPull** (these are in `roleAssignments.bicep` — granting CI
  Secrets Officer lets the deployment apply them).

**STEP 3 — Run/approve the first dev provision (ticket T-0318).** Run (or let the workflow's `provision`
job run on push to master):
```
az deployment group create \
  --resource-group rg-cleansia-weu-dev \
  --template-file deploy/bicep/main.bicep \
  --parameters deploy/bicep/weu.dev.bicepparam \
  --parameters postgresAdminPassword=$POSTGRES_ADMIN_PASSWORD
```
(On a PR the same `provision` job runs `az deployment group what-if` — a non-mutating preview — instead
of `create`.) When it completes, the five API hosts + SSR + 2 SWAs + Functions(ACR) + Postgres + Storage
+ Key Vault + App Insights exist in `rg-cleansia-weu-dev`.

**STEP 4 — Dev smoke (ticket T-0320, runs once the env is live).** With the env up, the T-0320 smoke
confirms: all 5 `api-cleansia-*-weu-dev` healthy + a partner-mobile **and** customer-mobile login issues
a token; photo→blob + payment→`generate-receipt`→Functions→`generated-receipts` PDF (queue→Functions
live); SSR renders + 2 SPAs load + reach APIs + CORS correct; committed migrations applied pre-deploy.
**Green smoke = the dev env is the stable API the Mac/iOS point at — the wave's outcome (ADR-0015 D6).**

**Notes for the owner:**
- **No new app code, no new ef-migration in this wave.** The config keys + connection-string slots +
  blob/queue usage all already exist (MI-vs-connection-string is a config choice the existing code
  supports). The CI EF-bundle applies **already-committed** migrations only (never `migrations add`).
- **Prod is authored, NOT deployed** (T-0322). Deploying prod is a separate future decision once
  Q-INFRA-01 (custom domain) + Q-INFRA-03 (VNet/private-endpoint + Postgres-MI hardening) are answered.
- All Q-INFRA-* + Q-REGION-* questions are **non-blocking** for this dev provision (defaults in
  `questions/open.md`).

---

## 8. Open questions (ADR-0015 — all non-blocking for the DEV provision)

- **Q-INFRA-01** (`pre-prod` for prod only) — custom domain? **Default: No for dev** (stable
  `*.azurewebsites.net`); a prod + DNS concern. The iOS base-URL is env-switched config.
- **Q-INFRA-02** (`post-prod`) — two subscriptions vs one-sub/two-RGs? **Default: one sub, two RGs** (clean
  blast-radius; the Bicep is RG-scoped so a later split is a parameter).
- **Q-INFRA-03** (`pre-prod` for prod hardening) — VNet/private-endpoint + Postgres-MI auth for prod? **Default
  for dev: public+firewall + MI-to-KeyVault/Storage**; the prod Bicep leaves the seam (a module flag).

---

## 9. Gates & verification (per `agents/process/quality-gates.md`)

- **Reviewer-per-developer** on every ticket. **Security review on T-0315/T-0318** (the secret/RBAC/network
  baseline — a leaked secret in Bicep or an over-broad firewall/role is a finding).
- **Reviewer compliance checks (ADR-0015 §"How a reviewer verifies"):** #1 Bicep in `deploy/bicep/`, one
  reusable `appService` module, prod param not referenced by deploy-dev · #2 **five** API App Services named
  correctly (incl. **customer-mobile**) · #3 Functions = container from ACR (no code/zip) · #4 **no secret
  committed** (Key Vault refs only) · #5 MI + Key Vault RBAC (Secrets User app / Officer CI) · #6 HTTPS-only +
  firewalled Postgres, no dev VNet · #7 CORS per host = dev origins (mobile hosts closed) · #8 OIDC +
  migrate-before-deploy preserved, CI only *applies* a committed migration · #9 GH Environments + the prod
  reviewer gate · #10 Bicep `what-if` on PR · #11 parallelized deploys with the migrate edge intact.
- **Reviewer compliance checks (ADR-0017 §"How a reviewer verifies" — the region seam):** #R1 **region token
  (`weu`) in every resource/RG/Key-Vault name** (a name without it is a finding) · #R2 **`region` is a Bicep
  parameter** (default `weu`) threaded through the modules (no per-region forks) · #R3 Environments are
  **`dev-weu`/`prod-weu`** (not bare) with `prod-weu` protected · #R4 the workflows carry **`matrix.region:
  [weu]`** (one-element today) · #R5 **one subscription** (region in RG/naming) · #R6 the **connection-string
  resolver indirection** exists (one place; no handler hard-codes a region/second connection string) · #R7 the
  **tenancy filter is UNCHANGED** — no region clause in `ApplyTenantQueryFilters` (a region clause is a
  conflation finding) · #R8 **no handler branches on a region code** · #R9 **no second region provisioned**
  (single-region this pass). **Forward-compat assertion:** adding a second region requires only a new param
  value + a matrix entry + an owner `HomeRegion` column-migration — NOT a rename/recreate of any live `weu`
  resource, a workflow restructure, or a tenancy-filter change.
- **Mechanical:** `az bicep build`/lint clean; `az deployment group what-if` on a dry run shows the expected
  resource set; the rewritten `deploy-dev.yml` parses; the SMOKE-* set (T-0320) green against the provisioned
  dev env; T-0330's resolver is the single connection-string source (grep: no second hard-coded connection
  string, tenancy filter untouched).

---

## 10. Definition of wave-done

Bicep authored + reviewed (`what-if`-clean), the dev env **provisioned by the owner** from `weu.dev.bicepparam`,
the rewritten `deploy-dev.yml` deploying all five APIs + SSR + 2 SPAs + Functions on push (Bicep-gated, OIDC +
EF-bundle preserved, parallelized), the SMOKE-* set green (5 APIs + SSR + 2 SPAs + Functions reachable, the
queue→Functions pipeline live, the two **mobile** hosts issuing tokens for the iOS apps), the catalog + living
docs updated, the **prod** Bicep authored (**not** deployed), and the **ADR-0017 region seam in place** (region
token in names, `region` param, `dev-weu`/`prod-weu` Environments, one-element matrix, the T-0330 connection-
string resolver — tenancy filter untouched, no second region provisioned). The three Q-INFRA-* + three
Q-REGION-* questions tracked with their defaults. **Outcome: the five stable `api-cleansia-*-weu-dev.azurewebsites.net`
hosts exist — the Mac points at dev, Wave 10 (iOS) can proceed against a stable API, and a second market/region
is a param value away, not a rewrite.**
