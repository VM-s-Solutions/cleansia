import { createFeatureSelector, createSelector } from '@ngrx/store';
import { DashboardState } from './dashboard.reducer';

export const selectDashboardState = createFeatureSelector<DashboardState>('dashboard');

export const selectDashboardStats = createSelector(
  selectDashboardState,
  (state) => state.stats
);

export const selectUpcomingOrders = createSelector(
  selectDashboardState,
  (state) => state.upcomingOrders
);

export const selectDashboardStatsLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.stats
);

export const selectUpcomingOrdersLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.upcomingOrders
);

export const selectDashboardLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.stats || state.loading.upcomingOrders
);

export const selectDashboardStatsError = createSelector(
  selectDashboardState,
  (state) => state.error.stats
);

export const selectUpcomingOrdersError = createSelector(
  selectDashboardState,
  (state) => state.error.upcomingOrders
);

// Analytics Selectors
export const selectEarningsAnalytics = createSelector(
  selectDashboardState,
  (state) => state.earningsAnalytics
);

export const selectTimeAnalytics = createSelector(
  selectDashboardState,
  (state) => state.timeAnalytics
);

export const selectOrderAnalytics = createSelector(
  selectDashboardState,
  (state) => state.orderAnalytics
);

export const selectProductivityMetrics = createSelector(
  selectDashboardState,
  (state) => state.productivityMetrics
);

export const selectSelectedDateRange = createSelector(
  selectDashboardState,
  (state) => state.selectedDateRange
);

// Analytics Loading Selectors
export const selectEarningsAnalyticsLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.earningsAnalytics
);

export const selectTimeAnalyticsLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.timeAnalytics
);

export const selectOrderAnalyticsLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.orderAnalytics
);

export const selectProductivityMetricsLoading = createSelector(
  selectDashboardState,
  (state) => state.loading.productivityMetrics
);

export const selectAnalyticsLoading = createSelector(
  selectDashboardState,
  (state) =>
    state.loading.earningsAnalytics ||
    state.loading.timeAnalytics ||
    state.loading.orderAnalytics ||
    state.loading.productivityMetrics
);

// Analytics Error Selectors
export const selectEarningsAnalyticsError = createSelector(
  selectDashboardState,
  (state) => state.error.earningsAnalytics
);

export const selectTimeAnalyticsError = createSelector(
  selectDashboardState,
  (state) => state.error.timeAnalytics
);

export const selectOrderAnalyticsError = createSelector(
  selectDashboardState,
  (state) => state.error.orderAnalytics
);

export const selectProductivityMetricsError = createSelector(
  selectDashboardState,
  (state) => state.error.productivityMetrics
);
