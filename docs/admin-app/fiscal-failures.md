# Fiscal Failures

The Fiscal Failures page is the admin action queue for receipts whose fiscal-authority registration did not succeed. It is implemented in the `@cleansia/admin-features/fiscal-failures` library and backed by `AdminFiscalFailureController` + the `FiscalFailures` CQRS feature.

For the full design of the fiscal subsystem (enforcement modes, retry schedule, error classification, resilience layers), see [Fiscal Compliance](/architecture/fiscal-compliance). This page covers only the admin UX.

## When a row appears here

A receipt lands on this page when **all** of the following are true:

- Its initial fiscal registration attempt failed.
- An admin has not yet acknowledged it.

Receipts whose initial call returned `FiscalResult.Success` never appear. Receipts in a country with `FiscalEnforcementMode.None` never appear (no fiscal call is made).

Rows are ordered by most recent activity first (`FiscalLastRetryAt ?? IssuedAt DESC`), so fresh failures bubble to the top. The list is capped at 200 rows per page load to keep it responsive.

## Columns

| Column | Description |
|---|---|
| Receipt # | Sequential receipt number (e.g., `2026-001234`) |
| Order # | Display order number for quick cross-reference |
| Issued | When the receipt was first created |
| Provider | Fiscal provider key (`CZ-EET2`, `DE-TSE`, etc.) |
| Error Kind | `Transient` / `Permanent` / `Configuration` / `Unknown` — colour-coded |
| Error | Last error message from the authority (max 1000 chars, truncated with ellipsis in UI) |
| Retries | Number of retry attempts the background job has already made |
| Next Retry | Timestamp of the next scheduled retry, or "Not scheduled" if the row has stopped retrying |

### Interpreting "Next Retry = Not scheduled"

A row stops retrying automatically when:

- The error kind is `Permanent` or `Configuration` (these are not retryable by design).
- `FiscalRetryCount` has reached `MaxFiscalRetries = 10`.
- The failure has been acknowledged.

In all three cases the admin has to take action — either fix the root cause and retry manually, or acknowledge the row.

## Actions

Each row exposes two admin actions:

### Retry now

Sends `POST /api/AdminFiscalFailure/{receiptId}/retry`. This calls `OrderReceipt.ScheduleImmediateFiscalRetry()`, which sets `FiscalNextRetryAt = UtcNow`. The next tick of the `RetryFailedFiscalRegistrations` timer function (within 5 minutes) picks it up. The admin does **not** wait synchronously — the UI shows a "Retry scheduled" snackbar and refreshes the list.

Use this when:

- You have fixed an upstream issue (e.g., corrected customer data on the order, updated fiscal credentials) and want to kick the receipt without waiting for the backoff timer.
- A `Permanent` row has been corrected and you want to verify the fix.

### Acknowledge

Sends `POST /api/AdminFiscalFailure/{receiptId}/acknowledge`. This calls `OrderReceipt.AcknowledgeFiscalFailure()`, which sets `FiscalAcknowledged = true`, records the timestamp, and clears `FiscalNextRetryAt`. The row disappears from the list.

Use this when:

- The failure is known-bad and will never succeed (e.g., an authority-side business rule that can't be worked around for a historical order).
- You are tracking the failure externally (Linear, Jira) and don't want it cluttering the dashboard.
- The receipt was cancelled and no longer needs a fiscal signature.

Acknowledged failures are **not** deleted — they remain in the database with `FiscalAcknowledged = true` so audits remain complete. They are simply filtered out of the default admin view.

## Typical workflows

### Transient outage

You'll typically see a burst of `Transient` rows during a fiscal authority outage. Do nothing — the retry job clears them automatically as the authority comes back online. Use this page to monitor the burn-down.

### Permanent — bad customer data

A receipt lands with `Permanent` error kind and a message like `INVALID_VAT_NUMBER: Customer VAT format is invalid`. The retry job will not touch it (permanent errors don't retry).

1. Open the order via Order # and fix the VAT number.
2. Return to Fiscal Failures and click **Retry now**.
3. Wait for the next retry tick (≤ 5 minutes) and verify the row is gone.

### Configuration — credentials expired

A burst of `Configuration` errors (`AUTH_FAILED`, `CERT_EXPIRED`) means ops work, not data work.

1. Fix the credential/certificate in the relevant country's configuration section.
2. Redeploy the backend (or hot-reload config if your environment supports it).
3. Click **Retry now** on one row to verify the fix.
4. Click **Retry now** on the remaining rows once confirmed.

### Unrecoverable historical order

A very old receipt cannot be re-signed because the authority no longer accepts filings for that period.

1. Click **Acknowledge**. The row disappears.
2. Document the exception externally per your compliance process.

## Permissions

The page requires the `CanManageFiscalFailures` policy, which is mapped to `PhysicalPolicy.AdminOnly` in `PolicyBuilder`. Non-admin roles receive a 403.

## API endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/AdminFiscalFailure` | List unacknowledged failures (max 200 rows) |
| `POST` | `/api/AdminFiscalFailure/{receiptId}/retry` | Schedule immediate retry |
| `POST` | `/api/AdminFiscalFailure/{receiptId}/acknowledge` | Acknowledge and hide |

All three are generated into the admin NSwag client as `ApiClient.adminFiscalFailure()` (GET) and `AdminFiscalFailureClient.retry()` / `.acknowledge()` (POST).

## Related code

- Frontend: `libs/cleansia-admin-features/fiscal-failures/`
- Backend feature: `src/Cleansia.Core.AppServices/Features/FiscalFailures/`
- Controller: `src/Cleansia.Web.Admin/Controllers/AdminFiscalFailureController.cs`
- Background job: `src/Cleansia.Functions/Functions/RetryFailedFiscalRegistrationsFunction.cs`

## See Also

- [Fiscal Compliance architecture](/architecture/fiscal-compliance) — How the subsystem works end-to-end, including retry schedule and enforcement modes per country.
