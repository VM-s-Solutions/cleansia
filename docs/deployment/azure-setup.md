# Azure Setup

Cleansia runs on Microsoft Azure with two environments: **DEV** (staging/development) and **PRO** (production). Each environment uses its own resource group with isolated resources.

## Resource Groups

| Environment | Resource Group | Purpose |
|-------------|---------------|---------|
| DEV | `rg-cleansia-dev` | Development and staging |
| PRO | `rg-cleansia-pro` | Production |

## Azure Resources

### Compute -- App Service

Four .NET API apps and one Node.js SSR app run on Azure App Service:

| Resource | DEV Name | PRO Name | Runtime |
|----------|----------|----------|---------|
| Partner API | `api-cleansia-partner-dev` | `api-cleansia-pro` | .NET 10 |
| Admin API | `api-cleansia-admin-dev` | `api-cleansia-admin-pro` | .NET 10 |
| Customer API | `api-cleansia-customer-dev` | `api-cleansia-customer-pro` | .NET 10 |
| Mobile API | `api-cleansia-mobile-dev` | `api-cleansia-mobile-pro` | .NET 10 |
| Customer SSR | `web-cleansia-customer-dev` | `web-cleansia-customer-pro` | Node.js 22 |

::: tip App Service Plan
All App Service apps share a single **B1** plan per environment. APIs are deployed sequentially to avoid overloading the B1 tier during deployments.
:::

### Compute -- Static Web Apps

Two Angular SPAs are hosted on Azure Static Web Apps:

| Resource | Purpose |
|----------|---------|
| Partner SPA | Partner/employee dashboard (`partner.cleansia.cz`) |
| Admin SPA | Admin management panel |

### Compute -- Azure Functions

| Resource | DEV Name | PRO Name | Runtime |
|----------|----------|----------|---------|
| Functions | `func-cleansia-dev` | `func-cleansia-pro` | .NET 10 (Docker) |

Functions run as Docker containers pulled from Azure Container Registry. They handle background jobs like receipt generation.

### Azure Container Registry (ACR)

| Resource | Purpose |
|----------|---------|
| ACR | Stores Docker images for Azure Functions |

Images are tagged with the git commit SHA:

```
{acr-name}.azurecr.io/cleansia-functions:{commit-sha}
```

### Database -- PostgreSQL

| Resource | Details |
|----------|---------|
| Type | Azure Database for PostgreSQL - Flexible Server |
| Engine | PostgreSQL |
| Migrations | EF Core (migration bundle applied via CI/CD) |

