import { ActionReducerMap } from '@ngrx/store';
import { EmployeeEffects, employeeReducer, EmployeeState } from './employee';
import { loadingReducer, LoadingState } from './loading';
import { UserEffects, userReducer, UserState } from './user';

export * from './employee';
export * from './loading';
export * from './user';

export interface AppState {
  user: UserState;
  loading: LoadingState;
  employee: EmployeeState;
}

export const reducers: ActionReducerMap<AppState> = {
  user: userReducer,
  loading: loadingReducer,
  employee: employeeReducer,
};

export const effects = [UserEffects, EmployeeEffects];
