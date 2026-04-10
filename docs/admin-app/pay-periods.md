# Pay Periods

The Pay Periods feature allows admins to manage payroll cycles. Each pay period is a time range during which employee earnings (`OrderEmployeePay`) accumulate, then aggregate into invoices when the period closes. It is implemented in the `@cleansia/admin-features/pay-periods` library.

## Architecture

- `PayPeriodManagementFacade` -- List management with filtering, sorting, pagination
- `PayPeriodDetailFacade` -- Individual pay period detail view
- API calls via `AdminClient.adminPayPeriodClient`

## Pay Period Lifecycle

```
  Open  →  Closed  →  Paid
```

| Status | Description |
|---|---|
| `Open` | Active period accepting new pay calculations. One period should be open at a time. |
| `Closed` | Locked. No new calculations. Invoices generated for all employees with unpaid earnings. |
| `Paid` | All invoices in the period have been marked paid. Final state. |

A period can only be **closed** when there are no unpaid order pays. A period can only be marked **paid** when all its invoices are `Paid`.

## Pay Period List

Route: `/pay-periods`

The list page displays all pay periods with:

| Column | Description |
|---|---|
| Period | Period label (e.g., "March 1-15, 2026") |
| Start Date | Period start |
| End Date | Period end |
| Duration | Number of days |
| Status | Open / Closed / Paid badge |
| Closed At | Timestamp when closed (if applicable) |
| Closed By | Admin who closed it |
| Actions | View Details, Close Period |

### Filtering

Filters available via the Filter drawer:
- **Status** — Open, Closed, Paid, Unknown
- **Year** — filter by year

Active filters are displayed as chips above the table. Click the X on a chip to remove it.

### Create New Pay Period

The "Create New Pay Period" button opens a dialog (right-side drawer pattern matching the filter drawer):

1. Pick a **Start Date** via the date picker
2. Pick an **End Date** via the date picker
3. Click **Create**

The new period is created in `Open` status. The backend validates that the period length is between 7 and 31 days.

API call:
```
POST /api/AdminPayPeriod/create
{
  "startDate": "2026-04-01",
  "endDate": "2026-04-15"
}
```

::: tip Automatic period creation
A background job (`PayPeriodTimerFunction`, runs daily at 02:00 UTC) automatically creates the next period when the current one expires. Manual creation is only needed for ad-hoc periods or initial setup.
:::

## Pay Period Detail

Route: `/pay-periods/:id`

The detail page shows comprehensive information about a single pay period:

### Overview Section

| Field | Description |
|---|---|
| Period Label | Human-readable period range |
| Status | Current status with badge styling |
| Start Date / End Date | Period range |
| Duration | Days |

### Closure Information (when closed)

| Field | Description |
|---|---|
| Closed At | Timestamp |
| Closed By | Admin email |
| Notes | Optional admin notes |

### Metadata

| Field | Description |
|---|---|
| Created At | When the period was created |
| Created By | Admin or system |
| Modified At | Last modification |
| Modified By | Last modifier |

### Close Period Action

Available when status is `Open`. Clicking "Close Period" prompts for confirmation. On confirm:

1. Period status transitions `Open → Closed`
2. Background job generates invoices for all employees with unpaid `OrderEmployeePay` records in this period
3. PDF invoices are generated and uploaded to Azure Blob Storage
4. Email notifications are sent to employees with their invoices attached

API call:
```
PUT /api/AdminPayPeriod/close
{
  "payPeriodId": "...",
  "notes": "Closed for monthly payroll cut-off"
}
```

::: warning
Once closed, a period cannot be reopened. Make sure all order pays have been calculated before closing.
:::

## Background Job

The `PayPeriodTimerFunction` runs daily at 02:00 UTC and:

1. Finds all `Open` periods where `EndDate < Today`
2. Closes each expired period with the system marker
3. Generates invoices for all affected employees
4. Sends invoice PDFs via email
5. Creates the next pay period if no `Open` period exists

This means **manual closure is rarely needed** — periods close themselves on their end date. Manual closure is reserved for early cut-off scenarios.

## Relationship to Invoices

When a pay period closes, every employee with unpaid `OrderEmployeePay` records gets an `EmployeeInvoice` aggregating all their pays for that period. See [Invoice Management](./order-management) for the invoice approval and payment workflow.

The chain of relationships:

```
PayPeriod (1) ── (N) OrderEmployeePay (1) ── (1) Order
PayPeriod (1) ── (N) EmployeeInvoice (1) ── (N) OrderEmployeePay
EmployeeInvoice (N) ── (1) Employee
```

When the admin marks an invoice as `Paid`, the linked `OrderEmployeePay` records are considered settled. When all invoices in a period are `Paid`, the period itself can be marked `Paid`.
