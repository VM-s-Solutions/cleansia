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

All reports are filtered by a date range:

- **Default range**: Last 30 days (1 month ago to today)
- Admins can adjust the range using a date picker
- Changing the date range clears cached report data and reloads the active tab

```typescript
setDateRange(startDate: Date, endDate: Date): void {
  this.dateRange.set({ startDate, endDate });
  this.revenueReport.set(null);
  this.payrollReport.set(null);
  // Reload active tab
}
```

The "Reset" button returns to the default date range.

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

## Currency Formatting

All monetary values are formatted in CZK:

```typescript
formatCurrency(value: number): string {
  return new Intl.NumberFormat('cs-CZ', {
    style: 'currency',
    currency: 'CZK',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
}
```

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
| `adminReportClient.revenue(startDate, endDate)` | `GET /api/admin/reports/revenue` | Fetch revenue report |
| `adminReportClient.payroll(startDate, endDate)` | `GET /api/admin/reports/payroll` | Fetch payroll report |
