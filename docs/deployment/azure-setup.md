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

Connection strings are stored as GitHub secrets (`DB_CONNECTION_STRING_DEV`, `DB_CONNECTION_STRING_PRO`) and Azure Key Vault references in app settings.

### Storage

| Resource | Purpose |
|----------|---------|
| Azure Blob Storage | Order photos, receipts (PDFs), employee documents |
| Azure Queue Storage | Background job messages (receipt generation, email sending) |

Storage is accessed via the `Cleansia.Infra.Azure.Storage.Blobs` and `Cleansia.Infra.Azure.Storage.Queues` infrastructure projects.

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

## Architecture Diagram

```
Internet
    |
    ├── partner.cleansia.cz ──> Static Web App (Partner SPA)
    ├── admin.cleansia.cz   ──> Static Web App (Admin SPA)
    ├── cleansia.cz         ──> App Service (Customer SSR)
    |
    ├── api.cleansia.cz          ──> App Service (Partner API)
    ├── api-admin.cleansia.cz    ──> App Service (Admin API)
    ├── api-customer.cleansia.cz ──> App Service (Customer API)
    └── api-mobile.cleansia.cz   ──> App Service (Mobile API)
                                        |
                                        ├──> PostgreSQL
                                        ├──> Blob Storage
                                        ├──> Queue Storage ──> Azure Functions
                                        └──> Key Vault (secrets)
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