The hosts read the database through the Key Vault secret `ConnectionStrings--cleansia-db`. On DEV it
holds the server administrator's connection string, and the migration reads the same secret. On
production it holds the least-privilege `cleansia_app` login's, and the migration signs in as the
administrator from the `POSTGRES_ADMIN_PASSWORD` Environment secret without reading the vault — see
[Production posture](#production-posture). Only `execute-sql.yml` still reads the
`DB_CONNECTION_STRING_DEV` / `DB_CONNECTION_STRING_PRO` GitHub secrets.

### Storage

| Resource | Purpose |
|----------|---------|
| Azure Blob Storage | Order photos, receipts (PDFs), employee documents |
| Azure Queue Storage | Background job messages (receipt generation, email sending) |

Storage is accessed via the `Cleansia.Infra.Azure.Storage.Blobs` and `Cleansia.Infra.Azure.Storage.Queues` infrastructure projects.
On DEV they use the connection string in `Storage--ConnectionString`; on production each host uses its
managed identity and the account refuses shared keys — see [Production posture](#production-posture).

### Key Vault

| Resource | Purpose |
|----------|---------|
| Azure Key Vault | Stores secrets (JWT secret, Stripe keys, SendGrid API key, connection strings) |

App Services access Key Vault via managed identity using Key Vault references in app settings:

```
@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=JwtSettings--Secret)
```

### Identity

| Resource | Purpose |
|----------|---------|
| Managed Identity | App Services authenticate to Key Vault, ACR, and Storage without credentials |
| Service Principal | GitHub Actions authenticates to Azure via OIDC (federated identity) |

**Basic publishing credentials are off.** Since 2026-09-28 the Bicep sets the `ftp` and `scm`
`basicPublishingCredentialsPolicies` to `allow: false` on every App Service site, on its staging slot
when the slot is enabled (`appService.bicep`), and on the Function App (`functionApp.bicep`), so
neither FTP nor a username-and-password deploy to Kudu is accepted. Nothing depends on them: the
deploys sign in through OIDC (`azure/login`) and publish with `azure/webapps-deploy` and
`az functionapp config container set`. The resources are named literally rather than in a loop,
because the loop form raised Bicep's `BCP225` and switched type checking off.

**The customer site sends security headers.** The SSR server (`apps/cleansia.app/server.ts`) sets
them in its first middleware after compression, so static files, `/health`, a fresh render and the
landing page's micro-cache hit all carry them:

| Header | Value |
|---|---|
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `frame-ancestors 'none'` — the only directive; there is no script or style policy yet |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Strict-Transport-Security` | `max-age=31536000` — without `includeSubDomains` or `preload` |

## Production posture {#production-posture}

Production is DEV's module set with stricter flags in `deploy/bicep/weu.prod.bicepparam`, from
engineering defaults E-1, E-3 and E-4 of the 2026-09-27 meeting plan (owner ruling 2026-09-28). It is
**authored, not deployed**: nothing here exists in Azure yet, and applying it is the owner's step.
Every flag defaults to the DEV value, so DEV is unchanged.

| | DEV | Production | Flag |
|---|---|---|---|
| Admin API | public at its own hostname | reachable only as `admin.cleansia.cz/api`, through the admin Static Web App's Microsoft (Entra) sign-in | `adminApiLinkedToAdminSpa` |
| Database | public endpoint, firewall with the allow-Azure-services rule and the admin IP | private endpoint only; public network access off | `privateNetworkingEnabled` |
| Key Vault | public | private endpoint only; public network access off | `privateNetworkingEnabled` |
| Storage | account key (`Storage--ConnectionString`) | each host's managed identity; shared keys refused; links are user-delegation SAS; the public blob endpoint stays open for those links | `storageManagedIdentityEnabled` |
| Database login of the hosts | the server administrator | `cleansia_app` — row reads and writes only, `Ssl Mode=VerifyFull`; the administrator is the migration's alone | `postgresAppLoginEnabled` |

- **The admin API.** The admin App Service is linked as the backend of the Standard-tier admin Static
  Web App, so every `/api` call first passes the SWA's `admin_console` route rule; the App Service
  answers 401 to anything the SWA did not proxy, and its staging slot answers 401 to everything but
  `/health`. The admin SPA's production build calls same-origin `/api`, so the admin API needs no
  custom domain and no CORS entry, and the host-only cookies and the CSRF header hold unchanged through
  the proxy. The Free-tier DEV SWA cannot link a backend.
- **The private database and vault.** The hosts, their staging slots and the Functions app are
  VNet-integrated and resolve the database, the vault and storage to private endpoints, with no
  configuration change. The deploy opens a temporary window for the runner's IP around the secret push
  and the migration and closes it even on failure — [CI/CD — the private database and vault](/deployment/ci-cd#production-window).
  An administrator's `psql` opens the same window by hand, as `deploy/AZURE-PROD-POSTURE.md` §6 shows.
- **The least-privilege login.** `deploy/db/grant-app-login.sql` creates `cleansia_app` and grants it
  after every migration; its password is the `POSTGRES_APP_PASSWORD` secret of `prod-weu`, which the
  deploy writes into `ConnectionStrings--cleansia-db`. The administrator credential never enters Key
  Vault.
- **Storage.** Blob and queue clients, the Functions queue triggers included, connect by identity
  (`BlobContainerConfiguration__AccountUrl`, and `QueueStorageConnectionString__queueServiceUri` with
  `__credential=managedidentity`); there is no storage secret. A person browsing the account needs a
  Storage Blob data role of their own.

What this means for code — where production refuses what DEV allows — is
[Security rules — the production perimeter](/architecture/security-rules#production-perimeter). The
knobs, the CI window and the rotations are `deploy/AZURE-PROD-POSTURE.md` §6–§7; the owner's steps are
`deploy/AZURE-DEV-RUNBOOK.md` §11.

## Estimated Monthly Costs

### DEV Environment (~$66/month)

| Resource | SKU | Estimated Cost |
|----------|-----|---------------|
| App Service Plan (B1) | 1x B1 Linux | ~$13 |
| PostgreSQL Flexible | Burstable B1ms | ~$25 |
| Storage Account | LRS | ~$1 |
| Key Vault | Standard | ~$1 |
| Static Web Apps | Free (2x) | $0 |
| Azure Functions | Consumption | ~$1 |
| ACR | Basic | ~$5 |
| Misc (bandwidth, etc.) | | ~$20 |
| **Total** | | **~$66** |

### PRO Environment (~$360/month)

| Resource | SKU | Estimated Cost |
|----------|-----|---------------|
| App Service Plan (B2/S1) | 1x S1 Linux | ~$55 |
| PostgreSQL Flexible | GP D2s v3 | ~$130 |
| Storage Account | LRS | ~$5 |
| Key Vault | Standard | ~$1 |
| Static Web Apps | Standard (2x) | ~$18 |
| Azure Functions | Consumption+ | ~$5 |
| ACR | Basic | ~$5 |
| Custom domains + SSL | | ~$0 (managed) |
| Monitoring (Sentry) | | ~$26 |
| Misc (bandwidth, backups) | | ~$115 |
| **Total** | | **~$360** |

::: warning
These are estimates based on typical usage patterns. Actual costs vary based on traffic, storage consumption, and database activity. Monitor costs via Azure Cost Management.
:::

::: warning Two known gaps in these tables
**Application Insights and Log Analytics are not line items in either table**, although both are
provisioned (`deploy/bicep/modules/appInsights.bicep`) and Log Analytics bills per GB ingested. **The
producer set changed in August 2026 and these estimates predate it**: the Functions host used to be the
only contributor, and since T-0500 the five APIs export as well (the SSR host still sends nothing). The
DEV workspace carries a 1 GB/day ingestion cap and prod defaults to 5 GB/day — and that cap is a
breaker, not a budget: when it trips, ingestion stops until the next UTC day, exceptions included. The
volume trade-off is written up under
[Infrastructure → Volume and cost](/architecture/infrastructure#volume-and-cost).

**The `Monitoring (Sentry)` line is a forecast for a subscription that is not active.** Sentry is
disabled in every deployed environment because the DSN is empty. See
[Infrastructure → Observability](/architecture/infrastructure#observability).
:::

## Architecture Diagram

Production as authored. Every hostname is planned: none resolves until the owner creates its DNS
records (`deploy/AZURE-DEV-RUNBOOK.md` §12).

```
Internet
    |
    ├── partner.cleansia.cz ──> Static Web App (Partner SPA)
    ├── admin.cleansia.cz   ──> Static Web App (Admin SPA) ── Entra sign-in ──> /api ──> App Service (Admin API)
    ├── cleansia.cz         ──> App Service (Customer SSR)
    |
    ├── api.cleansia.cz                 ──> App Service (Partner API)
    ├── api-customer.cleansia.cz        ──> App Service (Customer API)
    ├── api-partner-mobile.cleansia.cz  ──> App Service (Partner Mobile API)
    └── api-customer-mobile.cleansia.cz ──> App Service (Customer Mobile API)
                                        |   VNet integration → private endpoints
                                        ├──> PostgreSQL (private)
                                        ├──> Blob Storage (its public endpoint also serves the SAS links)
                                        ├──> Queue Storage ──> Azure Functions
                                        └──> Key Vault (private)
```

## Managed Identity Flow

```
App Service
    |
    ├── Key Vault (read secrets)
    ├── Blob Storage (read/write photos, receipts)
    └── Queue Storage (send messages)

Azure Functions
    |
    ├── Key Vault (read secrets)
    ├── Blob Storage (write receipts)
    └── Queue Storage (receive messages)
```

The Key Vault arrows are the identity in every stage. The storage arrows are the identity in
production only: DEV reaches storage with the connection string in `Storage--ConnectionString`.
PostgreSQL is never reached by identity — the hosts sign in with a password login, `cleansia_app` in
production.
