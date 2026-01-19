import { ActionReducerMap } from '@ngrx/store';
import { CodeEffects, codeReducer, CodeState } from './code';
import { DashboardEffects, dashboardReducer, DashboardState } from './dashboard';
import { EmployeeEffects, employeeReducer, EmployeeState } from './employee';
import { loadingReducer, LoadingState } from './loading';
import { OrderEffects, orderReducer, OrderState } from './order';
import { UserEffects, userReducer, UserState } from './user';

// Partner app state - includes all partner-specific stores
export interface PartnerAppState {
  user: UserState;
  loading: LoadingState;
  employee: EmployeeState;
  order: OrderState;
  dashboard: DashboardState;
  code: CodeState;
}

export const partnerReducers: ActionReducerMap<PartnerAppState> = {
  user: userReducer,
  loading: loadingReducer,
  employee: employeeReducer,
  order: orderReducer,
  dashboard: dashboardReducer,
  code: codeReducer,
};

export const partnerEffects = [
  UserEffects,
  EmployeeEffects,
  OrderEffects,
  DashboardEffects,
  CodeEffects,
];
