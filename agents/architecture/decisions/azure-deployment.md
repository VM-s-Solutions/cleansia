# Azure deployment (Bicep IaC + GitHub Environments) — living decision notes

> Companion to the **immutable** ADR-0015
> (`agents/backlog/adr/0015-azure-dev-deployment-bicep-and-github-environments.md`). The ADR is the frozen
> contract; this file is the *evolving* design notes — the topology, the dev/prod SKU table, the resource→secret
> map, the trade-off space, and the open questions. Update this as the deployment evolves; supersede the ADR for
> a real change. Canonical source of the resource inventory: `docs/architecture/infrastructure.md`. Consumer
> tickets: `status/sprint-13.md` (Wave 11, T-0315…T-0322).

## The problem in one sentence

The Azure resources are **torn down** and were not recoverable from code; the platform must be re-deployed to a
**stable DEV environment** (so the resource-constrained Mac points at a fixed dev API instead of running all
five hosts + Functions + Postgres + Azurite locally) — and this is the moment to make the footprint
**declarative IaC** so it can never again be an undocumented, unrecoverable portal state.

## Owner decisions this builds to (taken, not re-litigated)

- **B2 Linux** App Service Plan for dev (the B1→B2 bump; serialized deploy relaxes to parallel on B2).
- **Bicep for BOTH dev + prod**, parameterized; **deploy DEV only**, author prod Bicep (don't deploy).
- **GitHub Environments:** `dev` (auto on merge) + `prod` (**protected** — required reviewers + approval);
  migrate flat `*_DEV`/`*_PRO` secrets into per-env scopes.
- Architect's re-provision call: clean-slate from Bicep; **one subscription, two RGs** (`rg-cleansia-dev` /
  `rg-cleansia-prod`); West Europe.

## Current shape — the dev topology (ADR-0015)

```
rg-cleansia-dev  (West Europe, one subscription)
├── App Service Plan        B2 Linux  ── hosts the 6 App Services below
│   ├── api-cleansia-partner-dev          (Cleansia.Web.Partner,        :5000 local)
│   ├── api-cleansia-admin-dev            (Cleansia.Web.Admin,          :5001 local)
│   ├── api-cleansia-customer-dev         (Cleansia.Web.Customer,       :5003 local)
│   ├── api-cleansia-partner-mobile-dev   (Cleansia.Web.Mobile.Partner, :5002 local)  ← iOS partner host
│   ├── api-cleansia-customer-mobile-dev  (Cleansia.Web.Mobile.Customer,:5004 local)  ← iOS customer host (NEW — old YAML omitted it)
│   └── web-cleansia-customer-dev         (Customer SSR, node server/server.mjs)
├── Static Web App (Free) ×2  ── Partner SPA, Admin SPA
├── func-cleansia-dev         ── Functions, CONTAINER via ACR (QuestPDF native deps — mandatory)
├── ACR (Basic)               ── cleansia-functions:<sha>
├── PostgreSQL Flexible       Burstable B1ms  ── public + firewall (Azure services + admin IP), TLS-required
├── Storage Account (LRS)     ── blob containers + queues (+ poison) + AzureWebJobsStorage  (MANDATORY)
├── Key Vault (RBAC)          ── app secrets; app MIs = Secrets User; CI = Secrets Officer
└── Application Insights + Log Analytics  ── all 5 APIs + SSR + Functions telemetry/alerts
```

**The five-not-four correction (the key finding):** the runtime is **five** API hosts (AppHost
`Program.cs:35-69` + the five `Cleansia.Web.*.csproj`), but the March-2026 deploy YAML deployed only **four**
App Services — `api-cleansia-mobile-dev` was the **partner-mobile** host, and the **customer-mobile** host
(`Cleansia.Web.Mobile.Customer`) was **never deployed**. ADR-0013 D4 fixes the iOS **customer** app to that
host. This wave provisions all five, so the iOS customer app has a stable dev URL.

## Dev vs prod SKU table (env-parameterized; same modules)

