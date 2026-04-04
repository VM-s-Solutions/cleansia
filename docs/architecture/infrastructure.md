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
| `Jwt--Key` | All APIs | JWT signing key |
| `Jwt--Issuer` | All APIs | JWT issuer |
| `Jwt--Audience` | All APIs | JWT audience |
| `ConnectionStrings--cleansia-db` | All APIs, Functions | PostgreSQL connection string |
| `Stripe--SecretKey` | Customer API | Stripe payment processing |
| `Stripe--WebhookSecret` | Customer API | Stripe webhook signature verification |
| `SendGrid--ApiKey` | Functions, APIs | Email delivery |
| `Sentry--Dsn` | All APIs, Functions | Error tracking |
| `Storage--ConnectionString` | All APIs, Functions | Azure Blob/Queue Storage |

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
Poison queue messages indicate a bug or data issue. Set up Application Insights alerts on poison queue depth to catch failures early.
:::

## Azure Functions

All functions run in a single Azure Functions project deployed as a Docker container (required for QuestPDF native dependencies).

### Function Inventory

| Function | Trigger | Schedule | Purpose |
|----------|---------|----------|---------|
| `GenerateReceipt` | Queue: `generate-receipt` | On message | Generates receipt PDF with QuestPDF, uploads to blob storage, sends email via SendGrid |
| `GenerateInvoice` | Queue: `generate-invoice` | On message | Generates employee invoice PDF, uploads to blob storage |
| `CloseExpiredPayPeriods` | Timer | Daily at 2:00 AM UTC | Finds pay periods past their end date and marks them as closed |
| `SendPeriodEndReminders` | Timer | Daily at 9:00 AM UTC | Emails employees whose pay period ends in 3 days |
| `DataRetentionCleanup` | Timer | Weekly, Sunday 3:00 AM UTC | GDPR compliance — deletes expired user data, anonymizes old orders |

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

### Sentry

Used for error tracking and performance monitoring across all APIs and Functions.

```csharp
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.TracesSampleRate = 0.2;  // 20% of transactions
    options.Environment = builder.Environment.EnvironmentName;
});
```

## Application Insights

All APIs and Functions send telemetry to Application Insights for monitoring, logging, and alerting.

### Key Metrics Monitored

| Metric | Alert Threshold |
|--------|----------------|
| Response time (P95) | > 2 seconds |
| Failed requests (5xx) | > 1% of total |
| Poison queue depth | > 0 messages |
| Database connection failures | Any occurrence |
| Function execution failures | > 3 in 15 minutes |

### Logging

All APIs use structured logging via `ILogger` which flows to Application Insights:

```csharp
// Logs are enriched with tenant and user context
// by the RequestLoggingMiddleware (see Backend docs)
logger.LogInformation(
    "Order {OrderId} created for customer {CustomerId} with total {Total} {Currency}",
    order.Id, order.CustomerId, order.TotalPrice, order.Currency.Code);
```

::: tip Log Queries
Use Kusto Query Language (KQL) in Application Insights to query logs:
```kql
requests
| where timestamp > ago(1h)
| where resultCode >= 500
| summarize count() by cloud_RoleName, bin(timestamp, 5m)
| render timechart
```
:::
