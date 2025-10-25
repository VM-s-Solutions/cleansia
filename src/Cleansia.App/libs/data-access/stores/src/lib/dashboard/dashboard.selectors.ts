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