| Resource | Dev (`dev.bicepparam`) | Prod (`prod.bicepparam`, authored not deployed) |
|---|---|---|
| App Service Plan | **B2 Linux** (owner override of doc's B1) | S1 / Standard |
| API App Services | 5, on B2 | 5, on S1 |
| Customer SSR | App Service (Node), on B2 | App Service (Node), on S1 |
| Static Web Apps | 2 × **Free** | 2 × Standard |
| Functions | Consumption, **container/ACR** | Consumption, container/ACR |
| ACR | Basic | Basic |
| PostgreSQL Flexible | **Burstable B1ms** | General Purpose D2s_v3 |
| Storage Account | Standard **LRS** | Standard LRS |
| Key Vault | Standard, RBAC | Standard, RBAC |
| App Insights + Log Analytics | Basic | Basic |
| Network posture | public + firewall + MI-to-KV/Storage (dev-pragmatic) | **seam left** for VNet/private-endpoint + Postgres-MI (Q-INFRA-03) |

## Resource → secret map (Key Vault; ADR-0015 D4)

App config flows as **App Service settings = Key Vault references**, resolved by each host's
**system-assigned managed identity** (Key Vault Secrets User). **No secret value is committed** — Bicep emits
the Key Vault + the secret *names* + the MIs + the role assignments; the owner (or a guarded CI step reading a
GitHub-Environment secret) populates the *values*.

| Key Vault secret (name) | App config key it backs | Used by |
|---|---|---|
| `ConnectionStrings--cleansia-db` | `ConnectionStrings:ConnectionString` | all 5 APIs + Functions |
| `Jwt--Key` (+ Issuer/Audience) | `JwtSettings:Secret` etc. | all APIs |
| `Stripe--SecretKey` / `--WebhookSecret` | `Stripe:SecretKey` / `:WebhookSecret` | customer API |
| `SendGrid--ApiKey` | `SendGrid:ApiKey` | Functions + APIs |
| `Sentry--Dsn` | `Sentry:Dsn` | all APIs + Functions |
| `Storage--ConnectionString` | `ConnectionStrings:BlobContainerConfigurationConnectionString` + `:QueueStorageConnectionString` (fallback to MI) | all APIs + Functions |
| `Mapbox--GeocodingAccessToken` | `Mapbox:GeocodingAccessToken` | APIs that geocode (if exercised in dev) |

**Storage access** prefers **managed identity** (Blob/Queue Data Contributor) where the host runs a modern
`DefaultAzureCredential`; the `Storage--ConnectionString` secret is kept as the **compatible fallback** the
current code reads (so this ADR forces **no app code change**). **CI/provisioning secrets** (OIDC ids, SWA
tokens, `ACR_NAME`, bootstrap `DB_CONNECTION_STRING`) live in **per-environment GitHub secrets**, not Key
Vault — they are pipeline creds.

## The CI flow (ADR-0015 D5)

```
push to master → deploy-dev.yml  (scoped to GH Environment `dev`, auto)
  provision (az deployment group create dev.bicepparam)   ── what-if on PR
    → migrate-database (dotnet ef migrations bundle → apply)   ── KEPT; before deploys; applies COMMITTED migrations only
      → [parallel] deploy 5 APIs + SSR  (needs:[provision,migrate-database], NOT each other)   ── B2 relaxes the serial chain
      → Functions container (az acr build → config container set)
      → 2 Static Web Apps
  (OIDC preserved: azure/login@v2 + id-token:write, per-Environment ids)

workflow_dispatch → deploy-prod.yml (scoped to GH Environment `prod` — REQUIRED REVIEWERS + manual approval)
  same job graph, prod params; PAUSES for human approval before any mutating step
```

**Migrate-before-deploy ordering is preserved by the `needs: migrate-database` edge, NOT the serial chain** —
so parallelizing the app deploys cannot reorder migrate-vs-deploy. CI **applies** committed migrations; it never
runs `migrations add` (that stays owner-authored + reviewed).

## The iOS connection (ADR-0015 D6)

- iOS **partner** → `https://api-cleansia-partner-mobile-dev.azurewebsites.net`
- iOS **customer** → `https://api-cleansia-customer-mobile-dev.azurewebsites.net` (the host this wave adds)
- Custom domain **deferred for dev** (default hostnames are stable; Q-INFRA-01). The iOS base-URL is
  env-switched config, so a later custom domain is config, not code.

## The region seam (ADR-0017 — folded into this deployment, BUILD single-region only)

ADR-0017 (multi-region expansion) adds a **region seam** to this deployment. **Driver = market expansion, NOT
residency/latency** → the model is **one shared region + DB now**; the heavier region-pinned model is one
**named trigger** (a residency-regulated market or a latency SLA) away. **Tenancy stays APP-level** (the
row-scoped `TenantId` filter, unchanged); **region is INFRA/config** and orthogonal. See
`multi-tenancy-and-region.md` for the composition.

**What this deployment lays now (naming/param only — free at clean-slate; N-region NOT built):**
- **A region token (`weu`) in every resource/RG/Key-Vault name from day one** — Azure names are immutable, so
  the token must be present now or a second region forces a recreate. Names become
  `api-cleansia-<audience>-weu-dev`, `rg-cleansia-weu-dev`, `pg-cleansia-weu-dev`, `kv-cleansia-weu-dev`, …
- **A `region` Bicep parameter** (default `weu`) threaded through the modules (no per-region forks); param files
  are **`<region>.<stage>.bicepparam`** (`weu.dev.bicepparam`, `weu.prod.bicepparam`).
- **GitHub Environments `dev-weu` / `prod-weu`** (the `<stage>-<region>` scheme) — a second region is
  `dev-eus`/`prod-eus`, additive.
- **A one-element `strategy.matrix.region: [weu]`** in the workflows (no-op today; add a value tomorrow).
- **A connection-string resolver indirection** (T-0330) — one place the DB connection string is chosen, today
  returning the single shared West-Europe DB. This is the data-layer seam that makes per-region DBs later a
  resolver change, not an app rewrite. **The `CountryConfiguration.HomeRegion` column is DEFERRED** to
  first-second-region work (a schema change → owner ef-migration); only the resolver indirection is laid now, so
  this wave stays **migration-free**.

**Forward-compat assertion:** a second region = a new param value + a matrix entry + an owner `HomeRegion`
column-migration — NOT a rename/recreate of any live `weu` resource, a workflow restructure, or a tenancy-filter
change. Subscriptions stay **one** until a quota/billing-legal/blast-radius trigger (Q-REGION-03).

## Trade-off space (what was weighed — ADR-0015 §Alternatives + the Challenge trail)

- **Bicep vs imperative YAML / portal.** Bicep — the teardown proved portal/imperative state is unrecoverable.
  Declarative, what-if-reviewable, dev≡prod by construction. Clean-slate = the cheapest moment.
- **Bicep vs Terraform.** Bicep — Azure-native, no state backend, ARM what-if; multi-cloud is irrelevant here.
- **One sub + two RGs vs two subscriptions.** One sub / two RGs (clean blast-radius without the overhead); the
  Bicep is RG-scoped so a later split is a parameter (Q-INFRA-02).
- **Functions container vs code/zip.** Container — QuestPDF needs native `libfontconfig1`/`libfreetype6`; a
  code deploy would fail PDF generation. Mandatory.
- **Key Vault + MI vs raw GH secrets into app config.** Key Vault + MI (the canonical infra-doc model; one
  audited store, rotation without redeploy, no secret-to-read-secrets). GH Environment secrets stay for
  **pipeline** creds only.
- **App Insights added vs Sentry-only.** Added — the infra doc's alert set (poison-queue depth, 5xx, DB-conn
  failures) is infra telemetry Sentry doesn't cover; wiring it across five hosts is cheapest at clean-slate.
