# Cleansia — Azure Deployment Plan

> Created: 2026-03-15
> Status: DEV deployed, PRO ready
> Environments: DEV, PRO (Production)

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Azure Resources per Environment](#azure-resources-per-environment)
4. [Service Integration](#service-integration)
5. [Configuration Strategy](#configuration-strategy)
6. [CI/CD Pipeline](#cicd-pipeline)
7. [Step-by-Step Deployment Guide](#step-by-step-deployment-guide)
8. [Documentation Site Deployment](#documentation-site-deployment)
9. [Cost Breakdown](#cost-breakdown)

---

## Overview

Cleansia is deployed across two Azure environments:

- **DEV** — Development/staging environment for testing before production releases
- **PRO** — Production environment serving real customers and employees

Both environments are Azure-based. The user's own domain is configured in Azure DNS, with SSL certificates provided by Azure (managed certificates).

---

## Architecture

### Backend — 4 .NET 10 API Projects (Aspire-orchestrated)

| Project                 | Role                          | Port |
| ----------------------- | ----------------------------- | ---- |
| `Cleansia.Web`          | Partner API (employee-facing) | 5000 |
| `Cleansia.Web.Admin`    | Admin API (back-office)       | 5001 |
| `Cleansia.Web.Mobile`   | Mobile API (Android/iOS)      | 5002 |
| `Cleansia.Web.Customer` | Customer API (public-facing)  | 5003 |

All 4 APIs are orchestrated by .NET Aspire 13.1.1 (`Cleansia.AppHost`) and share `Cleansia.ServiceDefaults` for OpenTelemetry, health checks, Sentry, and HTTP resilience.

### Background Jobs — Azure Functions (`Cleansia.Functions`)

Deployed as a **Docker container** with native QuestPDF for PDF generation (no browser dependency).

| Function                  | Trigger                        | Purpose                                          |
| ------------------------- | ------------------------------ | ------------------------------------------------ |
| `CloseExpiredPayPeriods`  | Timer — daily 2 AM UTC         | Closes pay periods, generates invoice PDFs       |
| `SendPeriodEndReminders`  | Timer — daily 9 AM UTC         | Sends period end reminder emails                 |
| `DataRetentionCleanup`    | Timer — weekly Sunday 3 AM UTC | GDPR cleanup, PII anonymization, stale data      |
| `GenerateReceipt`         | Queue — `generate-receipt`     | Generates receipt PDF + sends email to customer  |
| `GenerateInvoice`         | Queue — `generate-invoice`     | Generates invoice PDF for employee (future)      |

APIs enqueue messages to Azure Storage Queues instead of generating PDFs synchronously. The Functions app picks up messages, generates PDFs using QuestPDF native layout builders, uploads to blob storage, and sends emails via SendGrid.

> **PDF Generation**: Uses QuestPDF fluent API with a country-aware layout builder pattern (`IReceiptLayoutBuilder`, `IInvoiceLayoutBuilder`). No browser/Chromium dependency. HTML templates in blob storage are no longer used for PDF — only for email body rendering via Handlebars. The `InvoiceTemplates` and `ReceiptTemplates` DB tables have been removed.

### Frontend — 3 Angular 19 Apps (Nx monorepo)

| App                              | Type                    | Deployment Target                                    |
| -------------------------------- | ----------------------- | ---------------------------------------------------- |
| `cleansia.app` (Customer)        | SSR (Angular Universal) | **Azure App Service (Node.js)** — NOT Static Web App |
| `cleansia-partner.app` (Partner) | SPA                     | Azure Static Web App (Free tier)                     |
| `cleansia-admin.app` (Admin)     | SPA                     | Azure Static Web App (Free tier)                     |

> **Important**: The Customer app uses Server-Side Rendering (SSR) for SEO and initial load performance. It requires a Node.js runtime and must be deployed to App Service, not Static Web App.

### Database

- **PostgreSQL** — Azure Database for PostgreSQL Flexible Server
- EF Core 10 with multi-tenancy (shared DB, `TenantId` filter)

### Mobile

- Android Partner app (Kotlin/Jetpack Compose) — distributed via Play Store
- Future: iOS Partner, Android/iOS Customer

---

## Azure Resources per Environment

### DEV (~$66/month estimated)

| Resource                    | Name                        | SKU/Tier                                 |
| --------------------------- | --------------------------- | ---------------------------------------- |
| Resource Group              | `rg-cleansia-dev`           | —                                        |
| App Service Plan (APIs)     | `asp-cleansia-dev`          | B1 (1 core, 1.75 GB RAM) — shared by all |
| App Service Plan (Functions)| `asp-func-cleansia-dev`     | B1 (1 core, 1.75 GB RAM) — Functions     |
| App Service (Partner API)   | `api-cleansia-partner-dev`  | .NET 10 — Partner/employee-facing API    |
| App Service (Admin API)     | `api-cleansia-admin-dev`    | .NET 10 — Back-office API                |
| App Service (Customer API)  | `api-cleansia-customer-dev` | .NET 10 — Public-facing API              |
| App Service (Mobile API)    | `api-cleansia-mobile-dev`   | .NET 10 — Android/iOS API                |
| Azure Functions             | `func-cleansia-dev`         | Docker container — background jobs + PDF |
| Container Registry          | `crcleansiadev`             | Basic — stores Functions Docker image    |
| App Service (Customer SSR)  | `web-cleansia-customer-dev` | Node.js 20 LTS — Angular SSR frontend    |
| Static Web App (Partner)    | `swa-cleansia-partner-dev`  | Free                                     |
| Static Web App (Admin)      | `swa-cleansia-admin-dev`    | Free                                     |
| Static Web App (Docs)       | `swa-cleansia-docs-dev`     | Free — VitePress documentation site      |
| Storage Account             | `stcleansiasdev`            | Standard LRS — blobs + queues            |
| PostgreSQL Flexible Server  | `psql-cleansia-dev`         | Burstable B1ms (1 vCore, 2 GB)           |
| Key Vault                   | `kv-cleansia-dev`           | Standard                                 |
| Application Insights        | `ai-cleansia-dev`           | Free tier (5 GB/month)                   |
| App Registration (EasyAuth) | `cleansia-dev-gate`         | Entra ID — restricts DEV access to allowed users |

### PRO (~$360/month estimated)

| Resource                    | Name                        | SKU/Tier                                     |
| --------------------------- | --------------------------- | -------------------------------------------- |
| Resource Group              | `rg-cleansia-pro`           | —                                            |
| App Service Plan (APIs)     | `asp-cleansia-pro`          | S1 or P1v2 (1 core, 1.75 GB RAM)             |
| App Service Plan (Functions)| `asp-func-cleansia-pro`     | B1 (1 core, 1.75 GB RAM) — Functions         |
| App Service (Partner API)   | `api-cleansia-partner-pro`  | .NET 10 — Partner/employee-facing API        |
| App Service (Admin API)     | `api-cleansia-admin-pro`    | .NET 10 — Back-office API                    |
| App Service (Customer API)  | `api-cleansia-customer-pro` | .NET 10 — Public-facing API                  |
| App Service (Mobile API)    | `api-cleansia-mobile-pro`   | .NET 10 — Android/iOS API                    |
| Azure Functions             | `func-cleansia-pro`         | Docker container — background jobs + PDF     |
| Container Registry          | `crcleansipro`              | Basic — stores Functions Docker image        |
| App Service (Customer SSR)  | `web-cleansia-customer-pro` | Node.js 20 LTS + staging slot                |
| Static Web App (Partner)    | `swa-cleansia-partner-pro`  | Free                                         |
| Static Web App (Admin)      | `swa-cleansia-admin-pro`    | Free                                         |
| Storage Account             | `stcleansiaspro`            | Standard LRS — blobs + queues                |
| PostgreSQL Flexible Server  | `psql-cleansia-pro`         | Standard S2 (2 vCores, 8 GB)                 |
| Key Vault                   | `kv-cleansia-pro`           | Standard                                     |
| Application Insights        | `ai-cleansia-pro`           | Pay-as-you-go (~$10/mo)                      |

> **PRO differences**: Staging slots on App Services for zero-downtime deployments, higher-tier PostgreSQL for production workloads, paid Application Insights for full telemetry.

> **DEV-only: EasyAuth Gate** — The DEV Customer SSR app (`web-cleansia-customer-dev`) has Azure AD (EasyAuth) authentication enabled to restrict access to authorized developers only. This gate:
> - Requires Microsoft login before accessing the website
> - Must exclude `/assets/*` from authentication so that SSR can load static files (translations, CSS, JS) after login
> - Is NOT used in PRO (production is public-facing)
>
> Configure excluded paths:
> ```bash
> az webapp auth update --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --excluded-paths "/assets/*"
> ```

---

## Service Integration

### Stripe (Payment Processing)

| Concern          | DEV                                                                    | PRO                                              |
| ---------------- | ---------------------------------------------------------------------- | ------------------------------------------------ |
| Mode             | Test mode                                                              | Live mode                                        |
| API keys         | Test keys (`sk_test_...`)                                              | Live keys (`sk_live_...`)                        |
| Webhook endpoint | `https://api-cleansia-partner-dev.azurewebsites.net/api/v1/payment/webhook`    | `https://api.cleansia.cz/api/v1/payment/webhook` |
| Local testing    | Use `stripe listen --forward-to localhost:5003/api/v1/payment/webhook` | N/A                                              |

**Key Vault secrets:**

```
Stripe__SecretKey
Stripe__WebhookSecret
Stripe__PublishableKey
```

### SendGrid (Email)

| Concern     | DEV                                            | PRO                                |
| ----------- | ---------------------------------------------- | ---------------------------------- |
| Mode        | Sandbox mode (no emails sent)                  | Live sending                       |
| Config      | `SendGrid__SandboxMode=true`                   | `SendGrid__SandboxMode=false`      |
| Plan        | Free (sandbox)                                 | Essentials ($19.95/mo, 50k emails) |
| Alternative | Use Mailtrap/Ethereal for visual email testing | N/A                                |

**Key Vault secrets:**

```
SendGrid__ApiKey
```

### Sentry (Error Monitoring)

| Concern           | DEV                   | PRO                                 |
| ----------------- | --------------------- | ----------------------------------- |
| Project           | `sentry-cleansia-dev` | `sentry-cleansia-pro`               |
| Trace sample rate | 100%                  | 20%                                 |
| Alerts            | Disabled              | Enabled (Slack/email notifications) |

**Key Vault secrets:**

```
Sentry__Dsn
```

> Both backend (via `ServiceDefaults`) and frontend (via `environment.ts`) read the Sentry DSN. Backend DSN goes in Key Vault; frontend DSN is baked into the Angular build.

---

## Configuration Strategy

### Principles

1. **Azure Key Vault for ALL secrets** — connection strings, API keys, webhook secrets, JWT signing keys
2. **App Configuration or Environment Variables** for non-secret config (feature flags, CORS origins, log levels)
3. **No secrets in `appsettings.json`** — use Azure Key Vault references in App Service configuration
4. **`appsettings.Development.json`** for local development only (uses User Secrets)
5. **`appsettings.Production.json`** for both DEV and PRO Azure environments — secrets come from Key Vault

### Key Vault Reference Format (App Service)

In App Service Configuration, reference Key Vault secrets using:

```
@Microsoft.KeyVault(SecretUri=https://kv-cleansia-dev.vault.azure.net/secrets/ConnectionStrings--ConnectionString)
```

### Configuration Hierarchy

```
1. Azure Key Vault (secrets)           → Connection strings, API keys, JWT keys
2. App Service Environment Variables   → ASPNETCORE_ENVIRONMENT, CORS origins, feature toggles
3. appsettings.Production.json         → Non-secret defaults (logging, rate limits)
4. appsettings.json                    → Base configuration
```

### Secrets Inventory (Key Vault)

| Secret                        | Key Vault Key                                          | Used By              |
| ----------------------------- | ------------------------------------------------------ | -------------------- |
| PostgreSQL connection string  | `ConnectionStrings--ConnectionString`                   | All 4 APIs, Functions|
| JWT signing key               | `JwtSettings--Secret`                                  | All 4 APIs, Functions|
| Stripe secret key             | `Stripe--SecretKey`                                    | Customer API         |
| Stripe webhook secret         | `Stripe--WebhookSecret`                                | Customer API         |
| Stripe publishable key        | `Stripe--PublishableKey`                               | Customer API         |
| SendGrid API key              | `SendGrid--ApiKey`                                     | All 4 APIs, Functions|
| Sentry DSN                    | `Sentry--Dsn`                                          | All 4 APIs           |
| Azure Blob Storage connection | `ConnectionStrings--BlobContainerConfigurationConnectionString` | All 4 APIs, Functions|

### Azure Functions App Settings

The Functions app (`func-cleansia-{env}`) requires these app settings. Secrets use Key Vault references; non-secrets are plain values.

| Setting                                                | Value / Source                              | Type          |
| ------------------------------------------------------ | ------------------------------------------- | ------------- |
| `AzureWebJobsStorage`                                  | Storage account connection string           | Secret (KV)   |
| `FUNCTIONS_WORKER_RUNTIME`                              | `dotnet-isolated`                           | Plain         |
| `BlobContainerConfiguration__AccountUrl`                | `https://stcleansia{env}.blob.core.windows.net/` | Plain     |
| `BlobContainerConfiguration__ConnectionStringName`      | `BlobContainerConfigurationConnectionString` | Plain        |
| `ConnectionStrings__ConnectionString`                   | KV ref: `ConnectionStrings--ConnectionString` | Secret (KV) |
| `ConnectionStrings__QueueStorageConnectionString`       | Storage account connection string           | Secret (KV)   |
| `ConnectionStrings__BlobContainerConfigurationConnectionString` | KV ref: `ConnectionStrings--BlobContainerConfigurationConnectionString` | Secret (KV) |
| `JwtSettings__Secret`                                   | KV ref: `JwtSettings--Secret`               | Secret (KV)   |
| `SendGrid__ApiKey`                                      | KV ref: `SendGrid--ApiKey`                  | Secret (KV)   |
| `SendGrid__OrderReceiptTemplateId`                      | `d-2e4f0bcc8af54b3d88c471d7e0cd507a`        | Plain         |
| `SendGrid__EmailConfirmationTemplateId`                 | `d-eb7daac9cbe94f01beb2ee1bb0ec5c29`        | Plain         |
| `SendGrid__PeriodClosedTemplateId`                      | `d-75a0f9cfdcc44eabb617de12e28d784d`        | Plain         |
| `SendGrid__PeriodEndReminderTemplateId`                 | `d-d8428c5ffff14355a59d0a35023445da`        | Plain         |
| `SendGrid__ResetPasswordTemplateId`                     | `d-c475f44d635f40569aa8b5171dc63270`        | Plain         |
| `SendGrid__AddressFrom`                                 | `it@cleansia.cz`                            | Plain         |
| `SendGrid__OrderStatusUpdateTemplateId`                  | `d-<template_id>`                           | Plain         |
| `SendGrid__OrderStatusUrl`                              | `https://{domain}/track-order`              | Plain         |
| `APPLICATIONINSIGHTS_CONNECTION_STRING`                  | Application Insights connection string      | Plain         |

> **Note**: `PUPPETEER_EXECUTABLE_PATH` is no longer needed — PDF generation uses QuestPDF natively without Chromium. The Azure Functions Dockerfile no longer installs Chromium packages.

> **Email links**: Order status emails use the `/track-order?orderNumber=X&email=Y` URL format. The `SendGrid__OrderStatusUrl` base is combined with query parameters at runtime.

> **Blob photo access**: Order photos and user files are accessed via SAS (Shared Access Signature) URLs generated at runtime, rather than direct blob access. This allows time-limited, secure access without exposing storage keys to clients.

> **Partner app authentication**: The Mobile API exposes a `PartnerLogin` endpoint (`POST /api/v1/auth/partner-login`) for the Android partner app, using the same JWT-based auth as the web Partner API.

> **Handlebars helpers**: Email body templates use Handlebars for rendering. A custom `eq` helper is registered for conditional logic in templates (e.g., `{{#if (eq status "Completed")}}`).

> **Email templates**: All SendGrid dynamic templates have been redesigned with a professional style (consistent branding, responsive layout). Template IDs above reflect the latest versions.

> **Currency**: The default currency is CZK (Czech Koruna), not EUR. All monetary formatting, Stripe charges, and invoice/receipt generation use CZK.

### Blob Storage Containers

| Container            | Purpose                              | Used By       |
| -------------------- | ------------------------------------ | ------------- |
| `generated-receipts` | Stores generated receipt PDFs        | Functions     |
| `generated-invoices` | Stores generated invoice PDFs        | Functions     |
| `user-files`         | User-uploaded files                  | APIs          |
| `employee-documents` | Employee document uploads            | APIs          |
| `order-photos`       | Order completion photos              | APIs          |

> **Removed containers**: `receipt-templates` and `invoice-templates` — no longer needed since PDF generation uses native QuestPDF layout builders instead of HTML templates.

### Storage Queues

| Queue                     | Purpose                              |
| ------------------------- | ------------------------------------ |
| `generate-receipt`        | Receipt PDF generation messages      |
| `generate-receipt-poison` | Failed receipt messages (after 5 retries) |
| `generate-invoice`        | Invoice PDF generation messages      |
| `generate-invoice-poison` | Failed invoice messages              |

---

## CI/CD Pipeline (GitHub Actions)

### Branch Strategy

```
feature/* ──► PR ──► master ──► auto-deploy DEV ──► manual approval ──► deploy PRO
```

### Pipeline Trigger

- **Push to `master`**: Automatically deploy to DEV
- **Manual approval**: Promote from DEV to PRO (GitHub Environments with required reviewers)

### Pipeline Steps

#### 1. Build & Test (.NET)

- [ ] Restore NuGet packages
- [ ] Build solution (`dotnet build Cleansia.Api.sln`)
- [ ] Run unit tests (`dotnet test`)
- [ ] Publish API project (`dotnet publish Cleansia.AppHost`)

#### 2. Build Angular Apps

- [ ] Install npm dependencies (`npm ci`)
- [ ] Build Customer app with SSR (`npx nx build cleansia.app --configuration=production`)
- [ ] Build Partner SPA (`npx nx build cleansia-partner.app --configuration=production`)
- [ ] Build Admin SPA (`npx nx build cleansia-admin.app --configuration=production`)

#### 3. Deploy to DEV

- [ ] Run EF Core migrations against DEV PostgreSQL
- [ ] Deploy .NET APIs to `api-cleansia-*-dev` App Services (sequential)
- [ ] Build Functions Docker image and push to ACR
- [ ] Deploy Functions container to `func-cleansia-dev`
- [ ] Deploy Customer SSR to `web-cleansia-customer-dev` App Service
- [ ] Deploy Partner SPA to `swa-cleansia-partner-dev` Static Web App
- [ ] Deploy Admin SPA to `swa-cleansia-admin-dev` Static Web App

#### 4. Deploy to PRO (manual approval)

- [ ] Run EF Core migrations against PRO PostgreSQL
- [ ] Deploy .NET APIs to `api-cleansia-*-pro` App Services
- [ ] Build Functions Docker image and push to ACR
- [ ] Deploy Functions container to `func-cleansia-pro`
- [ ] Deploy Customer SSR to `web-cleansia-customer-pro`
- [ ] Deploy Partner SPA to `swa-cleansia-partner-pro`
- [ ] Deploy Admin SPA to `swa-cleansia-admin-pro`

### GitHub Actions Workflow Structure

```
.github/workflows/
├── backend-ci.yml          # Build + test on PRs (no deploy)
├── deploy-dev.yml          # Triggered on push to master
├── deploy-pro.yml          # Triggered manually with confirmation
└── deploy-docs.yml         # Deploy VitePress docs to SWA on docs/ changes
```

Each workflow has jobs:

```
pr-check.yml:
  job: build-and-test        # dotnet build + test + nx build (no deploy)

deploy-dev.yml:
  job: build-dotnet           # Build + publish .NET APIs
  job: build-angular          # Build all 3 Angular apps (parallel with dotnet)
  job: migrate-database       # EF Core migrations (depends on build-dotnet)
  job: deploy-apis-dev        # Deploy 4 APIs sequentially (depends on migrate-database)
  job: build-deploy-functions # Build Docker image + deploy to Functions (depends on migrate-database)
  job: deploy-customer-dev    # Deploy SSR to App Service (depends on build-angular)
  job: deploy-partner-dev     # Deploy to Static Web App (depends on build-angular)
  job: deploy-admin-dev       # Deploy to Static Web App (depends on build-angular)

deploy-pro.yml:
  environment: production     # Requires manual approval
  job: deploy-apis-pro        # Deploy to App Service
  job: build-deploy-functions # Build Docker image + deploy to Functions
  job: deploy-customer-pro    # Deploy SSR to App Service
  job: deploy-partner-pro     # Deploy to Static Web App
  job: deploy-admin-pro       # Deploy to Static Web App
  job: migrate-db-pro         # Run EF Core migrations
```

---

## Step-by-Step Deployment Guide

### Prerequisites

- [ ] Azure subscription (Pay-As-You-Go or higher)
- [ ] Azure CLI installed (`az --version` >= 2.60)
- [ ] GitHub repository with Actions enabled
- [ ] Domain configured in Azure DNS (e.g., `cleansia.cz`)
- [ ] Node.js 20 LTS installed locally
- [ ] .NET 10 SDK installed locally

---

### Step 1: Create Azure Resources

#### 1.1 Create Resource Groups

```bash
# DEV
az group create --name rg-cleansia-dev --location westeurope

# PRO
az group create --name rg-cleansia-pro --location westeurope
```

#### 1.2 Create App Service Plans

```bash
# DEV — B1 (Basic, 1 core, 1.75 GB)
az appservice plan create --name asp-cleansia-dev --resource-group rg-cleansia-dev --sku B1 --is-linux

# PRO — S1 (Standard, 1 core, 1.75 GB, supports slots)
az appservice plan create \
  --name asp-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --sku S1 \
  --is-linux
```

#### 1.3 Create App Services (.NET APIs — one per API project)

Each .NET API project needs its own App Service. They all share the same App Service Plan (no extra cost).

```bash
# DEV — Partner API (employee-facing)
az webapp create --name api-cleansia-partner-dev --resource-group rg-cleansia-dev --plan asp-cleansia-dev --runtime "DOTNETCORE:10.0"

# DEV — Admin API (back-office)
az webapp create --name api-cleansia-admin-dev --resource-group rg-cleansia-dev --plan asp-cleansia-dev --runtime "DOTNETCORE:10.0"

# DEV — Customer API (public-facing)
az webapp create --name api-cleansia-customer-dev --resource-group rg-cleansia-dev --plan asp-cleansia-dev --runtime "DOTNETCORE:10.0"

# DEV — Mobile API (Android/iOS)
az webapp create --name api-cleansia-mobile-dev --resource-group rg-cleansia-dev --plan asp-cleansia-dev --runtime "DOTNETCORE:10.0"

# PRO — same pattern with staging slots
az webapp create --name api-cleansia-pro --resource-group rg-cleansia-pro --plan asp-cleansia-pro --runtime "DOTNETCORE:10.0"
az webapp create --name api-cleansia-admin-pro --resource-group rg-cleansia-pro --plan asp-cleansia-pro --runtime "DOTNETCORE:10.0"
az webapp create --name api-cleansia-customer-pro --resource-group rg-cleansia-pro --plan asp-cleansia-pro --runtime "DOTNETCORE:10.0"
az webapp create --name api-cleansia-mobile-pro --resource-group rg-cleansia-pro --plan asp-cleansia-pro --runtime "DOTNETCORE:10.0"

az webapp deployment slot create --name api-cleansia-pro --resource-group rg-cleansia-pro --slot staging
az webapp deployment slot create --name api-cleansia-admin-pro --resource-group rg-cleansia-pro --slot staging
az webapp deployment slot create --name api-cleansia-customer-pro --resource-group rg-cleansia-pro --slot staging
az webapp deployment slot create --name api-cleansia-mobile-pro --resource-group rg-cleansia-pro --slot staging
```

#### 1.4 Create App Services (Customer SSR — Node.js)

```bash
# DEV — Node.js 20 for Angular SSR
az webapp create --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --plan asp-cleansia-dev --runtime "NODE:20-lts"

# PRO — Node.js 20 + staging slot
az webapp create \
  --name web-cleansia-customer-pro \
  --resource-group rg-cleansia-pro \
  --plan asp-cleansia-pro \
  --runtime "NODE:20-lts"

az webapp deployment slot create \
  --name web-cleansia-customer-pro \
  --resource-group rg-cleansia-pro \
  --slot staging
```

#### 1.5 Create Static Web Apps (Partner & Admin SPAs)

```bash
# DEV
az staticwebapp create --name swa-cleansia-partner-dev --resource-group rg-cleansia-dev --location westeurope --sku Free

az staticwebapp create --name swa-cleansia-admin-dev --resource-group rg-cleansia-dev --location westeurope --sku Free

# PRO
az staticwebapp create \
  --name swa-cleansia-partner-pro \
  --resource-group rg-cleansia-pro \
  --location westeurope \
  --sku Free

az staticwebapp create \
  --name swa-cleansia-admin-pro \
  --resource-group rg-cleansia-pro \
  --location westeurope \
  --sku Free
```

#### 1.6 Create PostgreSQL Flexible Server

```bash
# DEV — Burstable B1ms
az postgres flexible-server create --name psql-cleansia-dev --resource-group rg-cleansia-dev --location westeurope --sku-name Standard_B1ms --tier Burstable --storage-size 32 --version 16 --admin-user cleansiaadmin --admin-password '<STRONG_PASSWORD_HERE>' --yes

az postgres flexible-server db create --resource-group rg-cleansia-dev --server-name psql-cleansia-dev --database-name cleansia

# PRO — General Purpose Standard_D2s_v3 (or Standard_B2ms for cost savings)
az postgres flexible-server create \
  --name psql-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --location westeurope \
  --sku-name Standard_D2s_v3 \
  --tier GeneralPurpose \
  --storage-size 64 \
  --version 16 \
  --admin-user cleansiaadmin \
  --admin-password '<STRONG_PASSWORD_HERE>' \
  --yes

az postgres flexible-server db create \
  --resource-group rg-cleansia-pro \
  --server-name psql-cleansia-pro \
  --database-name cleansia
```

#### 1.7 Create Key Vault

```bash
# DEV
az keyvault create --name kv-cleansia-dev --resource-group rg-cleansia-dev --location westeurope --sku standard

# PRO
az keyvault create \
  --name kv-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --location westeurope \
  --sku standard
```

#### 1.8 Create Application Insights

```bash
# DEV
az monitor app-insights component create --app ai-cleansia-dev --resource-group rg-cleansia-dev --location westeurope --kind web

# PRO
az monitor app-insights component create \
  --app ai-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --location westeurope \
  --kind web
```

---

### Step 2: Configure Key Vault

#### 2.1 Grant App Service Access to Key Vault

```bash
# Enable managed identity on all App Services
az webapp identity assign --name api-cleansia-partner-dev --resource-group rg-cleansia-dev
az webapp identity assign --name api-cleansia-admin-dev --resource-group rg-cleansia-dev
az webapp identity assign --name api-cleansia-customer-dev --resource-group rg-cleansia-dev
az webapp identity assign --name api-cleansia-mobile-dev --resource-group rg-cleansia-dev
az webapp identity assign --name web-cleansia-customer-dev --resource-group rg-cleansia-dev

# Get the principal IDs
PARTNER_PRINCIPAL=$(az webapp identity show --name api-cleansia-partner-dev --resource-group rg-cleansia-dev --query principalId -o tsv)
ADMIN_PRINCIPAL=$(az webapp identity show --name api-cleansia-admin-dev --resource-group rg-cleansia-dev --query principalId -o tsv)
CUSTOMER_API_PRINCIPAL=$(az webapp identity show --name api-cleansia-customer-dev --resource-group rg-cleansia-dev --query principalId -o tsv)
MOBILE_PRINCIPAL=$(az webapp identity show --name api-cleansia-mobile-dev --resource-group rg-cleansia-dev --query principalId -o tsv)
WEB_PRINCIPAL=$(az webapp identity show --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --query principalId -o tsv)

# Grant Key Vault access (RBAC-based — default for new vaults)
KV_SCOPE=$(az keyvault show --name kv-cleansia-dev --query id -o tsv)

az role assignment create --role "Key Vault Secrets User" --assignee $PARTNER_PRINCIPAL --scope $KV_SCOPE
az role assignment create --role "Key Vault Secrets User" --assignee $ADMIN_PRINCIPAL --scope $KV_SCOPE
az role assignment create --role "Key Vault Secrets User" --assignee $CUSTOMER_API_PRINCIPAL --scope $KV_SCOPE
az role assignment create --role "Key Vault Secrets User" --assignee $MOBILE_PRINCIPAL --scope $KV_SCOPE
az role assignment create --role "Key Vault Secrets User" --assignee $WEB_PRINCIPAL --scope $KV_SCOPE
```

Repeat for PRO environment.

#### 2.2 Store Secrets

```bash
# Connection string (use -- for nested keys)
az keyvault secret set --vault-name kv-cleansia-dev --name "ConnectionStrings--ConnectionString" --value 'Host=psql-cleansia-dev.postgres.database.azure.com;Database=cleansia;Username=cleansiaadmin;Password=<YOUR_ACTUAL_PASSWORD>;SSL Mode=Require'


# JWT
az keyvault secret set --vault-name kv-cleansia-dev --name "JwtSettings--Secret" --value "<GENERATE_A_256_BIT_KEY>"

# Stripe
az keyvault secret set \
  --vault-name kv-cleansia-dev \
  --name "Stripe--SecretKey" \
  --value "sk_test_..."

az keyvault secret set \
  --vault-name kv-cleansia-dev \
  --name "Stripe--WebhookSecret" \
  --value "whsec_..."

az keyvault secret set \
  --vault-name kv-cleansia-dev \
  --name "Stripe--PublishableKey" \
  --value "pk_test_..."

# SendGrid
az keyvault secret set \
  --vault-name kv-cleansia-dev \
  --name "SendGrid--ApiKey" \
  --value "SG...."

# Sentry
az keyvault secret set \
  --vault-name kv-cleansia-dev \
  --name "Sentry--Dsn" \
  --value "https://...@sentry.io/..."

# Azure Blob Storage
az keyvault secret set --vault-name kv-cleansia-dev --name "ConnectionStrings--BlobContainerConfigurationConnectionString" --value "$(az storage account show-connection-string --name stcleansiasdev --resource-group rg-cleansia-dev --query connectionString -o tsv)"

```

Repeat for PRO with production values (live Stripe keys, production SendGrid key, etc.).

---

### Step 3: Configure App Services

> **Important pattern**: Connection strings go in the **Connection Strings** tab (type: Custom), NOT as App Settings. The .NET code reads them via `configuration.GetConnectionString("ConnectionString")` which maps to the Connection Strings tab. Secrets use Key Vault references. Non-secret config (template IDs, URLs, log levels) is baked into `appsettings.Production.json`.

#### 3.1 Connection Strings (same for ALL 4 APIs)

All APIs share the same database and blob storage. These go in the **Connection Strings tab**, type `Custom`:

```bash
for APP in api-cleansia-partner-dev api-cleansia-admin-dev api-cleansia-customer-dev api-cleansia-mobile-dev; do
  az webapp config connection-string set \
    --name "$APP" --resource-group rg-cleansia-dev \
    --connection-string-type Custom \
    --settings \
      "ConnectionString=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=ConnectionStrings--ConnectionString)" \
      "BlobContainerConfigurationConnectionString=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=ConnectionStrings--BlobContainerConfigurationConnectionString)"
done
```

> **Do NOT** put `ConnectionStrings__*` as App Settings — the code uses `GetConnectionString()` which reads from the Connection Strings tab.

#### 3.2 App Settings — per API

Each API only gets the settings it actually uses. Not all APIs use Stripe.

**Partner API** (`api-cleansia-partner-dev`) — uses Stripe (has PaymentController):

```bash
az webapp config appsettings set \
  --name api-cleansia-partner-dev --resource-group rg-cleansia-dev \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "JwtSettings__Secret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=JwtSettings--Secret)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=SendGrid--ApiKey)" \
    SendGrid__SandboxMode=true \
    "Stripe__SecretKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--SecretKey)" \
    "Stripe__PublishableKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--PublishableKey)" \
    "Stripe__WebhookSecret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--WebhookSecret)" \
    "Stripe__SuccessUrlBase=https://web-cleansia-customer-dev.azurewebsites.net/checkout/success" \
    "Stripe__CancelUrlBase=https://web-cleansia-customer-dev.azurewebsites.net/checkout/cancel" \
    "CorsOrigins__0=https://swa-cleansia-partner-dev.azurestaticapps.net" \
    "CorsOrigins__1=https://web-cleansia-customer-dev.azurewebsites.net"
```

**Admin API** (`api-cleansia-admin-dev`) — NO Stripe (no PaymentController):

```bash
az webapp config appsettings set \
  --name api-cleansia-admin-dev --resource-group rg-cleansia-dev \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "JwtSettings__Secret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=JwtSettings--Secret)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=SendGrid--ApiKey)" \
    SendGrid__SandboxMode=true \
    "CorsOrigins__0=https://swa-cleansia-admin-dev.azurestaticapps.net"
```

**Customer API** (`api-cleansia-customer-dev`) — uses Stripe (has PaymentController):

```bash
az webapp config appsettings set \
  --name api-cleansia-customer-dev --resource-group rg-cleansia-dev \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "JwtSettings__Secret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=JwtSettings--Secret)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=SendGrid--ApiKey)" \
    SendGrid__SandboxMode=true \
    "Stripe__SecretKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--SecretKey)" \
    "Stripe__PublishableKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--PublishableKey)" \
    "Stripe__WebhookSecret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=Stripe--WebhookSecret)" \
    "Stripe__SuccessUrlBase=https://web-cleansia-customer-dev.azurewebsites.net/checkout/success" \
    "Stripe__CancelUrlBase=https://web-cleansia-customer-dev.azurewebsites.net/checkout/cancel" \
    "CorsOrigins__0=https://web-cleansia-customer-dev.azurewebsites.net" \
    "CorsOrigins__1=https://cleansia.cz"
```

**Mobile API** (`api-cleansia-mobile-dev`) — NO Stripe, NO CORS (native app):

```bash
az webapp config appsettings set \
  --name api-cleansia-mobile-dev --resource-group rg-cleansia-dev \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "JwtSettings__Secret=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=JwtSettings--Secret)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(VaultName=kv-cleansia-dev;SecretName=SendGrid--ApiKey)" \
    SendGrid__SandboxMode=true
```

#### 3.3 Which API uses what (reference table)

| Setting | Partner | Admin | Customer | Mobile |
|---|---|---|---|---|
| JwtSettings__Secret (KV) | Yes | Yes | Yes | Yes |
| SendGrid__ApiKey (KV) | Yes | Yes | Yes | Yes |
| SendGrid__SandboxMode | Yes | Yes | Yes | Yes |
| Stripe__SecretKey (KV) | Yes | **No** | Yes | **No** |
| Stripe__PublishableKey (KV) | Yes | **No** | Yes | **No** |
| Stripe__WebhookSecret (KV) | Yes | **No** | Yes | **No** |
| Stripe__SuccessUrlBase | Yes | **No** | Yes | **No** |
| Stripe__CancelUrlBase | Yes | **No** | Yes | **No** |
| CorsOrigins | Partner SWA + Customer SSR | Admin SWA | Customer SSR | None |
| ConnectionString (Conn. Strings tab) | Yes | Yes | Yes | Yes |
| BlobContainerConfigurationConnectionString (Conn. Strings tab) | Yes | Yes | Yes | Yes |

> **For PRO**: Replace `kv-cleansia-dev` → `kv-cleansia-pro`, `SendGrid__SandboxMode=false`, and use production CORS origins (custom domains like `partner.cleansia.cz`, `admin.cleansia.cz`, `cleansia.cz`).

#### 3.2 Customer SSR App Service Configuration

```bash
az webapp config appsettings set --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --settings WEBSITE_NODE_DEFAULT_VERSION="~20"

# Set startup command for Angular SSR
az webapp config set --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --startup-file "node server/server.mjs"
```

#### 3.3 Enable Always On (prevents cold starts)

```bash
az webapp config set --name api-cleansia-partner-dev --resource-group rg-cleansia-dev --always-on true
az webapp config set --name api-cleansia-admin-dev --resource-group rg-cleansia-dev --always-on true
az webapp config set --name api-cleansia-customer-dev --resource-group rg-cleansia-dev --always-on true
az webapp config set --name api-cleansia-mobile-dev --resource-group rg-cleansia-dev --always-on true
az webapp config set --name web-cleansia-customer-dev --resource-group rg-cleansia-dev --always-on true
```

### Step 3b: Set Up Azure Functions

Run the Azure Functions infrastructure setup script:

```bash
# Review and adjust variables at the top of the script (ENV, STORAGE_ACCOUNT, etc.)
chmod +x scripts/setup-azure-functions.sh
./scripts/setup-azure-functions.sh
```

This script creates:
- Azure Container Registry (stores Functions Docker images)
- Azure Functions App Service Plan (B1 Linux, separate from API plan)
- Azure Functions App (Docker container with native QuestPDF for PDF generation — no Chromium dependency)
- Managed Identity with roles: ACR Pull, Storage Blob Data Contributor, Storage Queue Data Contributor, Key Vault Secrets User
- Application Insights connection
- Queue storage connection string on all 4 API App Services
- Required blob containers: `generated-receipts`, `generated-invoices`
- Required storage queues: `generate-receipt`, `generate-invoice` (with poison queues)

**Important**: After running the script, add `ACR_NAME` to GitHub Actions secrets.

For PRO, change `ENV="pro"` at the top of the script and run again.

---

### Step 4: Configure Static Web Apps

#### 4.1 Routing Configuration

Create `staticwebapp.config.json` in each SPA's build output:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": [
      "/assets/*",
      "/*.ico",
      "/*.js",
      "/*.css",
      "/*.svg",
      "/*.png",
      "/*.jpg"
    ]
  },
  "responseOverrides": {
    "404": {
      "rewrite": "/index.html",
      "statusCode": 200
    }
  }
}
```

This ensures Angular client-side routing works (all routes fall back to `index.html`).

#### 4.2 Custom Domains

```bash
# Partner SPA — custom domain
az staticwebapp hostname set \
  --name swa-cleansia-partner-dev \
  --resource-group rg-cleansia-dev \
  --hostname partner-dev.cleansia.cz

# Admin SPA — custom domain
az staticwebapp hostname set \
  --name swa-cleansia-admin-dev \
  --resource-group rg-cleansia-dev \
  --hostname admin-dev.cleansia.cz
```

---

### Step 5: Set Up CI/CD

#### 5.1 GitHub Secrets

Add the following secrets to the GitHub repository:

| Secret                                          | Description                                     |
| ----------------------------------------------- | ----------------------------------------------- |
| `AZURE_CLIENT_ID`                               | Service principal client ID (OIDC federation)   |
| `AZURE_TENANT_ID`                               | Azure AD tenant ID                              |
| `AZURE_SUBSCRIPTION_ID`                         | Azure subscription ID                           |
| `DB_CONNECTION_STRING_DEV`                       | PostgreSQL connection string for DEV migrations |
| `DB_CONNECTION_STRING_PRO`                       | PostgreSQL connection string for PRO migrations |
| `ACR_NAME`                                       | Azure Container Registry name (e.g. crcleansiadev) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_DEV`   | Deployment token for partner SWA DEV            |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_DEV`     | Deployment token for admin SWA DEV              |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_PRO`   | Deployment token for partner SWA PRO            |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_PRO`     | Deployment token for admin SWA PRO              |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV`      | Deployment token for docs SWA DEV               |

#### 5.2 Create Service Principal

```bash
az ad sp create-for-rbac --name "github-cleansia-deploy" --role contributor --scopes /subscriptions/39aa3f73-f99d-4b0e-b946-0fe696cc3e13/resourceGroups/rg-cleansia-dev /subscriptions/39aa3f73-f99d-4b0e-b946-0fe696cc3e13/resourceGroups/rg-cleansia-pro --sdk-auth
```

Save the JSON output as `AZURE_CREDENTIALS` in GitHub Secrets.

#### 5.3 Get SWA Deployment Tokens

```bash
az staticwebapp secrets list --name swa-cleansia-partner-dev --resource-group rg-cleansia-dev --query "properties.apiKey" -o tsv
```

#### 5.4 GitHub Actions Workflow Outline

**`pr-check.yml`** — Runs on every PR:

- [ ] Checkout code
- [ ] Setup .NET 10 + Node.js 20
- [ ] `dotnet restore && dotnet build && dotnet test`
- [ ] `npm ci && npx nx run-many --target=build --all`

**`deploy-dev.yml`** — Runs on push to `master`:

- [ ] Build .NET solution and publish
- [ ] Build all 3 Angular apps (production config)
- [ ] Deploy APIs to `api-cleansia-partner-dev` via `azure/webapps-deploy@v3`
- [ ] Deploy Customer SSR to `web-cleansia-customer-dev`
- [ ] Deploy Partner SPA via `Azure/static-web-apps-deploy@v1`
- [ ] Deploy Admin SPA via `Azure/static-web-apps-deploy@v1`
- [ ] Run EF Core migrations

**`deploy-pro.yml`** — Manual trigger with approval:

- [ ] GitHub Environment: `production` (requires reviewer approval)
- [ ] Deploy to staging slots first
- [ ] Run smoke tests against staging
- [ ] Swap staging → production (zero downtime)

---

### Step 6: Database Migration

#### 6.1 Run Migrations Locally Against Azure

```bash
# Install EF Core tools if not already installed
dotnet tool install --global dotnet-ef

# Set connection string temporarily
export ConnectionStrings__DefaultConnection="Host=psql-cleansia-dev.postgres.database.azure.com;Database=cleansia;Username=cleansiaadmin;Password=<PASSWORD>;SSL Mode=Require"

# Run migrations from the infrastructure project
cd src/Cleansia.Infra.Database
dotnet ef database update \
  --startup-project ../Cleansia.Web/Cleansia.Web.csproj \
  --project Cleansia.Infra.Database.csproj
```

#### 6.2 Run Migrations in CI/CD

Add a step in the GitHub Actions workflow:

```bash
dotnet ef database update \
  --startup-project src/Cleansia.Web/Cleansia.Web.csproj \
  --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --connection "${{ secrets.DB_CONNECTION_STRING_DEV }}"
```

#### 6.3 Allow Azure App Service to Access PostgreSQL

```bash
# Allow Azure services to connect
az postgres flexible-server firewall-rule create \
  --resource-group rg-cleansia-dev \
  --name psql-cleansia-dev \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

> For PRO, consider using VNet integration + Private Endpoints instead of firewall rules for better security.

---

### Step 7: DNS Configuration

Assuming your domain is `cleansia.cz` and DNS is managed in Azure DNS.

#### 7.1 Create DNS Zone (if not already done)

```bash
az network dns zone create \
  --resource-group rg-cleansia-pro \
  --name cleansia.cz
```

#### 7.2 DNS Records

| Record Type | Name          | Value                                            | Environment |
| ----------- | ------------- | ------------------------------------------------ | ----------- |
| CNAME       | `api-dev`     | `api-cleansia-partner-dev.azurewebsites.net`             | DEV         |
| CNAME       | `dev`         | `web-cleansia-customer-dev.azurewebsites.net`    | DEV         |
| CNAME       | `partner-dev` | `<swa-cleansia-partner-dev>.azurestaticapps.net` | DEV         |
| CNAME       | `admin-dev`   | `<swa-cleansia-admin-dev>.azurestaticapps.net`   | DEV         |
| CNAME       | `api`         | `api-cleansia-pro.azurewebsites.net`             | PRO         |
| A / CNAME   | `@` or `www`  | `web-cleansia-customer-pro.azurewebsites.net`    | PRO         |
| CNAME       | `partner`     | `<swa-cleansia-partner-pro>.azurestaticapps.net` | PRO         |
| CNAME       | `admin`       | `<swa-cleansia-admin-pro>.azurestaticapps.net`   | PRO         |
| TXT         | `asuid.api`   | Custom domain verification ID                    | Both        |

```bash
# Example: Create CNAME for api-dev
az network dns record-set cname set-record \
  --resource-group rg-cleansia-pro \
  --zone-name cleansia.cz \
  --record-set-name api-dev \
  --cname api-cleansia-partner-dev.azurewebsites.net

# Example: Create CNAME for production API
az network dns record-set cname set-record \
  --resource-group rg-cleansia-pro \
  --zone-name cleansia.cz \
  --record-set-name api \
  --cname api-cleansia-pro.azurewebsites.net
```

#### 7.3 Add Custom Domains to App Services

```bash
# Add custom domain to API
az webapp config hostname add \
  --webapp-name api-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --hostname api.cleansia.cz
```

---

### Step 8: SSL Certificates

Azure provides free managed certificates for custom domains on App Services.

#### 8.1 Create Managed Certificates

```bash
# Create managed cert for API
az webapp config ssl create \
  --name api-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --hostname api.cleansia.cz

# Bind SSL certificate
az webapp config ssl bind \
  --name api-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --certificate-thumbprint <THUMBPRINT_FROM_PREVIOUS_COMMAND> \
  --ssl-type SNI
```

#### 8.2 Enforce HTTPS

```bash
az webapp update \
  --name api-cleansia-pro \
  --resource-group rg-cleansia-pro \
  --https-only true

az webapp update \
  --name web-cleansia-customer-pro \
  --resource-group rg-cleansia-pro \
  --https-only true
```

> **Static Web Apps** automatically provide SSL certificates for both the default `*.azurestaticapps.net` domain and any custom domains. No manual certificate management needed.

---

## Documentation Site Deployment

The project documentation is built with [VitePress](https://vitepress.dev/) and deployed to an Azure Static Web App.

### Architecture

- **Source**: `docs/` directory in the repository (Markdown files + VitePress config)
- **Build**: `npm ci && npm run build` inside `docs/`
- **Output**: `docs/.vitepress/dist`
- **Deployment target**: `swa-cleansia-docs-dev` (Azure Static Web App, Free tier)

### Azure Resource

```bash
# Create the Docs Static Web App (DEV)
az staticwebapp create \
  --name swa-cleansia-docs-dev \
  --resource-group rg-cleansia-dev \
  --location westeurope \
  --sku Free
```

### CI/CD

The docs site is deployed automatically via `.github/workflows/deploy-docs.yml` whenever files in `docs/` are pushed to `master`.

**GitHub Secret required:**

| Secret                                        | Description                              |
| --------------------------------------------- | ---------------------------------------- |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV`    | Deployment token for docs SWA DEV        |

Get the token:

```bash
az staticwebapp secrets list --name swa-cleansia-docs-dev --resource-group rg-cleansia-dev --query "properties.apiKey" -o tsv
```

### Custom Domain (optional)

```bash
az staticwebapp hostname set \
  --name swa-cleansia-docs-dev \
  --resource-group rg-cleansia-dev \
  --hostname docs-dev.cleansia.cz
```

---

## Cost Breakdown

| Resource                                   | DEV (Monthly)    | PRO (Monthly)      |
| ------------------------------------------ | ---------------- | ------------------ |
| App Service Plan (B1 / S1)                 | $13              | $73                |
| App Service Plan (Functions B1)              | $13              | $13                |
| PostgreSQL Flexible Server (B1ms / D2s_v3) | $15              | $130               |
| Static Web Apps (Free)                     | $0               | $0                 |
| Key Vault (operations)                     | ~$1              | ~$1                |
| Application Insights                       | $0 (free tier)   | ~$10               |
| Bandwidth (egress)                         | ~$5              | ~$50               |
| Azure Blob Storage                         | ~$2              | ~$10               |
| Azure Container Registry (Basic)             | ~$5              | ~$5                |
| SendGrid                                   | $0 (sandbox)     | ~$20 (Essentials)  |
| Sentry                                     | Free (Developer) | Free (Team)        |
| Stripe                                     | $0 (test mode)   | 2.9% + 30c per txn |
| **Total**                                  | **~$66/mo**      | **~$360/mo**       |

> **Notes:**
>
> - PRO costs will scale with usage (bandwidth, storage, App Insights data volume).
> - Static Web Apps Free tier supports custom domains and SSL at no cost.
> - Consider Azure Reserved Instances (1-year) for ~30% savings on App Service and PostgreSQL in PRO.
> - B1ms PostgreSQL in DEV has 640 IOPS and 2 GB RAM — sufficient for development/testing.

---

## Post-Deployment Checklist

- [ ] Verify all 4 APIs respond on `/health` and `/alive` endpoints
- [ ] Verify Customer app SSR renders correctly (view page source for server-rendered HTML)
- [ ] Verify Partner and Admin SPAs load and route correctly
- [ ] Test Stripe webhook delivery (DEV: use Stripe CLI, PRO: check Stripe Dashboard)
- [ ] Verify SendGrid emails (DEV: check sandbox logs, PRO: send test email)
- [ ] Verify Sentry error reporting (throw a test exception)
- [ ] Run a full order flow: create order → pay → complete → invoice
- [ ] Verify GDPR endpoints (data export, account deletion)
- [ ] Check Application Insights for telemetry data
- [ ] Verify custom domains resolve correctly with HTTPS
- [ ] Test database backup/restore procedure (PRO)
- [ ] Set up Azure Alerts for API response time > 5s, error rate > 5%, DB CPU > 80%
- [ ] Verify Azure Functions are running (check function list in Azure Portal)
- [ ] Test receipt generation: create a cash order → check if receipt PDF appears in blob storage
- [ ] Verify queue processing: check generate-receipt queue is being consumed
- [ ] Verify order status update emails are sent correctly (check `SendGrid__OrderStatusUpdateTemplateId` is configured)
- [ ] Verify `/track-order?orderNumber=X&email=Y` link in emails resolves correctly
- [ ] Verify SAS URLs for blob photo access are generated and expire appropriately
- [ ] Test PartnerLogin endpoint from Android app (`POST /api/v1/auth/partner-login`)
- [ ] If DEV: configure EasyAuth excluded paths for `/assets/*` on Customer SSR

---

## Security Checklist

- [ ] All secrets stored in Key Vault (none in appsettings, environment, or code)
- [ ] HTTPS enforced on all App Services
- [ ] CORS restricted to known origins only
- [ ] Rate limiting enabled on auth endpoints (10 req/min)
- [ ] PostgreSQL firewall rules restrict access (PRO: VNet + Private Endpoints)
- [ ] App Service managed identity used for Key Vault access (no stored credentials)
- [ ] GitHub Secrets used for CI/CD credentials (never committed to repo)
- [ ] API versioning enabled (`/api/v1/...`)
- [ ] Stripe webhook signature verification active
- [ ] JWT signing key is at least 256 bits
- [ ] Azure Functions Managed Identity has minimum required roles (no Owner/Contributor)
- [ ] Storage account configured for private access (Managed Identity, not connection string keys)
- [ ] DEV environment gated with EasyAuth (Microsoft Entra ID login required)

---

**Version**: 2.1.0
**Last Updated**: 2026-04-02
**Status**: DEV deployed and operational, PRO ready for deployment
