import {
  DashboardStatsDto,
  OrderListItem,
  EarningsAnalyticsDto,
  TimeAnalyticsDto,
  OrderAnalyticsDto,
  ProductivityMetricsDto,
} from '@cleansia/services';
import { createReducer, on } from '@ngrx/store';
import * as DashboardActions from './dashboard.actions';

export interface DashboardState {
  stats: DashboardStatsDto | null;
  upcomingOrders: OrderListItem[];
  earningsAnalytics: EarningsAnalyticsDto | null;
  timeAnalytics: TimeAnalyticsDto | null;
  orderAnalytics: OrderAnalyticsDto | null;
  productivityMetrics: ProductivityMetricsDto | null;
  selectedDateRange: {
    startDate: Date;
    endDate: Date;
  };
  loading: {
    stats: boolean;
    upcomingOrders: boolean;
    earningsAnalytics: boolean;
    timeAnalytics: boolean;
    orderAnalytics: boolean;
    productivityMetrics: boolean;
  };
  error: {
    stats: any | null;
    upcomingOrders: any | null;
    earningsAnalytics: any | null;
    timeAnalytics: any | null;
    orderAnalytics: any | null;
    productivityMetrics: any | null;
  };
}

export const initialState: DashboardState = {
  stats: null,
  upcomingOrders: [],
  earningsAnalytics: null,
  timeAnalytics: null,
  orderAnalytics: null,
  productivityMetrics: null,
  selectedDateRange: {
    startDate: new Date(new Date().getFullYear(), new Date().getMonth() - 5, 1), // Last 6 months
    endDate: new Date(),
  },
  loading: {
    stats: false,
    upcomingOrders: false,
    earningsAnalytics: false,
    timeAnalytics: false,
    orderAnalytics: false,
    productivityMetrics: false,
  },
  error: {
    stats: null,
    upcomingOrders: null,
    earningsAnalytics: null,
    timeAnalytics: null,
    orderAnalytics: null,
    productivityMetrics: null,
  },
};

export const dashboardReducer = createReducer(
  initialState,

  on(DashboardActions.loadDashboardStats, (state) => ({
    ...state,
    loading: { ...state.loading, stats: true },
    error: { ...state.error, stats: null },
  })),

  on(DashboardActions.loadDashboardStatsSuccess, (state, { stats }) => ({
    ...state,
    stats,
    loading: { ...state.loading, stats: false },
  })),

  on(DashboardActions.loadDashboardStatsFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, stats: false },
    error: { ...state.error, stats: error },
  })),

  on(DashboardActions.loadUpcomingOrders, (state) => ({
    ...state,
    loading: { ...state.loading, upcomingOrders: true },
    error: { ...state.error, upcomingOrders: null },
  })),

  on(DashboardActions.loadUpcomingOrdersSuccess, (state, { orders }) => ({
    ...state,
    upcomingOrders: orders,
    loading: { ...state.loading, upcomingOrders: false },
  })),

  on(DashboardActions.loadUpcomingOrdersFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, upcomingOrders: false },
    error: { ...state.error, upcomingOrders: error },
  })),

  // Earnings Analytics
  on(DashboardActions.loadEarningsAnalytics, (state) => ({
    ...state,
    loading: { ...state.loading, earningsAnalytics: true },
    error: { ...state.error, earningsAnalytics: null },
  })),

  on(DashboardActions.loadEarningsAnalyticsSuccess, (state, { data }) => ({
    ...state,
    earningsAnalytics: data,
    loading: { ...state.loading, earningsAnalytics: false },
  })),

  on(DashboardActions.loadEarningsAnalyticsFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, earningsAnalytics: false },
    error: { ...state.error, earningsAnalytics: error },
  })),

  // Time Analytics
  on(DashboardActions.loadTimeAnalytics, (state) => ({
    ...state,
    loading: { ...state.loading, timeAnalytics: true },
    error: { ...state.error, timeAnalytics: null },
  })),

  on(DashboardActions.loadTimeAnalyticsSuccess, (state, { data }) => ({
    ...state,
    timeAnalytics: data,
    loading: { ...state.loading, timeAnalytics: false },
  })),

  on(DashboardActions.loadTimeAnalyticsFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, timeAnalytics: false },
    error: { ...state.error, timeAnalytics: error },
  })),

  // Order Analytics
  on(DashboardActions.loadOrderAnalytics, (state) => ({
    ...state,
    loading: { ...state.loading, orderAnalytics: true },
    error: { ...state.error, orderAnalytics: null },
  })),

  on(DashboardActions.loadOrderAnalyticsSuccess, (state, { data }) => ({
    ...state,
    orderAnalytics: data,
    loading: { ...state.loading, orderAnalytics: false },
  })),

  on(DashboardActions.loadOrderAnalyticsFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, orderAnalytics: false },
    error: { ...state.error, orderAnalytics: error },
  })),

  // Productivity Metrics
  on(DashboardActions.loadProductivityMetrics, (state) => ({
    ...state,
    loading: { ...state.loading, productivityMetrics: true },
    error: { ...state.error, productivityMetrics: null },
  })),

  on(DashboardActions.loadProductivityMetricsSuccess, (state, { data }) => ({
    ...state,
    productivityMetrics: data,
    loading: { ...state.loading, productivityMetrics: false },
  })),

  on(DashboardActions.loadProductivityMetricsFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, productivityMetrics: false },
    error: { ...state.error, productivityMetrics: error },
  })),

  // Date Range
  on(DashboardActions.setDateRange, (state, { startDate, endDate }) => ({
    ...state,
    selectedDateRange: { startDate, endDate },
  })),

  on(DashboardActions.clearDashboard, () => initialState)
);
