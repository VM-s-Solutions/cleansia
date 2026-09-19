# Admin Reporting

The reporting feature provides administrators with business intelligence through revenue and payroll reports. It is implemented in the `@cleansia/admin-features/reports` library.

## Architecture

- `ReportsFacade` -- Manages report data, date range filtering, and tab switching
- `ReportsComponent` -- Main report page with tab layout

All data is fetched via `AdminClient.adminReportClient`.

## Report Types

The reports page has two tabs:

| Tab | Report | Description |
|---|---|---|
| `revenue` | Revenue Report | Business revenue and order metrics |
| `payroll` | Payroll Report | Employee earnings and payment data |

### Tab Switching

When switching tabs, data is loaded lazily -- the report is only fetched if it hasn't been loaded yet for the current date range:

```typescript
setActiveTab(tab: ReportType): void {
  this.activeTab.set(tab);
  if (tab === 'revenue' && !this.revenueReport()) {
    this.loadRevenueReport();
  } else if (tab === 'payroll' && !this.payrollReport()) {
    this.loadPayrollReport();
  }
}
```

## Date Range Selection

All reports are filtered by a date range and a currency (see [Currency](#currency)):

- **Default range**: Last 30 days (1 month ago to today)
- **Default currency**: the platform default (`currencyId` unset)
- Admins adjust both in the filter drawer
- Changing the filter clears cached report data and reloads the active tab

```typescript
setDateRange(startDate: Date, endDate: Date, currencyId?: string): void {
  this.selectedCurrencyId.set(currencyId);
  this.dateRange.set({ startDate, endDate });
  this.revenueReport.set(null);
  this.payrollReport.set(null);
  // Reload active tab
}
```

The "Reset" button returns to the default date range and the default currency.

## Revenue Report

The revenue report (`RevenueReportDto`) is **completed and paid orders by completion date, in one
currency, minus every refund on those orders** — the owner's definition of 2026-09-19, stated on the
page itself (`pages.reports.revenue_description`) together with its three caveats: a refund reduces
the month the order completed in, not the month it was issued; lost chargebacks are not subtracted;
cash orders refunded by hand have no refund record and show gross. The rules and the reasoning are in
[Business rules — the revenue report](/product/business-rules#revenue-report).

### Key Metrics

| Card | DTO member | What it is |
|---|---|---|
| **Net revenue** (the headline) | `netRevenue` = `totalRevenue − totalRefundedToCard − totalReturnedToCredit` | the sales, minus every refund on them; the sub-line reads *{gross} gross − {refunded} refunded (incl. {credit} returned as credit)* |
| Completed & paid orders | `totalOrders` (`completedOrders` equals it by construction) | the period's completed, paid orders — a fully refunded one included, netting to zero |
| Average order value | `averageOrderValue` | net revenue over the order count |
| Cancelled orders | `cancelledOrders` | cancelled **bookings** in the period by cancellation date, on their own axis and not part of revenue; an abandoned card checkout is not a booking (the card's tooltip says so) |
| Growth | `growthPercentage` | second half of the period's daily net against the first half |
| Revenue by service / package | `revenueByService`, `revenueByPackage` | net, split evenly across an order's lines |
| Revenue by payment type | `revenueByPaymentType` | per tender: the sale, *from customer credit*, *taken by this tender*, **refunded to card**, **returned as credit**, and **net on tender** — the figure to reconcile against the gateway statement (the table's hint) |
| Revenue by payment status | `revenueByPaymentStatus` | gross by `PaymentStatus` — `Paid`, `PartiallyRefunded`, `Refunded`; `Disputed` has no production writer and no copy |
| Daily revenue | `dailyRevenues` | by completion date, each day's `amount` net and its `refunded` beside it |

There is no *Completed orders* card any more — it would always equal the total.

## Payroll Report

The payroll report (`PayrollReportDto`) provides employee compensation data:

### Key Metrics

| Metric | Description |
|---|---|
| Total Payroll | Sum of all employee earnings |
| Partner Count | Number of active partners |
| Average Earnings | Per-partner average |
| Orders per Partner | Average workload distribution |
| Bonus Total | Total bonuses issued |
| Deduction Total | Total deductions applied |

### Partner Performance

The payroll report helps administrators:
- Identify top-performing partners
- Spot partners with low activity
- Review bonus and deduction distribution
- Plan payroll budgets

## Currency

A report is **one currency**. There is no "all currencies" report, because a sum across two currencies
is not a number. The admin picks the currency in the filter drawer next to the date range
(`currencyId`; leaving it unset means the platform default currency), the server filters the rows in
SQL to that currency, and a named currency that does not exist is refused as `currency.not_found`.
Both `RevenueReportDto` and `PayrollReportDto` carry `currencyCode` — the currency every amount on
that report is in.

The facade formats every amount with the code its own report names, never with a currency it assumed:

```typescript
/** The revenue report's amounts, in the currency THAT report names. */
formatRevenueAmount(value: number | undefined): string {
  return this.formatAmount(value, this.revenueReport()?.currencyCode);
}

/** The payroll report's amounts, in the currency THAT report names. */
formatPayrollAmount(value: number | undefined): string {
  return this.formatAmount(value, this.payrollReport()?.currencyCode);
}

private formatAmount(value: number | undefined, currencyCode: string | undefined): string {
  if (value === undefined || value === null) return '';
  if (!currencyCode) return String(value);
  return new Intl.NumberFormat(this.translate.currentLang || 'en-GB', {
    style: 'currency',
    currency: currencyCode,
  }).format(value);
}
```

There is no fraction-digit override: the per-tender column reconciles against a Stripe statement to
the cent, and rounding 45.10 € to 45 € is how lines stop summing.

The same "one currency or grouped by it" rule holds on the two admin lists that carry money. The
order list and the invoice list each take a `currencyId` filter, and a sort on their money column with
no currency filter set runs within currency rather than across it — the server leads the sort with
`CurrencyId` so the page is grouped, and the plain price order applies only once the filter pins the
page to one currency. → [Order management](./order-management#money-across-currencies)

The catalogue lists (Services, Packages, Extras) are a different case: they show a price per row, and
a list is priced in the **platform default** currency (each `ServiceListItem` / `PackageListItem` /
`ExtraListItem` carries its `currencyCode`, always the default's). The admin facades read the default
once from the currency overview and label every price with its code, printing a bare number rather
than a currency they cannot name — never a hard-coded "CZK". The per-currency rows themselves are
authored on the entry's form.

The partner side answers the same question the other way round. The partner dashboard earnings,
earnings chart, personal bests, order-distribution money columns, available-jobs headline and My Pay
are all scoped to the currency `ICurrencyResolutionService.ResolveCurrencyForEmployeeAsync` returns
for the cleaner — their work country's configured currency; the platform default only for a cleaner
with no work country, since a named country without a real currency throws rather than defaulting
(owner ruling 2026-09-12) — and that currency's code is what those screens print. Counts stay over all
orders; only the money is scoped. The cleaner's **board** is scoped the same way: an order in another
currency is not listed, counted or takeable by them. → [Business rules](/product/business-rules#cleaner-currency)

## Percentage Formatting

Change percentages include a sign indicator:

```typescript
formatPercentage(value: number): string {
  const sign = value >= 0 ? '+' : '';
  return `${sign}${value.toFixed(1)}%`;
}
```

## Loading States

Each report has its own loading signal:

| Signal | Report |
|---|---|
| `loadingRevenue` | Revenue report |
| `loadingPayroll` | Payroll report |
| `isLoading` | Combined (either report loading) |

::: tip
The `isLoading` computed signal combines both loading states, allowing the UI to show a single loading indicator when any report is being fetched:
```typescript
readonly isLoading = computed(
  () => this.loadingRevenue() || this.loadingPayroll()
);
```
:::

## Refresh

Admins can manually refresh the current report:

```typescript
refreshCurrentReport(): void {
  if (this.activeTab() === 'revenue') {
    this.loadRevenueReport();
  } else {
    this.loadPayrollReport();
  }
}
```

## Pay Config Management

Route: `/pay-config-management`

Admins can manage employee pay configurations through a dedicated CRUD interface. Pay configs define how employees are compensated based on their grade level.

### Grade Templates

| Grade | Multiplier | Description |
|---|---|---|
| Junior | 0.5x | Entry-level rate |
| Medior | 0.75x | Mid-level rate |
| Senior | 1.0x | Full rate |

The multiplier is applied to the base pay rate for each service to determine the employee's compensation.

### Operations

- **Create** -- Add a new pay config with grade selection
- **Read** -- View all existing pay configs
- **Update** -- Modify grade or rate parameters
- **Delete** -- Remove a pay config

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `adminReportClient.revenue(startDate, endDate, currencyId?)` | `GET /api/AdminReport/revenue` | Fetch revenue report in one currency |
| `adminReportClient.payroll(startDate, endDate, currencyId?)` | `GET /api/AdminReport/payroll` | Fetch payroll report in one currency |

`currencyId` is optional on both; omitted means the platform default currency.
