# CI/CD Pipeline

Cleansia uses GitHub Actions for continuous integration and deployment. There are three main workflows plus two utility workflows.

::: info Source Files
- `.github/workflows/backend-ci.yml`
- `.github/workflows/deploy-dev.yml`
- `.github/workflows/deploy-pro.yml`
- `.github/workflows/frontend-ci.yml`
- `.github/workflows/execute-sql.yml`
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

Runs on every pull request. Simple build-and-test pipeline.

```yaml
steps:
  - Setup .NET 10.x
  - dotnet restore Cleansia.Api.sln
  - dotnet build --configuration Release
  - dotnet test --configuration Release
```

## Deploy to DEV (`deploy-dev.yml`)

Triggered on every push to `master`. Builds, tests, migrates the database, and deploys all 8 components sequentially.

### Pipeline Stages

```
build-dotnet ──┬──> migrate-database ──> deploy-partner-api (1/7)
               │                    ──> deploy-admin-api (2/7)
               │                    ──> deploy-customer-api (3/7)
               │                    ──> deploy-mobile-api (4/7)
               │
               └──> build-and-deploy-functions (5/8)

build-angular ─────> deploy-customer-ssr (6/8)
              ─────> deploy-partner-spa (7/8)
              ─────> deploy-admin-spa (8/8)
```

### Job Details

#### 1. Build .NET APIs

Publishes four API projects as separate artifacts:

| Artifact | Project |
|----------|---------|
| `partner-api` | `Cleansia.Web.Partner.csproj` |
| `admin-api` | `Cleansia.Web.Admin.csproj` |
| `customer-api` | `Cleansia.Web.Customer.csproj` |
| `mobile-api` | `Cleansia.Web.Mobile.csproj` |

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
  --startup-project Cleansia.Web/Cleansia.Web.Partner.csproj \
  --configuration Release \
  --output ./efbundle

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
