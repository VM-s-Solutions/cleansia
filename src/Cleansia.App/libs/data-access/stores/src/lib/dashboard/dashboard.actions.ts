import { createAction, props } from '@ngrx/store';
import { ApiException, DashboardStatsDto, OrderListItem, SortDefinition } from '@cleansia/services';
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
