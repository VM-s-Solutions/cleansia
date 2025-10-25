import { DashboardStatsDto, OrderListItem } from '@cleansia/services';
import { createReducer, on } from '@ngrx/store';
import * as DashboardActions from './dashboard.actions';

export interface DashboardState {
  stats: DashboardStatsDto | null;
  upcomingOrders: OrderListItem[];
  loading: {
    stats: boolean;
    upcomingOrders: boolean;
  };
  error: {
    stats: any | null;
    upcomingOrders: any | null;
  };
}

export const initialState: DashboardState = {
  stats: null,
  upcomingOrders: [],
  loading: {
    stats: false,
    upcomingOrders: false,
  },
  error: {
    stats: null,
    upcomingOrders: null,
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

  on(DashboardActions.clearDashboard, () => initialState)
);
