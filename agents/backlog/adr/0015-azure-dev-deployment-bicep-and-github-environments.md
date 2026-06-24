# ADR-0015 — Azure DEV (re)deployment: a parameterized Bicep IaC source-of-truth for both environments, GitHub Environments (dev auto / prod protected), per-environment secrets, Key Vault references + managed identity, and a Bicep-gated rewrite of the deploy workflows

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** infra | devops | backend | db | cross-cutting (all 5 API hosts + Functions + SSR + 2 SPAs)
- **Extends:** the canonical `docs/architecture/infrastructure.md` (the authoritative resource inventory — this
  ADR turns that inventory into **Bicep IaC** and applies the owner's B1→B2 override; it does not contradict
  the doc). Consumes the existing OIDC federated-login + the in-pipeline `dotnet ef migrations bundle` path
  that the March-2026 `deploy-dev.yml`/`deploy-pro.yml` established. Pre-condition for **ADR-0013/0014** iOS
  (the iOS apps point at the stable dev API hosts this ADR provisions).
- **Ticket:** INFRA-ADR (this ADR) · **Consumers:** the Phase plan in `status/sprint-13.md` (P0 Bicep modules
  → P1 GH Environments + secret migration → P2 dev provision + workflow rewrite + smoke → P3 prod Bicep
  authored, **not** deployed). Living companion `architecture/decisions/azure-deployment.md`.

> This ADR freezes **how Cleansia is provisioned and deployed to Azure**: the Bicep module structure and where
> it lives, every resource and its dev SKU, the networking/security baseline, how config + secrets flow
> (App Service config / Key Vault references / GitHub-Environment secrets), the CI rewrite (Bicep-gated,
> OIDC-preserving, EF-bundle-preserving, GitHub-Environments-scoped), and the stable dev base URLs the iOS
> client points at. It ships **no Bicep and no YAML** — every concrete artifact is a consumer ticket. The
> Azure resources are currently **torn down**, so this is a **clean-slate re-provision**: Bicep is the
> unambiguous source of truth (there is no live resource to reconcile against), and the old imperative YAML is
> a **topology reference**, not something to preserve. Once `accepted` it is immutable — supersede, never edit.

> **Owner decisions this ADR is built to (locked):**
> (a) **B2** Linux App Service Plan for dev (the B1→B2 bump; the serialized-deploy workaround relaxes — see D5).
> (b) **Bicep for BOTH dev and prod**, parameterized (same modules, `env` param). **DEPLOY DEV ONLY**; author
>     prod Bicep but do not deploy it.
> (c) **GitHub Environments:** `dev` (auto-deploy on merge to master) + `prod` (**protected** — required
>     reviewers + manual approval). Migrate the flat `*_DEV`/`*_PRO` secrets into per-environment scopes.
> (d) **The architect recommends the re-provision strategy** — since resources are gone, clean-slate from
>     Bicep; this ADR makes the subscription/RG-layout, naming, and dev/prod-RG-separation calls (D1).

---

## Context

There **was** a working imperative deployment (`deploy-dev.yml` + `deploy-pro.yml`, March 2026) that reveals
the intended topology, and a canonical `docs/architecture/infrastructure.md` that already specifies the
resource inventory (App Service Plan, 4 App Services, 2 Static Web Apps, PostgreSQL Flexible, Storage Account,
Functions-as-Docker, Key Vault, Application Insights, Container Registry, West Europe, Key-Vault-RBAC + managed
identity). **The Azure resources are torn down** (owner confirmed) — so this is **clean-slate provisioning**,
which is the cheapest possible moment to set IaC: there is no drift to reconcile, Bicep is born as the source
of truth, and the old YAML's manual `az webapp ...` commands and portal-managed resources are replaced by
declarative modules.

### What the ground truth tells us (traced — not re-discovered)

**The runtime is FIVE API hosts + Functions, not four.** The AppHost (`src/Cleansia.AppHost/Program.cs`)
composes **partner-api** (`Cleansia.Web.Partner`, :5000), **admin-api** (`Cleansia.Web.Admin`, :5001),
**partner-mobile-api** (`Cleansia.Web.Mobile.Partner`, :5002), **customer-api** (`Cleansia.Web.Customer`,
:5003), **customer-mobile-api** (`Cleansia.Web.Mobile.Customer`, :5004), and **functions**
(`Cleansia.Functions`). The `.csproj` set confirms five web hosts: `Cleansia.Web.Partner`,
`Cleansia.Web.Admin`, `Cleansia.Web.Customer`, `Cleansia.Web.Mobile.Partner`, `Cleansia.Web.Mobile.Customer`.

**But the March-2026 deploy YAML only deployed FOUR App Services** —
`api-cleansia-{partner,admin,customer,mobile}-dev` — where `api-cleansia-mobile-dev` maps to the **partner**
mobile host (`Cleansia.Web.Mobile/Cleansia.Web.Mobile.Partner.csproj`). **The customer-mobile host
(`Cleansia.Web.Mobile.Customer`, :5004) was never deployed.** This is a **real gap**, not a naming detail:
ADR-0013 D4 fixes the iOS **customer** app to the customer-mobile host (`Cleansia.Web.Mobile.Customer`),
distinct from the partner-mobile host the iOS partner app uses. The iOS port therefore needs **two** mobile
dev hosts. This ADR provisions the customer-mobile App Service so the iOS customer app has a stable dev URL
(D2/D6). (The old YAML predates the partner-mobile/customer-mobile split and the Stripe-HttpOnly migration
that produced the dedicated customer-mobile host — see the AppHost comment at `Program.cs:60-69`.)

**Aspire is LOCAL-only; the cloud deploy does NOT use Aspire.** `src/Cleansia.AppHost` orchestrates the local
dev loop (containerized Postgres with a pinned password, **Azurite** with pinned ports 10000/10001/10002 for
blob/queue/table, all five hosts + Functions). The cloud deploy publishes the hosts **directly** to App
Services / Functions and points them at **real** Azure PostgreSQL + a real **Storage Account** (not Azurite).
This split is confirmed and **kept**: Bicep provisions the cloud resources; Aspire is never deployed.

**Storage is MANDATORY, for three distinct uses.** The code uses Azure **Blob** Storage
(`Cleansia.Infra.Azure.Storage.Blobs` — order photos, employee documents, generated receipt/invoice PDFs,
dispute evidence; containers `generated-receipts`/`generated-invoices`/`user-files`/`employee-documents`/
`order-photos` per the infra doc) **and** Azure **Storage Queues** (`Cleansia.Infra.Azure.Storage.Queues` —
the producer/consumer decoupling: `generate-receipt`/`generate-invoice` + poison queues drive the Functions)
**and** the Functions runtime's own `AzureWebJobsStorage`. The `appsettings.json` of the hosts carries
**three** distinct connection-string slots:
`ConnectionStrings:BlobContainerConfigurationConnectionString`,
`ConnectionStrings:QueueStorageConnectionString`, and `ConnectionStrings:ConnectionString` (the DB) — all
`UseDevelopmentStorage=true` / `SET_VIA_USER_SECRETS` locally. So a dev **Storage Account is not optional**;
without it, receipts/invoices/photos and the entire queue→Functions pipeline are dead.