- **Dev VNet/private-endpoint + Postgres-MI vs public+firewall+conn-string.** Public+firewall+TLS+MI-to-KV for
  dev; VNet + Postgres-MI is prod hardening (Q-INFRA-03) the prod Bicep leaves a seam for.
- **Parallel vs serial deploys.** Parallel on B2 (the serialization guarded B1, per the YAML comment); the
  migrate edge preserves the only ordering that matters; a `max-parallel` throttle is the cheap fallback.

## Current rollout state

| Step | Phase | State (2026-06-23) |
|---|---|---|
| ADR (deployment architecture) | — | **accepted** (ADR-0015) |
| Bicep modules + `main.bicep` + `dev.bicepparam` | P0 | planned (runnable on approval — pure authoring) |
| GitHub Environments + secret migration | P1 | **PENDING — owner-only** |
| Key Vault values + RBAC + first dev `az deployment group create` | P2 | **PENDING — owner-only** |
| Rewritten `deploy-dev.yml` (Bicep-gated, OIDC+EF-bundle kept, parallelized) | P2 | planned (authored; runs after the provision) |
| Dev smoke (5 APIs + SSR + 2 SPAs + Functions + queue→Functions) | P2 | planned |
| Prod Bicep (`prod.bicepparam` + prod-only flags) | P3 | planned — **authored, NOT deployed** |

## Open questions / future evolution

- **Q-INFRA-01** (custom domain) — default **No for dev** (stable default hostnames); a prod + DNS concern.
- **Q-INFRA-02** (two subscriptions vs one-sub/two-RGs) — default **one sub, two RGs**; the Bicep is RG-scoped
  so a later split is a parameter.
- **Q-INFRA-03** (prod VNet/private-endpoint + Postgres-MI) — default **dev = public+firewall+MI-to-KV**; the
  prod Bicep leaves the seam (a module flag).
- **Future:** when prod is deployed, keep `prod.bicepparam` in review-sync with the dev modules (same modules,
  prod scale). A new host/country = **one more `appService` module instantiation** + a name — the seam.
- **Postgres-MI auth** (drop the connection-string secret entirely) — needs Npgsql token plumbing (a code
  change); revisit for prod hardening, not dev.
