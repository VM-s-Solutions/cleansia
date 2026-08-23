# Infrastructure

Cleansia runs on Microsoft Azure (West Europe region) with separate DEV and PRO environments. Infrastructure is managed through the Azure Portal with Key Vault for secrets management.

## Environments and Cost

| Environment | Purpose | Estimated Cost |
|------------|---------|---------------|
| DEV | Development and testing | ~$66/month |
| PRO | Production | ~$360/month |

### Resource Inventory

| Resource | DEV | PRO |
|----------|-----|-----|
| **App Service Plan** | Basic B1 | Standard S1 |
| **App Service** (Customer API + SSR) | 1 instance | 1 instance |
| **App Service** (Partner API) | 1 instance | 1 instance |
| **App Service** (Admin API) | 1 instance | 1 instance |
| **App Service** (Mobile API) | 1 instance | 1 instance |
| **Static Web App** (Partner SPA) | Free tier | Standard |
| **Static Web App** (Admin SPA) | Free tier | Standard |
| **PostgreSQL Flexible Server** | Burstable B1ms | General Purpose D2s_v3 |
| **Storage Account** | LRS | LRS |
| **Azure Functions** | Consumption (Docker) | Consumption (Docker) |
| **Key Vault** | Standard | Standard |
| **Application Insights** | Basic | Basic |
| **Container Registry** | Basic | Basic |