**Functions MUST be a container (ACR), not a code deploy.** `docs/architecture/infrastructure.md` and the
`Cleansia.Functions/Dockerfile` are explicit: QuestPDF (the PDF engine) needs native Linux libraries
(`libfontconfig1`, `libfreetype6`) installed in the image. A non-container Functions deploy would lack them and
PDF generation would fail at runtime. The old YAML's `az acr build` + `az functionapp config container set` is
**kept**; ACR stays in the topology (D2).

**Config/secret shape today is flat `*_DEV`/`*_PRO` GitHub secrets** (`DB_CONNECTION_STRING_DEV`,
`AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_DEV`, `ACR_NAME`, the three `AZURE_*` OIDC ids). The canonical infra
doc already prescribes the **target** end-state: **Key Vault** holding app secrets (`Jwt--Key`,
`ConnectionStrings--cleansia-db`, `Stripe--SecretKey`/`--WebhookSecret`, `SendGrid--ApiKey`, `Sentry--Dsn`,
`Storage--ConnectionString`), each App Service + Functions with a **system-assigned managed identity** holding
**Key Vault Secrets User**, and CI holding **Key Vault Secrets Officer**. This ADR adopts that target (D4) —
the clean-slate is the moment to do it right rather than re-create the flat raw-secret-into-app-config form.

