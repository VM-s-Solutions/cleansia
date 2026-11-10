# Pay Periods

The Pay Periods feature allows admins to manage payroll cycles. Each pay period is a time range during which employee earnings (`OrderEmployeePay`) accumulate, then aggregate into invoices when the period closes. It is implemented in the `@cleansia/admin-features/pay-periods` library.

## Architecture

- `PayPeriodManagementFacade` -- List management with filtering, sorting, pagination
- `PayPeriodDetailFacade` -- Individual pay period detail view
- `AdminPayPeriodOpsComponent` / `AdminPayPeriodOpsFacade` -- the detail page's operations: `closeNow`, `changeEnd` and `markPaid`
- API calls via `AdminClient.adminPayPeriodClient`

## Pay Period Lifecycle

```
  Open  →  Closed  →  Paid
```

| Status | Description |
|---|---|
| `Open` | Active period accepting new pay calculations. **Exactly one period is open at a time.** |
| `Closed` | Locked. No new calculations. Every cleaner with pay in it is invoiced within about 20 minutes. **A closed period is never reopened.** |
| `Paid` | Every pay row is invoiced and every invoice is `Paid` or `Cancelled`. Final state. |

A period lasts at most the configured length, **14 days** in production and **3 days on DEV**, so testers see a
whole pay cycle within days (owner ruling 2026-11-09). It is one setting, `PayPeriodClosing:LengthInDays`.
→ [Business rules — pay periods](/product/business-rules#pay-periods), [ADR-0082](/decisions/adr-0082)

## Pay Period List

Route: `/pay-periods`

The list page displays all pay periods with:

| Column | Description |
|---|---|
| Start Date | Period start, as a calendar day in the admin's language |
| End Date | Period end, the same way |
| Duration | Number of days, as a plural in the admin's language |
| Status | Open / Closed / Paid badge |
| Closed At | Timestamp when closed (if applicable) |
| Closed By | The administrator who closed it by e-mail address, or *Automatically* for the night run and the wind-down |
| Actions | Opens the details |

A row opens the period's details; there is no row action to close it.

### Filtering

Filters available via the Filter drawer:
- **Status** — Open, Closed, Paid
- **Year** — filter by year

Active filters are displayed as chips above the table. Click the X on a chip to remove it.

### Create New Pay Period

The "Create New Pay Period" button -- shown only to a role with `CanCreatePayPeriod`, Accountant and
above, **and only while no period is open** (the server creates a period only when none is open) -- opens a
modal dialog with one field:

1. **Start Date**, which opens filled with the day after the latest period's end (one request sorted by end
   date, whatever filter the list shows), never later than today. It stays empty when no period exists or the
   read fails, and a date picked before the read lands is kept.
2. Click **Create**.

There is no end date: the backend sets it at the configured length and checks overlap over that span.

On success a toast says the period was created and the list is read again. A refusal is shown as an error and
the dialog stays open: `company.has_open_pay_period` ("A pay period is still open; close it first."),
`validation.invalid_date` for a start after today, and `pay_period.overlapping_period`.

The new period is created in `Open` status.

API call:
```
POST /api/AdminPayPeriod/create
{
  "startDate": "2026-04-01"
}
```

