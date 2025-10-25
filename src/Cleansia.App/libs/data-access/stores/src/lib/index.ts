import { ActionReducerMap } from '@ngrx/store';
import { DashboardEffects, dashboardReducer, DashboardState } from './dashboard';
import { EmployeeEffects, employeeReducer, EmployeeState } from './employee';
import { loadingReducer, LoadingState } from './loading';
import { OrderEffects, orderReducer, OrderState } from './order';
import { UserEffects, userReducer, UserState } from './user';

export * from './dashboard';
export * from './employee';
export * from './loading';
export * from './order';
export * from './user';

export interface AppState {
  user: UserState;
  loading: LoadingState;
  employee: EmployeeState;
  order: OrderState;
  dashboard: DashboardState;
}

export const reducers: ActionReducerMap<AppState> = {
  user: userReducer,
  loading: loadingReducer,
  employee: employeeReducer,
  order: orderReducer,
  dashboard: dashboardReducer,
};

export const effects = [UserEffects, EmployeeEffects, OrderEffects, DashboardEffects];