**Observability is currently absent in the YAML but canonical in the doc.** The infra doc specifies
**Application Insights** (P95 latency, 5xx rate, poison-queue depth, DB-connection-failure, Function-failure
alerts) for all APIs + Functions, and the code already calls Sentry. This ADR provisions **Application
Insights + a Log Analytics workspace** (App Insights' modern backing store) in Bicep (D2) — the clean-slate is
when to add it, since retrofitting telemetry wiring across five hosts later is more expensive.

**OIDC federated login is already in place and good.** `AZURE_CLIENT_ID`/`TENANT_ID`/`SUBSCRIPTION_ID` with
`azure/login@v2` and `permissions: id-token: write` — **no stored cloud credentials**. This is kept exactly
(D5). The EF-migration **bundle** step (`dotnet ef migrations bundle` built in-pipeline, applied against the
dev DB **before** the API deploys) is the **one** sanctioned place CI runs migrations — the CLAUDE.md
owner-only-migration rule governs **local** dev; this established CI bundle-apply is the prod/dev path and is
**kept**, with any *schema change* still owner-gated (a migration must be authored + reviewed; CI only
*applies* an already-committed migration). (D5.)

**Why this is ONE decision.** The Bicep module structure, the resource SKUs, the security baseline, the
config/secret flow, the CI rewrite, and the dev base URLs are **inseparable**: the secret model (Key Vault +
managed identity) determines what the Bicep modules must emit (identities, role assignments, Key Vault
references) **and** how the workflow injects config; the GitHub-Environments split determines where secrets
live **and** how the prod gate works; the Functions-as-container choice forces ACR into both the Bicep and the
workflow; the five-hosts-not-four correction changes the App Service module's count **and** the iOS base-URL
list. Splitting this would let the secret model be decided without the modules that must implement it, or the
environment split chosen without the workflow that enforces it. The *implementation* is split into the Phase
tickets.

---

## Decision

> **Contract principle.** Cleansia's Azure footprint is a **single parameterized Bicep deployment**
> (`main.bicep` + per-resource modules) living at **`deploy/bicep/`**, the **source of truth** for both `dev`
> and `prod` (one module set, an `env` parameter + per-env `*.bicepparam`). **Dev is provisioned now; prod
> Bicep is authored but not deployed.** Dev runs on a **B2 Linux** App Service Plan hosting **five** API App
> Services (partner, admin, customer, **partner-mobile**, **customer-mobile**) + the customer **SSR** App
> Service; the two **SPAs** are Static Web Apps; **Functions deploys as a container via ACR**; **PostgreSQL
> Flexible Server (Burstable B1ms)**, a single **Storage Account (LRS)** for blob+queue+Functions-runtime, a
> **Key Vault**, and **Application Insights + Log Analytics** complete the set — all in **`rg-cleansia-dev`**
> (West Europe), with a **separate `rg-cleansia-prod`** under the **same subscription**. App config flows via
> **App Service settings that are Key Vault references**, resolved by each host's **system-assigned managed
> identity** (Key Vault Secrets User); **no real secret is ever committed**. The CI is rewritten to: a
> **Bicep deploy** gate (`az deployment group create`, what-if on PR), **GitHub Environments** (`dev`
> auto-on-merge, `prod` protected with required reviewers + manual approval), **preserved OIDC** and
> **preserved EF-bundle migrate-before-deploy**, and **parallelized** API deploys (B2 lets the serialized
> chain relax — D5). The iOS apps point at the **five stable `api-cleansia-*-dev.azurewebsites.net` hosts**
> (custom domain deferred — D6).

### D1 — Layout, naming, RG/subscription model: `deploy/bicep/`, one subscription, two resource groups, env-parameterized

**Where it lives: `deploy/bicep/`.** The repo already has `deploy/` (the local Postgres `docker-compose`).
Bicep lives beside it at **`deploy/bicep/`** — co-located with the other deployment artifacts, discoverable,
and not a new top-level `infra/` tree (the project's convention is `deploy/` for deployment assets). The
workflows stay in `.github/workflows/`.

**Module structure (`main.bicep` orchestrates per-resource modules):**

```
deploy/bicep/
├── main.bicep                      # subscription- or RG-scoped orchestrator; wires modules; passes `env`
├── dev.bicepparam                  # env='dev', B2 plan, B1ms Postgres, the dev names
├── prod.bicepparam                 # env='prod', S1/GP plan, GP Postgres (AUTHORED, NOT deployed)
└── modules/
    ├── appServicePlan.bicep        # Linux plan; SKU by env (B2 dev / S1 prod)
    ├── appService.bicep            # ONE reusable module, instantiated 6× (5 APIs + SSR); takes name,
    │                               #   runtime (dotnet / node), app-settings (Key Vault refs), identity
    ├── staticWebApp.bicep          # instantiated 2× (partner SPA, admin SPA)
    ├── functionApp.bicep           # container Functions (Linux, ACR image ref) + its app settings
    ├── acr.bicep                   # Azure Container Registry (Basic) — Functions image
    ├── postgres.bicep              # PostgreSQL Flexible Server (Burstable B1ms dev) + DB + firewall
    ├── storage.bicep               # Storage Account (LRS) — blob containers + the queues + Functions store
    ├── keyVault.bicep              # Key Vault (RBAC) + role assignments (app MIs = Secrets User; CI = Officer)
    ├── appInsights.bicep           # Application Insights + Log Analytics workspace
    └── roleAssignments.bicep       # (if not folded into each module) MI→KeyVault, MI→Storage, MI→ACR pulls
```

- **One reusable `appService.bicep`, instantiated six times** (the 5 APIs + the SSR Node host), parameterized
  by name/runtime/app-settings/identity. This is the seam that makes adding a country/host cheap — a new host
  is one more module instantiation + one name, not a new bespoke block. The five API names follow the existing
  convention extended for the missing host: `api-cleansia-partner-dev`, `api-cleansia-admin-dev`,
  `api-cleansia-customer-dev`, `api-cleansia-partner-mobile-dev`, `api-cleansia-customer-mobile-dev`; SSR =
  `web-cleansia-customer-dev`; SPAs = the two Static Web Apps. **Naming-migration note:** the old YAML's
  `api-cleansia-mobile-dev` (which was the partner-mobile host) is **renamed** to
  `api-cleansia-partner-mobile-dev` for clarity now that both mobile hosts exist; the customer-mobile host is
  the **new** `api-cleansia-customer-mobile-dev`. Because the resources are torn down, this rename costs
  nothing (no live host to rename).
- **One subscription, two resource groups: `rg-cleansia-dev` and `rg-cleansia-prod`.** This matches the old
  YAML's `rg-cleansia-dev`/`rg-cleansia-pro` (we standardize on `-prod` for the new RG; the old `-pro` resource
  group is gone with the teardown so there is nothing to keep compatible). **RG-level isolation, shared
  subscription** is the dev-pragmatic call: it gives a clean blast-radius boundary (a `dev` Bicep apply can
  never touch a prod resource; RBAC and the protected `prod` GitHub Environment gate prod separately) without
  the cost/overhead of two subscriptions for a project this size. (If the owner later wants hard billing/policy
  isolation, separate subscriptions is a future change — recorded as Q-INFRA-02, non-blocking; the Bicep is
  scoped per-RG so the move is a parameter, not a rewrite.)
- **`env` parameter drives every difference.** SKUs (B2 vs S1, B1ms vs GP D2s_v3), names (`-dev`/`-prod`
  suffix), Static-Web-App tier (Free vs Standard), and Storage redundancy are all `env`-switched in the
  `.bicepparam` files. **Same modules, two parameter files** — the prod topology is provably the dev topology
  at a different scale, which is the whole point of one IaC source.

### D2 — The resources and their dev SKUs (codifying `infrastructure.md` + the owner's B2 override)

| Resource | Bicep module | Dev SKU/tier | Notes (vs the canonical infra doc) |
|---|---|---|---|
| **App Service Plan** | `appServicePlan` | **B2 Linux** (owner override of the doc's B1) | One plan hosts all 6 App Services (5 APIs + SSR). B2 = 2 cores / 3.5 GB — the headroom that relaxes the serialized deploy (D5). |
| **API App Services ×5** | `appService` (×5) | on the B2 plan | partner / admin / customer / **partner-mobile** / **customer-mobile** — the five-not-four correction. .NET 10, HTTPS-only, system-assigned MI. |
| **Customer SSR App Service** | `appService` (Node) | on the B2 plan | `web-cleansia-customer-dev`, startup `node server/server.mjs` (kept from the YAML). |
| **Static Web Apps ×2** | `staticWebApp` (×2) | **Free** (dev) | Partner SPA + Admin SPA. |
| **Functions** | `functionApp` | **Consumption, Linux container** | Image from ACR (QuestPDF native deps — mandatory container). MI + Key Vault + Storage access. |
| **Container Registry** | `acr` | **Basic** | Holds `cleansia-functions:<sha>`. |
| **PostgreSQL Flexible** | `postgres` | **Burstable B1ms** (dev) | West Europe. Public access + firewall (D3). One `Cleansia` database. |
| **Storage Account** | `storage` | **Standard LRS** | The blob containers + the queues (+ poison) + `AzureWebJobsStorage`. **Mandatory** (blob+queue+Functions). |
| **Key Vault** | `keyVault` | **Standard, RBAC** | App secrets; MIs = Secrets User; CI = Secrets Officer (D4). |
| **Application Insights + Log Analytics** | `appInsights` | **Basic / pay-as-you-go LA** | All 5 APIs + SSR + Functions emit telemetry (D3/observability). New vs the YAML, canonical in the doc. |

- **Functions stays a container (ACR)** — **not** flipped to a code deploy. Recommendation made and held:
  QuestPDF's native `libfontconfig1`/`libfreetype6` requirement (the `Dockerfile` + infra doc) makes the
  container non-negotiable; a code/zip Functions deploy would fail PDF generation at runtime. The only cost is
  keeping ACR + the `az acr build` step, which the workflow already had.
- **Application Insights + Log Analytics is ADDED** (recommendation): the infra doc already designs the alert
  set (P95 > 2s, 5xx > 1%, poison-queue depth > 0, DB-connection failures, Function failures), and wiring
  telemetry across five hosts is cheapest at clean-slate. Sentry stays as the app already uses it; App
  Insights is the platform/infra telemetry + alerting layer.

### D3 — Networking/security baseline for dev (pragmatic, not over-built)

- **HTTPS-only on every App Service + the SSR + Functions** (`httpsOnly: true`, min TLS 1.2). The two SPAs are
  HTTPS by default on Static Web Apps.
- **PostgreSQL: public endpoint + firewall, NOT VNet** (the dev-pragmatic call). Firewall allows **Azure
  services** (so the App Services / Functions reach it) and the owner's admin IP for the EF-bundle apply +
  manual access. `require_secure_transport=on`. **Rationale:** VNet integration + a private endpoint for
  Postgres is a prod-hardening concern; for dev it adds NAT/private-DNS complexity for little gain. Recorded as
  the dev posture; **prod Bicep may tighten to VNet/private-endpoint** (a `prod.bicepparam` + module flag,
  Q-INFRA-03, non-blocking — the seam exists). The CI EF-bundle job reaches the DB through the firewall
  (the OIDC login + the connection string), exactly as the old YAML did.
- **Managed identity for app → Key Vault** (D4) — the app secrets path uses MI, **no connection-string secret
  for Key Vault access**. **Postgres access stays a connection string** (the canonical
  `ConnectionStrings--cleansia-db` secret in Key Vault, referenced by the app) rather than Postgres AAD/MI auth
  — Postgres MI auth is a larger lift (token plumbing in the Npgsql data source) and is recorded as a possible
  prod hardening (Q-INFRA-03), not a dev requirement. **Storage access via MI where feasible** (the app's
  `BlobServiceClient`/`QueueClient` can take a `DefaultAzureCredential` against the Storage Account with the MI
  holding **Storage Blob Data Contributor** + **Storage Queue Data Contributor**); the **Functions runtime
  store** (`AzureWebJobsStorage`) can also be MI-based on a modern Functions host. The `Storage--ConnectionString`
  Key Vault secret is kept as the **fallback** the current code reads (`BlobContainerConfigurationConnectionString`
  / `QueueStorageConnectionString` config slots) so no app code change is forced by this ADR — MI is the
  preferred path, the connection string is the compatible path the code already supports.
- **CORS** — each API host's `CorsOrigins` (today `localhost:4200/4201` in `appsettings.json`) is set, **per
  host, via app settings from Bicep**, to the **dev** SPA + SSR origins (the Static Web App default hostnames +
  `web-cleansia-customer-dev`), not localhost. The mobile hosts (partner-mobile, customer-mobile) are
  body-token JWT with **no cookies/CSRF** (AppHost comment + ADR-0013) so they do not need browser CORS for the
  native clients; their CORS stays closed/minimal. The per-audience host separation (the Cleansia seam) is
  preserved: each host gets its own CORS + config, and a change to one host's origins does not touch another.
- **Public exposure:** all five APIs + SSR + the two SPAs are public (they must be — the mobile/customer hosts
  serve native + browser clients, the Stripe webhook hits the customer host). There is **no** inter-service
  east-west call between the API hosts that needs private networking (they share the DB + Storage, not each
  other) — the queues are the only cross-process channel and they go through the Storage Account. So the dev
  baseline is: public + HTTPS-only + firewalled Postgres + MI-to-KeyVault/Storage. No VNet for dev.

### D4 — Config & secrets: Key Vault references + managed identity; per-environment GitHub secrets; nothing real committed

The **standing rule holds: no real secret is ever committed** — not in Bicep, not in `.bicepparam`, not in YAML.
The flow, in three tiers:

1. **App runtime config → App Service settings that are Key Vault references.** Each App Service / Functions
   app gets app settings like `ConnectionStrings__ConnectionString =
   @Microsoft.KeyVault(SecretUri=https://kv-cleansia-dev.../secrets/ConnectionStrings--cleansia-db)`. The host's
   **system-assigned managed identity** (provisioned by the `appService`/`functionApp` module, granted **Key
   Vault Secrets User** by `keyVault`/`roleAssignments`) resolves them at runtime. This is exactly the canonical
   infra-doc model. The app reads the same config keys it reads today (`ConnectionStrings:ConnectionString`,
   `Stripe:SecretKey`, `SendGrid:ApiKey`, `Sentry:Dsn`, the two storage connection-string slots) — **App
   Service config double-underscore → colon mapping** means **no app code change**.
2. **Bicep emits the structure, never the values.** The Bicep declares the Key Vault, the secret *names* (the
   `infrastructure.md` inventory: `Jwt--Key`, `ConnectionStrings--cleansia-db`, `Stripe--SecretKey`/
   `--WebhookSecret`, `SendGrid--ApiKey`, `Sentry--Dsn`, `Storage--ConnectionString`), the MIs, and the role
   assignments — but the **secret values are populated by the owner** (portal / `az keyvault secret set`) or by
   a guarded CI step that reads a **GitHub-Environment secret** and writes it to Key Vault (CI holds **Secrets
   Officer**). The Bicep parameter files carry **only non-secret** values (names, SKUs, regions, the Postgres
   *admin login name* — the *password* is a secret).
3. **CI/provisioning secrets → per-environment GitHub Environment secrets** (D5): the OIDC `AZURE_CLIENT_ID`/
   `TENANT_ID`/`SUBSCRIPTION_ID`, the `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER`/`_ADMIN`, `ACR_NAME`, and the
   bootstrap `DB_CONNECTION_STRING` (for the EF-bundle apply) move from flat `*_DEV`/`*_PRO` names into the
   **`dev`** and **`prod`** Environment scopes (so the name is just `AZURE_CLIENT_ID` within each Environment).

**Where do Stripe / SendGrid / Mapbox / Firebase(APNs) keys live for dev?** In **Key Vault** as the secret
inventory above (`Stripe--SecretKey`/`--WebhookSecret`, `SendGrid--ApiKey`; Mapbox `GeocodingAccessToken` —
needed only if the backend geocoding is exercised in dev — as a `Mapbox--GeocodingAccessToken` secret; the iOS
**APNs** auth key is an Apple-Developer artifact the *push* path uses, not an Azure secret, and is the
ADR-0013 owner step — it is **not** an Azure deploy secret). **Dev uses Stripe *test* keys and a SendGrid dev
sender** — the values are owner-populated; the ADR mandates the slot, not the value. The owner populating these
is a **`manual_step` (owner-only)** on the provision ticket.

### D5 — The CI rewrite: Bicep-gated, OIDC-preserved, EF-bundle-preserved, GitHub-Environments-scoped, parallelized on B2

The two workflows are rewritten (not patched — the resources are gone, so the imperative `az webapp create`/
manual-portal assumptions are replaced by declarative provision). **What changes and what is kept:**

- **(a) Provision/update via Bicep as a gated step.** A new **`provision`** job runs
  `az deployment group create --resource-group rg-cleansia-<env> --template-file deploy/bicep/main.bicep
  --parameters deploy/bicep/<env>.bicepparam` **before** the app deploys. On a **PR**, it runs
  `az deployment group what-if` (a non-mutating preview the reviewer reads) so infra changes are reviewable like
  code. The provision job is **idempotent** (Bicep is declarative) — re-running it is safe and is how the dev
  env stays in sync with the committed Bicep.
- **(b) GitHub Environments.** `deploy-dev.yml` runs on **push to master**, all jobs scoped to the **`dev`**
  Environment (auto-deploy). `deploy-prod.yml` runs on **`workflow_dispatch`** with every deploy job scoped to
  the **`prod`** Environment, which has **required reviewers + a manual approval gate** configured in GitHub
  (so a prod deploy *pauses* for human approval before any mutating step — the protection the owner mandated).
  The old `confirm: "deploy"` typed-input check is **replaced** by the Environment's reviewer gate (a stronger,
  GitHub-native control). Per-environment **secrets** are read by name within each Environment scope.
- **(c) OIDC + the EF-migration bundle are KEPT verbatim in spirit.** `azure/login@v2` with the three OIDC ids
  (now per-Environment), `permissions: id-token: write`. The **`migrate-database`** job still builds
  `dotnet ef migrations bundle` and applies it **before** the API deploys, against the env DB. **Schema-change
  governance:** CI only *applies* a migration that is **already committed** (authored + reviewed per the
  owner-gated rule); the pipeline never *creates* one. The job runs after `provision` (the DB must exist) and
  before the API deploys (the schema must be current when the new code starts).
- **(d) Parallelize the API deploys (B2 relaxes the serial chain).** The old YAML serialized all 8 deploy jobs
  (`needs: deploy-previous`) **specifically to avoid overloading B1** (the YAML comment says so). On **B2**
  (2 cores / 3.5 GB) the App Services have headroom; the API + SSR deploys can run **in parallel** (each
  `needs: [provision, migrate-database]` only, not each other). **Recorded judgment:** parallel deploy is
  **safe on B2** for the deploy operation itself (each `azure/webapps-deploy` pushes a package to a different
  App Service; the load is the brief restart, which B2 absorbs across 6 apps far better than B1). The SPAs
  (Static Web Apps) were always independent. **Net:** the deploy fans out instead of chaining — faster, and the
  ordering that *matters* (migrate-before-deploy) is preserved by the `needs: migrate-database` edge, not by the
  serial chain. (If a parallel restart ever shows contention on B2, the fallback is to re-introduce a small
  `max-parallel` — a workflow tweak, not an architecture change.)
- **The Functions container path is kept:** `az acr build` (image `cleansia-functions:<sha>`) →
  `az functionapp config container set` → restart, now reading the per-Environment `ACR_NAME`.

### D6 — The iOS connection: five stable dev base URLs; custom domain deferred

The iOS apps (ADR-0013/0014) and the regenerated mobile specs point at the **default Azure hostnames** of the
two **mobile** dev hosts:

- iOS **partner** app → `https://api-cleansia-partner-mobile-dev.azurewebsites.net`
  (host `Cleansia.Web.Mobile.Partner`).
- iOS **customer** app → `https://api-cleansia-customer-mobile-dev.azurewebsites.net`
  (host `Cleansia.Web.Mobile.Customer` — the host this ADR adds that the old YAML omitted).

The web/SSR/SPA dev URLs are the partner/admin/customer API hosts + the customer SSR App Service + the two
Static Web App default hostnames. **Custom domain is DEFERRED for dev** (the `*.azurewebsites.net` /
`*.azurestaticapps.net` defaults are stable, TLS-terminated, and sufficient for a dev environment a Mac points
at). A custom domain (`*.cleansia.cz`) is a **prod** concern and an owner DNS step — recorded as Q-INFRA-01
(non-blocking). The iOS base-URL config is environment-switched in the app (a dev scheme/config pointing at
these hosts), so when prod + a custom domain land, it is a config change, not a code change. **The dev hosts
being stable (declared in Bicep, not torn down) is the whole point of this ADR for the iOS pivot** — the Mac
points at a fixed dev API instead of running all five hosts + Functions + Postgres + Azurite locally.

### D7 — Scope guard

This ADR decides the **provisioning + deployment architecture**. It does **not**: write the Bicep or YAML
(consumer tickets); change any application code (the config keys, the connection-string slots, the
`BlobServiceClient`/`QueueClient` usage all stay — MI-vs-connection-string is a config choice the existing code
already supports); author a **prod deploy** (prod Bicep is *authored* in P3, **not applied**); add a new
country/host/provider (the reusable `appService` module is the seam for that later); or change the fiscal /
pay / tenancy seams (deployment is orthogonal to them). A future move to two subscriptions (Q-INFRA-02), a
custom domain (Q-INFRA-01), or VNet/private-endpoint + Postgres-MI prod hardening (Q-INFRA-03) is revisited
against this ADR (a new ADR if it changes the security model; a living-doc note if it only flips an `env`
parameter).

---

## Alternatives considered

- **Keep the imperative `az webapp ...` YAML; skip Bicep.** Rejected (owner decision (b) + the clean-slate).
  With the resources torn down, *something* must create them; doing it imperatively in YAML (or by portal
  clicks) reproduces the exact drift-prone, undocumented state that made the teardown unrecoverable from code.
  Bicep makes the topology declarative, reviewable (what-if on PR), and identical between dev and prod by
  construction. The clean-slate is the cheapest moment to adopt IaC — there is nothing to reconcile.
- **Terraform instead of Bicep.** Rejected (owner decision (b) names Bicep). Bicep is Azure-native (no state
  backend to manage, first-class ARM what-if, no provider versioning), the target is Azure-only, and the
  canonical infra doc is already Azure-shaped. Terraform's multi-cloud strength is irrelevant here and its
  remote-state management is overhead for a single-cloud, two-RG footprint.
- **Separate subscriptions for dev and prod.** Rejected as the dev default (D1); recorded as Q-INFRA-02. Two
  RGs under one subscription give clean blast-radius isolation (a `dev` apply cannot touch prod; the protected
  `prod` Environment gates prod deploys) without the billing/governance overhead of two subscriptions for a
  project this size. The Bicep is RG-scoped, so a later split to two subscriptions is a parameter change, not a
  rewrite — the seam is preserved.
- **Flip Functions to a non-container (code/zip) deploy.** Rejected (D2). QuestPDF needs native Linux libs
  (`libfontconfig1`/`libfreetype6`) baked into the image (`Cleansia.Functions/Dockerfile` + infra doc); a code
  deploy would lack them and PDF (receipt/invoice) generation would fail at runtime. The container + ACR is
  mandatory, not a preference.
- **Deploy only four App Services (keep the old YAML's host set).** Rejected — it is the **bug** the ground
  truth exposed. The runtime has **five** hosts (the AppHost + the `.csproj` set); the old YAML omitted the
  **customer-mobile** host (`Cleansia.Web.Mobile.Customer`), which is exactly the host the iOS **customer** app
  (ADR-0013 D4) must reach. Re-provisioning four hosts would leave the iOS customer app with no dev API. The
  ADR provisions all five.
- **Raw GitHub secrets injected straight into App Service config (no Key Vault).** Rejected (D4). The canonical
  infra doc prescribes Key Vault + managed identity, and the clean-slate is when to adopt it. Key Vault gives
  one audited secret store, rotation without redeploying app config, and MI access with no secret-to-read-the-
  secrets. Raw GH-secret-into-app-config spreads secret material across N app configs and couples every
  rotation to a redeploy. (GitHub Environment secrets are still used for the **CI/provisioning** credentials —
  OIDC, SWA tokens, ACR name, the bootstrap DB connection string — which is the right home for *pipeline*
  secrets.)
- **Skip Application Insights / Log Analytics for dev (Sentry is enough).** Rejected (D2/D3). The infra doc
  designs an alert set (poison-queue depth, 5xx rate, DB-connection failures) that is **infra/platform**
  telemetry Sentry (app-level error tracking) does not cover, and wiring App Insights across five hosts later
  is more expensive than at clean-slate. Dev gets the Basic tier; it is cheap and proves the wiring before prod.
- **VNet + private endpoints for Postgres/Storage in dev.** Rejected as the dev baseline (D3); recorded as
  Q-INFRA-03 (a prod-hardening option). For dev, public-endpoint-with-firewall + MI-to-KeyVault/Storage is the
  pragmatic posture; VNet adds private-DNS/NAT complexity for little dev benefit. The prod Bicep can flip it on
  via a parameter — the module structure leaves the seam.
- **Postgres AAD/managed-identity auth instead of a connection-string secret.** Rejected for dev (D3/D4);
  recorded as Q-INFRA-03. It requires token plumbing in the Npgsql data source (a code change) for marginal dev
  benefit; the connection-string-in-Key-Vault path is what the code reads today and is secure (MI reads the
  secret; the secret is never committed). Prod may adopt it later.
- **Keep the serialized deploy chain on B2 (don't parallelize).** Rejected (D5). The serialization existed
  *only* to protect B1 (the YAML comment is explicit); on B2 the App Services have the headroom to deploy in
  parallel, and the *correct* ordering (migrate-before-deploy) is preserved by the `needs: migrate-database`
  edge, not by chaining the app deploys. Parallel is faster with the real ordering intact; a `max-parallel`
  throttle is the cheap fallback if B2 ever shows contention.
- **A custom domain for dev now.** Rejected (D6); recorded as Q-INFRA-01. The default `*.azurewebsites.net`
  hostnames are stable and TLS-terminated — sufficient for the Mac-points-at-dev goal. A custom domain is a prod
  concern + an owner DNS step; the iOS base-URL is env-switched config, so adding a domain later is config, not
  code.

---

## Consequences

**Cheaper / safer:**
- **The Azure footprint is declarative, reviewable, and reproducible.** A torn-down environment is re-creatable
  from `deploy/bicep/` in one `az deployment group create`; infra changes are reviewed via `what-if` on PR;
  dev and prod are the **same** modules at different SKUs, so prod cannot silently diverge from dev's proven
  shape. This is the structural fix for the "resources got torn down and were not recoverable from code" state.
- **The five-hosts correction unblocks the iOS pivot.** Provisioning the missing **customer-mobile** dev host
  gives the iOS customer app a stable dev URL — the deploy now matches the runtime, and the Mac points at fixed
  dev hosts instead of running the full local stack.
- **Secrets are centralized + MI-accessed.** Key Vault + managed identity means no secret in app config, no
  secret in the repo, rotation without redeploy, and one audited store — the canonical model, adopted at the
  cheapest moment.
- **The seams are preserved.** One reusable `appService` module = adding a host/country is a module instance,
  not a bespoke block; the per-audience CORS/config-per-host separation holds (a change to one host's config
  does not touch another); the `env` parameter is the single point of dev/prod variation.
- **CI is faster and properly gated.** Bicep-gated provision, OIDC (no stored creds), migrate-before-deploy
  preserved, prod behind a GitHub-native required-reviewer gate, dev auto-on-merge, API deploys parallelized.

**More expensive (new obligations):**
- **A new `deploy/bicep/` tree** (main + ~10 modules + 2 param files) and a **rewritten** `deploy-dev.yml` +
  `deploy-prod.yml` — real authoring work (the P0/P2 tickets).
- **Owner-only provisioning steps** (D4/D5): create the two **GitHub Environments** (`dev` auto, `prod` with
  required reviewers), **migrate the flat `*_DEV`/`*_PRO` secrets** into the per-environment scopes, **populate
  the Key Vault secret values** (DB connection string, JWT key, Stripe *test* keys, SendGrid key, Sentry DSN,
  Storage connection string, Mapbox token), grant the CI principal **Key Vault Secrets Officer**, and run (or
  approve) the **first `az deployment group create`** for dev. These are portal/subscription/secret-value steps
  an agent cannot do.
- **Application Insights + Log Analytics + Key Vault + ACR** are now standing dev resources (cost ~the infra
  doc's ~$66/mo dev estimate, slightly higher with the B2 bump + App Insights) — accepted for a stable dev env.
- **Schema-change governance must be honored:** CI applies the EF bundle, but a *new* migration is still
  owner-authored + reviewed before it is committed; the pipeline never creates one. A reviewer/PM must treat any
  PR carrying a new migration as owner-gated.
- **The prod Bicep is authored but unapplied** — a real artifact that must be kept in sync with dev's modules
  until the owner decides to deploy prod (it is the *same* modules, so drift risk is low, but the param file is
  prod-specific and must be reviewed before any future prod apply).

**Rollout (consumer tickets — see `status/sprint-13.md`):**
- **P0:** Bicep modules + `main.bicep` + `dev.bicepparam` authored + reviewed (what-if-clean against a dry run).
- **P1:** GitHub Environments created + the flat secrets migrated into the `dev`/`prod` scopes (owner) + the
  Key Vault secret values populated (owner).
- **P2:** dev provisioned (`az deployment group create` — owner-run/approved) + the rewritten `deploy-dev.yml`
  + a smoke that all 5 APIs + SSR + 2 SPAs + Functions are reachable and healthy.
- **P3:** prod Bicep (`prod.bicepparam` + any prod-only module flags) authored — **not deployed**.

---

## How a reviewer verifies compliance

**Mechanical / structural (the gate):**
1. **Bicep is the source of truth, in `deploy/bicep/`.** `main.bicep` + per-resource `modules/*.bicep` +
   `dev.bicepparam` + `prod.bicepparam` exist; there is **one reusable `appService.bicep`** instantiated for
   the **five** APIs + the SSR (not five bespoke blocks); the prod param file exists and is **not** referenced
   by `deploy-dev.yml`.
2. **Five API App Services, named correctly.** `api-cleansia-partner-dev`, `-admin-dev`, `-customer-dev`,
   `-partner-mobile-dev`, **`-customer-mobile-dev`** are all declared (the customer-mobile host is present — its
   absence is a blocking finding, since the iOS customer app depends on it). SSR = `web-cleansia-customer-dev`;
   two Static Web Apps.
3. **Functions is a container from ACR.** The `functionApp` module references an ACR image; the workflow does
   `az acr build` + `az functionapp config container set`; there is **no** code/zip Functions deploy.
4. **No secret committed anywhere.** Grep the Bicep, `.bicepparam`, and YAML: **no** real connection string,
   JWT key, Stripe/SendGrid/Mapbox/Sentry value. Secrets are **Key Vault references** in app settings
   (`@Microsoft.KeyVault(SecretUri=...)`) and Key Vault *names* in Bicep; values are owner/CI-populated. Any
   literal secret is a blocking finding (the standing rule).
5. **Managed identity + Key Vault RBAC.** Each App Service / Functions has a system-assigned MI; the `keyVault`/
   `roleAssignments` module grants the app MIs **Key Vault Secrets User** and the CI principal **Secrets
   Officer**; Storage access grants the MIs the Blob/Queue Data Contributor roles.
6. **HTTPS-only + firewalled Postgres.** Every App Service + SSR + Functions has `httpsOnly: true`, min TLS 1.2;
   Postgres has `require_secure_transport=on` + a firewall (Azure services + the admin IP), **no** public
   open-to-all rule; no VNet for dev (the dev posture).
7. **CORS per host from Bicep.** Each API host's `CorsOrigins` app setting is the **dev** SPA/SSR origins (not
   `localhost`); the mobile hosts' CORS is closed/minimal (body-token, no cookies). No host's CORS is copied
   into another (the per-audience seam).
8. **OIDC + migrate-before-deploy preserved.** The workflows use `azure/login@v2` + `id-token: write` with
   per-Environment OIDC ids (no stored cloud creds); the `migrate-database` job builds the EF bundle and runs
   **before** the API deploys (`needs: migrate-database` on every API/SSR deploy); CI only *applies* a
   committed migration (never `migrations add`).
9. **GitHub Environments + the prod gate.** `deploy-dev.yml` runs on push-to-master scoped to `dev`;
   `deploy-prod.yml` is `workflow_dispatch` with all deploy jobs scoped to `prod` (required reviewers + manual
   approval). Secrets are read per-Environment (no flat `*_DEV`/`*_PRO` names in the rewritten workflows).
10. **Bicep gate on PR.** A PR touching `deploy/bicep/**` runs `az deployment group what-if` (a reviewable,
    non-mutating preview); the mutating `az deployment group create` runs only on the dev push / the approved
    prod dispatch.
11. **Parallelized API deploys with ordering intact.** The API/SSR deploy jobs `needs: [provision,
    migrate-database]` (not each other) — they fan out; the migrate-before-deploy ordering is preserved by the
    edge, not by a serial chain. (A reviewer confirms the parallelization did not drop the migrate edge.)

**Smoke contract (the P2 verification — what "dev is up" means):**
- **SMOKE-API:** all five `api-cleansia-*-dev.azurewebsites.net` hosts return healthy on their health endpoint
  (the partner host has `HealthController`); a login against the partner-mobile + customer-mobile hosts issues a
  token (the iOS connection works).
- **SMOKE-STORAGE:** an order-photo upload writes to the `order-photos` blob container; a payment enqueues
  `generate-receipt` and the Functions container produces the PDF into `generated-receipts` (the queue→Functions
  pipeline is live end-to-end).
- **SMOKE-WEB:** the customer SSR host renders; the two SPAs load and reach their API hosts (CORS correct).
- **SMOKE-MIGRATE:** the `migrate-database` job applied the committed migrations to the dev DB before deploy
  (the schema matches the deployed code).

---

## Roles affected

No domain-aggregate role changes (deployment is orthogonal to the domain). The catalog edit (same change as this
ADR, per the pattern-evolution loop): `agents/knowledge/patterns-backend.md` gains a short **Deployment / IaC**
note cross-referencing ADR-0015 (Bicep-in-`deploy/bicep/`, one reusable `appService` module, Key-Vault-refs +
MI for config, GitHub Environments dev-auto/prod-protected, OIDC + EF-bundle preserved, Functions-as-container
mandatory); `conventions.md` notes "no secret in Bicep/param/YAML — Key Vault names only" as an enforceable
rule. The living companion `agents/architecture/decisions/azure-deployment.md` is created in parallel and is the
evolving home for the topology diagram + the dev/prod SKU table + the resource→secret map.

---

## Open questions raised (owner — all non-blocking for the DEV provision; defaults taken)

Filed in `agents/backlog/questions/open.md`:
- **Q-INFRA-01 (`pre-prod` for prod only; non-blocking for dev, owner)** — **custom domain** for the
  environments (`*.cleansia.cz`)? **Default taken:** **No for dev** — use the stable `*.azurewebsites.net` /
  `*.azurestaticapps.net` defaults; a custom domain is a prod concern + an owner DNS step. The iOS base-URL is
  env-switched config, so adding a domain later is config, not code.
- **Q-INFRA-02 (`post-prod`, owner)** — **two subscriptions** for hard dev/prod billing+policy isolation, or
  keep **one subscription, two RGs**? **Default taken:** one subscription, two RGs (clean blast-radius without
  the overhead). The Bicep is RG-scoped, so a later split is a parameter change.
- **Q-INFRA-03 (`pre-prod` for prod hardening; non-blocking for dev, owner)** — **prod** network/auth
  hardening: VNet + private endpoints for Postgres/Storage, and Postgres **AAD/MI auth** instead of a
  connection-string secret? **Default taken for dev:** public-endpoint + firewall + MI-to-KeyVault/Storage,
  connection-string-in-Key-Vault for Postgres. The prod Bicep leaves the seam (a module flag) to flip these on
  before prod go-live.

These do **not** block the **dev** provision. The **owner manual steps** (GitHub Environments + reviewers,
secret migration, Key Vault secret-value population, running/approving the first `az deployment group create`,
the subscription/RBAC grants, the dev Stripe-test/SendGrid values) are **provisioning prerequisites** an agent
cannot perform — they are flagged per-ticket in `status/sprint-13.md`, not architecture questions.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted (grounded in the AppHost, the `.csproj` set, the two deploy YAMLs, the blob/queue code, and the
canonical `infrastructure.md`); challengers (cost/pragmatism, seam/coupling, cross-client/iOS) attacked; the
Lead re-verified every load-bearing citation against the real artifacts and adjudicated.
**Verdict: all challenges RESOLVED; zero blocking (three non-blocking owner questions escalated with defaults);
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (correctness) | The prompt and the old YAML both say **four** API App Services — provisioning a fifth (customer-mobile) is scope the owner didn't ask for and may be wrong. (MAJOR — it changes the host count + the iOS URLs) | REBUT (with evidence) | D1/D2 + Context: the **runtime** is five hosts (`AppHost Program.cs:35-69` + the five `Cleansia.Web.*.csproj`). The old YAML's `api-cleansia-mobile-dev` = the **partner-mobile** host; the **customer-mobile** host (`Cleansia.Web.Mobile.Customer`, the AppHost comment at `:60-69`) was **never deployed**. ADR-0013 D4 fixes the iOS **customer** app to that host. Provisioning four would leave the iOS customer app with **no dev API** — the omission is the bug, not the fix. |
| CH-2 (pragmatism/cost) | Key Vault + managed identity + App Insights + ACR for a **dev** env is gold-plating — raw GH secrets into app config and Sentry-only is cheaper and faster. (MODERATE — dev should be lean) | REBUT + FRAME | D2/D4 + Alternatives: the **canonical `infrastructure.md` already prescribes** Key Vault + MI + App Insights — this ADR codifies the doc, it does not invent. Clean-slate is the cheapest moment to wire MI/Key-Vault/telemetry across five hosts; retrofitting later is dearer. Raw-GH-secret-into-config spreads secret material across N configs and couples rotation to redeploys. GH **Environment** secrets are still used — for the **pipeline** creds (OIDC/SWA/ACR/bootstrap DB), their correct home. Dev tiers stay cheap (B1ms Postgres, Free SWA, Basic App Insights, Consumption Functions). |
| CH-3 (seam/coupling) | One reusable `appService` module for six different hosts couples them — partner/admin/mobile have different runtimes + config; a shared module will leak. (MODERATE — the per-audience seam) | DEFEND | D1: the module is parameterized (name/runtime/app-settings/identity), and **each host gets its own CORS + config + Key Vault refs from its own instantiation** — the per-audience separation is *preserved*, not broken (a change to one instance's settings touches only that host). The SSR is the same module with the Node runtime param. This is the seam that makes adding a host/country **one instance**, not a bespoke block — the opposite of coupling. |
| CH-4 (Functions) | Flipping Functions to a code/zip deploy drops ACR + the Docker step and is simpler — why keep the container? (MODERATE — schedule/simplicity) | REBUT | D2 + Alternatives + the `Dockerfile`/infra doc: QuestPDF needs native `libfontconfig1`/`libfreetype6` baked into the image; a code deploy lacks them and **PDF (receipt/invoice) generation fails at runtime**. The container is **mandatory**, not a preference. ACR + `az acr build` stays. |
| CH-5 (CI safety) | Parallelizing the API deploys on B2 risks the same overload the serialization guarded against, **and** could break the migrate-before-deploy ordering. (MAJOR — a bad deploy ordering corrupts the env) | CONCEDE-IN-PART + DEFEND | D5: the serialization guarded **B1** (the YAML comment); **B2** has the headroom for parallel package-pushes + brief restarts across 6 apps. Critically, the **ordering that matters** (migrate-before-deploy) is preserved by the **`needs: migrate-database` edge on every API/SSR deploy**, *not* by the serial chain — so parallelizing the *app* deploys cannot reorder migrate-vs-deploy. The conceded part: a `max-parallel` throttle is the documented fallback if B2 ever shows restart contention (a workflow tweak, not an architecture change). |
| CH-6 (security) | Public Postgres + connection-string auth (no VNet, no Postgres-MI) is a security hole for a system holding PII + payment data. (MAJOR — go-live security) | DEFEND + SCOPE | D3/D4: this is the **dev** baseline (public-endpoint + firewall to Azure-services-only + the admin IP, TLS-required, secret in Key Vault read via MI — the secret is never committed). VNet/private-endpoint + Postgres-AAD/MI auth are **prod-hardening** options the prod Bicep leaves a seam for (Q-INFRA-03) — they add private-DNS/NAT + Npgsql token plumbing for marginal **dev** benefit. Dev is firewalled + TLS + Key-Vault-secret; prod tightens before go-live. The ADR is explicit that prod is not deployed and Q-INFRA-03 gates its hardening. |
| CH-7 (governance) | The CI EF-bundle apply **violates** the CLAUDE.md owner-only-migration rule — CI is running migrations. (MAJOR — a stated rule) | REBUT (with the framing the prompt set) | D5 + Context: the owner-only rule governs **local** `migrations add`/`database update`. The in-pipeline **bundle apply** is the established prod/dev path (the March-2026 YAML) and is **kept** — CI only **applies** a migration that is **already committed** (authored + reviewed, owner-gated); the pipeline never **creates** one. So schema changes stay owner-gated; CI is the *deployment* of an approved schema, not an authoring of a new one. The reviewer check (#8) enforces "no `migrations add` in CI." |

**Affirmed unchallenged:** the Aspire-is-local / cloud-deploys-hosts-directly split (AppHost is never deployed);
the mandatory Storage Account (blob + queue + Functions-runtime, three connection-string slots the code reads);
OIDC federated login kept verbatim (no stored creds); the `env`-parameterized one-module-set for dev+prod; the
`deploy/bicep/` location beside the existing `deploy/`; deferring the custom domain for dev (default
hostnames are stable for the Mac-points-at-dev goal).

**Lead re-verification (against current artifacts, 2026-06-23):** `AppHost/Program.cs:35-74` (five hosts +
Functions, Azurite pinned ports, the customer-mobile host comment `:60-69`); the five `Cleansia.Web.*.csproj`;
`deploy-dev.yml` (four App Services, `api-cleansia-mobile-dev` = partner-mobile; ACR Functions; migrate-database
EF-bundle before deploy; OIDC; serialized deploys with the explicit "avoid overloading the B1 Plan" comment);
`deploy-pro.yml` (the `confirm: deploy` typed input + `environment: production` already present);
`docs/architecture/infrastructure.md` (the canonical inventory: B1/S1 plan, B1ms/GP Postgres, Storage LRS,
Functions-Docker, Key Vault RBAC + MI, App Insights alert set, the secret + container + queue inventories);
`Cleansia.Web.Partner/appsettings.json` (the three connection-string slots:
`BlobContainerConfigurationConnectionString`, `QueueStorageConnectionString`, `ConnectionString`; `CorsOrigins`
= localhost; Stripe/SendGrid/Mapbox `SET_VIA_USER_SECRETS`/empty); the blob + queue infra projects
(`Cleansia.Infra.Azure.Storage.Blobs`/`.Queues`) confirming Storage is mandatory. Owner confirmation that the
Azure resources are torn down → clean-slate, Bicep is the unambiguous source of truth.

**Escalations to the owner:** three **non-blocking** questions with defaults (Q-INFRA-01 no custom domain for
dev; Q-INFRA-02 one subscription / two RGs; Q-INFRA-03 dev public+firewall, prod hardening deferred). The
**provisioning prerequisites** (GitHub Environments + reviewers, secret migration, Key Vault value population,
running/approving the first dev `az deployment group create`, subscription/RBAC grants) are **owner-only manual
steps** flagged per-ticket — they gate the *apply*, not the *authoring* (the Bicep + YAML are agent-authorable
on approval).