::: tip Automatic period creation
A background job (`PayPeriodTimerFunction`, runs daily at 02:00 UTC) closes the open period the night after its
end date and opens the next one from the day after it, at the configured length. Manual creation is only needed
for initial setup, or after a period was closed with no successor (a deactivated company has none).
→ [Business rules — pay periods](/product/business-rules#pay-periods)
:::

## Pay Period Detail

Route: `/pay-periods/:id`

The heading is the period's first and last day in the admin's language.

### Overview Section

| Field | Description |
|---|---|
| Status | Current status with badge styling |
| Start Date / End Date | Period range |
| Duration | Days |

### Closure Information (when closed)

| Field | Description |
|---|---|
| Closed At | Timestamp |
| Closed By | The administrator's e-mail, or *Automatically* |
| Paid At | When the period was marked paid, if it was |
| Notes | Optional admin notes |

### Invoices

| Field | Description |
|---|---|
| All invoices | Every invoice of the period, whatever its status |
| Unpaid invoices | Neither `Paid` nor `Cancelled` |
| Pay items not invoiced yet | Pay rows no invoice holds |

A **View invoices** button (`CanViewPagedInvoicesAdmin`) opens the invoice list filtered to this period
(`/invoice-management?payPeriodId={id}`). Only the details read fills the three counts; the period list leaves
them empty.

### Settlement Operations

An `Open` period offers **Close now** and **Change end date**; a `Closed` one offers **Mark Period as Paid**.
Each opens a panel under the button. There is no Reopen and no Open: a closed period stays closed.

#### Close now

Shown to a role with `CanClosePayPeriod`, Accountant and above, on an `Open` period whose start is today or
earlier (a period that has not started cannot be closed, `pay_period.not_started`). The panel says:

- the period ends today instead of its end date, and the next period starts tomorrow and runs the configured
  length (3 days on DEV, read from the period's `latestEndDate`);
- every cleaner with pay in it gets an invoice by e-mail within about 20 minutes;
- jobs finished after the close go into the next period;
- a closed period can't be reopened.

On a period already past its end (between midnight and the 02:00 UTC run, or with the night run off) the hint
says it ended on its end date, and the next period runs from the day after it, which is what the server does
then.

It asks for a **reason** of up to 500 characters, labelled as kept in the audit log; the button stays disabled
until one is typed and shows its loading state. A refusal shows inline under the field; a success toasts "Pay
period closed successfully" and reloads the page.

```
POST /api/AdminPayPeriod/close
{
  "payPeriodId": "...",
  "reason": "A cleaner left; invoice the period now"
}
```

The server stamps the administrator's e-mail as the closer, ends the period today (UTC) unless it already
ended earlier, records the old and new end and the reason in the audit log (never on the period's notes), and
opens the next period in the same commit when no other is open. It opens none for a deactivated company.
Invoices follow from the reconciliation sweep, about 15 to 20 minutes after the close, and the night-run switch
does not stop them. The reason is free text about a cleaner, so the request logger suppresses the body of
every `/adminpayperiod/close` request.

#### Change end date

Shown to a role with `CanUpdatePayPeriod` on an `Open` period. The calendar offers the days from tomorrow (and
the day after the start) to the period's last allowed day, `latestEndDate`: the start plus the configured
length minus one. The period then closes the night after the new end date, and the next one starts the day
after it; jobs already finished stay in it. To end a period today, use Close now.

```
PUT /api/AdminPayPeriod/update
{
  "payPeriodId": "...",
  "startDate": "2026-04-01",
  "endDate": "2026-04-10"
}
```

The start must equal the stored one (`validation.invalid_date`). An end today or earlier is refused with
`pay_period.end_too_soon`, which points to Close now, and one past the last allowed day with
`pay_period.invalid_duration`. The old and new end go in the audit log.

#### Mark Period as Paid

Shown to a role with `CanMarkPayPeriodPaid` on a `Closed` period. It is disabled, with a one-line reason,
while any invoice is neither paid nor cancelled or any pay is not invoiced yet
(`pay_period.has_unpaid_invoices`, `pay_period.has_uninvoiced_pay`).

## Background Job

The `PayPeriodTimerFunction` runs daily at 02:00 UTC, unless `PayPeriodClosing:Enabled` is off, and:

1. Finds all `Open` periods where `EndDate < Today`
2. Closes each expired period with the system marker (*Automatically*), and creates the next pay period at the
   configured length if no `Open` period exists, committing both **before** it invoices
3. Generates invoices for every employee with pay in the period
4. Sends invoice PDFs via email

An employee the run cannot invoice, or whose invoice is not issued, is picked up by the reconciliation sweep
about 15 minutes later, and the sweep's invoices are e-mailed too. The sweep is not stopped by the switch.

This means **manual closure is rarely needed** -- periods close themselves the night after their end date.
Close now is for something that happened: it ends the period early.

## Relationship to Invoices

When a pay period closes, every employee with pay in it gets an `EmployeeInvoice` aggregating all their pays
for that period. See [Invoice Management](./order-management) for the invoice approval and payment workflow.
There is no Reject action on an invoice, and a cancelled invoice is replaced by a corrected one within minutes.
→ [Pay and payouts](/flows/pay-and-payouts)

The chain of relationships:

```
PayPeriod (1) ── (N) OrderEmployeePay (1) ── (1) Order
PayPeriod (1) ── (N) EmployeeInvoice (1) ── (N) OrderEmployeePay
EmployeeInvoice (N) ── (1) Employee
```

When the admin marks an invoice as `Paid`, the linked `OrderEmployeePay` records are considered settled. The
period can be marked `Paid` only when all its invoices are `Paid` or `Cancelled` and all its pay is invoiced.
