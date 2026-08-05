# Environment Configuration

This page documents the configuration settings for each Cleansia API, covering app settings, Key Vault references, connection strings, and local development setup.

::: info Source Files
- Partner API settings: `src/Cleansia.Web/appsettings.json`, `appsettings.Production.json`
- Stripe config: `src/Cleansia.Infra.Common/Configuration/StripeConfig.cs`
- JWT config: `src/Cleansia.Infra.Common/Configuration/Interfaces/IJwtSettings.cs`
:::

## App Settings Structure

Each API project has three configuration files:

| File | Purpose |
|------|---------|
| `appsettings.json` | Base settings (shared across environments) |
| `appsettings.Development.json` | Local development overrides |
| `appsettings.Production.json` | Production overrides |

Settings are loaded in order with later files overriding earlier ones. Sensitive values are injected via Azure Key Vault references in deployed environments or `dotnet user-secrets` locally.

## Configuration Sections

### Connection Strings

```json
{
  "ConnectionStrings": {
    "ConnectionString": "Host=...;Database=cleansia;Username=...;Password=...",
    "BlobContainerConfigurationConnectionString": "DefaultEndpointsProtocol=https;..."
  }
}
```

| Key | Purpose | Source |
|-----|---------|--------|
| `ConnectionString` | PostgreSQL database | Key Vault / User Secrets |
| `BlobContainerConfigurationConnectionString` | Azure Blob Storage | Key Vault (`UseDevelopmentStorage=true` locally) |

### JWT Settings

```json
{
  "JwtSettings": {
    "Secret": "SET_VIA_USER_SECRETS",
    "DefaultTokenExpHours": 6,
    "CookieTokenExpHours": 1
  }
}
```

| Setting | Type | Description |
|---------|------|-------------|
| `Secret` | string | HMAC-SHA256 signing key (min 32 chars) |
| `DefaultTokenExpHours` | double | Standard token lifetime (6h) |
| `CookieTokenExpHours` | double | Remember-me token lifetime (1h) |

::: warning
`Secret` must be at least 32 characters and must be the same across all API instances in an environment to ensure tokens are valid across services.
:::

### Stripe

```json
{
  "Stripe": {
    "SecretKey": "sk_...",
    "PublishableKey": "pk_...",
    "WebhookSecret": "whsec_...",
    "WebhookUrl": "/api/payments/webhook",
    "SuccessUrlBase": "https://cleansia.cz/checkout/success",
    "CancelUrlBase": "https://cleansia.cz/checkout/cancel"
  }
}
```

| Setting | Sensitive | Description |
|---------|-----------|-------------|
| `SecretKey` | Yes | Stripe secret API key |
| `PublishableKey` | Yes | Stripe publishable key |
| `WebhookSecret` | Yes | Stripe webhook signing secret |
| `WebhookUrl` | No | Relative path for webhook endpoint |
| `SuccessUrlBase` | No | Redirect URL after successful payment |
| `CancelUrlBase` | No | Redirect URL after cancelled payment |

### SendGrid

```json
{
  "SendGrid": {
    "ApiKey": "SG...",
    "ResetPasswordTemplateId": "d-c475f44d635f40569aa8b5171dc63270",
    "OrderReceiptTemplateId": "d-2e4f0bcc8af54b3d88c471d7e0cd507a",
    "EmailConfirmationTemplateId": "d-eb7daac9cbe94f01beb2ee1bb0ec5c29",
    "PeriodClosedTemplateId": "d-75a0f9cfdcc44eabb617de12e28d784d",
    "PeriodEndReminderTemplateId": "d-d8428c5ffff14355a59d0a35023445da",
    "AddressFrom": "it@cleansia.cz",
    "ResetPasswordUrl": "https://partner.cleansia.cz/forgot-password",
    "OrderStatusUrl": "https://partner.cleansia.cz/orders"
  }
}
```

| Setting | Sensitive | Description |
|---------|-----------|-------------|
| `ApiKey` | Yes | SendGrid API key |
| `*TemplateId` | No | SendGrid dynamic template IDs |
| `AddressFrom` | No | Sender email address |
| `ResetPasswordUrl` | No | Frontend password reset page URL |
| `OrderStatusUrl` | No | Frontend order status page URL |

### CORS Origins

```json
{
  "CorsOrigins": [
    "http://localhost:4200",
    "https://partner.cleansia.cz"
  ]
}
```

Production (`appsettings.Production.json`):

```json
{
  "CorsOrigins": [
    "https://partner.cleansia.cz",
    "https://cleansia.cz"
  ]
}
```

### Sentry (Error Tracking)

```json
{
  "Sentry": {
    "Dsn": "",
    "Environment": "production",
    "TracesSampleRate": 0.2
  }
}
```

::: warning Only `Dsn` is read, and it is empty everywhere
`UseSentryMonitoring` (`Cleansia.ServiceDefaults/Extensions.cs:85-112`) reads exactly one key —
`Sentry:Dsn`. `Environment` and `TracesSampleRate` in this block are **not bound**; the sample rate is
fixed at `0.2` in code. A blank or absent DSN leaves the SDK uninitialized, which is the deliberate
guard that stops a DSN-less host from failing to boot.

