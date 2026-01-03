import { createReducer, on } from '@ngrx/store';
import * as EmployeeActions from './employee.actions';
import { employeeInitialState, EmployeeState } from './employee.state';

const setFlag = (
  state: EmployeeState,
  key: string,
  loading: boolean,
  error?: string
) => ({
  ...state,
  loading: { ...state.loading, [key]: loading },
  error: { ...state.error, [key]: error ?? null },
});

export const employeeReducer = createReducer(
  employeeInitialState,

  on(EmployeeActions.checkEmployeeCurrent, (state) =>
    setFlag(state, 'current', true)
  ),
  on(EmployeeActions.checkEmployeeCurrentSuccess, (state, { checkResult }) =>
    setFlag({ ...state, isEmployeeConfirmed: checkResult }, 'current', false)
  ),
  on(EmployeeActions.checkEmployeeCurrentFailure, (state, { error }) =>
    setFlag(state, 'current', false, error.message)
  )
);
