# CI/CD Pipeline

Cleansia uses GitHub Actions. **Five workflows gate a pull request** — one per stack, plus the docs —
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

**Deploy and operational**

- `deploy-dev.yml`, `deploy-pro.yml`, `deploy-azure.yml`, `deploy-docs.yml`
- `execute-sql.yml` — the manual, environment-gated SQL runner
:::

## Workflows Overview

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `backend-ci` | PR to any branch | Build + test .NET solution |
| `frontend-ci` | PR to any branch | Build Angular apps |
| `deploy-dev` | Push to `master` | Deploy everything to DEV |
| `deploy-pro` | Manual (`workflow_dispatch`) | Deploy everything to PRO |
| `execute-sql` | Manual | Run ad-hoc SQL scripts |

## Branch Strategy

```
feature/* ──PR──> master ──auto──> DEV
                    |
                    └──manual──> PRO
```

- **Feature branches** -- all development work
- **`master`** -- integration branch, auto-deploys to DEV on push
- **PRO deployment** -- manual trigger with confirmation (`type "deploy"`)

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
  --file src/Cleansia.Functions/Dockerfile src/

az functionapp config container set \
  --name func-cleansia-dev \
  --image "$ACR_NAME.azurecr.io/cleansia-functions:$GITHUB_SHA"
```

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

## Deploy to PRO (`deploy-pro.yml`)

Same pipeline as DEV with these differences:

| Aspect | DEV | PRO |
|--------|-----|-----|
| Trigger | Auto (push to master) | Manual with confirmation |
| Confirmation | None | Must type `"deploy"` |
| Concurrency | Cancel in-progress | Never cancel |
| Environment | (default) | `production` (GitHub Environment) |
| Angular config | `staging` | `production` |
| Resource group | `rg-cleansia-dev` | `rg-cleansia-pro` |
| App names | `*-dev` | `*-pro` |

::: warning Production Safety
The PRO workflow requires typing "deploy" to confirm. If the confirmation doesn't match, the workflow fails immediately.
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
