# Partner Dashboard

The dashboard provides partners with an overview of their performance, earnings, and upcoming work. It is implemented in the `@cleansia-partner/dashboard` library with multiple chart components.

## Architecture

- `DashboardFacade` -- Orchestrates data loading and date range management
- `DashboardComponent` -- Main layout with stat cards and chart grid
- Five chart sub-components (detailed below)
- All data is loaded via NgRx actions and selectors

## Stat Cards

The dashboard displays four key metric cards:

| Card | Value | Icon | Color |
|---|---|---|---|
| Available Orders | Count of unassigned orders | `pi pi-inbox` | Blue (`#0284c7`) |
| My Active Orders | Count of partner's in-progress orders | `pi pi-clock` | Amber (`#f59e0b`) |
| Completed This Month | Orders completed in current month | `pi pi-check-circle` | Green (`#10b981`) |
| Pending Earnings | Current pay period earnings in CZK | `pi pi-wallet` | Purple (`#8b5cf6`) |

The "Completed This Month" card includes a **trend indicator** showing the percentage change compared to last month:

```typescript
// Trend calculation
const percentChange = ((current - previous) / previous) * 100;
return {
  value: Math.abs(Math.round(percentChange)),
  direction: percentChange > 0 ? 'up' : percentChange < 0 ? 'down' : 'neutral',
};
```

Cards with a `route` property are clickable and navigate to the relevant page (e.g., clicking "Available Orders" navigates to `/orders`).

## Stat Card Model

```typescript
interface StatCard {
  title: string;           // Translation key
  value: number | string;  // Display value
  icon: string;            // PrimeIcons class
  color: string;           // Hex color
  route?: string;          // Optional navigation target
  trend?: {
    value: number;         // Percentage change
    direction: 'up' | 'down' | 'neutral';
  };
}
```

## Charts & Analytics

### Earnings Chart

**Component:** `CleansiaEarningsChartComponent`

Displays earnings over time within the selected date range. Uses Chart.js line/bar chart to visualize daily or weekly earnings data.

**Data source:** `selectEarningsAnalytics` NgRx selector

### Order Distribution Pie Chart

**Component:** `CleansiaOrderDistributionChartComponent`

Shows the breakdown of orders by status (Pending, Confirmed, InProgress, Completed, Cancelled) as a pie/doughnut chart.

**Data source:** `selectOrderAnalytics` NgRx selector

### Time Analytics Chart

**Component:** `CleansiaTimeAnalyticsChartComponent`

Visualizes time-related metrics such as average completion time, time per order, and time distribution across working hours.

**Data source:** `selectTimeAnalytics` NgRx selector

### Productivity Gauges

**Component:** `CleansiaProductivityGaugesComponent`

Displays productivity metrics as gauge/meter visualizations, showing metrics like completion rate, on-time percentage, and efficiency scores.

**Data source:** `selectProductivityMetrics` NgRx selector

### Date Range Selector

**Component:** `CleansiaDateRangeSelectorComponent`

Allows the partner to select a custom date range for all analytics. When the date range changes:

1. `DashboardFacade.onDateRangeChanged(startDate, endDate)` is called
2. `setDateRange` NgRx action is dispatched
3. `refreshAllAnalytics` action reloads all analytics data for the new range

## Data Loading

On initialization, the dashboard:

1. Fetches the current employee profile via `partnerClient.employeeClient.getCurrentEmployee()`
2. Loads dashboard stats via `loadDashboardStats` NgRx action
3. Loads upcoming orders (next 5 orders with status Pending/Confirmed/InProgress, sorted by cleaning date ascending)
4. Loads all analytics data via `refreshAllAnalytics` action

```typescript
// Upcoming orders filter
const upcomingOrdersFilter = new OrderFilter({
  employeeId,
  cleaningDateFrom: new Date(),
  orderStatuses: [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.InProgress],
});
```

## Loading States

Each analytics section has its own loading signal:

| Signal | Section |
|---|---|
| `statsLoading` | Stat cards |
| `upcomingOrdersLoading` | Upcoming orders list |
| `earningsLoading` | Earnings chart |
| `timeLoading` | Time analytics |
| `orderLoading` | Order distribution |
| `productivityLoading` | Productivity gauges |
| `analyticsLoading` | Combined analytics loading |

## Refresh

The dashboard can be refreshed via:
- `DashboardFacade.refresh()` -- Reloads all dashboard data
- `DashboardFacade.refreshAnalytics()` -- Reloads only analytics
- Changing the date range triggers an automatic refresh

## Currency Formatting

Earnings values are formatted using the partner's current locale:

```typescript
stats.currentPeriodEarnings.toLocaleString(
  this.translate.currentLang || 'en-GB'
) + ' Kc'
```
