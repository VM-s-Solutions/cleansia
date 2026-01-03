import { RegistrationCompletionStatus } from '@cleansia/partner-services';

export const EMPLOYEE_FEATURE_KEY = 'employee';

export interface EmployeeState {
  isEmployeeConfirmed?: RegistrationCompletionStatus;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const employeeInitialState: EmployeeState = {
  isEmployeeConfirmed: undefined,
  loading: {},
  error: {},
};
