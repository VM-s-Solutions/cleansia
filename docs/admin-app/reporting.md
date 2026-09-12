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

The revenue report (`RevenueReportDto`) provides business-level financial data:

### Key Metrics

| Metric | Description |
|---|---|
| Total Revenue | Sum of all completed order payments |
| Order Count | Total number of orders in the period |
| Average Order Value | Revenue / order count |
| Revenue by Service | Breakdown by service type |
| Revenue by Payment Method | Card vs cash distribution |
| Revenue Trend | Comparison with previous period |
| Customer Metrics | New vs returning customers |

### Revenue Breakdown

The report includes breakdowns that help identify:
- Which services generate the most revenue
- Payment method distribution (card vs cash)
- Revenue trends over time
- Customer acquisition patterns

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

The partner side answers the same question the other way round. The partner dashboard earnings,
earnings chart, personal bests, order-distribution money columns, available-jobs headline and My Pay
are all scoped to the currency `ICurrencyResolutionService.ResolveCurrencyForEmployeeAsync` returns
for the cleaner — their work country's default currency when that names a real currency, otherwise
the platform default — and that currency's code is what those screens print. Counts stay over all
orders; only the money is scoped.

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
