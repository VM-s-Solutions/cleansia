import { createAction, props } from '@ngrx/store';
import {
  ApiException,
  DashboardStatsDto,
  OrderListItem,
  SortDefinition,
  EarningsAnalyticsDto,
  TimeAnalyticsDto,
  OrderAnalyticsDto,
  ProductivityMetricsDto,
} from '@cleansia/services';
import { OrderFilter } from '@cleansia/models';

// Load dashboard stats (single endpoint call)
export const loadDashboardStats = createAction(
  '[Dashboard] Load Dashboard Stats',
  props<{ employeeId: string }>()
);

export const loadDashboardStatsSuccess = createAction(
  '[Dashboard] Load Dashboard Stats Success',
  props<{ stats: DashboardStatsDto }>()
);

export const loadDashboardStatsFailure = createAction(
  '[Dashboard] Load Dashboard Stats Failure',
  props<{ error: ApiException }>()
);

// Load upcoming orders (separate call for order details)
export const loadUpcomingOrders = createAction(
  '[Dashboard] Load Upcoming Orders',
  props<{
    filter?: OrderFilter;
    sort?: SortDefinition[];
    offset?: number;
    limit?: number;
  }>()
);

export const loadUpcomingOrdersSuccess = createAction(
  '[Dashboard] Load Upcoming Orders Success',
  props<{ orders: OrderListItem[] }>()
);

export const loadUpcomingOrdersFailure = createAction(
  '[Dashboard] Load Upcoming Orders Failure',
  props<{ error: ApiException }>()
);

export const clearDashboard = createAction('[Dashboard] Clear Dashboard');

// Load earnings analytics
export const loadEarningsAnalytics = createAction(
  '[Dashboard] Load Earnings Analytics',
  props<{ employeeId: string; startDate: Date; endDate: Date }>()
);

export const loadEarningsAnalyticsSuccess = createAction(
  '[Dashboard] Load Earnings Analytics Success',
  props<{ data: EarningsAnalyticsDto }>()
);

export const loadEarningsAnalyticsFailure = createAction(
  '[Dashboard] Load Earnings Analytics Failure',
  props<{ error: ApiException }>()
);

// Load time analytics
export const loadTimeAnalytics = createAction(
  '[Dashboard] Load Time Analytics',
  props<{ employeeId: string; startDate: Date; endDate: Date }>()
);

export const loadTimeAnalyticsSuccess = createAction(
  '[Dashboard] Load Time Analytics Success',
  props<{ data: TimeAnalyticsDto }>()
);

export const loadTimeAnalyticsFailure = createAction(
  '[Dashboard] Load Time Analytics Failure',
  props<{ error: ApiException }>()
);

// Load order analytics
export const loadOrderAnalytics = createAction(
  '[Dashboard] Load Order Analytics',
  props<{ employeeId: string; startDate: Date; endDate: Date }>()
);

export const loadOrderAnalyticsSuccess = createAction(
  '[Dashboard] Load Order Analytics Success',
  props<{ data: OrderAnalyticsDto }>()
);

export const loadOrderAnalyticsFailure = createAction(
  '[Dashboard] Load Order Analytics Failure',
  props<{ error: ApiException }>()
);

// Load productivity metrics
export const loadProductivityMetrics = createAction(
  '[Dashboard] Load Productivity Metrics',
  props<{ employeeId: string }>()
);

export const loadProductivityMetricsSuccess = createAction(
  '[Dashboard] Load Productivity Metrics Success',
  props<{ data: ProductivityMetricsDto }>()
);

export const loadProductivityMetricsFailure = createAction(
  '[Dashboard] Load Productivity Metrics Failure',
  props<{ error: ApiException }>()
);

// Set date range
export const setDateRange = createAction(
  '[Dashboard] Set Date Range',
  props<{ startDate: Date; endDate: Date }>()
);

// Refresh all analytics
export const refreshAllAnalytics = createAction(
  '[Dashboard] Refresh All Analytics',
  props<{ employeeId: string }>()
);
