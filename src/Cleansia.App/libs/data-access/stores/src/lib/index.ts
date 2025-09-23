import { ActionReducerMap } from '@ngrx/store';
import { EmployeeEffects, employeeReducer, EmployeeState } from './employee';
import { loadingReducer, LoadingState } from './loading';
import { OrderEffects, orderReducer, OrderState } from './order';
import { UserEffects, userReducer, UserState } from './user';

export * from './employee';
export * from './loading';
export * from './order';
export * from './user';

export interface AppState {
  user: UserState;
  loading: LoadingState;
  employee: EmployeeState;
  order: OrderState;
}

export const reducers: ActionReducerMap<AppState> = {
  user: userReducer,
  loading: loadingReducer,
  employee: employeeReducer,
  order: orderReducer,
};

export const effects = [UserEffects, EmployeeEffects, OrderEffects];
