import { ActionReducerMap } from '@ngrx/store';
import { CodeEffects, codeReducer, CodeState } from './code';
import { DashboardEffects, dashboardReducer, DashboardState } from './dashboard';
import { EmployeeEffects, employeeReducer, EmployeeState } from './employee';
import { loadingReducer, LoadingState } from './loading';
import { OrderEffects, orderReducer, OrderState } from './order';

// Partner app state - includes all partner-specific stores
export interface PartnerAppState {
  loading: LoadingState;
  employee: EmployeeState;
  order: OrderState;
  dashboard: DashboardState;
  code: CodeState;
}

export const partnerReducers: ActionReducerMap<PartnerAppState> = {
  loading: loadingReducer,
  employee: employeeReducer,
  order: orderReducer,
  dashboard: dashboardReducer,
  code: codeReducer,
};

export const partnerEffects = [
  EmployeeEffects,
  OrderEffects,
  DashboardEffects,
  CodeEffects,
];
