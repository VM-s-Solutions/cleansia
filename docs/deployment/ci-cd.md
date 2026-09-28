# CI/CD Pipeline

Cleansia uses GitHub Actions. **Eight workflows gate a pull request** — one per stack, the docs, two
dependency-free Node gates whose drift spans trees no single stack job can see, and the secret scan —
and five more deploy or run operational jobs.

::: info Source Files
**Gates on a PR**

| Workflow | Guards |
|---|---|
| `backend-ci.yml` | the .NET solution — unit, integration (Testcontainers) and host tests. Also runs on pushes to `master`, because direct-to-master commits used to bypass it entirely. Scoped to `src/**` **and `sql-scripts/**`** |
| `frontend-ci.yml` | the Nx workspace — lint, test, build across the three apps |
| `android-ci.yml` | the Gradle multi-module build |
| `ios-ci.yml` | SwiftFormat, then SwiftLint, then three test schemes |
| `docs-ci.yml` | both halves of the reference contract: two checkers with their own self-tests blocking first, then `vitepress build` with `ignoreDeadLinks: false` |
| `ios-symbols-ci.yml` | the compiler-free half of the iOS gate — `check-ios-symbols.mjs` on Linux |
| `booking-policy-parity.yml` | every client's stated booking figures against `BookingPolicy`, and the legal seed's currency placeholders |
| `secret-scan.yml` | gitleaks over every commit reachable from the head — [below](#secret-scan) |

**Deploy and operational**

- `deploy-dev.yml`, `deploy-pro.yml`, `deploy-azure.yml`, `deploy-docs.yml`
- `execute-sql.yml` — the manual, environment-gated SQL runner
:::

## Secret scan (`secret-scan.yml`) {#secret-scan}

Runs on every pull request, whatever its base branch, and on every push to `master`. It installs the
gitleaks **8.30.1** CLI, pinned by version and SHA-256 (the GitHub Action needs a paid licence on an
organisation repository), checks out the full history and runs:

```bash
./gitleaks git --config .gitleaks.toml --redact --no-banner --verbose --log-opts="HEAD" .
```

So a secret committed anywhere in a pull request fails it, even if a later commit deletes it again —
deleting a file does not un-publish it. `--redact` keeps a finding out of the job log.

| File | What it holds |
|---|---|
| `.gitleaks.toml` | gitleaks' default rules, plus two allow-lists that apply to the **generic API key rule only**: the test-code paths (the published test JWT key, sandbox-shaped Stripe ids) and dotted translation keys. The Stripe, SendGrid and AWS rules still fire everywhere |
| `.gitleaksignore` | the baseline — the fingerprints of what is already in history, in commented groups (two old SendGrid keys, an old Nx Cloud token, the published test JWT key in early host settings, false positives). Only a **new** finding fails |

A finding that is a real secret is revoked at its provider first; baselining it only stops the build
failing, it does not make the secret safe.

## Workflows Overview

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `backend-ci` | PR to any branch | Build + test .NET solution |
| `frontend-ci` | PR to any branch | Build Angular apps |
| `secret-scan` | PR to any branch, push to `master` | gitleaks — no new secret in the history |
| `deploy-dev` | **Manual (`workflow_dispatch`)** | Deploy everything to DEV |
| `deploy-pro` | Manual (`workflow_dispatch`) | Deploy everything to PRO |
| `execute-sql` | Manual | Run ad-hoc SQL scripts. Refuses `insert_seed_data.sql` on PRO and `insert_local_dev_admin.sql` on DEV and PRO (compared by file name, so a relative path cannot walk around it) |

## Branch Strategy

```
feature/* ──PR──> master ──manual──> DEV
                    |
                    └──manual──> PRO
```

- **Feature branches** -- all development work
- **`master`** -- integration branch. **It does NOT auto-deploy.** The push trigger was removed on
  owner request 2026-07-17; a DEV deploy is a deliberate button press, like prod. `deploy-dev.yml`
  offers a `what-if` mode that previews the Bicep change without mutating anything.
- **PRO deployment** -- manual, gated by **required reviewers on the `prod-weu` GitHub Environment**.
  That protection is UI configuration rather than YAML — see the PROD section of
  `deploy/AZURE-DEV-RUNBOOK.md`.

## Backend CI (`backend-ci.yml`)

Runs on every pull request. Every step uses `working-directory: ./src` — the solution lives at
`src/Cleansia.Api.sln`, not the repo root.

```yaml
steps:
  - Setup .NET 10.x
  - Cache ~/.nuget/packages   # keyed on Directory.Packages.props + all csprojs
  - dotnet restore Cleansia.Api.sln
  - dotnet build Cleansia.Api.sln --configuration Release --no-restore
  # Three suites, single-threaded, fast-first:
  - dotnet test Cleansia.Tests/Cleansia.Tests.csproj                        # unit
  - dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj  # Testcontainers Postgres
  - dotnet test Cleansia.HostTests/Cleansia.HostTests.csproj                # authz/isolation
```

::: tip Why the container-backed suites exist
`Cleansia.IntegrationTests` and `Cleansia.HostTests` spin a real PostgreSQL via Testcontainers.
They are what catch the multi-tenant / FK / migration / webhook bugs the SQLite-and-mocks unit tests
structurally cannot — for example the SQL-vs-C# equivalence pins on `OrderAvailability` and
`OrderVisibility`. Both run with `xUnit.parallelizeTestCollections=false`.
:::

## Deploy to DEV (`deploy-dev.yml`)

Triggered on every push to `master`. It is a thin caller — the pipeline itself lives in the reusable
`deploy-azure.yml`, which `deploy-pro.yml` also calls. Nine deployable components.

### Pipeline Stages

```
build-dotnet ──┬──> provision (Bicep) ──> migrate-database ──> deploy-partner-api
               │                                          ──> deploy-admin-api
               │                                          ──> deploy-customer-api
               │                                          ──> deploy-partner-mobile-api
               │                                          ──> deploy-customer-mobile-api
               │
               └──> build-and-deploy-functions

build-angular ─────> deploy-customer-ssr
              ─────> deploy-partner-spa
              ─────> deploy-admin-spa
```

### Job Details

#### 1. Build .NET APIs

Publishes **five** API projects as separate artifacts (`deploy-azure.yml:120-136`):

| Artifact | Project |
|----------|---------|
| `partner-api` | `Cleansia.Web.Partner/Cleansia.Web.Partner.csproj` |
| `admin-api` | `Cleansia.Web.Admin/Cleansia.Web.Admin.csproj` |
| `customer-api` | `Cleansia.Web.Customer/Cleansia.Web.Customer.csproj` |
| `partner-mobile-api` | `Cleansia.Web.Mobile.Partner/Cleansia.Web.Mobile.Partner.csproj` |
| `customer-mobile-api` | `Cleansia.Web.Mobile.Customer/Cleansia.Web.Mobile.Customer.csproj` |

#### 2. Build Angular Apps

Builds three Angular apps using Nx:

| Artifact | Nx Project | Configuration |
|----------|-----------|---------------|
| `customer-app` | `cleansia.app` | `staging` (SSR) |
| `partner-app` | `cleansia-partner.app` | `staging` |
| `admin-app` | `cleansia-admin.app` | `staging` |

The Customer app includes SSR with a generated `package.json` for Node.js startup.

#### 3. Database Migration

Creates and runs an EF Core migrations bundle:

```bash
dotnet ef migrations bundle \
  --project Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project Cleansia.Web.Partner/Cleansia.Web.Partner.csproj \
  --configuration Release \
  --output ./efbundle

# Connection string read from Key Vault at run time (ConnectionStrings--cleansia-db) —
# the same secret the runtime hosts resolve, so a rotation touches one place.
DB_CONNECTION_STRING="$(az keyvault secret show \
  --vault-name kv-cleansia-<region>-<env> \
  --name ConnectionStrings--cleansia-db --query value -o tsv)"
./efbundle --connection "$DB_CONNECTION_STRING"
```

::: warning
Migrations run **before** any API deploys to ensure the database schema is ready.
:::

#### 4-7. API Deployments

Each API deploys sequentially to Azure App Service:

```bash
az webapps-deploy --app-name api-cleansia-{service}-dev
az webapp stop --name api-cleansia-{service}-dev --resource-group rg-cleansia-dev
az webapp start --name api-cleansia-{service}-dev --resource-group rg-cleansia-dev
```

::: tip Sequential Deployment
APIs deploy one at a time to avoid overloading the B1 App Service Plan. Each deployment includes a stop/start to force container refresh.
:::

#### 5. Azure Functions

Built as a Docker image, pushed to ACR, and deployed:

```bash
az acr build --registry $ACR_NAME \
  --image cleansia-functions:$GITHUB_SHA \
  --file src/Cleansia.Functions/Dockerfile .

az functionapp config container set \
  --name func-cleansia-dev \
  --image "$ACR_NAME.azurecr.io/cleansia-functions:$GITHUB_SHA"
```

The Functions build context must be the repository root: `Cleansia.Core.AppServices` embeds HTML
from the root-level `email-templates/` directory. The Dockerfile copies both `src/` and those
templates, and the project fails its build if a required template is missing. A `src/`-only context
previously produced an image without templates, causing email rendering to fail before sending.

The request API accepting an email means it was queued, not delivered. After deploying a delivery
fix, messages that have already exhausted the five queue attempts remain in `DeadLetters` and
need controlled replay; submitting the public promo form again does not resend them.

#### 6. Customer SSR

Deployed as a Node.js app with startup command:

```bash
az webapp config set --startup-file "node server/server.mjs"
```

#### 7-8. SPAs

Partner and Admin apps deploy to Azure Static Web Apps:

```yaml
- uses: Azure/static-web-apps-deploy@v1
  with:
    azure_static_web_apps_api_token: ${{ secrets.TOKEN }}
    action: upload
    app_location: ./partner-app
    skip_app_build: true
```

### Warming is one job, after every deploy {#warm-dev-sites}

`warm-dev-sites` runs once all five APIs and the SSR app have deployed, and proves each answers before
the run is called green. A site that never answers is still a failed deploy — the job reports **every**
unreachable site rather than stopping at the first.

**It used to be a step inside each deploy job, and that made two of them fail every time.** Six sites
share a single **B2** plan — 2 vCPU, 3.5 GB, five .NET APIs plus the Node SSR app, all `alwaysOn` — and
they deploy in parallel, so they all cold-start at once. Whichever finishes deploying last warms
straight into the worst of that contention.

That is why it was always the same two. `partner-mobile` and `customer-mobile` are last in the
`apiHosts` array, so they deploy last and start last. They failed together on 2026-08-12 and again on
2026-08-15 while `partner-api` warmed in **one second** — and both were serving normally within the
hour. Nothing was wrong with them except *when* they were asked.

::: warning If this starts failing again, the answer is the plan
Do not raise the retry budget and do not re-serialise the deploys. The parallel fan-out is
[ADR-0015](/decisions/adr-0015) D5/B2 and the B2 SKU is its D2 owner cost override — a genuine
trade, deliberately made. A warm failure here means six cold starts no longer fit in two cores, and
the honest response is the SKU or fewer always-on sites, not a longer wait.
:::

**Prod is untouched.** There, each job warms its own **staging slot** and only then swaps it, because a
slot must be proven healthy before it takes traffic. That ordering cannot be moved to the end.

## Deploy to PRO (`deploy-pro.yml`)

Same pipeline as DEV with these differences:

| Aspect | DEV | PRO |
|--------|-----|-----|
| Trigger | Manual (`workflow_dispatch`) | Manual (`workflow_dispatch`) |
| Approval | None | **Required reviewers on the `prod-weu` Environment** |
| Default mode | `deploy` or `what-if`, chosen at dispatch | `what-if` — the no-thought path is the non-mutating preview |
| Concurrency | Cancel in-progress | Never cancel |
| Environment | (default) | `prod-weu` (GitHub Environment) |
| Angular config | `staging` | `production` |
| Resource group | `rg-cleansia-dev` | `rg-cleansia-pro` |
| App names | `*-dev` | `*-pro` |
| Legal-text gate | none | the `legal-texts` job runs first, in both modes — below |

**A production deploy is refused while a legal text in force, or one still to come, is a draft.**
`deploy-pro.yml`'s first job, `legal-texts`, runs `agents/tools/check-legal-drafts.test.mjs` (its
self-test) and then `check-legal-drafts.mjs`, and the deploy job `needs:` it, so neither `what-if` nor
`deploy` provisions anything until it passes. The checker walks every audience, document type and
market under `src/Cleansia.Infra.Database/Seed/Legal` and reads, in each folder, the version in force
today (the newest `yyyy-MM-dd` on or before today, UTC) **and every version dated after today** — the
same deploy seeds a future version, and it comes into force on its date with no deploy in between. It
fails if any of their language files carries the draft banner — a line starting `> Návrh —` (cs, sk),
`> Draft —` (en), `> Черновик —` (ru) or `> Чернетка —` (uk). It also fails on an empty scan: no seed
tree, nothing in force anywhere, or a version in force with no text (an upcoming folder with no text
does not fail). A market with nothing in force yet is only noted, provided its future versions carry no
banner. The gate is on the production deploy only; pull requests and the DEV deploy do not run it,
because every text seeded today is a draft.

::: warning Production Safety
Two things guard prod, and neither is the typed confirmation this page used to describe — that gate was
**replaced** by GitHub Environment protection. Every job touching prod runs in the `prod-weu`
Environment, whose **required reviewers must approve the run before any prod secret is released**; and
the dispatch mode defaults to `what-if`, so a run started without thinking previews rather than
mutates.
:::

## Authentication

All workflows use Azure federated identity (OIDC):

```yaml
permissions:
  id-token: write
  contents: read

- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

## Required Secrets

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | Service principal client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription |
| `ACR_NAME` | Azure Container Registry name |
| `DB_CONNECTION_STRING_DEV` | DEV database connection string |
| `DB_CONNECTION_STRING_PRO` | PRO database connection string |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_DEV` | Partner SPA deploy token (DEV) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_DEV` | Admin SPA deploy token (DEV) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_PRO` | Partner SPA deploy token (PRO) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_PRO` | Admin SPA deploy token (PRO) |
