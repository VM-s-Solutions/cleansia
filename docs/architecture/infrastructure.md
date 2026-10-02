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
| **App Service Plan** | Basic B2 | Standard S1 |
| **App Service** (Customer API + SSR) | 1 instance | 1 instance |
| **App Service** (Partner API) | 1 instance | 1 instance |
| **App Service** (Admin API) | 1 instance | 1 instance |
| **App Service** (Mobile API) | 1 instance | 1 instance |
| **Static Web App** (Partner SPA) | Free tier | Standard |
| **Static Web App** (Admin SPA) | Free tier | Standard |
| **PostgreSQL Flexible Server** | Burstable B1ms | General Purpose D2s_v3 |
| **Storage Account** | LRS | LRS |
| **Azure Functions** | Container on the shared App Service plan, Always On | Container on the shared App Service plan, Always On |
| **Key Vault** | Standard | Standard |
| **Application Insights** | Basic | Basic |
| **Container Registry** | Basic | Basic |

**Seven always-on processes share one plan:** the five APIs, the customer SSR and the Functions
container. The Functions app is not on Consumption — `functionApp.bicep` gives it the shared plan's
`serverFarmId` and `alwaysOn: true`, because on a dedicated plan an idle Functions host stops polling
its queues. On DEV that plan is a B2 (2 vCPU, 3.5 GB) with no staging slots, so every deploy restarts
its sites on the cores the rest are serving from — see [Deploying onto the shared plan](#deploys).

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
| `ConnectionStrings--cleansia-db` | All APIs, Functions; the CI migrate job on DEV | PostgreSQL connection string. On production it is the least-privilege `cleansia_app` login's, `Ssl Mode=VerifyFull`, and the migration signs in as the administrator without it — [Azure setup — production posture](/deployment/azure-setup#production-posture) |
| `Stripe--SecretKey` | Customer API | Stripe payment processing |
| `Stripe--WebhookSecret` | Customer API | Stripe webhook signature verification |
| `SendGrid--ApiKey` | Functions, APIs | Email delivery |
| `Sentry--Dsn` | The five APIs and the Functions worker | Error tracking — **set on DEV, so Sentry is on**: the CI secret push writes it from `SENTRY_DSN`, as the log of `Deploy to DEV` run 36763660261 records. See [Sentry](#sentry) |
| `Storage--ConnectionString` | All APIs, Functions — **DEV only** | Azure Blob/Queue Storage. Production has no storage secret: the hosts use their managed identity and the account refuses shared keys |
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
| `dispute-evidence` | Private | Files a customer attached to a dispute | Deleted at the customer's erasure |
| `company-archives` | Private | An archived company's sealed bundle — `<tenantId>/<freeze instant>/` holding the books as JSON Lines, every receipt and payout-invoice PDF (copied, not moved) and a `manifest.json` written last with a SHA-256 per file; the manifest's hash is on the company's row ([ADR-0064](/decisions/adr-0064) D3) | Indefinite — no immutability policy yet (O-6); retrieval is an operations step |

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
| `generate-receipt` | `generate-receipt-poison` | Every issue of an order's receipt, keyed `receipt:{orderId}`, only ever for a `Paid` order: the Stripe webhook on each host that runs it (a card payment settles), the partner hosts' `CompleteOrder` (an order with no receipt yet — a cash sale's first), the admin host's `AdminOverrideOrderStatus` to `Completed` and `AdminRecordCashReceived` on a completed order (a cash sale), and the `FiscalReconciliation` timer (one that never landed). A cash booking and a recurring cash confirm queue no receipt — they queue the `order-booked` e-mail on `send-email` — and nothing restates a receipt (since 2026-09-28) → [what the receipt says](/flows/payment-and-fiscal#what-the-receipt-says) | `GenerateReceipt` function |
| `generate-invoice` | `generate-invoice-poison` | Admin API (period close) | `GenerateInvoice` function |
| `company-wind-down` | `company-wind-down-poison` | Admin API (`WindDownCompany`, and `DeactivateCompany` when a date is set) — one message per act, keyed `wind-down:{tenantId}:{request instant}` | `CompanyWindDown` function — the idempotent sweep ([ADR-0064](/decisions/adr-0064) D2) |
| `company-archive` | `company-archive-poison` | Admin API (`ArchiveCompany`) — keyed `archive:{tenantId}:{request instant}` | `CompanyArchive` function — builds the bundle into `company-archives` and stamps the manifest hash (ADR-0064 D3) |

The other five (`calculate-order-pay`, `send-email`, `notifications-dispatch`, `live-activity-dispatch`,
`sitewide-promo-fanout`) are in the consumer inventory below; every one of the nine has its poison twin
in `storage.bicep`'s `queueBaseNames` and an alert in `queueAlerts.bicep`.

### Queue Message Format

```json
// generate-receipt queue message — a QueueEnvelope<GenerateReceiptMessage>
{
  "messageKey": "receipt:01J9Z3K4M5N6P7Q8R9S0T1V2W3",
  "tenantId": "cleansia-cz",
  "payload": {
    "orderId": "01J9Z3K4M5N6P7Q8R9S0T1V2W3",
    "languageCode": "cs"    // a fallback only: the order's own language wins
  }
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

**40 functions**, 22 timers and 18 queue consumers (`grep -l TimerTrigger src/Cleansia.Functions/Functions/*.cs | wc -l`
is the check — the table below was one short, `ExpireStaleCredit`, until 2026-09-14; the two
company-lifecycle consumers and their poison twins joined on 2026-09-16). This inventory
listed five of them until 2026-08-22, which is a large part of why nobody noticed that eight timers
had never fired at all — see [the schedule tokens](#timer-schedules) below.

#### Timers

| Function | Schedule | Purpose |
|---|---|---|
| `OutboxDrainer` | every 10 s | Drains the transactional outbox onto the queues |
| `FiscalReconciliation` | every 5 min | Reconciles fiscal registrations against the EET API |
| `RetryFailedFiscalRegistrations` | every 5 min | Retries registrations that failed transiently |
| `NotifyLapsedPreferredOffers` | every 5 min | Closes a preferred-cleaner hold that expired and reopens the order |
| `SendPreCleaningReminders` | `%Cron%` — every 5 min | Reminds a **customer** their cleaning is coming up |
| `SendCleanerJobReminders` | `%Cron%` — every 5 min | Reminds a **cleaner** two hours out; nudges them close to the start if they have not set off; tells the company's administrators once when a staffed job is still not started 30 min after its start (`admin.order.cleaner_not_started`) |
| `CleanupStalePendingOrders` | every 15 min | Releases orders stuck awaiting payment |
| `SendNewJobsDigest` | `%Cron%` — hourly | Tells cleaners how many new offerable jobs are near them |
| `SendTomorrowJobDigest` | `%Cron%` — hourly | Tells each cleaner how many jobs they have tomorrow, at 18:00 **local** — hourly because a UTC cron cannot be timezone-aware |
| `AutoCancelStaleRecurringOrders` | hourly | Cancels recurring instances the customer did not confirm in time; the same tick re-drives pending card refunds of cancelled orders (`RedrivePendingRefunds`) and alerts the administrators after 24 h |
| `CloseExpiredPayPeriods` | daily 02:00 UTC | Marks pay periods past their end date as closed and opens the successor — no successor for a deactivated company |
| `MaterializeRecurringBookings` | `%Cron%` — daily 02:00 UTC | Turns recurring bookings into real orders — none for a deactivated company's templates |
| `SendRecurringOrderReminders` | `%Cron%` — daily 02:30 UTC | Warns a customer about an upcoming recurring instance |
| `SendMembershipLifecycleNotifications` | `%Cron%` — daily 03:00 UTC | Expiry, renewal and cancellation notices |
| `RefreshTokenCleanup` | daily 03:30 UTC | Deletes expired refresh tokens |
| `ExpireStaleCredit` | daily 03:30 UTC | Expires customer credit past its expiry date |
| `ExpireStaleReferrals` | `%Cron%` — daily 03:30 UTC | Expires referrals nobody redeemed |
| `LiveActivityJanitor` | daily 04:00 UTC | Ends Live Activities whose orders are long finished |
| `PruneOutbox` | daily 04:00 UTC | Deletes drained outbox rows |
| `RetryFailedUserDeletions` | daily 05:00 UTC | Re-runs every GDPR erasure left `Failed` (or `Processing` for over 30 min), once per row per day, in its own scope per row; logs a still-failed one at Error. Under `DataRetention__Enabled` |
| `SendPeriodEndReminders` | daily 09:00 UTC | Emails employees whose pay period ends in 3 days |
| `DataRetentionCleanup` | weekly, Sun 03:00 UTC | Fifteen tasks under fourteen retention settings: expired user data, old-order PII, customer/admin/cleaner audit rows (3 y per row by default), dispute text after erasure, contract- and cleaner-document-acceptance metadata, photos of completed or cancelled orders (7 d by default, held by unresolved disputes), receipt PDFs (10 y from the end of the year of issue), and expired or revoked guest access tokens. Runs **once per operating company** under that company's own settings; token expiry/revocation needs no separate setting → [Retention](/flows/gdpr-and-audit#retention) |

#### Queue consumers

| Function | Queue | Purpose |
|---|---|---|
| `GenerateReceipt` | `generate-receipt` | Issues the receipt for a `Paid` order: number, fiscal registration, PDF via QuestPDF → blob storage → SendGrid |
| `GenerateInvoice` | `generate-invoice` | Employee invoice PDF → blob storage |
| `CalculateOrderPay` | `calculate-order-pay` | Computes a cleaner's pay for a finished order |
| `SendEmail` | `send-email` | SendGrid delivery |
| `SendPushNotification` | `notifications-dispatch` | FCM delivery |
| `SendLiveActivityUpdate` | `live-activity-dispatch` | APNs Live Activity updates |
| `SendSitewidePromoFanout` | `sitewide-promo-fanout` | Fans a sitewide promo out to recipients |
| `CompanyWindDown` | `company-wind-down` | Winds a company down under the envelope's tenant: notices to every customer and cleaner, open bookings on or after the date cancelled and refunded (failed refunds re-driven), templates paused, every Plus ended, credit discharged and the last period invoiced once the company is deactivated; converges on re-run |
| `CompanyArchive` | `company-archive` | Builds a frozen company's sealed bundle into `company-archives` and stamps the manifest's hash on the row; a no-op for a company not frozen, already archived, or a stale request |

Each of those nine has a matching `*Poison` consumer on `<queue>-poison`. Two of them also classify one
exception as permanent on first delivery: `CalculateOrderPay` and `GenerateReceipt` dead-letter and ack a
message whose write a **frozen** company's books refused (`tenant.archived`), rather than retrying five
times towards the same row. → [Cross-cutting — dead letters](/flows/cross-cutting#dead-letters)

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

### Kill switches — how to stop something without a deploy {#kill-switches}

Three things can be switched off by configuration. All three default to **ON** when their section is
absent, so "off" is always something a person typed — never the consequence of an unset value. That
direction is deliberate and was learned the hard way: the retention sweep's switch used to be a database
row that no migration inserted, an absent row read as "off", and the job therefore never ran on any
deployed database while reporting success (T-0685).

| Setting | Turns off | Keeps working |
|---|---|---|
| `DataRetention__Enabled` | All fourteen weekly GDPR retention tasks (Sun 03:00), including order photos, all three audit tables, contract-acceptance metadata and dead guest access tokens — **and** the daily failed-erasure retry (05:00) | Everything else |
| `PayPeriodClosing__Enabled` | The nightly pay-period job (02:00) — closing expired periods, opening the next, **and generating + emailing an invoice per employee** | `EnsureOpenPeriodAsync`, called inline by pay calculation, so pay-calc never fails with `NoActivePeriod` |
| `Stripe__Enabled` | **All seven card-charge surfaces** — web checkout, resume checkout, mobile PaymentSheet, recurring-occurrence confirm, membership subscribe, membership checkout, membership plan swap | **Cash orders** — which only a signed-in customer's one-cleaner booking may use ([the cash rule](/product/business-rules#cash)), so with card off a guest or a larger booking has no way to pay — and everything that returns or releases money: refunds, cash-collection intent cancellation, membership cancellation, and GDPR erasure of the Stripe customer |

::: danger Set these as app settings, never in `Cleansia.Functions/appsettings.json`
The Functions worker composes configuration in the **opposite order** to the five API hosts:
`ConfigureFunctionsWorkerDefaults` registers the environment-variable providers first and `Program.cs`
adds `appsettings.json` last. **Last provider wins**, so a value committed to that file BEATS the app
setting an operator sets in the portal — silently removing the very switch it looks like it documents.

Set `Section__Key` as an application setting (the double underscore is the nesting separator). Leave the
section absent from `appsettings.json` entirely; the C# default supplies the ON value.
:::

A card charge refused by `Stripe__Enabled` returns the existing `order.payment_gateway_unavailable` key,
which is already translated in all three web apps, both Android apps and iOS — the same message a
customer sees when Stripe is genuinely down, which is what it means to them.

`CardPaymentsChargeSurfaceCoverageTests` fails the build if a new Stripe call is added without the gate,
because the first draft of that switch closed three of the seven surfaces and left memberships and
recurring orders charging.

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

The trigger is a thin shell. The body is `GenerateReceiptHandler` in `Cleansia.Functions.Core`, where
it can be tested without the Functions host:

```csharp
public class GenerateReceiptFunction(GenerateReceiptHandler handler)
{
    [Function("GenerateReceipt")]
    public Task Run(
        [QueueTrigger("generate-receipt", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
```

The handler reads the envelope, or a bare message from an older deploy. A message is discarded unless
the order is `Paid` — whatever the tender, so a cash booking not yet collected and a cancelled cash
order earn none — and is a no-op once the order has its receipt. Otherwise it claims the receipt number, registers it
with the fiscal authority, renders the PDF and e-mails it. A failure rethrows so the queue retries; a
write refused by a frozen company is dead-lettered instead.

## Service Integrations

### Stripe

Used for customer payments via Checkout Sessions. One account per environment: a sandbox on DEV, and
on production the operating company's own account (decision 49) —
[Environment configuration — Stripe](/deployment/environment-config#stripe).

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
| Receipt | When the receipt is issued (with PDF attachment): on settlement for card, at completion for cash; one sent after the clean has a post-service subject |
| Cash booking | An informational e-mail when a cash booking is made or a recurring cash occurrence confirmed — the amount to pay in cash, the slot, the address, the free-cancellation window (`order-booked` on `send-email`) |
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

## Deploying onto the shared plan {#deploys}

A DEV deploy lands every artifact straight on its live site: B-series plans accept no staging slot. All
seven processes restart on the same B2, and on 2026-10-01 the deploy was changed in four places so those
restarts stop feeding on each other.

| What | DEV | Prod | Where |
|---|---|---|---|
| API deploys | `deploy-api` is one matrix job of five legs at `max-parallel: 2`. Each leg deploys, then warms its own `/health` through `.github/scripts/warm-site.sh` before it gives up its seat | `max-parallel: 5`, the full fan-out: each leg deploys to its staging slot, warms it and swaps | `.github/workflows/deploy-azure.yml` |
| Container start limit | `WEBSITES_CONTAINER_START_TIME_LIMIT` = `600` on the five APIs and the SSR | the platform default, 230 s | `containerStartSettings`, `main.bicep` |
| SSR startup command | `node server/server.mjs`, set by Bicep as `appCommandLine` on the site and its staging slot | the same | `appService.bicep`, passed by `main.bicep` |
| Functions deploy | `az functionapp config container set` to the build's sha, and no separate restart | the same | `build-and-deploy-functions` |

**Two API sites at a time on DEV.** Fanned out, the five API deploys started in the same second as the
Functions and SSR deploys, warming took 7–17 minutes, and the two mobile APIs — the last to start —
failed together on 2026-08-12 and 2026-08-15 with nothing wrong with them. Holding the seat until the
site answers means the next host starts only once this one has finished starting. `warm-site.sh` is the
DEV warm loop: 30 attempts 10 s apart, 15 s each, and a site that never answers fails its leg rather
than leaving a green run. The SSR is warmed after every API leg by `warm-dev-sites`, on both its
hostnames, against Angular's server-render marker. This is the `max-parallel` fallback
[ADR-0015](/decisions/adr-0015) D5(d) named for contention on B2. **Prod keeps the full fan-out of
five**, so its slot swaps land together and its environment approvals arrive at once; throttling it is
the owner's call.

**600 seconds to start, on DEV only.** Linux App Service kills a container that has not answered its
start ping within 230 s. Each API runs its database-bound hosted services — `NpgsqlTypeCatalogInitializer`,
`LegalDocumentSeedHostedService` — against a one-vCore Postgres before Kestrel listens, so on a busy
B2 a slow start was killed and retried rather than waited for.

**Bicep owns the SSR startup command.** It used to be a deploy step, and a config write restarts the
site — seconds before the artifact deploy restarted it again. `appCommandLine` travels with the slot on
a swap, so prod needs nothing more. The Functions deploy lost its restart step for the same reason:
setting the image already restarts the site.

The alert mails those restarts would trip are muted for the length of the deploy —
[the deploy quiet window](#deploy-quiet-window).

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

### The Functions probe is bounded too

The Functions host has no `/alive` either. `functionApp.bicep` points Azure's probe at `/api/health`,
the worker's only HTTP route, so a non-200 there recycles the worker, and the `HealthCheckStatus`
metric it feeds is what the Functions health alert watches. `FunctionsHealthCheck` runs two probes —
the database (`CanConnectAsync`) and queue storage (one page of the queue list) — and since 2026-10-01
each answers within the same 5-second `ReadinessCheckTimeout`. The bound holds on the awaiting side as
well as on the token, because Npgsql's open ignores the token once the server has accepted the
connection and then stalls. A probe that fails or runs out of time names itself and the endpoint answers
503; it never hangs. The database probe builds a context of its own rather than using the request
scope's, so an open abandoned at the bound is disposed in the background instead of throwing when the
request scope ends.

## Observability

::: tip What changed, and what did not (T-0500)
The five API hosts now export their OpenTelemetry pipeline — **logs, exceptions, requests and
metrics** — to Application Insights. Before T-0500 they exported nothing: the exporter existed but hung
off an `AddServiceDefaults` overload no host calls, so seven hosts were handed a connection string and
exactly one read it.

**This takes effect on the next deploy**, not on merge. Until `Deploy to DEV` has run against a commit
containing the fix, everything below describes the intended state and the "what you can see today"
answer is still platform metrics only.

**Sentry is on in DEV.** It is wired into the five API hosts and the Functions worker. Its DSN is
empty in every committed configuration file and reaches Azure through Key Vault, where the CI secret
push wrote it on DEV — see [Sentry](#sentry).
:::

### Who sends what

| Host | Application Insights | Sentry |
|---|---|---|
| Partner API | **yes** — logs, exceptions, requests, metrics | **yes** — on in DEV |
| Admin API | **yes** | **yes** |
| Customer API | **yes** | **yes** |
| Partner Mobile API | **yes** | **yes** |
| Customer Mobile API | **yes** | **yes** |
| Customer SSR (Node) | no — connection string injected, no client in the app | not wired |
| Azure Functions | **yes** — via the Application Insights worker SDK, not OpenTelemetry | **yes** — the logging integration: every log at `Error` or above is an event |

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
non-empty (`main.bicep:970`); `deploy/bicep/weu.dev.bicepparam:45` sets it, so these are live on DEV and
mail the ops Action Group.

| Signal | Source | Dev threshold |
|---|---|---|
| HTTP 5xx count per site | `Microsoft.Web/sites` platform metric (`alerts.bicep:84`) | > 25 in 15 min, severity 3 |
| Average response time per site | `Microsoft.Web/sites` platform metric (`alerts.bicep:159`) | **disabled on DEV** (`enabled: isProd`); prod > 2 s |
| Functions host health probe | `HealthCheckStatus` platform metric (`alerts.bicep:124`) | < 50% healthy; prod < 100% |
| Postgres failed connections / CPU / storage | `Microsoft.DBforPostgreSQL` platform metrics (`alerts.bicep:273`) | > 10 failures; > 90% CPU; > 85% storage |
| Poison-queue arrivals | queue diagnostic settings → Log Analytics scheduled query (`queueAlerts.bicep`) | any `PutMessage` into a `*-poison` queue |
| Server exceptions | App Insights `exceptions/count` (`alerts.bicep:208`) | > 25 in 15 min — **now genuinely covers the five APIs + Functions**, as that file always claimed |

Two of those DEV values exist because every deploy restarts every site (2026-10-01). The six latency
rules are **disabled on DEV, not removed**: on sites with no traffic, the cold-start requests after a
deploy dominate the average and trip them, and deleting the loop would leave the rules behind, since
the Bicep deploys incrementally. The Functions health alert fires on DEV only when the host is
unhealthy for at least **half** the window, so the 2–5 minute restart of a deploy stays quiet; prod
keeps 100, where any unhealthy minute counts.

**Alerting still tells you almost nothing about a single failure.** Every threshold above is a
*volume* threshold: on DEV one 500 does not reach any of them (the 5xx alert takes 26 in 15 minutes).
What changed at T-0500 is not who gets emailed — it is that the failure is now **recorded**, so there
is something to look at once you know to look. Being *told* about a first occurrence is the gap
[Sentry](#sentry) fills, and on DEV it is on.

#### The deploy quiet window {#deploy-quiet-window}

A DEV deploy mutes the mails, not the alerts. The provision job opens an alert processing rule,
`apr-cleansia-<region>-<env>-deploy`, that removes the action groups from every alert raised in the
resource group: the alerts still fire and stay visible in Monitor > Alerts, and only the mails are
suppressed. It opens set to lapse after **120 minutes**, so a run that never reaches its end cannot mute
DEV for longer. `close-deploy-quiet-window` runs after every restarting job — passed, failed or
cancelled — and rewrites the rule to lapse **25 minutes** later, because an alert tripped during the
deploy resolves up to a 15-minute window plus a 5-minute evaluation afterwards, and that Resolved mail is
the deploy's too. The workflow owns the rule, not Bicep; `deploy/AZURE-DEV-RUNBOOK.md` §0 says why. Prod
has no quiet window.

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

Sentry is wired into the five API hosts and the Functions worker, and **it is on in DEV**: the
`SENTRY_DSN` secret is set in the `dev-weu` GitHub Environment, and the CI secret push writes it to
`Sentry--Dsn` — the log of `Deploy to DEV` run 36763660261 (2026-09-30) records it. The customer SSR is
not wired.

`Program.cs:16` on each API calls `UseSentryMonitoring()`, which adds the OpenTelemetry bridge. The
Functions worker has no web host to hang `UseSentry` on and builds no `TracerProvider` for the bridge,
so its `Program.cs` calls `AddSentryMonitoring` on the logging builder instead: every log at `Error` or
above becomes an event, which is the alert contract `PoisonHandlerBase` relies on. Both go through one
guard in `Cleansia.ServiceDefaults/Extensions.cs`, which reads `Sentry:Dsn` and **leaves the SDK
uninitialized when that value is absent or blank**:

```csharp
private static bool ConfigureSentry(SentryOptions options, string? dsn)
{
    if (string.IsNullOrWhiteSpace(dsn))
    {
        options.Dsn = string.Empty;
        options.AutoSessionTracking = false;
        return false;
    }

    options.Dsn = dsn;
    options.SendDefaultPii = false;
    options.AttachStacktrace = true;
    options.AutoSessionTracking = true;
    options.TracesSampleRate = 0.2;
    options.SetBeforeSend((evt, _) => evt.Exception is OperationCanceledException ? null : evt);
    return true;
}
```

The empty-DSN branch is deliberate, not a bug — it is what keeps a host with no DSN from failing to
boot. `TracesSampleRate` and `SendDefaultPii` are fixed in code, not read from configuration.

Every committed `appsettings*.json` sets `"Dsn": ""`. In Azure the value arrives from Key Vault as the
`Sentry__Dsn` app setting on the five APIs and the Functions app, populated by CI from the `SENTRY_DSN`
GitHub secret. **Turning Sentry on or off is a secret value, not a code change.**

#### Is Sentry redundant now that App Insights works?

**No — the two answer different questions, and the one Sentry answers is the one nobody else does.**
App Insights *records*; its alerting is metric-threshold based (`> 25 exceptions in 15 minutes`), so a
first-ever `NullReferenceException` on a demo is captured and nobody is told. Sentry's default unit is
the **issue**: first-seen, regression-after-release, deduplicated and grouped, with a notification on
occurrence one. It also carries release health and a far better stack-trace reader.

So they are complementary, and the honest split is *App Insights is the record, Sentry is the pager*.

It was turned on the way this section said it would be — a Sentry project, the `SENTRY_DSN` secret in
the `dev-weu` GitHub Environment, and the next deploy writing it to Key Vault, with no code change. Two
things stay true while it is on:

1. Know the blast radius. `TracesSampleRate = 0.2` also sends 20% of *transactions*, not just errors,
   and `AutoSessionTracking = true` sends a session per request-ish unit — those, not the errors, are
   what consume a free tier.
2. `SendDefaultPii = false` is already set and must stay. Together with the deployed `Warning` log
   level (which keeps the `RequestLoggingMiddleware` Information records — and the caller email, name,
   phone and birth date they can contain — out of the breadcrumb trail entirely) that is what keeps a
   third-party error tracker from becoming a PII export. **Lowering the deployed log level and enabling
   Sentry are individually fine and jointly not**; sprint 14's T-0457 is the ticket that owns that PII.

Two smaller things are worth fixing when someone next touches this. One is live now that the DSN is
set: `appsettings.Production.json` hardcodes `Sentry:Environment = "production"` on every API host and
every deployed host runs as `Production`, so **DEV API events arrive in Sentry tagged `production`**.
The other is unchanged: `TracesSampleRate` / `SendDefaultPii` are fixed in code rather than read from
configuration, so there is no way to tune the sample rate per environment without a redeploy.