::: warning The SSR host still sends nothing, and App Insights volume changed in August 2026
The connection string is injected into all seven hosts. The five APIs began exporting to it at T-0500
(they had been silently exporting nowhere); the Functions host always did; the **customer SSR host is
Node and reads it in no environment**. Read [Observability](#observability) before using this row to
reason about monitoring coverage or App Insights cost — the cost side of this row is now six producers,
not one.
:::

::: tip Cost Optimization
The DEV environment uses burstable and basic tiers everywhere. The biggest cost difference is the PostgreSQL server — Burstable B1ms (~$13/mo) vs General Purpose D2s_v3 (~$130/mo).
:::

## Key Vault

### RBAC Strategy

Key Vault uses Azure RBAC (not access policies) for authorization. Each App Service has a system-assigned managed identity with the **Key Vault Secrets User** role.

```
Key Vault
├── App Services ──► Key Vault Secrets User (read-only)
├── Functions ──► Key Vault Secrets User (read-only)
└── CI/CD (GitHub Actions) ──► Key Vault Secrets Officer (read/write)
```

### Secrets Inventory

| Secret | Used By | Purpose |
|--------|---------|---------|
| `Jwt--Key` | All APIs | JWT signing key (issuer/audience are code-side constants, not KV secrets) |
| `ConnectionStrings--cleansia-db` | All APIs, Functions, CI migrate job | PostgreSQL connection string |
| `Stripe--SecretKey` | Customer API | Stripe payment processing |
| `Stripe--WebhookSecret` | Customer API | Stripe webhook signature verification |
| `SendGrid--ApiKey` | Functions, APIs | Email delivery |
| `Sentry--Dsn` | The five APIs (not Functions) | Error tracking — **empty on DEV, so Sentry is off**. See [Observability](#observability) |
| `Storage--ConnectionString` | All APIs, Functions | Azure Blob/Queue Storage |
| `Fiscal--CzechEet2--ApiKey` | APIs, Functions (only once `fiscalSecretProvisioned` is true) | Czech EET fiscal API key |
| `Fiscal--CzechEet2--CertificatePassword` | APIs, Functions (only once `fiscalSecretProvisioned` is true) | Czech EET certificate password |

::: warning Secret Rotation
The `Jwt--Key` and `Stripe--SecretKey` should be rotated periodically. Coordinate JWT key rotation with a grace period where both old and new keys are valid.
:::

## Blob Storage

### Containers

| Container | Access Level | Purpose | Retention |
|-----------|-------------|---------|-----------|
| `generated-receipts` | Private | Customer receipt PDFs | Indefinite |
| `generated-invoices` | Private | Employee invoice PDFs | Indefinite |
| `user-files` | Private | Customer-uploaded files | Until account deletion |
| `employee-documents` | Private | Contracts, IDs, certifications | Per GDPR policy |
| `order-photos` | Private | Before/after cleaning photos | Tied to order lifecycle |

### Blob Naming Convention

```
{container}/{tenantId}/{entityId}/{filename}

Examples:
generated-receipts/abc123/order-456/receipt-2025-01-15.pdf
employee-documents/abc123/emp-789/contract-2025.pdf
order-photos/abc123/order-456/before-kitchen-001.jpg
```

### Usage in Code

```csharp
public class BlobStorageService(BlobServiceClient blobServiceClient) : IBlobStorageService
{
    public async Task<Uri> UploadAsync(
        string containerName, string blobPath, Stream content, string contentType)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(content, new BlobHttpHeaders
        {
            ContentType = contentType
        });

        return blobClient.Uri;
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobPath)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }
}
```

## Storage Queues

Queues decouple the APIs from long-running operations (PDF generation). Each queue has a corresponding poison queue for failed messages.

| Queue | Poison Queue | Producer | Consumer |
|-------|-------------|----------|----------|
| `generate-receipt` | `generate-receipt-poison` | Customer API (after payment) | `GenerateReceipt` function |
| `generate-invoice` | `generate-invoice-poison` | Admin API (period close) | `GenerateInvoice` function |

### Queue Message Format

```json
// generate-receipt queue message
{
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "a1b2c3d4-...",
  "locale": "cs-CZ"
}

// generate-invoice queue message
{
  "payPeriodId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "employeeId": "9fa85f64-...",
  "tenantId": "a1b2c3d4-..."
}
```

### Poison Queue Handling

Messages that fail processing 5 times are moved to the poison queue automatically by the Azure Functions runtime. Poison queue messages should be monitored and investigated.

::: warning
Poison queue messages indicate a bug or data issue. Alerting on them is provisioned by `deploy/bicep/modules/queueAlerts.bicep`: queue-service diagnostic settings ship `StorageWrite`/`StorageDelete` logs to Log Analytics, and a scheduled-query rule (`alert-poison-queue-cleansia-<region>-<env>`) fires on any successful `PutMessage` into a `*-poison` queue, notifying the ops Action Group.
:::

## Azure Functions

All functions run in a single Azure Functions project deployed as a Docker container (required for QuestPDF native dependencies).

### Function Inventory

**34 functions**, 20 timers and 14 queue consumers. This inventory listed five of them until
2026-08-22, which is a large part of why nobody noticed that eight timers had never fired at all — see
[the schedule tokens](#timer-schedules) below.

#### Timers

| Function | Schedule | Purpose |
|---|---|---|
| `OutboxDrainer` | every 10 s | Drains the transactional outbox onto the queues |
| `FiscalReconciliation` | every 5 min | Reconciles fiscal registrations against the EET API |
| `RetryFailedFiscalRegistrations` | every 5 min | Retries registrations that failed transiently |
| `NotifyLapsedPreferredOffers` | every 5 min | Closes a preferred-cleaner hold that expired and reopens the order |
| `SendPreCleaningReminders` | `%Cron%` — every 5 min | Reminds a **customer** their cleaning is coming up |
| `SendCleanerJobReminders` | `%Cron%` — every 5 min | Reminds a **cleaner** two hours out; nudges them close to the start if they have not set off |
| `CleanupStalePendingOrders` | every 15 min | Releases orders stuck awaiting payment |
| `SendNewJobsDigest` | `%Cron%` — hourly | Tells cleaners how many new offerable jobs are near them |
| `SendTomorrowJobDigest` | `%Cron%` — hourly | Tells each cleaner how many jobs they have tomorrow, at 18:00 **local** — hourly because a UTC cron cannot be timezone-aware |
| `AutoCancelStaleRecurringOrders` | hourly | Cancels recurring instances nobody took in time |
| `CloseExpiredPayPeriods` | daily 02:00 UTC | Marks pay periods past their end date as closed |
| `MaterializeRecurringBookings` | `%Cron%` — daily 02:00 UTC | Turns recurring bookings into real orders |
| `SendRecurringOrderReminders` | `%Cron%` — daily 02:30 UTC | Warns a customer about an upcoming recurring instance |
| `SendMembershipLifecycleNotifications` | `%Cron%` — daily 03:00 UTC | Expiry, renewal and cancellation notices |
| `RefreshTokenCleanup` | daily 03:30 UTC | Deletes expired refresh tokens |
| `ExpireStaleReferrals` | `%Cron%` — daily 03:30 UTC | Expires referrals nobody redeemed |
| `LiveActivityJanitor` | daily 04:00 UTC | Ends Live Activities whose orders are long finished |
| `PruneOutbox` | daily 04:00 UTC | Deletes drained outbox rows |
| `SendPeriodEndReminders` | daily 09:00 UTC | Emails employees whose pay period ends in 3 days |
| `DataRetentionCleanup` | weekly, Sun 03:00 UTC | GDPR — deletes expired user data, anonymizes old orders |

#### Queue consumers

| Function | Queue | Purpose |
|---|---|---|
| `GenerateReceipt` | `generate-receipt` | Receipt PDF via QuestPDF → blob storage → SendGrid |
| `GenerateInvoice` | `generate-invoice` | Employee invoice PDF → blob storage |
| `CalculateOrderPay` | `calculate-order-pay` | Computes a cleaner's pay for a finished order |
| `SendEmail` | `send-email` | SendGrid delivery |
| `SendPushNotification` | `notifications-dispatch` | FCM delivery |
| `SendLiveActivityUpdate` | `live-activity-dispatch` | APNs Live Activity updates |
| `SendSitewidePromoFanout` | `sitewide-promo-fanout` | Fans a sitewide promo out to recipients |

Each of those seven has a matching `*Poison` consumer on `<queue>-poison`.

### The eight `%Cron%` schedules — and how they never ran {#timer-schedules}

Eight timers above declare their schedule as `[TimerTrigger("%SomeCron%")]` rather than a literal. That
token is expanded by the Functions **host**, from platform **application settings**.

`src/Cleansia.Functions/appsettings.json` is loaded by `Program.cs` into the **isolated worker's**
`IConfiguration` — a different process and a different configuration object, which the host never reads.
Nothing in `deploy/bicep` set a single one of these keys until 2026-08-22, so the tokens never resolved,
the timer listeners were never created, and those functions **simply never ran in Azure**: no error, no
invocation, no telemetry. The timers carrying literal crons were unaffected, which is the only reason
the split was ever visible.

They now live in the `cronSettings` var in `main.bicep`, unioned into the Function App's app settings.
`TimerCronSettingsAreDeployedTests` discovers the tokens by **reflection** and fails the build if a
tokenized timer is added without a matching key — a hand-maintained list would not have caught the two
timers added the same day.

### Docker Deployment

Functions run in a custom Docker image because QuestPDF requires native Linux libraries:

```dockerfile
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
WORKDIR /home/site/wwwroot
COPY ./publish .

# QuestPDF native dependencies
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    && rm -rf /var/lib/apt/lists/*
```

### Example: GenerateReceipt Function

```csharp
public class GenerateReceiptFunction(
    ISender sender,
    ILogger<GenerateReceiptFunction> logger)
{
    [Function("GenerateReceipt")]
    public async Task Run(
        [QueueTrigger("generate-receipt")] GenerateReceiptMessage message)
    {
        logger.LogInformation("Generating receipt for order {OrderId}", message.OrderId);

        var result = await sender.Send(new GenerateReceipt.Command(
            message.OrderId, message.TenantId, message.Locale));

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"Receipt generation failed: {result.Error!.Message}");
    }
}
```

## Service Integrations

### Stripe

Used for customer payments via Checkout Sessions.

| Configuration | Purpose |
|--------------|---------|
| `Stripe:SecretKey` | Server-side API calls |
| `Stripe:WebhookSecret` | Webhook signature verification |
| `Stripe:SuccessUrl` | Redirect after successful payment |
| `Stripe:CancelUrl` | Redirect after cancelled payment |

**Flow:**
1. Customer API creates a Stripe Checkout Session
2. Customer completes payment on Stripe-hosted page
3. Stripe sends `checkout.session.completed` webhook to Customer API
4. Customer API updates order status and enqueues receipt generation

### SendGrid

Used for all transactional emails via Dynamic Templates.

| Template | Trigger |
|----------|---------|
| Order Confirmation | After order creation |
| Receipt | After payment (with PDF attachment) |
| Pay Period Reminder | 3 days before period end |
| Welcome Email | After registration |
| Password Reset | On password reset request |

```csharp
public class EmailService(ISendGridClient client) : IEmailService
{
    public async Task SendTemplateEmailAsync(
        string to, string templateId, object templateData)
    {
        var message = new SendGridMessage();
        message.SetFrom("noreply@cleansia.cz", "Cleansia");
        message.AddTo(to);
        message.SetTemplateId(templateId);
        message.SetTemplateData(templateData);

        await client.SendEmailAsync(message);
    }
}
```

## Health probes — liveness restarts, readiness reports {#health-probes}

Two endpoints, and the difference is a restart policy rather than a naming preference.

| Endpoint | Answers | Who acts on it |
|---|---|---|
| `/alive` | is this process broken? | **Azure App Service** — it responds by **recycling the instance** |
| `/health` | are my dependencies reachable? | the deploy warm loop, and monitoring |

**Azure's probe polls `/alive`.** It must, because the only honest input to "kill and restart this
instance" is whether the instance itself is broken. Handing a supervisor a signal that goes red when a
*shared* dependency slows down means every instance restarts at once, and the restart is the most
expensive thing you can do to a dependency that is already short of capacity.

::: danger This was learned the hard way, on 2026-08-15
`healthCheckPath` pointed at `/health`. A saturated dev Postgres — `Standard_B1ms`, one burstable vCore,
shared by five APIs and the Functions host — made that probe take **95 seconds**. App Service recycled
the instance; the restart cold-started onto the contended B2 plan, rebuilt a 70-entity EF model and a
fresh connection pool against the same saturated server, and failed the next probe. Both mobile APIs
became unusable, and every recycle took capacity from the database that had none to give.

The blob check had already reasoned its way to the right answer — *"recycling everything during a
storage outage only amplifies it"* — and returned Degraded-but-200 for exactly that reason. It simply
was not applied to the database, where the premise was a **per-instance wedged pool** rather than a
shared server with nothing left. A recycle does rebuild a wedged pool; it cannot manufacture capacity.
:::

**Both readiness checks are bounded at 5 seconds.** An unbounded check cannot be acted on by anyone: the
deploy warm loop allows 15 seconds per probe, so a 95-second answer is indistinguishable from no answer.
`ReadinessHealthChecks.ReadinessCheckTimeout` is the single value, and `AppServiceHealthProbeTests` pins
it against the warm loop's patience so the two cannot drift apart.

`/health` keeps its failure semantics: database Unhealthy (an instance with no database fails every
request anyway), blob Degraded-but-200. What changed is only who is allowed to respond by killing things.

> The SSR host is the one exception and is set explicitly in `main.bicep`. It has no `/alive` — that
> comes from the .NET `MapDefaultEndpoints` — and its `/health` touches no database and no storage, so
> it is already a liveness probe by construction.

## Observability

::: tip What changed, and what did not (T-0500)
The five API hosts now export their OpenTelemetry pipeline — **logs, exceptions, requests and
metrics** — to Application Insights. Before T-0500 they exported nothing: the exporter existed but hung
off an `AddServiceDefaults` overload no host calls, so seven hosts were handed a connection string and
exactly one read it.

**This takes effect on the next deploy**, not on merge. Until `Deploy to DEV` has run against a commit
containing the fix, everything below describes the intended state and the "what you can see today"
answer is still platform metrics only.

**Sentry is unchanged and still off.** It is wired into the five API hosts, its DSN is empty in every
committed configuration file, and DEV is deployed with it empty. That is now a *supported* posture
rather than a blind one — see [Sentry](#sentry) for what it would still add.
:::

### Who sends what

| Host | Application Insights | Sentry |
|---|---|---|
| Partner API | **yes** — logs, exceptions, requests, metrics | wired, DSN empty |
| Admin API | **yes** | wired, DSN empty |
| Customer API | **yes** | wired, DSN empty |
| Partner Mobile API | **yes** | wired, DSN empty |
| Customer Mobile API | **yes** | wired, DSN empty |
| Customer SSR (Node) | no — connection string injected, no client in the app | not wired |
| Azure Functions | **yes** — via the Application Insights worker SDK, not OpenTelemetry | not wired |

### How the APIs reach App Insights

`Cleansia.ServiceDefaults/Extensions.cs` carries **two** `AddServiceDefaults` overloads, and both now
funnel their exporter registration through one private `AddTelemetryExporters(IServiceCollection,
IConfiguration)`:

| Overload | Called by |
|---|---|
| `AddServiceDefaults(IHostApplicationBuilder)` | nothing today — the stock Aspire shape, kept for a future minimal-hosting host |
| `AddServiceDefaults(IServiceCollection, IConfiguration, IHostEnvironment)` | all five APIs |

All five APIs use the Startup-class pattern: `Program.cs:17` calls `UseStartup<Startup>()`, each
`Startup` derives from `CleansiaStartupBase`, and `CleansiaStartupBase.cs:138` calls the
`IServiceCollection` overload. **That asymmetry is the whole history of this section** — the exporter
was added to the other overload in July 2026 under a commit message announcing that every API host now
shipped telemetry, and it shipped none. `Cleansia.Tests/Configuration/AppInsightsExporterWiringTests.cs`
pins the chain end to end, including through the real `CleansiaStartupBase`, because a test that only
called the extension method directly would have been green throughout.

Registration is guarded on `APPLICATIONINSIGHTS_CONNECTION_STRING` being non-empty, so a laptop and the
test hosts register no exporter at all. The OTLP exporter is still registered separately when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set — that is the local Aspire dashboard
(`Cleansia.AppHost/Properties/launchSettings.json:11`) and no Azure app setting sets it.

There is no codeless agent anywhere: `ApplicationInsightsAgent_EXTENSION_VERSION` is set on no host in
`deploy/`. That is why the SSR host sends nothing — it is Node, ships no telemetry package, and has no
agent to fall back on.

### What an operator can see

Two independent layers, and the distinction matters during an incident: **platform metrics** are
emitted by Azure itself and need no instrumentation, so they were the entire diagnostic surface before
T-0500 and are unaffected by any application change. The alert set is gated on `alertEmail` being
non-empty (`main.bicep:793`); `deploy/bicep/weu.dev.bicepparam:45` sets it, so these are live on DEV and
mail the ops Action Group.

| Signal | Source | Dev threshold |
|---|---|---|
| HTTP 5xx count per site | `Microsoft.Web/sites` platform metric (`alerts.bicep:81`) | > 25 in 15 min, severity 3 |
| Average response time per site | `Microsoft.Web/sites` platform metric (`alerts.bicep:154`) | > 2 s over 15 min |
| Functions host health probe | `HealthCheckStatus` platform metric (`alerts.bicep:121`) | < 100% healthy |
| Postgres failed connections / CPU / storage | `Microsoft.DBforPostgreSQL` platform metrics (`alerts.bicep:259`) | > 10 failures; > 90% CPU; > 85% storage |
| Poison-queue arrivals | queue diagnostic settings → Log Analytics scheduled query (`queueAlerts.bicep`) | any `PutMessage` into a `*-poison` queue |
| Server exceptions | App Insights `exceptions/count` (`alerts.bicep:194`) | > 25 in 15 min — **now genuinely covers the five APIs + Functions**, as that file always claimed |

**Alerting still tells you almost nothing about a single failure.** Every threshold above is a
*volume* threshold: on DEV one 500 does not reach any of them (the 5xx alert takes 26 in 15 minutes).
What changed at T-0500 is not who gets emailed — it is that the failure is now **recorded**, so there
is something to look at once you know to look. Being *told* about a first occurrence is the gap
[Sentry](#sentry) would fill.

### Reading API telemetry

The five APIs write structured logs through `ILogger`, enriched with tenant and user context by
`RequestLoggingMiddleware`:

```csharp
logger.LogInformation(
    "Order {OrderId} created for customer {CustomerId} with total {Total} {Currency}",
    order.Id, order.CustomerId, order.TotalPrice, order.Currency.Code);
```

These reach App Insights through the OpenTelemetry logging provider that the Azure Monitor distro
registers, so an unhandled exception's stack trace lands in `exceptions` and its log record in
`traces`, correlated to the failing request by `operation_Id`:

```kql
exceptions
| where timestamp > ago(1h)
| project timestamp, cloud_RoleName, problemId, outerMessage, operation_Id
| join kind=leftouter (requests | project operation_Id, name, resultCode, url) on operation_Id
```

`cloud_RoleName` is the host's application name, which is how the five APIs and the Functions host are
told apart in one query.

::: warning What the deployed log level actually admits
No `ASPNETCORE_ENVIRONMENT` is set on any host in `deploy/`, so every deployed host runs as
**`Production`** and binds `appsettings.Production.json`: `Logging:LogLevel:Default = Warning`, with
`Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` also at `Warning`.

That is load-bearing in both directions. **Warning and above ships** — which includes every
`LogError`, and the framework's own `ExceptionHandlerMiddleware` record of an unhandled exception. And
**Information does not** — which is why `RequestLoggingMiddleware`'s request/response body slices, and
the caller PII they can carry, do not leave the process at all. Lowering `Default` to `Information` on
a deployed host would send both the volume and that PII to App Insights.

**Since 2026-08-22 there is one exception, and only on non-prod.** `main.bicep:533` sets
`Logging__LogLevel__Cleansia` to `Information` when `env != 'prod'` (prod stays `Warning`). App Service
surfaces every app setting as an environment variable and `Host.CreateDefaultBuilder` layers those
**after** the JSON files, so it wins without a code change.

It is scoped to the **`Cleansia` category, never `Default`** — that is the whole point. Our own
`LogInformation` calls ("this sweep considered 40 assignments and sent 3") now reach App Insights on
DEV, while `Default` stays at `Warning` so `RequestLoggingMiddleware` and the framework's Information
chatter still do not. Before this, a DEV timer that ran and did nothing was indistinguishable from one
that never ran at all — which is exactly the bug that took a day to find.
:::

`deploy/bicep/modules/appService.bicep` still configures no `Microsoft.Insights/diagnosticSettings` for
the App Services, so the container's raw **stdout** is still not in Log Analytics. Live tailing remains
the way to watch a boot failure — a crash before the OTel pipeline starts is reported by nothing else:

```bash
az webapp log tail --name api-cleansia-partner-weu-dev --resource-group rg-cleansia-weu-dev
```

::: tip Functions telemetry has its own sampling posture
The Functions host uses the Application Insights worker SDK, not OpenTelemetry, and is tuned by
`src/Cleansia.Functions/host.json` (T-0499): adaptive sampling at 5 items/second with `Exception`
excluded, and `logLevel.default` raised to `Warning`. The APIs do **not** share those settings — see
[Volume and cost](#volume-and-cost).
:::

### Volume and cost

The APIs export at the Azure Monitor distro's defaults: **no SDK-side sampling** (`SamplingRatio` 1.0),
Live Metrics on, no per-route filtering. That is a deliberate choice for the environment that exists,
and it is the opposite of the Functions posture for a reason:

- **Sampling protects against traffic, and DEV has none.** Real DEV traffic is the owner's phone and a
  demo. Dropping 80% of a handful of requests means the one request that failed is most likely the one
  discarded — blindness bought for a rounding error. The Functions host samples because 14 queue
  listeners poll continuously whether or not anyone is using the system.
- **Logs are self-limiting.** At `Warning` an idle healthy host emits almost nothing; volume appears
  only when something is wrong, which is when you want to pay for it.
- **The two cost brakes that do exist are deploy-time, and neither was touched.** The Log Analytics
  workspace carries `dailyQuotaGb` (dev 1 GB, prod 5 GB) and the component carries `SamplingPercentage`
  (dev 100, prod 50) — both in `deploy/bicep/modules/appInsights.bicep`. **Do not add an SDK
  `SamplingRatio` in prod without accounting for the component's 50%: they compound**, and 0.2 × 50%
  keeps one trace in ten.
- **The daily cap is a breaker, not a budget.** When it trips, ingestion stops until the next UTC day —
  including exceptions. Blowing the cap on routine telemetry therefore blinds the signal this whole
  section exists for.

Two known noise sources were left alone rather than fixed here, both measured:

- **Health probes dominate DEV request volume.** App Service polls `/health` on six sites, and
  `/health` runs the readiness checks — a Postgres query *and* an Azure Blob `ExistsAsync`
  (`ReadinessHealthChecks.cs`) — so each probe emits a request span *and* an HttpClient dependency span.
- **Filtering them at the instrumentation is the wrong tool.** Measured directly: an
  `AspNetCoreTraceInstrumentationOptions.Filter` that drops `/health` removes the server span but the
  HttpClient dependency span underneath it is still exported, now parented to a span that was never
  sent. That trades one noisy record for one orphaned record. The correct fix is a sampler (or not
  making a storage round trip on every probe) and belongs to a cost ticket, not this one.

### Sentry

Sentry is wired into the five API hosts only — the Functions host does not use it. `Program.cs:16` on
each API calls `UseSentryMonitoring()` (`Cleansia.ServiceDefaults/Extensions.cs:85-112`), which reads
`Sentry:Dsn` and **leaves the SDK uninitialized when that value is absent or blank**:

```csharp
webBuilder.UseSentry((context, options) =>
{
    var dsn = context.Configuration["Sentry:Dsn"];
    if (string.IsNullOrWhiteSpace(dsn))
    {
        // Empty DSN is treated as "disabled" — the SDK rejects a blank DSN and would fail startup.
        options.Dsn = string.Empty;
        options.AutoSessionTracking = false;
        return;
    }

    options.Dsn = dsn;
    options.SendDefaultPii = false;
    options.AttachStacktrace = true;
    options.AutoSessionTracking = true;
    options.TracesSampleRate = 0.2;
    options.UseOpenTelemetry();
    options.SetBeforeSend((evt, _) => evt.Exception is OperationCanceledException ? null : evt);
});
```

The empty-DSN branch is deliberate, not a bug — it is what keeps a host with no DSN from failing to
boot. `TracesSampleRate` and `SendDefaultPii` are fixed in code, not read from configuration.

Every committed `appsettings*.json` sets `"Dsn": ""`. In Azure the value arrives from Key Vault,
populated by CI from the `SENTRY_DSN` GitHub secret — and the DEV runbook instructs that it be left
empty. **Turning Sentry on is a secret value, not a code change.**

#### Is Sentry redundant now that App Insights works?

**No — the two answer different questions, and the one Sentry answers is the one nobody else does.**
App Insights *records*; its alerting is metric-threshold based (`> 25 exceptions in 15 minutes`), so a
first-ever `NullReferenceException` on a demo is captured and nobody is told. Sentry's default unit is
the **issue**: first-seen, regression-after-release, deduplicated and grouped, with a notification on
occurrence one. It also carries release health and a far better stack-trace reader.

So they are complementary, and the honest split is *App Insights is the record, Sentry is the pager*.

What the owner would have to do to turn it on, in order:

1. Create a Sentry project (free tier: 5k errors/month, enough for DEV) and set the `SENTRY_DSN` secret
   in the `dev-weu` GitHub Environment. CI writes it to Key Vault on the next deploy; no code change.
2. Know the blast radius before doing it. `TracesSampleRate = 0.2` also sends 20% of *transactions*,
   not just errors, and `AutoSessionTracking = true` sends a session per request-ish unit — those, not
   the errors, are what consume a free tier.
3. `SendDefaultPii = false` is already set and must stay. Together with the deployed `Warning` log
   level (which keeps the `RequestLoggingMiddleware` Information records — and the caller email, name,
   phone and birth date they can contain — out of the breadcrumb trail entirely) that is what keeps a
   third-party error tracker from becoming a PII export. **Lowering the deployed log level and enabling
   Sentry are individually fine and jointly not**; sprint 14's T-0457 is the ticket that owns that PII.

Two smaller things worth fixing when someone next touches this, neither done here because both are
inert while the DSN is empty: `appsettings.Production.json` hardcodes `Sentry:Environment =
"production"`, so DEV errors would arrive tagged as production; and `TracesSampleRate` /
`SendDefaultPii` are fixed in code rather than read from configuration, so there is no way to tune the
sample rate per environment without a redeploy.
