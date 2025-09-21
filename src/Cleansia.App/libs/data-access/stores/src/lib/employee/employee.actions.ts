import { ApiException, RegistrationCompletionStatus } from '@cleansia/services';

import { createAction, props } from '@ngrx/store';

export const checkEmployeeCurrent = createAction('[Employee] Check Current');
export const checkEmployeeCurrentSuccess = createAction(
  '[Employee] Check Current Success',
  props<{ checkResult: RegistrationCompletionStatus }>()
);
export const checkEmployeeCurrentFailure = createAction(
  '[Employee] Check Current Failure',
  props<{ error: ApiException }>()
);