**Every committed `appsettings*.json` across the five APIs sets `"Dsn": ""`.** In Azure the value is a
Key Vault reference (`main.bicep:467`) fed by the `SENTRY_DSN` GitHub secret, and the DEV runbook
directs that it stay empty (`deploy/AZURE-DEV-RUNBOOK.md:239`). Since DEV is the only deployed
environment, **Sentry is not collecting anywhere right now**. See
[Infrastructure → Observability](/architecture/infrastructure#observability) for what does still
alert.
:::

### Logging

Development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Production:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

## Per-Environment Values

### Development

| Setting | Value |
|---------|-------|
| CORS | `http://localhost:4200`, `http://localhost:4201` |
| Stripe Success URL | `http://localhost:4200/checkout/success` |
| Stripe Cancel URL | `http://localhost:4200/checkout/cancel` |
| Blob Storage | `UseDevelopmentStorage=true` (Azurite) |
| SendGrid URLs | `http://localhost:4200/...` |
| Logging | `Information` level |

### Production

| Setting | Value |
|---------|-------|
| CORS | `https://partner.cleansia.cz`, `https://cleansia.cz` |
| Stripe Success URL | `https://cleansia.cz/checkout/success` |
| Stripe Cancel URL | `https://cleansia.cz/checkout/cancel` |
| SendGrid URLs | `https://partner.cleansia.cz/...` |
| Logging | `Warning` level |
| Sentry | Enabled with a 20% trace rate **only once `Sentry--Dsn` holds a real DSN** — production has never been deployed, and the committed value is empty |

## Key Vault References

In Azure App Service, sensitive settings are stored as Key Vault references:

```
@Microsoft.KeyVault(VaultName=kv-cleansia-{env};SecretName={section}--{key})
```

Examples:

| App Setting Key | Key Vault Secret Name |
|----------------|----------------------|
| `JwtSettings__Secret` | `JwtSettings--Secret` |
| `Stripe__SecretKey` | `Stripe--SecretKey` |
| `Stripe__PublishableKey` | `Stripe--PublishableKey` |
| `Stripe__WebhookSecret` | `Stripe--WebhookSecret` |
| `SendGrid__ApiKey` | `SendGrid--ApiKey` |
| `ConnectionStrings__ConnectionString` | `ConnectionStrings--ConnectionString` |
| `ConnectionStrings__BlobContainerConfigurationConnectionString` | `ConnectionStrings--BlobStorage` |

::: tip
Azure uses `__` (double underscore) as the hierarchy separator in environment variables, while Key Vault uses `--` (double dash) in secret names. Both map to the `:` separator in .NET configuration.
:::

## Local Development Setup

### 1. Initialize User Secrets

```bash
cd src/Cleansia.Web.Partner
dotnet user-secrets init
```

### 2. Set Required Secrets

```bash
dotnet user-secrets set "ConnectionStrings:ConnectionString" "Host=localhost;Database=cleansia;Username=postgres;Password=yourpassword"
dotnet user-secrets set "JwtSettings:Secret" "your-32-char-minimum-secret-key-here"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "SendGrid:ApiKey" "SG...."
```

### 3. Start Local Dependencies

```bash
# Start Azurite for Blob/Queue storage emulation
azurite --silent --location ./azurite-data

# Start PostgreSQL (if not running)
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=yourpassword postgres:16
```

### 4. Run Database Migrations

```bash
cd src
dotnet ef database update \
  --project Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project Cleansia.Web.Partner/Cleansia.Web.Partner.csproj
```

::: warning Migrations are owner-run
Claude never runs `dotnet ef migrations add` / `database update` — it flags
`manual_step: ef-migration` instead. Running the Aspire AppHost applies migrations automatically via
`Cleansia.MigrationService`, so this step is only for a standalone single-API run.
:::

### 5. Start the API

```bash
cd src
dotnet run --project Cleansia.Web.Partner
```

The Partner API starts at `http://localhost:5000` (HTTP) / `https://localhost:8000`.

| Host | Port |
|---|---|
| `Cleansia.Web.Partner` | 5000 |
| `Cleansia.Web.Admin` | 5001 |
| `Cleansia.Web.Mobile.Partner` | 5002 |
| `Cleansia.Web.Customer` | 5003 |
| `Cleansia.Web.Mobile.Customer` | 5004 |

Or start everything at once — Postgres, the migrator, all five APIs and the Functions host:

```bash
cd src
dotnet run --project Cleansia.AppHost
```

::: tip Stripe Webhooks Locally
Use the Stripe CLI to forward webhooks to the host that owns the endpoint you are exercising:
```bash
stripe listen --forward-to http://localhost:5000/api/Payment/webhook   # Partner API
```
The CLI will print a webhook signing secret (`whsec_...`) to use as `Stripe:WebhookSecret`.
:::

### 6. Android App

For the Android app connecting to a local backend, the debug build type uses
`http://10.0.2.2:5002/api` — `10.0.2.2` maps to the host machine's `localhost` from the Android
emulator, and `5002` is the **Partner Mobile API**. The customer Android app targets `5004`.
