import { createFeatureSelector, createSelector } from '@ngrx/store';
import { EMPLOYEE_FEATURE_KEY, EmployeeState } from './employee.state';

export const selectEmployeeState =
  createFeatureSelector<EmployeeState>(EMPLOYEE_FEATURE_KEY);

export const selectEmployeeConfirmation = createSelector(
  selectEmployeeState,
  (s) => s.isEmployeeConfirmed
);
